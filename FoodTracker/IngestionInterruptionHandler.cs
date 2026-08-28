using RimWorld;
using UnityEngine;
using Verse;

namespace FoodTracker
{
    public static class IngestionInterruptionHandler
    {
        public static void Handle(IngestionState state)
        {

            if (state == null || state.Pawn == null || state.Food == null || state.MealDef == null)
            {
                Log.Warning("[FoodTracker] Interruption state, food, meal def or pawn reference is null.");
                return;
            }

            // Rimworld may destroy original Thing, particularly when Un-Drafting a pawn eating on a stack.
            if (state.Food.Destroyed)
            {
                if (FoodTrackerSettings.Verbose)
                {
                    Log.Message($"[FoodTracker] {state.MealDef.defName ?? "NULL"} has been destroyed. Attempting to recover.");
                }

                // Checking to see if its a tracked meal.
                if (state.MealDef.defName.StartsWith("FoodTracker_"))
                {
                    DestroyedFoodRecovery.HandleDestroyedMeal(state);

                    return;
                }

                // Otherwise processing as a batch food item.
                else
                {
                    DestroyedFoodRecovery.HandleDestroyedBatchFood(state);

                    return;
                }
            }

            // Test if food item is stackable and calculate the number of items and nutrition eaten based on the fraction of the chewing time completed.
            if (FoodTrackingHelpers.IsBatchFood(state.Food))
            {
                // Calculate the number of items eaten and removed and nutrition eaten
                int itemsEaten = Mathf.Clamp(Mathf.FloorToInt(state.EatenFraction * state.IngestCount), 0, state.IngestCount);

                int itemsToRemove = Mathf.Min(itemsEaten, state.Food.stackCount);
                float nutritionOnStackEaten = itemsToRemove * state.NutritionPerItem;

                // Subtract the number of items eaten from the stack count and destroy the food if the batch is empty.
                state.Food.stackCount -= itemsToRemove;

                // Give the pawn and its records exactly the amount removed from the food.
                FoodTrackingHelpers.ApplyNutritionToPawn(state.Pawn, nutritionOnStackEaten);

                if (FoodTrackerSettings.Verbose)
                {
                    Log.Message($"[FoodTracker] Batch food interrupted. Nutrition Eaten: {nutritionOnStackEaten:F2}, " +
                        $"New Stack Count: {state.Food.stackCount}, " +
                        $"Items Eaten: {itemsEaten}, Food: {state.MealDef.defName}, Food ID: {state.Food.thingIDNumber}");
                }

                // Do NOT fall through to meal component consumption, as the stackable food has already been handled.
                return;
            }

            // Calculate nutrition to consume based on the fraction of the chewing time completed and the nutrition at the start of the eating action.
            float nutritionToConsume = state.NutritionAtStart * state.EatenFraction;

            // Calculate the actual nutrition removed from the food item, which is clamped to prevent exceeding the remaining nutrition.
            float nutritionRemoved = FoodTrackingHelpers.ConsumeNutritionFromFood(state.Food, nutritionToConsume);

            // Give the pawn and its records exactly the amount removed from the food.
            FoodTrackingHelpers.ApplyNutritionToPawn(state.Pawn, nutritionRemoved);

            // Get the remaining nutrition after consumption to create a partial meal or only output to log.
            float remainingNutrition = FoodTrackingHelpers.GetRemainingNutrition(state.Food);

            if (FoodTrackerSettings.Verbose)
            {
                Log.Message($"[FoodTracker] Meal food interrupted. Nutrition Eaten: {nutritionRemoved:F2}, NutritionRemaining: {remainingNutrition:F2}, " +
                    $"Food: {state.MealDef.defName}, Food ID: {state.Food.thingIDNumber}");
            }

            // Check if we just processed a partial meal or a vanilla meal, i.e. if its being tracked already
            if (state.MealDef.defName.StartsWith("FoodTracker_"))
            {
                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker] {state.MealDef.defName} is already tracked, exiting interruption.");

                return;
            }

            ThingDef trackerDef = DynamicMealDefFactory.CreateTrackerMeal(state.MealDef);

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker] Replacing {state.MealDef.defName}, Food ID: {state.Food.thingIDNumber} with {trackerDef?.defName ?? "NULL"}");

            // Create a new Thing to represent the new meal, and drop it in the world.
            Thing newFood = PartialMealFactory.ReplaceAndDropPartialMeal(state, remainingNutrition);

            if (newFood == null)
            {
                Log.Warning($"[FoodTracker] Failed to make partial {newFood?.def.defName ?? "NULL"} near {state.Pawn.Label}. Food ID: {newFood?.thingIDNumber ?? 0}");
            }

        }
    }
}
