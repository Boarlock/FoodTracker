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

            // Calculations to be used by both batch foods and meals, total nutrition eaten, and total items eaten.
            float nutritionEaten = state.TotalNutrition * state.EatenFraction;
            float itemsEatenExact = nutritionEaten / state.NutritionPerItem;

            // Determine how many whole 'meals' were eaten rounded down.
            int wholeItemsEaten = Mathf.FloorToInt(itemsEatenExact);
            int itemsToRemove;

            // Rimworld may destroy original Thing, particularly when Un-Drafting a pawn eating on a stack.
            if (state.Food.Destroyed)
            {
                // Checking to see if its a destroyed batch food.
                if (FoodTrackingHelpers.IsBatchFood(state))
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

            if (FoodTrackingHelpers.IsBatchFood(state))
            {

                // Vanilla restored the food to the stack and no whole batch items were consumed.
                if (wholeItemsEaten <= 0)
                {
                    return;
                }

                // Never remove move than what's in the current stack.
                itemsToRemove = Mathf.Min(wholeItemsEaten, state.Food.stackCount);
                float nutritionOnStackEaten = itemsToRemove * state.NutritionPerItem;

                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker][T{state.TraceID}] Eating interrupted: {state.FoodDef.defName} (ID {state.Food.thingIDNumber}) " +
                        $"| Pawn: {state.Pawn.LabelShort} | Ingest Count: {state.IngestCount} | Eaten: {state.EatenFraction:P0} " +
                        $"| Total Nutrition: {state.TotalNutrition:F2} | Total Consumed: {nutritionEaten:F2} " +
                        $"| Total Remaining: {(state.NutritionPerItem * (state.IngestCount - wholeItemsEaten)):F2} " +
                        $"| Whole Items Remaining: {(state.IngestCount - wholeItemsEaten)}");

                // Only remove items up to stackCount, if it would leave stackCount at 0 then simply delte the object
                if (itemsToRemove >= state.Food.stackCount)
                    state.Food.Destroy(DestroyMode.Vanish);
                else
                    state.Food.stackCount -= itemsToRemove;

                // Give the pawn and its records the amount removed from the food.
                FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionOnStackEaten);


                return;
            }

            // Then determine how much of a partial meal is leftover and how much nutrition is leftover.
            float partialMealEatenFraction = itemsEatenExact - wholeItemsEaten;
            float partialMealRemainingFraction = 1f - partialMealEatenFraction;
            float partialMealNutrition = partialMealRemainingFraction * state.NutritionAtStart;

            itemsToRemove = Mathf.Min(Mathf.CeilToInt(itemsEatenExact), state.Food.stackCount);

            CompFoodTracker tracker = state.Food.TryGetComp<CompFoodTracker>();

            // If meal doesn't have our component we have to spawn a new meal with out tracked ThingDef.
            if (tracker == null)
            {

                // Create a new Thing to represent the new meal, and drop it in the world.
                Thing newFood = PartialMealFactory.ReplaceAndDropPartialMeal(state, partialMealNutrition);

                if (newFood == null)
                {
                    Log.Warning($"[FoodTracker][T{state.TraceID}] Failed to make {state.TrackerDef.defName} (ID {newFood?.thingIDNumber ?? 0})");

                    itemsToRemove--;

                    if (itemsToRemove >= state.Food.stackCount)
                        state.Food.Destroy(DestroyMode.Vanish);
                    else
                        state.Food.stackCount -= itemsToRemove;

                    // Give the pawn and its records exactly the amount removed from the food.
                    FoodTrackingHelpers.ApplyNutritionToPawn(state, (wholeItemsEaten * state.NutritionPerItem));

                    return;
                }

                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker][T{state.TraceID}] Eating interrupted: {state.FoodDef.defName} (ID {state.Food.thingIDNumber}) " +
                        $"| Pawn: {state.Pawn.LabelShort} | Ingest Count: {state.IngestCount} | Eaten: {state.EatenFraction:P0} " +
                        $"| Total Nutrition: {state.TotalNutrition:F2} | Total Consumed: {nutritionEaten:F2} | Total Remaining: {(state.TotalNutrition - nutritionEaten):F2} " +
                        $"| Partial Meal: {newFood.def.defName} (ID {newFood.thingIDNumber}) | Partial Nutrition: {partialMealNutrition:F2} " +
                        $"| Whole Items Remaining: {(state.IngestCount - wholeItemsEaten)}");

                // Only remove items up to stackCount, if it would leave stackCount at 0 then simply delte the object
                if (itemsToRemove >= state.Food.stackCount)
                    state.Food.Destroy(DestroyMode.Vanish);
                else
                    state.Food.stackCount -= itemsToRemove;

                // Give the pawn and its records exactly the amount removed from the food.
                FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionEaten);

                return;
            }

            // Set remaining nutrition on the partial meal
            tracker.SetRemainingNutrition(partialMealNutrition);

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{state.TraceID}] Eating interrupted: {state.FoodDef.defName} (ID {state.Food.thingIDNumber}) " +
                    $"| Pawn: {state.Pawn.LabelShort} | Ingest Count: {state.IngestCount} | Eaten: {state.EatenFraction:P0} " +
                    $"| Total Nutrition: {state.TotalNutrition:F2} | Total Consumed: {nutritionEaten:F2} | Total Remaining: {(state.TotalNutrition - nutritionEaten):F2} " +
                    $"| Partial Nutrition: {partialMealNutrition:F2} | Whole Items Remaining: {(state.IngestCount - wholeItemsEaten)}");

            // Give the pawn and its records exactly the amount removed from the food.
            FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionEaten);

        }
    }
}
