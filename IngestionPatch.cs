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
            // Get the job, original food, original def, and tracker def.
            Job curJob = chewer.CurJob;
            Thing food = curJob?.GetTarget(ingestibleInd).Thing;
            ThingDef originalDef = FoodTrackingHelpers.GetOriginalMealDef(food.def);
            ThingDef trackerDef = DynamicMealDefFactory.CreateTrackerMeal(food.def);

            // Validate pawn is human and not null and food gives nutrition and not null.
            if (!FoodTrackingHelpers.ValidateFoodEatingAttempt(chewer, food))
                return;

            // Get the FoodTracker and Ingredients components if they exist.
            CompFoodTracker tracker = food.TryGetComp<CompFoodTracker>();
            CompIngredients ingredients = food.TryGetComp<CompIngredients>();

            // Initialize nutrition entries and ingredients lists before ingestion.
            List<float> nutritionEntriesBefore = new List<float>();
            List<ThingDef> ingredientsBefore = null;

            // If the food has ingredients, copy them to the ingredientsBefore list.
            if (ingredients != null && ingredients.ingredients != null)
            {
                ingredientsBefore = new List<ThingDef>(ingredients.ingredients);
            }

            // Initialize nutrition per item, total nutrition and ingest count for the split path of FoodTracker and Non-FoodTracker foods.
            int ingestCount = curJob.count;
            float totalNutrition = 0f;
            float nutritionPerItem = 0f;

            // FoodTracker meals use their actual individual tracked nutrition values.
            if (tracker != null)
            {
                // SINGLETON STATE
                if (tracker.NutritionEntries.Count == 0)
                {
                    // Total nutrition is the partial nutrition value for singleton meals.
                    totalNutrition = tracker.PartialNutrition;
                }
                // STACK STATE
                else
                {
                    // Copy the nutrition entries before ingestion to the state for later calculation.
                    nutritionEntriesBefore = new List<float>(tracker.NutritionEntries);
                    int mealsToConsume = Mathf.Min(ingestCount, tracker.NutritionEntries.Count);

                    for (int i = 0; i < mealsToConsume; i++)
                    {
                        totalNutrition += tracker.NutritionEntries[i];
                    }
                }
            }
            // Get Non-FoodTracker food total nutrition, and nutrition per item.
            else
            {
                nutritionPerItem = food.GetStatValue(StatDefOf.Nutrition);
                totalNutrition = ingestCount * nutritionPerItem;

            }

            // Initialize ingestion state.
            __state = new IngestionState
            {
                TraceID = ++nextTraceId, // Increment the trace ID for each ingestion.

                Pawn = chewer, // The pawn who is eating the food.

                PreFood = food, // The food object(s), or stack of food, that the pawn is attempting to eat in this job.

                BaseDef = originalDef, // Always the original def of the food, even if it is a FoodTracker ingest job.

                TrackerDef = trackerDef, // The FoodTracker def.

                FoodDef = food.def, // The actual def of the food being eaten, which may be a FoodTracker ingest job.

                PreStackCount = food.stackCount, // The stack count of the food when the job starts.

                IngestCount = curJob.count, // The number of items the pawn is attempting to eat in this job.

                TotalNutrition = totalNutrition, // The total nutrition being consumed in this job.

                NutritionPerItem = nutritionPerItem, // The nutrition per item of the food being eaten, if it is a Non-FoodTracker ingest job.

                NutritionEntriesBefore = nutritionEntriesBefore, // The nutrition entries of the food before ingestion, if it is a FoodTracker ingest job.

                IngredientsBefore = ingredientsBefore  // The ingredients of the food before ingestion, if it has a CompIngredients component.
            };

            // Eating duration is based on the actual total nutrition being consumed.
            durationMultiplier *= Mathf.Max(0.01f, __state.TotalNutrition / 0.9f);

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{__state.TraceID}] ChewIngestible Prefix completed: {food.def.defName} (ID {food.thingIDNumber}) " +
                    $"| Starting Stack Count: {__state.PreStackCount} | Ingest Count: {ingestCount} | Multiplier: {durationMultiplier:P0}");

        }

        public static void Postfix(Toil __result, Pawn chewer, TargetIndex ingestibleInd, IngestionState __state)
        {
            // If Prefix didn't produce a state, FoodTracker has nothing to track.
            if (__state == null)
                return;

            // Assigning __state to local state variable and assigning the toil to local variable.
            IngestionState state = __state;
            Toil toil = __result;

            // Save vanilla's existing tick action and vanilla init action.
            Action<int> originalTickAction = toil.tickIntervalAction;
            Action originalInit = toil.initAction;

            toil.initAction = () =>
            {
                // Let vanilla initialize the toil exactly as normal.
                originalInit?.Invoke();

                // Capture ticks immediately after Toil Init then capture food.
                int totalTicks = Mathf.Max(1, chewer.jobs.curDriver.ticksLeftThisToil);
                Thing food = chewer.CurJob?.GetTarget(ingestibleInd).Thing;

                // Finish populating the state.

                state.PostFood = food; // The food object(s) in the pawns hands/on the ground during interruption. This may be .Destroyed if the pawn is interrupted while drafted.

                state.HungerAtStart = chewer.needs.food.CurLevel; // The hunger level of the pawn at the start of the job used to calculate how much nutrition to substract from vanilla.

                state.FoodCell = chewer.Position; // The cell the pawn is standing on when they start eating, used to determine survivingStack if the food is .Destroyed.

                state.TotalTicks = totalTicks; // The total number of ticks the pawn will spend eating the food, used to calculate how much of the food was eaten.

                FoodTrackerIngestionTracker.Register(state);

                // If the food doesn't have a FoodTracker component.
                if (FoodTrackerSettings.Verbose && food.TryGetComp<CompFoodTracker>() == null)
                {
                    Log.Message($"[FoodTracker][T{state.TraceID}] Eating started: {state.FoodDef.defName} (ID {state.PostFood.thingIDNumber}) " +
                        $"| Pawn: {state.Pawn} | Ingest Count: {state.IngestCount} | Total Nutrition: {state.TotalNutrition:F2}");

                    return;
                }

                // If the food has a FoodTracker component.
                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker][T{state.TraceID}] Eating started: {state.FoodDef.defName} (ID {state.PostFood.thingIDNumber}) " +
                        $"| Pawn: {state.Pawn} | Ingest Count: {state.IngestCount} | Available Nutrition: {state.TotalNutrition:F2}");
            };

            // Wrap vanilla's tick action.
            toil.tickIntervalAction = delta =>
            {
                // Always let vanilla run first.
                originalTickAction?.Invoke(delta);

                // If initialization hasn't succeeded, nothing for us to do.
                if (state.TotalTicks <= 0)
                    return;

                // Read vanilla's actual timer.
                int ticksLeft = chewer.jobs?.curDriver?.ticksLeftThisToil ?? 0;

                // Update the fraction of food eaten based on ticks left and total ticks.
                state.EatenFraction = Mathf.Clamp01(1f - ((float)ticksLeft / state.TotalTicks));
            };

            // Wrap vanilla's finish action to handle completion or interruption.
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

                if (state.EatenFraction >= 0.99F)
                {
                    // Effectively completed don't interfere with vanilla.
                    FoodTrackerIngestionTracker.Remove(chewer);
                    return;
                }

                // ChewIngestible ended before the completion threshold and FinalizeIngest did not happen. Treat this as a genuine interruption.
                IngestionInterruptionHandler.Handle(state);

                // At this point the interruption handler has finished all synchronization and has marked any empty Thing for destruction.
                if (state.DestroyFoodAfterIngestion && state.ThingsToDestroy != null)
                {
                    DeferredFoodDestruction.Schedule(state.ThingsToDestroy);
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
            if (state?.PostFood?.TryGetComp<CompFoodTracker>() == null)
                return;

            // Calculate correction based off how mnuch nutrition should be applied, and how mucn was applied.
            float vanillaNutritionAdded = state.Pawn.needs.food.CurLevel - state.HungerAtStart;
            float trueNutritionConsumed = state.TotalNutrition;
            float correction = trueNutritionConsumed - vanillaNutritionAdded;

            FoodTrackingHelpers.ApplyNutritionToPawn(state, correction);

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{state.TraceID}] Eating Completed: {state.FoodDef.defName} (ID {state.PostFood.thingIDNumber}) " +
                    $"| Nutrition Consumed: {trueNutritionConsumed:F2} | Vanilla Added: {vanillaNutritionAdded:F2} | Correction Applied: {correction:F2}");
        }
    }
}
