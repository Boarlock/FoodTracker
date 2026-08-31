using RimWorld;
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
                Log.Warning($"[FoodTracker][T{state.TraceID}] Inputs are not valid. State Null: {state == null} | Pawn Null: {state.Pawn == null} " +
                    $"| Food Null: {state.Food == null} | ThingDef Null: {state.FoodDef == null}");

                return;
            }

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

                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker][T{state.TraceID}] {state.FoodDef.defName} (ID {state?.Food?.thingIDNumber ?? 0}) " +
                        $"| Food Thing reference has been .Destroyed(), handling with DestroyedFoodRecovery.");

                // Otherwise process as a destroyed meal.
                DestroyedFoodRecovery.HandleDestroyedMeal(state);

                return;
            }

            float nutritionEaten = state.TotalNutrition * state.EatenFraction;
            int itemsRemoved = 0;

            // FoodTracker Meal route. 
            CompFoodTracker tracker = state.Food.TryGetComp<CompFoodTracker>();
            if (tracker != null)
            {

                float nutritionRemainder = nutritionEaten;

                float nextItem = 0f;

                while (nutritionRemainder > 0f && state.NutritionEntriesBefore.Count > 0)
                {
                    nextItem = state.NutritionEntriesBefore[0];

                    if (nutritionRemainder < nextItem)
                    {
                        // The current item was only partially consumed.
                        state.NutritionEntriesBefore[0] = (nextItem - nutritionRemainder);
                        break;
                    }

                    // The entire item at index 0 was consumed.
                    nutritionRemainder -= nextItem;
                    state.NutritionEntriesBefore.RemoveAt(0);
                    itemsRemoved++;
                }

                tracker.NutritionEntries = state.NutritionEntriesBefore;

                // Remove the consumed physical meals from the stack.
                if (itemsRemoved >= state.Food.stackCount)
                {
                    state.Food.Destroy(DestroyMode.Vanish);
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

                // Only remove items up to stackCount, if it would leave stackCount at 0 then simply delte the object
                if (itemsRemoved >= state.Food.stackCount)
                    state.Food.Destroy(DestroyMode.Vanish);
                else
                    state.Food.stackCount -= itemsRemoved;

                // Give the pawn and its records the amount removed from the food.
                FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionEaten);

                return;
            }

            // Calculate nutrition to go into a partial and items to remove
            float nutritionIntoPartial = nutritionEaten - (wholeItemsEaten * state.NutritionPerItem);
            itemsRemoved = Mathf.CeilToInt(exactItemsEaten);

            // Create a new Thing to represent the new meal, and drop it in the world.
            Thing newFood = PartialMealFactory.ReplaceAndDropPartialMeal(state, nutritionIntoPartial);

            if (newFood == null)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Failed to make {state.TrackerDef.defName} (ID {newFood?.thingIDNumber ?? 0})");

                float nutritionCorrection = itemsRemoved * state.NutritionPerItem;

                itemsRemoved--;

                if (itemsRemoved >= state.Food.stackCount)
                    state.Food.Destroy(DestroyMode.Vanish);
                else
                    state.Food.stackCount -= itemsRemoved;

                // Give the pawn and its records exactly the amount removed from the food.
                FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionCorrection);

                return;
            }

            // Remove the consumed physical meals from the stack.
            if (itemsRemoved >= state.Food.stackCount)
                state.Food.Destroy(DestroyMode.Vanish);
            else
                state.Food.stackCount -= itemsRemoved;

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{state.TraceID}] Eating interrupted: {state.FoodDef.defName} (ID {state.Food.thingIDNumber}) " +
                    $"| Pawn: {state.Pawn.LabelShort} | Ingest Count: {state.IngestCount} | Eaten: {state.EatenFraction:P0} " +
                    $"| Total Nutrition: {state.TotalNutrition:F2} | Total Consumed: {nutritionEaten:F2} | Total Remaining: {(state.TotalNutrition - nutritionEaten):F2} " +
                    $"| Partial Meal: {newFood.def.defName} (ID {newFood.thingIDNumber}) | Partial Nutrition: {nutritionIntoPartial:F2} " +
                    $"| Whole Items Remaining: {(state.IngestCount - wholeItemsEaten)}");

            // Give the pawn and its records exactly the amount removed from the food.
            FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionEaten);

            return;
            
        }
    }
}
