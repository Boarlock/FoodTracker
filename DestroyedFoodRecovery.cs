using RimWorld;
using UnityEngine;
using Verse;
using static FoodTracker.FoodTrackerDrugEffects;

namespace FoodTracker
{
    public static class DestroyedFoodRecovery
    {
        // Handle edge cases where vanilla destroys the food instance before we can instantiate a partial
        public static void HandleDestroyedMeal(IngestionState state)
        {
            // Validate inputs
            if (state == null || state.Pawn == null || state.PreIngestObject == null || state.PreIngestObject.Destroyed)
            {
                Log.Warning($"[FoodTracker][T{state?.TraceID.ToString() ?? "?"}] Inputs are not valid. State Null: {state == null} " +
                    $"| Pawn Null: {state?.Pawn == null} | ThingDef Null: {state.ObjectDef == null}");

                return;
            }

            FoodTrackerDrugEffects.FoodTrackerDrugType drugType = FoodTrackingHelpers.GetDrugType(state.ObjectGameDef);

            float consumedFraction = state.TotalFraction * state.IngestedFraction;
            float fractionIntoPartial = ((state.TotalFraction - consumedFraction) % 1f);
            int itemsRemoved = Mathf.CeilToInt(consumedFraction);

            // If nutrition is below a meaningful amount we don't even track it.
            if (fractionIntoPartial < FoodTrackingHelpers.MinimumPartialFraction)
            {

                // If items to be removed equal or exceed the stack count then set the Thing for destruction.
                if (itemsRemoved >= state.PostIngestObject.stackCount)
                {
                    state.ThingsToDestroy.Add(state.PostIngestObject);
                    state.DestroyFoodAfterIngestion = true;
                }
                // Otherwise stubtract items to remove from the stack count.
                else
                {
                    state.PostIngestObject.stackCount -= itemsRemoved;
                }

                // Need to rework from ingested fraction.
//                if (!state.IsDrug)
//                    FoodTrackingHelpers.ApplyNutritionToPawn(state, consumedFraction );

                return;
            }

            // Create the partial meal and drop at specified cell
            Thing partialMeal = PartialMealFactory.CreateAndDropPartialMeal(state, fractionIntoPartial, state.FoodCell);

            // If failed to create a partial meal then remove one from items to remove and correct nutrition to give to pawn.
            if (partialMeal == null)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Failed to make {partialMeal?.def.defName ?? "NULL"} (ID {partialMeal?.thingIDNumber ?? 0})");

                itemsRemoved--;

                // If items to be removed equal or exceed the stack count then set the Thing for destruction.
                if (itemsRemoved >= state.PreIngestObject.stackCount)
                {
                    state.ThingsToDestroy.Add(state.PreIngestObject);
                    state.DestroyFoodAfterIngestion = true;
                }
                // Otherwise stubtract items to remove from the stack count.
                else
                {
                    state.PreIngestObject.stackCount -= itemsRemoved;
                }

                // Need to rework from ingested fraction.
//                if (!state.IsDrug)
//                    FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionCorrection);

                return;
            }
            // Remove the consumed physical meals from the stack.
            if (itemsRemoved >= state.PreIngestObject.stackCount)
            {
                state.ThingsToDestroy.Add(state.PreIngestObject);
                state.DestroyFoodAfterIngestion = true;
            }
            // Otherwise stubtract items to remove from the stack count.
            else
            {
                state.PreIngestObject.stackCount -= itemsRemoved;
            }

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{state.TraceID}] Eating interrupted: {state.ObjectDef.defName} (ID {state.PostIngestObject?.thingIDNumber ?? 0}) " +
                    $"Pawn: {state.Pawn.LabelShort} | Ingest Count: {state.IngestCount} | Eaten: {state.IngestedFraction:P0} " +
                    $"| Consumed Fraction: {consumedFraction:F2} | Partial Fraction: {fractionIntoPartial:F2} " +
                    $"| Whole Items Remaining: {(state.IngestCount - itemsRemoved)}");

            // If ingested item is a supported drug type, otherwise give the pawn and its records the amount ingested.
            if (state.IsDrug)
            {
                FoodTrackerDrugEffects.ApplyIngestionEffects(state, drugType);
            }

            if (!state.IsDrug || drugType == FoodTrackerDrugType.Ambrosia || drugType == FoodTrackerDrugType.Beer)
            {
                float nutritionPerItem = state.ObjectDef.GetStatValueAbstract(StatDefOf.Nutrition);
                float nutritionConsumed = consumedFraction * nutritionPerItem;

                FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionConsumed);
            }
        }

        public static void HandleDestroyedFoodTrackerMeal(IngestionState state)
        {
            // Validate inputs
            if (state == null || state.Pawn == null || state.PreIngestObject == null || state.PreIngestObject.Destroyed)
            {
                Log.Warning($"[FoodTracker][T{state?.TraceID.ToString() ?? "?"}] Inputs are not valid. State Null: {state == null} " +
                    $"| Pawn Null: {state?.Pawn == null} | ThingDef Null: {state?.ObjectDef == null}");

                return;
            }

            CompFoodTracker tracker = state.PreIngestObject.TryGetComp<CompFoodTracker>();
            FoodTrackerDrugEffects.FoodTrackerDrugType drugType = FoodTrackingHelpers.GetDrugType(state.ObjectGameDef);

            // Calculate nutrition eaten.
            float consumedFraction = state.TotalFraction * state.IngestedFraction;

            // Used in FoodTracker loop to iterate through nutrition entries, nextItem is next item to process.
            float remainder = consumedFraction;
            float nextItem = 0;

            // Initialize items to remove from the stack that is dropped after interruption.
            int itemsRemoved = 0;
            
            while (remainder > 0f)
            {
                // Seperate cases for singletons and tracked nutrition lists.
                if (state.RemainingFractionsBefore.Count > 0)
                    nextItem = state.RemainingFractionsBefore[0];
                else
                    nextItem = tracker.PartialFraction;

                // If the next item is greater than nutrition remainder.
                if (remainder < nextItem)
                {
                    // Calculate the remaining nutrition in the partial.
                    float amountRemaining = nextItem - remainder;

                    // If nutrition is less than a meaningfull amount then consider fully consumed.
                    if (amountRemaining < FoodTrackingHelpers.MinimumPartialFraction)
                    {
                        remainder = 0f;
                        itemsRemoved++;

                        // Set either the singleton or the next item in the list with the remaining nutrition.
                        if (state.RemainingFractionsBefore.Count > 0)
                            state.RemainingFractionsBefore.RemoveAt(0);
                        else
                            tracker.PartialFraction = 0f;

                        break;
                    }
                    // Otherwise preserve the remaining nutrition.
                    if (state.RemainingFractionsBefore.Count > 0)
                        state.RemainingFractionsBefore[0] = amountRemaining;
                    else
                        tracker.PartialFraction = amountRemaining;

                    break;
                }

                remainder -= nextItem;
                itemsRemoved++;
                if (state.RemainingFractionsBefore.Count > 0)
                    state.RemainingFractionsBefore.RemoveAt(0);
            }

            // If the list is empty this clears it.
            if (state.RemainingFractionsBefore.Count == 0)
            {
                tracker.RemainingFractions.Clear();
            }
            // If the list has one item this sets the partial nutrition and clears it or if the ingest job was only one then only one partial meal can exist..
            else if (state.RemainingFractionsBefore.Count == 1 || state.IngestCount == 1)
            {
                tracker.PartialFraction = state.RemainingFractionsBefore[0];
                tracker.RemainingFractions.Clear();
            }
            // Otherwise treat as a stack of tracked meals, this resets the singleton and sets the actual nutrition lists from the working list.
            else
            {
                tracker.PartialFraction = -1f;
                tracker.RemainingFractions = state.RemainingFractionsBefore;
            }

            // If items to be removed equal or exceed the stack count then set the Thing for destruction.
            if (itemsRemoved >= state.PreIngestObject.stackCount)
            {
                state.ThingsToDestroy.Add(state.PreIngestObject);
                state.DestroyFoodAfterIngestion = true;
            }
            // Otherwise stubtract items to remove from the stack count.
            else
            {
                state.PreIngestObject.stackCount -= itemsRemoved;
            }

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{state.TraceID}] Eating interrupted: {state.ObjectDef.defName} (ID {state?.PostIngestObject?.thingIDNumber ?? 0}) " +
                    $"Pawn: {state.Pawn.LabelShort} | Ingest Count: {state.IngestCount} | Eaten: {state.IngestedFraction:P0} " +
                    $"| Total Nutrition: {state.TotalFraction:F2} | Consumed Fraction: {consumedFraction:F2} | Remaining Fraction: {(state.TotalFraction - consumedFraction ):F2} " +
                    $"| Partial Fraction: {(nextItem - remainder)} | Whole Items Remaining: {(state.IngestCount - itemsRemoved)}");

            // If ingested item is a supported drug type, otherwise give the pawn and its records the amount ingested.
            if (state.IsDrug)
            {
                FoodTrackerDrugEffects.ApplyIngestionEffects(state, drugType);
            }

            if (!state.IsDrug || drugType == FoodTrackerDrugType.Ambrosia || drugType == FoodTrackerDrugType.Beer)
            {
                float nutritionPerItem = state.ObjectDef.GetStatValueAbstract(StatDefOf.Nutrition);
                float nutritionConsumed = consumedFraction * nutritionPerItem;

                FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionConsumed);
            }
        }
    }
}