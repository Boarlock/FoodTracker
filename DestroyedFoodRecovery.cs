using UnityEngine;
using Verse;

namespace FoodTracker
{
    public static class DestroyedFoodRecovery
    {
        // Handle edge cases where vanilla destroys the food instance before we can instantiate a partial
        public static void HandleDestroyedMeal(IngestionState state)
        {
            // Validate inputs
            if (state == null || state.Pawn == null || state.PreFood == null || state.PreFood.Destroyed)
            {
                Log.Warning($"[FoodTracker][T{state?.TraceID.ToString() ?? "?"}] Inputs are not valid. State Null: {state == null} " +
                    $"| Pawn Null: {state?.Pawn == null} | ThingDef Null: {state?.FoodDef == null}");

                return;
            }

            float nutritionEaten = state.TotalNutrition * state.EatenFraction;
            float exactItemsEaten = nutritionEaten / state.NutritionPerItem;
            float nutritionIntoPartial = ((state.TotalNutrition - nutritionEaten) % state.NutritionPerItem);
            int itemsRemoved = Mathf.CeilToInt(exactItemsEaten);

            // Create the partial meal and drop at specified cell
            Thing partialMeal = PartialMealFactory.CreateAndDropPartialMeal(state, nutritionIntoPartial, state.FoodCell);

            if (partialMeal == null)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Failed to make {partialMeal?.def.defName ?? "NULL"} (ID {partialMeal?.thingIDNumber ?? 0})");

                // If failed to create a partial meal then remove one from items to remove and correct nutrition to give to pawn.
                float nutritionCorrection = itemsRemoved * state.NutritionPerItem;
                itemsRemoved--;

                // If items to be removed equal or exceed the stack count then set the Thing for destruction.
                if (itemsRemoved >= state.PreFood.stackCount)
                {
                    state.ThingsToDestroy.Add(state.PreFood);
                    state.DestroyFoodAfterIngestion = true;
                }
                // Otherwise stubtract items to remove from the stack count.
                else
                {
                    state.PreFood.stackCount -= itemsRemoved;
                }

                // Give the pawn and its records exactly the amount removed from the food.
                FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionCorrection);

                return;
            }
            // Remove the consumed physical meals from the stack.
            if (itemsRemoved >= state.PreFood.stackCount)
            {
                state.ThingsToDestroy.Add(state.PreFood);
                state.DestroyFoodAfterIngestion = true;
            }
            // Otherwise stubtract items to remove from the stack count.
            else
            {
                state.PreFood.stackCount -= itemsRemoved;
            }

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{state.TraceID}] Eating interrupted: {state.FoodDef.defName} (ID {state?.PostFood?.thingIDNumber ?? 0}) " +
                    $"Pawn: {state.Pawn.LabelShort} | Ingest Count: {state.IngestCount} | Eaten: {state.EatenFraction:P0} " +
                    $"| Total Consumed: {nutritionEaten:F2} | Partial Nutrition: {nutritionIntoPartial:F2} " +
                    $"| Whole Items Remaining: {(state.IngestCount - itemsRemoved)}");

            // Give the pawn and its records exactly the amount removed from the food.
            FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionEaten);

        }

        public static void HandleDestroyedFoodTrackerMeal(IngestionState state)
        {
            // Validate inputs
            if (state == null || state.Pawn == null || state.PreFood == null || state.PreFood.Destroyed)
            {
                Log.Warning($"[FoodTracker][T{state?.TraceID.ToString() ?? "?"}] Inputs are not valid. State Null: {state == null} " +
                    $"| Pawn Null: {state?.Pawn == null} | ThingDef Null: {state?.FoodDef == null}");

                return;
            }

            CompFoodTracker tracker = state.PreFood.TryGetComp<CompFoodTracker>();

            // Calculate nutrition eaten.
            float nutritionEaten = state.TotalNutrition * state.EatenFraction;

            // Used in FoodTracker loop to iterate through nutrition entries, nextItem is next item to process.
            float nutritionRemainder = nutritionEaten;
            float nextItem = 0;

            // Initialize items to remove from the stack that is dropped after interruption.
            int itemsRemoved = 0;
            
            while (nutritionRemainder > 0f)
            {
                // Seperate cases for singletons and tracked nutrition lists.
                if (state.NutritionEntriesBefore.Count > 0)
                    nextItem = state.NutritionEntriesBefore[0];
                else
                    nextItem = tracker.PartialNutrition;

                if (nutritionRemainder < nextItem)
                {
                    // Set either the singleton or the next item in the list with the remaining nutrition.
                    if (state.NutritionEntriesBefore.Count > 0)
                        state.NutritionEntriesBefore[0] = nextItem - nutritionRemainder;
                    else
                        tracker.PartialNutrition = nextItem - nutritionRemainder;

                    break;
                }

                nutritionRemainder -= nextItem;
                itemsRemoved++;
                if (state.NutritionEntriesBefore.Count > 0)
                    state.NutritionEntriesBefore.RemoveAt(0);
            }

            // If the list is empty this clears it.
            if (state.NutritionEntriesBefore.Count == 0)
            {
                tracker.NutritionEntries.Clear();
            }
            // If the list has one item this sets the partial nutrition and clears it.
            else if (state.NutritionEntriesBefore.Count == 1)
            {
                tracker.PartialNutrition = state.NutritionEntriesBefore[0];
                tracker.NutritionEntries.Clear();
            }
            // If the ingest job was only one then only one partial meal can exist.
            else if (state.IngestCount == 1)
            {
                tracker.PartialNutrition = state.NutritionEntriesBefore[0];
                tracker.NutritionEntries.Clear();
            }
            // Otherwise treat as a stack of tracked meals, this resets the singleton and sets the actual nutrition lists from the working list.
            else
            {
                tracker.PartialNutrition = -1f;
                tracker.NutritionEntries = state.NutritionEntriesBefore;
            }

            // If items to be removed equal or exceed the stack count then set the Thing for destruction.
            if (itemsRemoved >= state.PreFood.stackCount)
            {
                state.ThingsToDestroy.Add(state.PreFood);
                state.DestroyFoodAfterIngestion = true;
            }
            // Otherwise stubtract items to remove from the stack count.
            else
            {
                state.PreFood.stackCount -= itemsRemoved;
            }

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{state.TraceID}] Eating interrupted: {state.FoodDef.defName} (ID {state?.PostFood?.thingIDNumber ?? 0}) " +
                    $"Pawn: {state.Pawn.LabelShort} | Ingest Count: {state.IngestCount} | Eaten: {state.EatenFraction:P0} " +
                    $"| Total Nutrition: {state.TotalNutrition:F2} | Total Consumed: {nutritionEaten:F2} | Total Remaining: {(state.TotalNutrition - nutritionEaten):F2} " +
                    $"| Partial Nutrition: {(nextItem - nutritionRemainder)} | Whole Items Remaining: {(state.IngestCount - itemsRemoved)}");

            // Give the pawn and its records exactly the amount removed from the food.
            FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionEaten);

        }
    }
}