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
                Log.Warning($"[FoodTracker] Inputs are not valid. State Null: {state == null}, Pawn Null: {state.Pawn == null}, " +
                    $"Food Null: {state.Food == null}, ThingDef Null: {state.MealDef == null}");
                return;
            }

            // Rimworld may destroy original Thing, particularly when Un-Drafting a pawn eating on a stack.
            if (state.Food.Destroyed)
            {
                // Checking to see if its a destroyed batch food.
                if (FoodTrackingHelpers.IsBatchFood(state.MealDef))
                {
                    if (FoodTrackerSettings.Verbose)
                        Log.Message($"[FoodTracker] {state?.MealDef?.defName ?? "NULL"}, Food ID: {state?.Food?.thingIDNumber ?? 0} " +
                            $"has been destroyed. Adjusting stack count accordingly.");

                    DestroyedFoodRecovery.HandleDestroyedBatchFood(state);

                    return;

                }

                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker] {state?.MealDef?.defName ?? "NULL"}, Food ID: {state?.Food?.thingIDNumber ?? 0} " +
                        $"has been destroyed, creating and dropping new {state.TrackerDef?.defName ?? "NULL"}.");

                // Otherwise process as a destroyed meal.
                DestroyedFoodRecovery.HandleDestroyedMeal(state);

                return;

            }

            // Test if food item is stackable and calculate the number of items and nutrition eaten based on the fraction of the chewing time completed.
            if (FoodTrackingHelpers.IsBatchFood(state.MealDef))
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
                    Log.Message($"[FoodTracker] Consumption complete for {state.MealDef.defName}, Food ID: {state.Food.thingIDNumber}. " +
                        $"Starting Stack: {state.StartingStackCount}, Items Eaten: {itemsEaten}, Ending Stack: {state.Food.stackCount}, Nutrition Eaten: {nutritionOnStackEaten:F2}");
                }

                return;
            }

            // Calculate nutrition to consume based on the fraction of the chewing time completed and the nutrition at the start of the eating action.
            float nutritionConsumed = state.NutritionAtStart * state.EatenFraction;

            // Calculate nutrition to be removed from the food
            float nutritionRemoved = Mathf.Clamp(nutritionConsumed, 0f, state.NutritionAtStart);

            // Give the pawn and its records exactly the amount removed from the food.
            FoodTrackingHelpers.ApplyNutritionToPawn(state.Pawn, nutritionRemoved);

            // If meal doesn't have our component we have to spawn a new meal with out tracked ThingDef
            if (state.Food.TryGetComp<CompPartialNutrition>() == null)
            {
                // Calculate leftover nutrition on partial meal
                float leftoverNutrition = Mathf.Max(0f, state.NutritionAtStart - nutritionRemoved);

                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker] Replacing {state.MealDef.defName}, Food ID: {state.Food.thingIDNumber} with {state.TrackerDef?.defName ?? "NULL"}.");

                // Create a new Thing to represent the new meal, and drop it in the world.
                Thing newFood = PartialMealFactory.ReplaceAndDropPartialMeal(state, leftoverNutrition);

                if (newFood == null)
                    Log.Warning($"[FoodTracker] Failed to make partial {state?.TrackerDef?.defName ?? "NULL"}, Food ID: {newFood?.thingIDNumber ?? 0} near {state.Pawn.Label}.");

                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker] Consumption complete for {state.MealDef.defName}, Food ID: {state.Food.thingIDNumber}. " +
                        $"Consumed Nutrition: {nutritionRemoved:F2}, Nutrition Remaining: {leftoverNutrition:F2}");

                return;
            }

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker] {state.MealDef.defName} is already tracked, adjusting nutrition to reflect new value.");

            // Get the remaining nutrition after consumption.
            float currentNutrition = FoodTrackingHelpers.GetRemainingNutrition(state.Food);

            // Calculate how much nutrition is leftover in a partial meal and set it.
            float remainingNutrition = Mathf.Max(0f, currentNutrition - nutritionRemoved);
            FoodTrackingHelpers.SetRemainingNutrition(state.Food, remainingNutrition);

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker] Consumption complete for {state.MealDef.defName}, Food ID: {state.Food.thingIDNumber}. " +
                    $"Consumed Nutrition: {nutritionRemoved:F2}, Nutrition Remaining: {remainingNutrition:F2}");

        }
    }
}
