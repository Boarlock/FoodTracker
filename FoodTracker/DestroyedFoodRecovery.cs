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
            if (state == null || state.Pawn == null || state.MealDef == null)
            {
                Log.Warning($"[FoodTracker] Inputs are not valid. State Null: {state == null}, Pawn Null: {state.Pawn == null}, ThingDef Null: {state.MealDef == null}");

                return;
            }

            // Calculate the actual nutrition eaten based off eating time and nutrition in food at the start, clamped at 0 and nutrition max.
            float nutritionEaten = state.TotalNutrition * state.EatenFraction;
            float itemsEatenExact = nutritionEaten / state.NutritionPerItem;

            // Get map of pawn which is where destroyedFood resided before destruction
            Map map = state.Pawn.Map;

            if (map == null || !state.FoodCell.IsValid)
            {
                Log.Warning($"[FoodTracker] Invalid map or food cell. Map Null: {map == null}, Food Cell: {state.FoodCell}");

                return;
            }

            // Initializing item stack where destroyedFood resided before eating event
            Thing survivingStack = null;

            // Find stack of destroyedFood
            foreach (Thing thing in state.FoodCell.GetThingList(map))
            {
                if (thing == null || thing.Destroyed)
                    continue;

                if (thing.def != state.MealDef)
                    continue;

                if (!thing.def.IsNutritionGivingIngestible)
                    continue;

                // Store the leftover stack
                survivingStack = thing;
                break;
            }

            // If surviving stack cannot be found 
            if (survivingStack == null)
            {
                Log.Warning($"[FoodTracker] Could not find a surviving {state?.MealDef?.defName ?? "NULL"}, {state?.Food?.thingIDNumber ?? 0} stack at {state.FoodCell}.");

                return;
            }

            int wholeItemsEaten = Mathf.FloorToInt(itemsEatenExact);
            int itemsToRemove = Mathf.Min(Mathf.CeilToInt(itemsEatenExact), survivingStack.stackCount);

            // Only remove items up to stackCount, if it would leave stackCount at 0 then simply delte the object
            if (itemsToRemove >= survivingStack.stackCount)
                survivingStack.Destroy(DestroyMode.Vanish);
            else
                survivingStack.stackCount -= itemsToRemove;

            // Calculate remaining nutrition to not go below zero.
            float partialMealEatenFraction = itemsEatenExact - wholeItemsEaten;
            float partialMealRemainingFraction = 1f - partialMealEatenFraction;
            float partialMealNutrition = partialMealRemainingFraction * state.NutritionAtStart;

            // Create the partial meal and drop at specified cell
            Thing partialMeal = PartialMealFactory.CreateAndDropPartialMeal(state, partialMealNutrition);

            if (partialMeal == null)
            {
                Log.Warning($"[FoodTracker] Failed to make partial {partialMeal?.def.defName ?? "NULL"}, {partialMeal?.thingIDNumber ?? 0} near {state.Pawn.Label}");

                return;
            }

            // Give the pawn and its records exactly the amount removed from the food.
            FoodTrackingHelpers.ApplyNutritionToPawn(state.Pawn, nutritionEaten);

        }

        public static void HandleDestroyedBatchFood(IngestionState state)
        {
            if (state == null || state.Pawn == null || state.MealDef == null)
            {
                Log.Warning($"[FoodTracker] Inputs are not valid. State Null: {state == null}, Pawn Null: {state.Pawn == null}, ThingDef Null: {state.MealDef == null}");

                return;
            }

            // Calculate the number of items eaten and nutrition eaten
            float nutritionEaten = state.TotalNutrition * state.EatenFraction;
            int wholeItemsEaten = Mathf.FloorToInt(nutritionEaten / state.NutritionPerItem);


            // Vanilla restored the food to the stack and no whole batch items were consumed.
            if (wholeItemsEaten <= 0)
                return;

            // Get map of pawn which is where destroyedFood resided before destruction
            Map map = state.Pawn.Map;

            if (map == null || !state.FoodCell.IsValid)
            {
                Log.Warning($"[FoodTracker] Invalid map or food cell. Map Null: {map == null}, Food Cell: {state.FoodCell}");

                return;
            }

            // Initializing item stack where destroyedFood resided before eating event
            Thing survivingStack = null;

            // Find stack of destroyedFood
            foreach (Thing thing in state.FoodCell.GetThingList(map))
            {
                if (thing == null || thing.Destroyed)
                    continue;

                if (thing.def != state.MealDef)
                    continue;

                if (!thing.def.IsNutritionGivingIngestible)
                    continue;

                // Store the leftover stack
                survivingStack = thing;
                break;
            }

            // If surviving stack cannot be found 
            if (survivingStack == null)
            {
                Log.Warning($"[FoodTracker] Could not find a surviving {state?.MealDef?.defName ?? "NULL"}, {state?.Food?.thingIDNumber ?? 0} stack at {state.FoodCell}.");

                return;
            }

            // Only remove items from stack up to max stack size otherwise destroy the Thing
            int itemsToRemove = Mathf.Min(wholeItemsEaten, survivingStack.stackCount);
            float nutritionOnStackEaten = itemsToRemove * state.NutritionPerItem;

            // Give the pawn and its records exactly the amount removed from the food.
            FoodTrackingHelpers.ApplyNutritionToPawn(state.Pawn, nutritionOnStackEaten);

            if (itemsToRemove >= survivingStack.stackCount)
                survivingStack.Destroy(DestroyMode.Vanish);
            else
                survivingStack.stackCount -= itemsToRemove;

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker] Consumption complete for {state.MealDef.defName}, Food ID: {state.Food.thingIDNumber}. " +
                        $"Items Eaten: {itemsToRemove}, Nutrition Eaten: {nutritionOnStackEaten:F2}");
        }
    }
}