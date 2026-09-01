using UnityEngine;
using Verse;

namespace FoodTracker
{
    public static class IngestionInterruptionHandler
    {
        public static void Handle(IngestionState state)
        {

            if (state == null || state.Pawn == null || state.Food == null || state.FoodDef == null)
            {
                Log.Warning($"[FoodTracker][T{state?.TraceID.ToString() ?? "?"}] Inputs are not valid. State Null: {state == null} | Pawn Null: {state.Pawn == null} " +
                    $"| Food Null: {state.Food == null} | ThingDef Null: {state.FoodDef == null}");

                return;
            }

            CompFoodTracker tracker = state.Food.TryGetComp<CompFoodTracker>();

            // Rimworld may destroy original Thing, particularly when Un-Drafting a pawn eating on a stack.
            if (state.Food.Destroyed)
            {
                // Checking to see if its a destroyed batch food.
                if (FoodTrackingHelpers.IsBatchFood(state.FoodDef))
                {
                    if (FoodTrackerSettings.Verbose)
                        Log.Message($"[FoodTracker][T{state.TraceID}] {state.FoodDef.defName} (ID {state.Food.thingIDNumber}) " +
                            $"| Food Thing reference has been .Destroyed(), handling with DestroyedFoodRecovery.");

                    DestroyedFoodRecovery.HandleDestroyedBatchFood(state);

                    return;
                }
                else if (tracker != null)
                {
                    if (FoodTrackerSettings.Verbose)
                        Log.Message($"[FoodTracker][T{state.TraceID}] {state.FoodDef.defName} (ID {state?.Food?.thingIDNumber ?? 0}) " +
                            $"| Food Thing reference has been .Destroyed(), handling with DestroyedFoodRecovery.");

                    // Otherwise process as a destroyed FoodTracker meal.
                    DestroyedFoodRecovery.HandleDestroyedFoodTrackerMeal(state);

                    return;
                }
                else
                {
                    if (FoodTrackerSettings.Verbose)
                        Log.Message($"[FoodTracker][T{state.TraceID}] {state.FoodDef.defName} (ID {state?.Food?.thingIDNumber ?? 0}) " +
                            $"| Food Thing reference has been .Destroyed(), handling with DestroyedFoodRecovery.");

                    // Otherwise process as a destroyed meal.
                    DestroyedFoodRecovery.HandleDestroyedMeal(state);

                    return;
                }

            }

            float nutritionEaten = state.TotalNutrition * state.EatenFraction;
            float nutritionRemainder = nutritionEaten;
            int itemsRemoved = 0;
            float nextItem = 0;

            if (tracker != null)
            {

                while (nutritionRemainder > 0f)
                {
                    if (state.NutritionEntriesBefore.Count > 0)
                        nextItem = state.NutritionEntriesBefore[0];
                    else
                        nextItem = tracker.PartialNutrition;

                    if (nutritionRemainder < nextItem)
                    {
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

                // Now the nutrition representation is authoritative.
                if (state.NutritionEntriesBefore.Count == 0)
                {
                    // Singleton FT meal. PartialNutrition was already updated above.
                    tracker.NutritionEntries.Clear();
                }
                else if (state.NutritionEntriesBefore.Count == 1)
                {
                    // Singleton FT meal.
                    tracker.PartialNutrition = state.NutritionEntriesBefore[0];
                    tracker.NutritionEntries.Clear();
                }
                else if (state.IngestCount == 1)
                {
                    // Also.. Singleton FT meal.
                    tracker.PartialNutrition = state.NutritionEntriesBefore[0];
                    tracker.NutritionEntries.Clear();
                }
                else
                {
                    // Stack FT meal. Clear the singleton and restore the nutrition entries.
                    tracker.PartialNutrition = -1f;
                    tracker.NutritionEntries = state.NutritionEntriesBefore;
                }

                // Now synchronize the physical Thing.
                if (itemsRemoved >= state.Food.stackCount)
                {
                    state.FoodToDestroy = state.Food;
                    state.DestroyFoodAfterIngestion = true;
                }
                else
                {
                    state.Food.stackCount -= itemsRemoved;
                }


                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker][T{state.TraceID}] Eating interrupted: {state.FoodDef.defName} (ID {state.Food.thingIDNumber}) " +
                        $"| Pawn: {state.Pawn.LabelShort} | Ingest Count: {state.IngestCount} | Eaten: {state.EatenFraction:P0} " +
                        $"| Total Nutrition: {state.TotalNutrition:F2} | Total Consumed: {nutritionEaten:F2} | Total Remaining: {(state.TotalNutrition - nutritionEaten):F2} " +
                        $"| Partial Nutrition: {(nextItem - nutritionRemainder)} | Whole Items Remaining: {(state.StartingStackCount - itemsRemoved)}");

                // Give the pawn and its records exactly the amount removed from the food.
                FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionEaten);

                return;

            }

            // Calculate exact, whole and nutrition eaten, and nutrition leftover in food.
            float exactItemsEaten = nutritionEaten / state.NutritionPerItem;
            int wholeItemsEaten = Mathf.FloorToInt(exactItemsEaten);

            if (FoodTrackingHelpers.IsBatchFood(state.FoodDef))
            {
                // Vanilla restored the food to the stack and no whole batch items were consumed.
                if (wholeItemsEaten <= 0)
                {
                    return;
                }

                // For batch foods simply round to nearest int.
                itemsRemoved = Mathf.RoundToInt(exactItemsEaten);
                nutritionEaten = itemsRemoved * state.NutritionPerItem;

                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker][T{state.TraceID}] Eating interrupted: {state.FoodDef.defName} (ID {state.Food.thingIDNumber}) " +
                        $"| Pawn: {state.Pawn.LabelShort} | Ingest Count: {state.IngestCount} | Eaten: {state.EatenFraction:P0} " +
                        $"| Total Nutrition: {state.TotalNutrition:F2} | Whole Items Remaining: {(state.IngestCount - wholeItemsEaten)}");

                if (itemsRemoved >= state.Food.stackCount)
                {
                    state.FoodToDestroy = state.Food;
                    state.DestroyFoodAfterIngestion = true;
                }
                else
                    state.Food.stackCount -= itemsRemoved;

                // Give the pawn and its records the amount removed from the food.
                FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionEaten);

                return;
            }

            // Calculate nutrition to go into a partial and items to remove
            float nutritionIntoPartial = state.TotalNutrition - nutritionEaten;
            itemsRemoved = Mathf.CeilToInt(exactItemsEaten);

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{state.TraceID}] Eating interrupted: {state.FoodDef.defName} (ID {state.Food.thingIDNumber}) " +
                    $"| Pawn: {state.Pawn.LabelShort} | Ingest Count: {state.IngestCount} | Eaten: {state.EatenFraction:P0} " +
                    $"| Total Nutrition: {state.TotalNutrition:F2} | Total Consumed: {nutritionEaten:F2} | Total Remaining: {(state.TotalNutrition - nutritionEaten):F2} " +
                    $"| Partial Nutrition: {nutritionIntoPartial:F2} | Whole Items Remaining: {(state.IngestCount - wholeItemsEaten)}");

            // Create a new Thing to represent the new meal, and drop it in the world.
            Thing newFood = PartialMealFactory.CreateAndDropPartialMeal(state, nutritionIntoPartial, state.Pawn.Position);

            if (newFood == null)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Failed to make {state.TrackerDef.defName} (ID {newFood?.thingIDNumber ?? 0})");

                float nutritionCorrection = itemsRemoved * state.NutritionPerItem;

                itemsRemoved--;

                if (itemsRemoved >= state.Food.stackCount)
                {
                    state.FoodToDestroy = state.Food;
                    state.DestroyFoodAfterIngestion = true;
                }
                else
                    state.Food.stackCount -= itemsRemoved;

                // Give the pawn and its records exactly the amount removed from the food.
                FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionCorrection);

                return;
            }

            if (itemsRemoved >= state.Food.stackCount)
            {
                state.FoodToDestroy = state.Food;
                state.DestroyFoodAfterIngestion = true;
            }
            else
                state.Food.stackCount -= itemsRemoved;

            // Give the pawn and its records exactly the amount removed from the food.
            FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionEaten);

            return;

        }
    }
}
