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

            if (FoodTrackingHelpers.IsBatchFood(state.MealDef))
            {

                // Vanilla restored the food to the stack and no whole batch items were consumed.
                if (wholeItemsEaten <= 0)
                {
                    return;
                }

                // Never remove move than what's in the current stack.
                itemsToRemove = Mathf.Min(wholeItemsEaten, state.Food.stackCount);
                float nutritionOnStackEaten = itemsToRemove * state.NutritionPerItem;

                // Give the pawn and its records the amount removed from the food.
                FoodTrackingHelpers.ApplyNutritionToPawn(state.Pawn, nutritionOnStackEaten);

                // Only remove items up to stackCount, if it would leave stackCount at 0 then simply delte the object
                if (itemsToRemove >= state.Food.stackCount)
                    state.Food.Destroy(DestroyMode.Vanish);
                else
                    state.Food.stackCount -= itemsToRemove;

                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker] Consumption complete for {state.MealDef.defName}, Food ID: {state.Food.thingIDNumber}. " +
                        $"Items Eaten: {itemsToRemove}, Nutrition Eaten: {nutritionOnStackEaten:F2}");

                return;
            }

            // Then determine how much of a partial meal is leftover and how much nutrition is leftover.
            float partialMealEatenFraction = itemsEatenExact - wholeItemsEaten;
            float partialMealRemainingFraction = 1f - partialMealEatenFraction;
            float partialMealNutrition = partialMealRemainingFraction * state.NutritionAtStart;

            // Give the pawn and its records exactly the amount removed from the food.
            FoodTrackingHelpers.ApplyNutritionToPawn(state.Pawn, nutritionEaten);

            itemsToRemove = Mathf.Min(Mathf.CeilToInt(itemsEatenExact), state.Food.stackCount);

            // If meal doesn't have our component we have to spawn a new meal with out tracked ThingDef.
            if (state.Food.TryGetComp<CompPartialNutrition>() == null)
            {

                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker] Replacing {state.MealDef.defName}, Food ID: {state.Food.thingIDNumber} with {state.TrackerDef?.defName ?? "NULL"}.");

                // Create a new Thing to represent the new meal, and drop it in the world.
                Thing newFood = PartialMealFactory.ReplaceAndDropPartialMeal(state, partialMealNutrition, itemsToRemove);

                if (newFood == null)
                    Log.Warning($"[FoodTracker] Failed to make partial {state?.TrackerDef?.defName ?? "NULL"}, Food ID: {newFood?.thingIDNumber ?? 0} near {state.Pawn.Label}.");

                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker] Consumption complete for {state.MealDef.defName}, Food ID: {state.Food.thingIDNumber}. Items Eaten: {wholeItemsEaten}, " +
                        $"Partial Eaten: {partialMealEatenFraction:P0}, Consumed Nutrition: {nutritionEaten:F2}, Nutrition Remaining: {partialMealNutrition:F2}");

                return;
            }

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker] {state.MealDef.defName} is already tracked. Remaining Nutrition: {partialMealNutrition:F2}");

            // Calculate how much nutrition is leftover in a partial meal and set it.
            FoodTrackingHelpers.SetRemainingNutrition(state.Food, partialMealNutrition);

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker] Consumption complete for {state.MealDef.defName}, Food ID: {state.Food.thingIDNumber}. " +
                    $"Partial Eaten: {partialMealEatenFraction:P0}, Consumed Nutrition: {nutritionEaten:F2}, Nutrition Remaining: {partialMealNutrition:F2}");

        }
    }
}
