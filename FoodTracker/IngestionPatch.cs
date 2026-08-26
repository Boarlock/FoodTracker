using HarmonyLib;
using RimWorld;
using System;
using UnityEngine;
using Verse;
using Verse.AI;

namespace FoodTracker
{

    [HarmonyPatch(typeof(Toils_Ingest), nameof(Toils_Ingest.ChewIngestible))]
    public static class ChewIngestiblePatch
    {

        public static void Prefix(
            Pawn chewer,
            ref float durationMultiplier,
            TargetIndex ingestibleInd,
            ref ThingDef __state)
        {

            // Get food being consumed by current job and count of items to be consumed
            Job curJob = chewer.CurJob;
            Thing food = curJob?.GetTarget(ingestibleInd).Thing;
            __state = food?.def;

            int ingestCount = curJob?.count ?? 0;

            if (food == null || food.Destroyed)
            {
                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker] Inputs are not valid. Food: {food?.Label ?? "NULL"}, Food ID: " +
                        $"{food?.thingIDNumber ?? 0}, Food Def: {food?.def.defName ?? "NULL"}, Food Destroyed: {food?.Destroyed ?? false}");

                return;
            }

            float nutritionPerItem = FoodTrackingHelpers.GetFullNutrition(food);

            if (nutritionPerItem <= 0f || ingestCount <= 0)
            {
                Log.Warning($"[FoodTracker] Invalid nutrition/stack: Pawn: {chewer.Label}, Food: {food?.Label ?? "NULL"} " +
                    $"Food ID: {food?.thingIDNumber}, Nutrition Per Item: {nutritionPerItem:F2}, Count: {ingestCount}");

                return;
            }

            // Eating time scales with the total nutrition being consumed.
            if (FoodTrackingHelpers.IsBatchFood(food))
            {
                float totalNutrition = nutritionPerItem * ingestCount;

                durationMultiplier *= Mathf.Max(0.01f, totalNutrition / 0.9f);

                if (FoodTrackerSettings.Verbose)
                {
                    Log.Message($"[FoodTracker] Batch food detected. Scaling eating duration to {durationMultiplier:P0}");
                }

                return;
            }

            // Eating time scales with the nutrition remaining.
            if (FoodTrackingHelpers.IsTracked(food))
            {
                float remainingFraction = Mathf.Clamp01(FoodTrackingHelpers.GetRemainingNutrition(food) / nutritionPerItem);

                durationMultiplier *= remainingFraction;

                if (FoodTrackerSettings.Verbose)
                {
                    Log.Message($"[FoodTracker] Meal food detected. Scaling eating duration to {durationMultiplier:P0}");
                }

                return;
            }

            // Everything else uses vanilla eating duration.

        }

        public static void Postfix(
            Toil __result,
            Pawn chewer,
            float durationMultiplier,
            TargetIndex ingestibleInd,
            ThingDef __state)
        {
            Toil toil = __result;

            // Save vanilla's existing tick action.
            Action<int> originalTickAction = toil.tickIntervalAction;

            // State for this particular ingestion.
            IngestionState state = null;

            ThingDef originalFoodDef = __state;

            // Wrap vanilla's tick action.
            toil.tickIntervalAction = delta =>
            {
                // ALWAYS let vanilla run first.
                originalTickAction?.Invoke(delta);

                // If initialization hasn't succeeded, nothing for us to do.
                if (state == null)
                    return;

                // Read vanilla's actual timer.
                int ticksLeft = chewer.jobs?.curDriver?.ticksLeftThisToil ?? 0;

                state.EatenFraction = Mathf.Clamp01(1f - ((float)ticksLeft / state.TotalTicks));
            };

            Action originalInit = toil.initAction;

            toil.initAction = () =>
            {
                // First: let vanilla initialize the toil exactly as normal.
                originalInit?.Invoke();

                // Capture immediately after Toil Init
                int totalTicks = Mathf.Max(1, chewer.jobs.curDriver.ticksLeftThisToil);

                // Now vanilla has populated ticksLeftThisToil.
                // We can inspect the actual food/job.

                Job curJob = chewer.CurJob;
                Thing targetFood = chewer.CurJob?.GetTarget(ingestibleInd).Thing;

                //
                if (!FoodTrackingHelpers.ValidateFood(chewer, targetFood))
                    return;

                state = new IngestionState
                {
                    Pawn = chewer,
                    HungerAtStart = chewer.needs.food.CurLevel,
                    Food = targetFood,
                    MealDef = originalFoodDef,
                    NutritionAtStart = FoodTrackingHelpers.GetRemainingNutrition(targetFood),
                    NutritionPerItem = FoodTrackingHelpers.GetFullNutrition(targetFood),
                    IngestCount = curJob?.count ?? 0,
                    StartingStackCount = targetFood?.stackCount ?? 0,
                    FoodCell = chewer.Position,
                    TotalTicks = totalTicks,
                };

                FoodTrackerIngestionTracker.Register(state);

                if (FoodTrackerSettings.Verbose)
                {
                    Log.Message($"[FoodTracker] Eating toil has started. Pawn: {state.Pawn}, Source Stack: {state.StartingStackCount}, Ingest Count: {state.IngestCount}, " +
                        $"Remaining Nutrition: {state.NutritionAtStart:F2}, Food: {state.Food.Label}, Food ID: {state.Food.thingIDNumber}, Food Def: {state.Food.def.defName}");
                }

            };

            toil.AddFinishAction(() =>
            {
                if (state == null)
                    return;

                if (state.Finalized)
                {
                    // FinalizeIngest already confirmed this ingestion.
                    FoodTrackerIngestionTracker.Remove(chewer);
                    return;
                }

                // ChewIngestible ended, but FinalizeIngest did NOT happen. That means this was actually interrupted.
                IngestionInterruptionHandler.Handle(state);

                FoodTrackerIngestionTracker.Remove(chewer);
            });
        }
    }

    [HarmonyPatch(typeof(Toils_Ingest), nameof(Toils_Ingest.FinalizeIngest))]
    public static class FinalizeIngestPatch
    {
        public static void Postfix(Pawn ingester, TargetIndex ingestibleInd)
        {
            if (!FoodTrackerIngestionTracker.TryGet(ingester, out IngestionState state))
                return;

            state.Finalized = true;
            IngestionCompletionHandler.Handle(state);
        }
    }

    public static class IngestionCompletionHandler
    {
        public static void Handle(IngestionState state)
        {
            if (state == null || state.Food == null || !FoodTrackingHelpers.IsTracked(state.Food))
            {
                if (FoodTrackerSettings.Verbose)
                {
                    Log.Message($"[FoodTracker] {state?.Food?.Label ?? "NULL"} is not tracked, letting vanilla handle it. Food ID: {state?.Food?.thingIDNumber ?? 0}, Food Def: {state?.Food?.def?.defName ?? "NULL"}");
                }

                return;
            }

            float vanillaNutritionAdded = state.Pawn.needs.food.CurLevel - state.HungerAtStart;
            float correction = state.NutritionAtStart - vanillaNutritionAdded;

            state.Pawn.needs.food.CurLevel += correction;
            state.Pawn.needs.food.CurLevel = Mathf.Clamp(state.Pawn.needs.food.CurLevel, 0f, state.Pawn.needs.food.MaxLevel);
            state.Pawn.records.AddTo(RecordDefOf.NutritionEaten, correction);

            if (!state.Food.Destroyed)
            {
                if (FoodTrackerSettings.Verbose)
                {
                    Log.Message($"[FoodTracker] Destroying {state.Food.Label}, nutrition has been exhausted. Nutrition Consumed: " +
                        $"{state.NutritionAtStart:F2}, Food ID: {state.Food.thingIDNumber}, Food Def: {state.MealDef.defName}.");
                }

                state.Food.Destroy(DestroyMode.Vanish);
            }
        }
    }
}
