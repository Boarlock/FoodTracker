using RimWorld;
using UnityEngine;
using Verse;

namespace FoodTracker
{
    public static class IngestionInterruptionHandler
    {
        public static void Handle(IngestionState state)
        {

            if (state == null || state.Pawn == null || state.PostIngestObject == null || state.ObjectDef == null)
            {
                Log.Warning($"[FoodTracker][T{state?.TraceID.ToString() ?? "?"}] Inputs are not valid. State Null: {state == null} | Pawn Null: {state.Pawn == null} " +
                    $"| Food Null: {state.PostIngestObject == null} | ThingDef Null: {state.ObjectDef == null}");

                return;
            }

            CompFoodTracker tracker = state.PostIngestObject.TryGetComp<CompFoodTracker>();
            FoodTrackerDrugEffects.FoodTrackerDrugType drugType = FoodTrackingHelpers.GetDrugType(state.ObjectGameDef);

            // Calculate nutrition eaten.
            float consumedFraction = state.TotalFraction * state.IngestedFraction;

            // Used in FoodTracker loop to iterate through nutrition entries, nextItem is next item to process.
            float remainder = consumedFraction;
            float nextItem = 0;

            // Initialize items to remove from the stack that is dropped after interruption.
            int itemsRemoved = 0;

            if (tracker != null)
            {

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

                // If the working list is empty this clears the real list.
                if (state.RemainingFractionsBefore.Count == 0)
                {
                    tracker.RemainingFractions.Clear();
                }
                // If the list has one item this sets the partial nutrition and clears it or if the ingest job was only one then only one partial meal can exist.
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

                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker][T{state.TraceID}] Eating interrupted: {state.ObjectDef.defName} (ID {state.PostIngestObject.thingIDNumber}) " +
                        $"| Pawn: {state.Pawn.LabelShort} | Ingest Count: {state.IngestCount} | Eaten: {state.IngestedFraction:P0} " +
                        $"| Total Nutrition: {state.TotalFraction:F2} | Consumed Fraction: {consumedFraction:F2} | Remaining Fraction: {(state.TotalFraction - consumedFraction):F2} " +
                        $"| Partial Fraction: {(nextItem - remainder)} | Whole Items Remaining: {(state.IngestCount - itemsRemoved - 1)}");

                // If ingested item is a supported drug type, otherwise give the pawn and its records the amount ingested.
                if (state.IsDrug)
                {
                    FoodTrackerDrugEffects.ApplyIngestionEffects(state, drugType);
                }

                if (!state.IsDrug || drugType == FoodTrackerDrugEffects.FoodTrackerDrugType.Ambrosia || drugType == FoodTrackerDrugEffects.FoodTrackerDrugType.Beer)
                {
                    float nutritionPerItem = state.ObjectDef.GetStatValueAbstract(StatDefOf.Nutrition);
                    float nutritionConsumed = consumedFraction * nutritionPerItem;

                    FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionConsumed);
                }

                return;

            }

            // Calculate nutrition to go into a partial and items to remove from stack.
            itemsRemoved = Mathf.CeilToInt(consumedFraction);
            float fractionIntoPartial = ((state.TotalFraction - consumedFraction) % 1f);

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{state.TraceID}] Eating interrupted: {state.ObjectDef.defName} (ID {state.PostIngestObject.thingIDNumber}) " +
                    $"| Pawn: {state.Pawn.LabelShort} | Ingest Count: {state.IngestCount} | Eaten: {state.IngestedFraction:P0} " +
                    $"| Consumed Fraction: {consumedFraction:F2} | Partial Fraction: {fractionIntoPartial:F2} " +
                    $"| Whole Items Remaining: {(state.IngestCount - itemsRemoved)}");

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

                // If ingested item is a supported drug type, otherwise give the pawn and its records the amount ingested.
                if (state.IsDrug)
                {
                    FoodTrackerDrugEffects.ApplyIngestionEffects(state, drugType);
                }

                if (!state.IsDrug || drugType == FoodTrackerDrugEffects.FoodTrackerDrugType.Ambrosia || drugType == FoodTrackerDrugEffects.FoodTrackerDrugType.Beer)
                {
                    float nutritionPerItem = state.ObjectDef.GetStatValueAbstract(StatDefOf.Nutrition);
                    float nutritionConsumed = consumedFraction * nutritionPerItem;

                    FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionConsumed);
                }

                return;
            }

            // Create a new Thing to represent the new meal, and drop it in the world.
            Thing newFood = PartialMealFactory.CreateAndDropPartialMeal(state, fractionIntoPartial, state.Pawn.Position);

            // If failed to create a partial meal then remove one from items to remove and correct nutrition to give to pawn.
            if (newFood == null)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Failed to make {state.ObjectTrackerDef.defName} (ID {newFood?.thingIDNumber ?? 0})");

                return;
            }

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

            // If ingested item is a supported drug type, otherwise give the pawn and its records the amount ingested.
            if (state.IsDrug)
            {
                FoodTrackerDrugEffects.ApplyIngestionEffects(state, drugType);
            }

            if (!state.IsDrug || drugType == FoodTrackerDrugEffects.FoodTrackerDrugType.Ambrosia || drugType == FoodTrackerDrugEffects.FoodTrackerDrugType.Beer)
            {
                float nutritionPerItem = state.ObjectDef.GetStatValueAbstract(StatDefOf.Nutrition);
                float nutritionConsumed = consumedFraction * nutritionPerItem;

                FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionConsumed);
            }
        }
    }
}
