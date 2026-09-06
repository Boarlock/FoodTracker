using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using static FoodTracker.FoodTrackerDrugEffects;

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
            Thing obj = curJob?.GetTarget(ingestibleInd).Thing;
            ThingDef trackerDef = DynamicMealDefFactory.CreateTrackerMeal(obj.def);

            if (obj == null || chewer == null)
                return;

            if (!obj.def.IsNutritionGivingIngestible && FoodTrackingHelpers.GetDrugType(obj.def) == FoodTrackerDrugEffects.FoodTrackerDrugType.Unsupported)
                return;

            bool isDrug = (FoodTrackingHelpers.GetDrugType(obj.def) != FoodTrackerDrugEffects.FoodTrackerDrugType.Unsupported);

            // Get the FoodTracker and Ingredients components if they exist.
            CompFoodTracker tracker = obj.TryGetComp<CompFoodTracker>();
            CompIngredients ingredients = obj.TryGetComp<CompIngredients>();

            // Initialize nutrition entries and ingredients lists before ingestion.
            List<float> remainingFractionsBefore = new List<float>();
            List<ThingDef> ingredientsBefore = null;

            // If the food has ingredients, copy them to the ingredientsBefore list.
            if (ingredients != null && ingredients.ingredients != null)
            {
                ingredientsBefore = new List<ThingDef>(ingredients.ingredients);
            }

            // Initialize nutrition per item, total nutrition and ingest count for the split path of FoodTracker and Non-FoodTracker foods.
            int ingestCount = curJob.count;
            float totalFraction = 0f;

            // FoodTracker meals use their actual individual tracked nutrition values.
            if (tracker != null)
            {
                // SINGLETON STATE
                if (tracker.RemainingFractions.Count == 0)
                {
                    // Total nutrition is the partial nutrition value for singleton meals.
                    totalFraction = tracker.PartialFraction > 0f ? tracker.PartialFraction : 1f;
                }
                // STACK STATE
                else
                {
                    // Copy the nutrition entries before ingestion to the state for later calculation.
                    remainingFractionsBefore = new List<float>(tracker.RemainingFractions);
                    int mealsToConsume = Mathf.Min(ingestCount, tracker.RemainingFractions.Count);

                    for (int i = 0; i < mealsToConsume; i++)
                    {
                        totalFraction += tracker.RemainingFractions[i];
                    }
                }
            }
            // Get Non-FoodTracker food total nutrition, and nutrition per item.
            else
            {
                totalFraction = ingestCount;
            }

            // Initialize ingestion state.
            __state = new IngestionState
            {
                TraceID = ++nextTraceId, // Increment the trace ID for each ingestion.

                IsDrug = isDrug, // Bool to track if current ingestion is a food or drug item.

                Pawn = chewer, // The pawn who is eating the food.

                PreIngestObject = obj, // The food object(s), or stack of food, that the pawn is attempting to eat in this job.

                ObjectTrackerDef = trackerDef, // The FoodTracker def.

                ObjectDef = obj.def, // The actual def of the food being eaten, which may be a FoodTracker ingest job.

                ObjectGameDef = FoodTrackingHelpers.GetOriginalDef(obj.def), // We use normal meal defs for drug effect calculations.

                PreStackCount = obj.stackCount, // The stack count of the food when the job starts.

                IngestCount = curJob.count, // The number of items the pawn is attempting to eat in this job.

                TotalFraction = totalFraction, // The sum of individual fractions in this ingestion job.

                RemainingFractionsBefore = remainingFractionsBefore, // The nutrition entries of the food before ingestion, if it is a FoodTracker ingest job.

                IngredientsBefore = ingredientsBefore  // The ingredients of the food before ingestion, if it has a CompIngredients component.
            };

            float minimumMultiplier = 40f / FoodTrackingHelpers.GetDrugBaseIngestTicks(obj.def);

            // Eating duration is based on the actual total amount being consumed.
            if (tracker != null)
            {
                durationMultiplier *= Mathf.Max(minimumMultiplier, __state.TotalFraction);
            }
            else
            {
                // Full non-FoodTracker drug. Multiplier remains 1 unless vanilla has another modifier.
            }

            __state.TotalTicks = Mathf.RoundToInt((float)obj.def.ingestible.baseIngestTicks * durationMultiplier);

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{__state.TraceID}] ChewIngestible Prefix completed: {obj.def.defName} (ID {obj.thingIDNumber}) " +
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

            // Save vanilla's existing init action.
            Action originalInit = toil.initAction;

            toil.initAction = () =>
            {
                // Let vanilla initialize the toil exactly as normal.
                originalInit?.Invoke();

                state.StartTick = Find.TickManager.TicksGame; // Get starting tick to calculate if eating has ended.

                Thing obj = chewer.CurJob?.GetTarget(ingestibleInd).Thing;

                // Finish populating the state.

                state.PostIngestObject = obj; // The food object(s) in the pawns hands/on the ground during interruption. This may be .Destroyed if the pawn is interrupted while drafted.

                state.HungerAtStart = chewer.needs.food.CurLevel; // The hunger level of the pawn at the start of the job used to calculate how much nutrition to substract from vanilla.

                state.FoodCell = chewer.Position; // The cell the pawn is standing on when they start eating, used to determine survivingStack if the food is .Destroyed.

                FoodTrackerIngestionTracker.Register(state);

                // If the food doesn't have a FoodTracker component.
                if (FoodTrackerSettings.Verbose && obj.TryGetComp<CompFoodTracker>() == null)
                {
                    Log.Message($"[FoodTracker][T{state.TraceID}] Eating started: {state.ObjectDef.defName} (ID {state.PostIngestObject.thingIDNumber}) " +
                        $"| Pawn: {state.Pawn} | Ingest Count: {state.IngestCount} | Total Fraction: {state.TotalFraction:F2}");

                    return;
                }

                // If the food has a FoodTracker component.
                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker][T{state.TraceID}] Eating started: {state.ObjectDef.defName} (ID {state.PostIngestObject.thingIDNumber}) " +
                        $"| Pawn: {state.Pawn} | Ingest Count: {state.IngestCount} | Total Fraction: {state.TotalFraction:F2}");

            };
        }
    }

    // Patch for Job Driver cleanup where we detect if the job was interrupted.
    [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.Cleanup))]
    public static class JobDriverCleanupPatch
    {
        public static void Prefix(JobDriver __instance, JobCondition condition)
        {
            // If pawn is null, or our tracker doesn't exist, or Job Condition has succeeded then we return.
            if (__instance == null || (!FoodTrackerIngestionTracker.TryGet(__instance.pawn, out IngestionState state)) || condition == JobCondition.Succeeded)
                return;

            if (state.HandlingInterruption)
            {
                Log.Message($"[FoodTracker][T{state.TraceID}] Reentrant interruption detected. Ignoring nested Cleanup call.");
                return;
            }

            state.HandlingInterruption = true;

            // Calculate elapsed ticks since toil started and fraction of food eaten.
            int elapsedTicks = Find.TickManager.TicksGame - state.StartTick;
            state.IngestedFraction = Mathf.Clamp01((float)elapsedTicks / state.TotalTicks);

            // If job condition is not suceeded and our tracker exists this is a genuine interruption.
            IngestionInterruptionHandler.Handle(state);

            // Check if any food is scheduled for destruction.
            if (state.DestroyFoodAfterIngestion && state.ThingsToDestroy != null)
                DeferredFoodDestruction.Schedule(state.ThingsToDestroy);

            state.HandlingInterruption = false;

            FoodTrackerIngestionTracker.Remove(__instance.pawn);
        }
    }

    // Patch for Finalize Ingest where we detect if the job completed successfully.
    [HarmonyPatch(typeof(Toils_Ingest), nameof(Toils_Ingest.FinalizeIngest))]
    public static class FinalizeIngestPatch
    {
        public static void Postfix(Pawn ingester)
        {

            if (!FoodTrackerIngestionTracker.TryGet(ingester, out IngestionState state))
                return;

            // Only tracked objects require FoodTracker correction.
            if (state.PostIngestObject?.TryGetComp<CompFoodTracker>() == null)
            {
                FoodTrackerIngestionTracker.Remove(ingester);
                return;
            }

            FoodTrackerDrugEffects.FoodTrackerDrugType drugType = FoodTrackingHelpers.GetDrugType(state.ObjectGameDef);

            // How much should have been applied.
            float consumedFraction = state.TotalFraction * state.IngestedFraction;

            // Turn consumed fraction into its nutrition equivalent.
            float nutritionPerItem = state.ObjectDef.GetStatValueAbstract(StatDefOf.Nutrition);
            float expectedNutrition = consumedFraction * nutritionPerItem;

            // How much nutrition vanilla applied and correction to be applied.
            float vanillaNutritionAdded = state.Pawn.needs.food.CurLevel - state.HungerAtStart;
            float correction = expectedNutrition - vanillaNutritionAdded;

            if (!state.IsDrug || drugType == FoodTrackerDrugType.Ambrosia || drugType == FoodTrackerDrugType.Beer)
                FoodTrackingHelpers.ApplyNutritionToPawn(state, correction);

            FoodTrackerIngestionTracker.Remove(state.Pawn);

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{state.TraceID}] Eating Completed: {state.ObjectDef.defName} (ID {state.PostIngestObject.thingIDNumber}) " +
                    $"| Consumed Fraction: {consumedFraction:F2} | Expected Nutrition: {expectedNutrition:F2} | Vanilla Added: {vanillaNutritionAdded:F2} | Correction Applied: {correction:F2}");
        }
    }
}
