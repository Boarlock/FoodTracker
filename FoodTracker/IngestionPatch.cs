using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace FoodTracker
{

    [HarmonyPatch(typeof(Toils_Ingest), nameof(Toils_Ingest.ChewIngestible))]
    public static class ChewIngestiblePatch
    {

        // Internal ID to track each ingestion.
        private static int nextTraceId = 0;

        public static void Prefix(Pawn chewer, ref float durationMultiplier, TargetIndex ingestibleInd, ref IngestionState __state)
        {
            // Get current job and target food.
            Job curJob = chewer.CurJob;
            Thing food = curJob?.GetTarget(ingestibleInd).Thing;

            // Validate pawn is human and not null and food is nutrition giving and not null.
            if (!FoodTrackingHelpers.ValidateFoodEatingAttempt(chewer, food))
                return;

            int ingestCount = curJob?.count ?? 0;

            CompFoodTracker tracker = food.TryGetComp<CompFoodTracker>();
            CompIngredients ingredients = food.TryGetComp<CompIngredients>();

            float totalNutrition = 0f;
            float nutritionPerItem = 0f;

            List<float> nutritionEntriesBefore = new List<float>();
            List<ThingDef> ingredientsBefore = null;

            if (ingredients != null && ingredients.ingredients != null)
            {
                ingredientsBefore = new List<ThingDef>(ingredients.ingredients);
            }

            // FoodTracker meals use their actual individual tracked nutrition values.
            if (tracker != null)
            {
                // SINGLETON STATE
                if (tracker.NutritionEntries.Count == 0)
                {
                    totalNutrition = tracker.PartialNutrition;
                }
                // STACK STATE
                else
                {
                    nutritionEntriesBefore = new List<float>(tracker.NutritionEntries);

                    int mealsToConsume = Mathf.Min(
                        ingestCount,
                        tracker.NutritionEntries.Count
                    );

                    for (int i = 0; i < mealsToConsume; i++)
                    {
                        totalNutrition += tracker.NutritionEntries[i];
                    }
                }
            }
            else
            {
                // Get the non-FT Def and store it in the state with total nutrition.
                nutritionPerItem = food.GetStatValue(StatDefOf.Nutrition);
                totalNutrition = ingestCount * nutritionPerItem;

            }

            ThingDef originalDef = FoodTrackingHelpers.GetOriginalMealDef(food.def);

            // Initialize ingestion state.
            __state = new IngestionState
            {

                TraceID = ++nextTraceId,
                Pawn = chewer,
                FoodDef = food.def,
                BaseDef = originalDef,
                StartingStackCount = food.stackCount,
                IngestCount = ingestCount,
                TotalNutrition = totalNutrition,
                NutritionEntriesBefore = nutritionEntriesBefore,
                IngredientsBefore = ingredientsBefore

            };

            // Batch foods don't carry a component, this scales eating duration off total nutrition eaten.
            if (FoodTrackingHelpers.IsBatchFood(food.def))
            {
                durationMultiplier *= Mathf.Max(0.01f, __state.TotalNutrition / 0.9f);

                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker][T{__state.TraceID}] Eating duration: {food.def.defName} (ID {food.thingIDNumber}) " +
                        $"| Ingest Count: {ingestCount} | Total Nutrition: {__state.TotalNutrition:F2} | Multiplier: {durationMultiplier:P0}");

                return;
            }

            ThingDef trackerDef = DynamicMealDefFactory.CreateTrackerMeal(__state);

            __state.TrackerDef = trackerDef;
            __state.NutritionPerItem = nutritionPerItem;

            // Eating duration is based on the actual total nutrition being consumed.
            durationMultiplier *= Mathf.Max(0.01f, __state.TotalNutrition / 0.9f);

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{__state.TraceID}] Eating duration: {food.def.defName} (ID {food.thingIDNumber}) " +
                    $"| Ingest Count: {ingestCount} | Total Nutrition: {__state.TotalNutrition:F2} | Multiplier: {durationMultiplier:P0}");

        }

        public static void Postfix(Toil __result, Pawn chewer, TargetIndex ingestibleInd, IngestionState __state)
        {
            // If Prefix didn't produce a state, FoodTracker has nothing to track.
            if (__state == null)
                return;

            // Assigning __state to local state variable and assigning the toil to local variable.
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

                // Capture ticks immediately after Toil Init then capture food and job.
                int totalTicks = Mathf.Max(1, chewer.jobs.curDriver.ticksLeftThisToil);
                Job curJob = chewer.CurJob;
                Thing food = chewer.CurJob?.GetTarget(ingestibleInd).Thing;

                // Finish populating the state.
                state.Food = food;
                state.HungerAtStart = chewer.needs.food.CurLevel;
                state.FoodCell = chewer.Position;
                state.TotalTicks = totalTicks;

                FoodTrackerIngestionTracker.Register(state);

                if (FoodTrackerSettings.Verbose && food.TryGetComp<CompFoodTracker>() == null)
                {
                    Log.Message($"[FoodTracker][T{state.TraceID}] Eating started: {state.FoodDef.defName} (ID {state.Food.thingIDNumber}) " +
                        $"| Pawn: {state.Pawn} | Ingest Count: {state.IngestCount} | Total Nutrition: {state.TotalNutrition:F2}");

                    return;
                }

                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker][T{state.TraceID}] Eating started: {state.FoodDef.defName} (ID {state.Food.thingIDNumber}) " +
                        $"| Pawn: {state.Pawn} | Ingest Count: {state.IngestCount} | Available Nutrition: {state.TotalNutrition:F2}");
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
                // exceeds 99%. This protects vanilla's Thing lifecycle from our replacement logic when the two systems are out of sync.

                if (state.EatenFraction >= FoodTrackingHelpers.MealCompletionThreshold)
                {
                    // Effectively completed don't interfere with vanilla.
                    FoodTrackerIngestionTracker.Remove(chewer);
                    return;
                }

                // ChewIngestible ended before the completion threshold and FinalizeIngest did not happen. Treat this as a genuine interruption.
                IngestionInterruptionHandler.Handle(state);

                // At this point the interruption handler has finished all
                // synchronization and has marked any empty Thing for destruction.
                if (state.DestroyFoodAfterIngestion && state.FoodToDestroy != null)
                {
                    DeferredFoodDestruction.Schedule(state.FoodToDestroy);
                }

                FoodTrackerIngestionTracker.Remove(chewer);
            });
        }
    }

    // Tries to get state from our dictionary, if one is found mark state.Finalized true and begin our completion handler.
    [HarmonyPatch(typeof(Toils_Ingest), nameof(Toils_Ingest.FinalizeIngest))]
    public static class FinalizeIngestPatch
    {
        public static void Postfix(Pawn ingester)
        {
            if (!FoodTrackerIngestionTracker.TryGet(ingester, out IngestionState state))
                return;

            state.Finalized = true;
            IngestionCompletionHandler.Handle(state);
        }
    }

    // Final completion handler to apply any corrections to nutrition after a meal is finished.
    public static class IngestionCompletionHandler
    {
        public static void Handle(IngestionState state)
        {

            // If food doesn't carry our component there is nothing to change.
            if (state?.Food?.TryGetComp<CompFoodTracker>() == null)
                return;

            // Calculate correction based off how mnuch nutrition should be applied, and how mucn was applied.
            float vanillaNutritionAdded = state.Pawn.needs.food.CurLevel - state.HungerAtStart;
            float trueNutritionConsumed = state.TotalNutrition;
            float correction = trueNutritionConsumed - vanillaNutritionAdded;

            FoodTrackingHelpers.ApplyNutritionToPawn(state, correction);

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{state.TraceID}] Eating Completed: {state.FoodDef.defName} (ID {state.Food.thingIDNumber}) " +
                    $"| Nutrition Consumed: {trueNutritionConsumed:F2} | Vanilla Added: {vanillaNutritionAdded:F2} " +
                    $"| Correction Applied: {correction:F2}");
        }
    }
}
