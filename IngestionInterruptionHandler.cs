using UnityEngine;
using Verse;

namespace FoodTracker
{
    public static class IngestionInterruptionHandler
    {
        public static void Handle(IngestionState state)
        {

            if (state == null || state.Pawn == null || state.PostFood == null || state.FoodDef == null)
            {
                Log.Warning($"[FoodTracker][T{state?.TraceID.ToString() ?? "?"}] Inputs are not valid. State Null: {state == null} | Pawn Null: {state.Pawn == null} " +
                    $"| Food Null: {state.PostFood == null} | ThingDef Null: {state.FoodDef == null}");

                return;
            }

            CompFoodTracker tracker = state.PostFood.TryGetComp<CompFoodTracker>();

            // Rimworld may destroy original Thing, particularly when Un-Drafting a pawn eating on a stack.
            if (state.PostFood.Destroyed)
            {
                if (tracker != null)
                {
                    if (FoodTrackerSettings.Verbose)
                        Log.Message($"[FoodTracker][T{state.TraceID}] {state.FoodDef.defName} (ID {state?.PostFood?.thingIDNumber ?? 0}) " +
                            $"| Food Thing reference has been .Destroyed(), handling with DestroyedFoodRecovery.");

                    // Otherwise process as a destroyed FoodTracker meal.
                    DestroyedFoodRecovery.HandleDestroyedFoodTrackerMeal(state);

                    return;
                }
                else
                {
                    if (FoodTrackerSettings.Verbose)
                        Log.Message($"[FoodTracker][T{state.TraceID}] {state.FoodDef.defName} (ID {state?.PostFood?.thingIDNumber ?? 0}) " +
                            $"| Food Thing reference has been .Destroyed(), handling with DestroyedFoodRecovery.");

                    // Otherwise process as a destroyed meal.
                    DestroyedFoodRecovery.HandleDestroyedMeal(state);

                    return;
                }
            }

            // Calculate nutrition eaten.
            float nutritionEaten = state.TotalNutrition * state.EatenFraction;

            // Used in FoodTracker loop to iterate through nutrition entries, nextItem is next item to process.
            float nutritionRemainder = nutritionEaten;
            float nextItem = 0;

            // Initialize items to remove from the stack that is dropped after interruption.
            int itemsRemoved = 0;

            if (tracker != null)
            {

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
                    // Also.. Singleton FT meal.
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
                if (itemsRemoved >= state.PostFood.stackCount)
                {
                    state.ThingsToDestroy.Add(state.PostFood);
                    state.DestroyFoodAfterIngestion = true;
                }
                // Otherwise stubtract items to remove from the stack count.
                else
                {
                    state.PostFood.stackCount -= itemsRemoved;
                }

                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker][T{state.TraceID}] Eating interrupted: {state.FoodDef.defName} (ID {state.PostFood.thingIDNumber}) " +
                        $"| Pawn: {state.Pawn.LabelShort} | Ingest Count: {state.IngestCount} | Eaten: {state.EatenFraction:P0} " +
                        $"| Total Nutrition: {state.TotalNutrition:F2} | Total Consumed: {nutritionEaten:F2} | Total Remaining: {(state.TotalNutrition - nutritionEaten):F2} " +
                        $"| Partial Nutrition: {(nextItem - nutritionRemainder)} | Whole Items Remaining: {(state.IngestCount - itemsRemoved)}");

                // Give the pawn and its records exactly the amount removed from the food.
                FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionEaten);

                return;

            }

            // Calculate exact fraction eaten and whole items eaten.
            float exactItemsEaten = nutritionEaten / state.NutritionPerItem;

            // Calculate nutrition to go into a partial and items to remove from stack.
            itemsRemoved = Mathf.CeilToInt(exactItemsEaten);
            float nutritionIntoPartial = ((state.TotalNutrition - nutritionEaten) % state.NutritionPerItem);

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{state.TraceID}] Eating interrupted: {state.FoodDef.defName} (ID {state.PostFood.thingIDNumber}) " +
                    $"| Pawn: {state.Pawn.LabelShort} | Ingest Count: {state.IngestCount} | Eaten: {state.EatenFraction:P0} " +
                    $"| Total Nutrition: {state.TotalNutrition:F2} | Total Consumed: {nutritionEaten:F2} | Total Remaining: {(state.TotalNutrition - nutritionEaten):F2} " +
                    $"| Partial Nutrition: {nutritionIntoPartial:F2} | Whole Items Remaining: {(state.IngestCount - itemsRemoved)}");

            // Create a new Thing to represent the new meal, and drop it in the world.
            Thing newFood = PartialMealFactory.CreateAndDropPartialMeal(state, nutritionIntoPartial, state.Pawn.Position);

            if (newFood == null)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Failed to make {state.TrackerDef.defName} (ID {newFood?.thingIDNumber ?? 0})");

                // If failed to create a partial meal then remove one from items to remove and correct nutrition to give to pawn.
                float nutritionCorrection = itemsRemoved * state.NutritionPerItem;
                itemsRemoved--;

                // If items to be removed equal or exceed the stack count then set the Thing for destruction.
                if (itemsRemoved >= state.PostFood.stackCount)
                {
                    state.ThingsToDestroy.Add(state.PostFood);
                    state.DestroyFoodAfterIngestion = true;
                }
                // Otherwise stubtract items to remove from the stack count.
                else
                {
                    state.PostFood.stackCount -= itemsRemoved;
                }

                // Give the pawn and its records exactly the amount removed from the food.
                FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionCorrection);

                return;
            }

            // If items to be removed equal or exceed the stack count then set the Thing for destruction.
            if (itemsRemoved >= state.PostFood.stackCount)
            {
                state.ThingsToDestroy.Add(state.PostFood);
                state.DestroyFoodAfterIngestion = true;
            }
            // Otherwise stubtract items to remove from the stack count.
            else
            {
                state.PostFood.stackCount -= itemsRemoved;
            }

            // Give the pawn and its records exactly the amount removed from the food.
            FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionEaten);

            return;

        }
    }
}
