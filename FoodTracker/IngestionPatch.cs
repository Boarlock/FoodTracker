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
            ref IngestionState __state)
        {

            // Get food being consumed by current job and count of items to be consumed
            Job curJob = chewer.CurJob;
            Thing food = curJob?.GetTarget(ingestibleInd).Thing;

            // Validate pawn is human and not null and food is nutrition giving and not null
            if (!FoodTrackingHelpers.ValidateFoodEatingAttempt(chewer, food))
                return;

            __state = new IngestionState
            {
                MealDef = food.def,
                NutritionAtStart = FoodTrackingHelpers.GetRemainingNutrition(food),
                NutritionPerItem = food.GetStatValue(StatDefOf.Nutrition),
                IngestCount = curJob?.count ?? 0
            };

            if (__state.NutritionPerItem <= 0f || __state.IngestCount <= 0)
            {
                Log.Warning($"[FoodTracker] Invalid nutrition or stack: Pawn: {chewer.Label}, Food: {food?.def?.defName ?? "NULL"} " +
                    $"Food ID: {food?.thingIDNumber ?? 0}, Nutrition Per Item: {__state?.NutritionPerItem ?? 0}, Count: {__state?.IngestCount ?? 0}");

                return;
            }

            // Eating time scales with the total nutrition being consumed.
            if (FoodTrackingHelpers.IsBatchFood(food))
            {
                float totalNutrition = __state.NutritionPerItem * __state.IngestCount;

                durationMultiplier *= Mathf.Max(0.01f, totalNutrition / 0.9f);

                if (FoodTrackerSettings.Verbose)
                {
                    Log.Message($"[FoodTracker] Batch food detected. Scaling eating duration to {durationMultiplier:P0}");
                }

                return;
            }

            ThingDef generatedDef = DynamicMealDefFactory.CreateTrackerMeal(food.def);

            // Eating time scales with the nutrition remaining.
            if (food.def.defName.StartsWith("FoodTracker_"))
            {

                float remainingFraction = Mathf.Clamp01(FoodTrackingHelpers.GetRemainingNutrition(food) / __state.NutritionPerItem);

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
            IngestionState __state)
        {
            // If Prefix didn't produce a state, FoodTracker has nothing to track.
            if (__state == null)
                return;

            // Assigning __state to local state variable and assigning the toil to local variable
            IngestionState state = __state;
            Toil toil = __result;

            // Save vanilla's existing tick action.
            Action<int> originalTickAction = toil.tickIntervalAction;

            // Wrap vanilla's tick action.
            toil.tickIntervalAction = delta =>
            {
                // ALWAYS let vanilla run first.
                originalTickAction?.Invoke(delta);

                // If initialization hasn't succeeded, nothing for us to do.
                if (state.TotalTicks <= 0)
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

                // Capture ticks immediately after Toil Init then capture food and job
                int totalTicks = Mathf.Max(1, chewer.jobs.curDriver.ticksLeftThisToil);
                Job curJob = chewer.CurJob;
                Thing food = chewer.CurJob?.GetTarget(ingestibleInd).Thing;

                // Finish populating the state
                state.Food = food;
                state.Pawn = chewer;
                state.HungerAtStart = chewer.needs.food.CurLevel;
                state.FoodCell = chewer.Position;
                state.TotalTicks = totalTicks;

                FoodTrackerIngestionTracker.Register(state);

                if (FoodTrackerSettings.Verbose)
                {
                    Log.Message($"[FoodTracker] Eating toil has started. Pawn: {state.Pawn}, Ingest Count: {state.IngestCount}, " +
                        $"Remaining Nutrition: {state.NutritionAtStart:F2}, Food: {state.MealDef.defName}, Food ID: {state.Food.thingIDNumber}");
                }

            };

            toil.AddFinishAction(() =>
            {
                if (state == null)
                    return;

                if (state.Finalized)
                {
                    // Ingestion has completed
                    FoodTrackerIngestionTracker.Remove(chewer);
                    return;
                }

                // ChewIngestible ended without FinalizeIngest. Before treating this as an interruption.  Check if fraction of food eaten, 
                //  exceeds 99.5%. This protects vanilla's Thing lifecycle from our replacement logic when the two systems are out of sync.

                if (state.EatenFraction >= FoodTrackingHelpers.MealCompletionThreshold)
                {
                    // Effectively completed don't interfere with vanilla.
                    FoodTrackerIngestionTracker.Remove(chewer);
                    return;
                }

                // ChewIngestible ended before the completion threshold and FinalizeIngest did not happen. Treat this as a genuine interruption.
                IngestionInterruptionHandler.Handle(state);

                FoodTrackerIngestionTracker.Remove(chewer);
            });
        }
    }

    // Tries to get state from our dictionary, if one is found mark state.Finalized true and begin our completion handler.
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

    // Final completion handler to apply any corrections to nutrition after a meal is finished
    public static class IngestionCompletionHandler
    {
        public static void Handle(IngestionState state)
        {
            if (state.MealDef.defName.StartsWith("FoodTracker_") == false)
            {
                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker] {state?.MealDef.defName ?? "NULL"} is not a tracked partial meal or, has no " +
                        $"corresponding partial meal reference. letting vanilla handle it. Food ID: {state?.Food?.thingIDNumber ?? 0}");

                return;
            }

            float vanillaNutritionAdded = state.Pawn.needs.food.CurLevel - state.HungerAtStart;
            float correction = state.NutritionAtStart - vanillaNutritionAdded;
            float trueNutritionConsumed = vanillaNutritionAdded + correction;
            
            FoodTrackingHelpers.ApplyNutritionToPawn(state.Pawn, correction);

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker] Nutrition has been exhausted for {state.MealDef.defName}. Nutrition Consumed: " +
                    $"{trueNutritionConsumed:F2}, Food ID: {state.Food.thingIDNumber}");


        }
    }
}
