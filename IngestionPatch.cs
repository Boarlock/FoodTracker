using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;
using static UnityEngine.GraphicsBuffer;

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
            Job curJob = chewer?.CurJob;

            if (curJob == null || chewer == null)
                return;

            Thing obj = curJob?.GetTarget(ingestibleInd).Thing;

            if (obj == null)
                return;

            bool isSupportedDrug = FoodTrackingHelpers.GetDrugType(obj.def) != FoodTrackerDrugEffects.FoodTrackerDrugType.Unsupported;

            if (!chewer.WillEat(obj) && !obj.def.IsNutritionGivingIngestible && !isSupportedDrug)
                return;

            // Get the FoodTracker and Ingredients components if they exist.
            ThingDef trackerDef = DynamicMealDefFactory.CreateTrackerMeal(obj.def);
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

                IsDrug = isSupportedDrug, // Bool to track if current ingestion is a food or drug item.

                Pawn = chewer, // The pawn who is eating the food.

                ObjectTrackerDef = trackerDef, // The FoodTracker def.

                ObjectDef = obj.def, // The actual def of the food being eaten, which may be a FoodTracker ingest job.

                ObjectGameDef = FoodTrackingHelpers.GetOriginalDef(obj.def), // We use normal meal defs for drug effect calculations.

                PreStackCount = obj.stackCount, // The stack count of the food when the job starts.

                IngestCount = curJob.count, // The number of items the pawn is attempting to eat in this job.

                TotalFraction = totalFraction, // The sum of individual fractions in this ingestion job.

                RemainingFractionsBefore = remainingFractionsBefore, // The nutrition entries of the food before ingestion, if it is a FoodTracker ingest job.

                IngredientsBefore = ingredientsBefore  // The ingredients of the food before ingestion, if it has a CompIngredients component.
            };

            float minimumMultiplier = 40f / obj.def.ingestible.baseIngestTicks;

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

                state.ThingID = obj.thingIDNumber;

                FoodTrackerIngestionTracker.Register(state);

                // If the food has a FoodTracker component.
                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker][T{state.TraceID}] Eating started: {state.ObjectDef.defName} (ID {state.PostIngestObject.thingIDNumber}) " +
                        $"| Pawn: {state.Pawn} | Ingest Count: {state.IngestCount} | Total Fraction: {state.TotalFraction:F2}");

            };
        }
    }

    // Patch for JobTracker cleanup where we detect if the job was interrupted.
    [HarmonyPatch(typeof(Pawn_JobTracker), "CleanupCurrentJob", typeof(JobCondition), typeof(bool), typeof(bool), typeof(bool), typeof(bool))]
    public static class JobDriver_IngestCleanupPatch
    {
        public static void Prefix(Pawn ___pawn, JobCondition condition)
        {

            if (___pawn == null)
                return;

            bool tryget = FoodTrackerIngestionTracker.TryGet(___pawn, out IngestionState state);

            if (tryget == false)
                return;

            // Calculate elapsed ticks since toil started and fraction of food eaten.
            int elapsedTicks = Find.TickManager.TicksGame - state.StartTick;
            state.IngestedFraction = Mathf.Clamp01((float)elapsedTicks / state.TotalTicks);

            if (condition == JobCondition.Succeeded)
            {
                // How much should have been applied.
                float consumedFraction = state.TotalFraction * state.IngestedFraction;

                FoodTrackerIngestionTracker.Remove(state.Pawn);

                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker][T{state.TraceID}] Eating Completed: {state.ObjectDef.defName} (ID {state.PostIngestObject.thingIDNumber}) " +
                        $"| How much should have been applied: {consumedFraction:F2}");

                return;
            }

            if (state.HandlingInterruption)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Reentrant interruption detected. Ignoring nested Cleanup call.");
                return;
            }

            state.HandlingInterruption = true;

            // If job condition has not suceeded and our tracker exists this is a genuine interruption.
            IngestionInterruptionHandler.Handle(state);

            // Check if any food is scheduled for destruction.
            if (state.DestroyFoodAfterIngestion && state.ThingsToDestroy != null)
                DeferredFoodDestruction.Schedule(state.ThingsToDestroy);

            state.HandlingInterruption = false;

            FoodTrackerIngestionTracker.Remove(___pawn);
        }
    }


    [HarmonyPatch(typeof(Thing), "IngestedCalculateAmounts")]
    public static class Thing_IngestedCalculateAmounts_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Thing __instance, Pawn ingester, float nutritionWanted, ref int numTaken, ref float nutritionIngested)
        {

            if (ingester == null || __instance == null)
                return;

            float nutrition = FoodUtility.NutritionForEater(ingester, __instance);
            CompFoodTracker tracker = __instance.TryGetComp<CompFoodTracker>();

            if (tracker == null)
                return;

            CompFoodTrackerUtility.NormalizeState(__instance);

            float totalFractions = 0f;
            float totalNutrition = 0f;
            int itemsToRemove = 0;

            if (tracker.RemainingFractions.Count > 0 && itemsToRemove < numTaken)
            {
                for (int i = 0; i < tracker.RemainingFractions.Count && itemsToRemove < numTaken; i++)
                {
                    totalFractions += tracker.RemainingFractions[i];
                    totalNutrition = totalFractions * nutrition;

                    itemsToRemove++;
                }
            }
            else
            {
                totalFractions = tracker.PartialFraction;
                totalNutrition = totalFractions * nutrition;

                itemsToRemove++;
            }

            if (numTaken > itemsToRemove)
            {
                numTaken = itemsToRemove;
            }

            nutritionIngested = totalNutrition;

            return;
        }
    }

    [HarmonyPatch]
    public static class TryDropPatch
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(ThingOwner), nameof(ThingOwner.TryDrop), new Type[] { typeof(Thing), typeof(IntVec3), typeof(Map),
            typeof(ThingPlaceMode), typeof(Thing).MakeByRefType(), typeof(Action<Thing, int>), typeof(Predicate<IntVec3>), typeof(bool)});
        }

        public static bool Prefix(Thing thing)
        {
            // Only intercept this exact Thing if it is actively being ingested.
            if (FoodTrackerIngestionTracker.IsBeingIngested(thing, out IngestionState state))
            {
                return false;
            }

            // We aren't intercepting.
            return true;
        }
    }
}

