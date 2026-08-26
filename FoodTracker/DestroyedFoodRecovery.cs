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
            if (state == null || state.Pawn == null || state.Food == null || !state.Food.Destroyed)
            {
                Log.Warning($"[FoodTracker] Inputs are not valid. State Null: {state == null}, Pawn: {state?.Pawn?.Label ?? "NULL"}, Food: {state?.Food?.Label ?? "NULL"}, " +
                    $"Food ID: {state?.Food?.thingIDNumber ?? 0}, Food Def: {state?.Food?.def?.defName ?? "NULL"}, Food Destroyed: {state?.Food?.Destroyed ?? false}");

                return;
            }

            // Calculate the actual nutrition eaten based off eating time and nutrition in food at the start, clamped at 0 and nutrition max.
            float nutritionEaten = Mathf.Clamp(state.NutritionAtStart * state.EatenFraction, 0f, state.NutritionAtStart);

            // Calculate remaining nutrition to not go below zero.
            float remainingNutrition = Mathf.Max(0f, state.NutritionAtStart - nutritionEaten);

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

            ThingDef mealDef = state.MealDef ?? survivingStack?.def;

            // If surviving stack cannot be found 
            if (survivingStack == null)
            {
                Log.Warning($"[FoodTracker] Could not find a surviving {mealDef} stack at {state.FoodCell}.");

                return;
            }

            // Only remove item if stack is greater than 0
            if (survivingStack.stackCount > 0)
            {
                survivingStack.stackCount--;

                if (FoodTrackerSettings.Verbose)
                {
                    Log.Message($"[FoodTracker] Removed one item from surviving stack to compensate for " +
                        $"vanilla restoration. New Stack Count: {survivingStack.stackCount}");
                }
            }

            // Create the partial meal and drop at specified cell
            Thing partialMeal = PartialMealFactory.CreateAndDropPartialMeal(state, remainingNutrition);

            if (partialMeal == null)
            {
                Log.Warning($"[FoodTracker] Failed to make partial {partialMeal?.Label ?? "NULL"} near {state.Pawn.Label}. Food ID: {partialMeal?.thingIDNumber ?? 0}, Food Def: {partialMeal?.def.defName ?? "NULL"}.");

                return;
            }

            // Give the pawn and its records exactly the amount removed from the food.
            FoodTrackingHelpers.ApplyNutritionToPawn(state.Pawn, nutritionEaten);

        }

        public static void HandleDestroyedBatchFood(IngestionState state)
        {

            // Validate inputs
            if (state == null || state.Pawn == null || state.Food == null || !state.Food.Destroyed)
            {
                Log.Warning($"[FoodTracker] Inputs are not valid. State Null: {state == null}, Pawn: {state?.Pawn?.Label ?? "NULL"}, Food: {state?.Food?.Label ?? "NULL"}, " +
                    $"Food ID: {state?.Food?.thingIDNumber ?? 0}, Food Def: {state?.Food?.def?.defName ?? "NULL"}, Food Destroyed: {state?.Food?.Destroyed ?? false}");

                return;
            }

            // Calculate the number of items eaten and nutrition eaten
            int itemsEaten = Mathf.Clamp(Mathf.FloorToInt(state.EatenFraction * state.IngestCount), 0, state.IngestCount);
            float nutritionOnStackEaten = itemsEaten * state.NutritionPerItem;

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
                Log.Warning($"[FoodTracker] Could not find a surviving {state.Food.Label} stack at {state.FoodCell}.");

                return;
            }

            // Only remove items from stack up to max stack size
            int itemsToRemove = Mathf.Min(itemsEaten, survivingStack.stackCount);
            survivingStack.stackCount -= itemsToRemove;

            // Give the pawn and its records exactly the amount removed from the food.
            FoodTrackingHelpers.ApplyNutritionToPawn(state.Pawn, nutritionOnStackEaten);

            if (FoodTrackerSettings.Verbose)
            {
                Log.Message($"[FoodTracker] Removed {itemsToRemove} item(s) from surviving stack to compensate for " +
                    $"vanilla restoration. New Stack Count: {survivingStack.stackCount}");
            }


        }

    }
}