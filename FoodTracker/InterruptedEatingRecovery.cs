using RimWorld;
using Verse;
using UnityEngine;

namespace FoodTracker
{
    public static class InterruptedEatingRecovery
    {
        // Handle edge cases where vanilla destroys the food instance before we can instantiate a partial
        public static void HandleDestroyedMeal(
            Pawn pawn,
            Thing destroyedFood,
            IntVec3 foodCell,
            float nutritionAtStart,
            float eatenFraction)
        {
            // Validate inputs
            if (pawn == null || destroyedFood == null || !destroyedFood.Destroyed)
            {
                Log.Warning($"[FoodTracker] Inputs are not valid. Pawn: {pawn?.Label ?? "NULL"}, Food: {destroyedFood?.Label ?? "NULL"}, " +
                    $"Food ID: {destroyedFood?.thingIDNumber ?? 0}, Food Def: {destroyedFood?.def.defName}, Food Not Destroyed: {!destroyedFood.Destroyed}");

                return;
            }

            // Calculate the actual nutrition eaten based off eating time and nutrition in food at the start, clamped at 0 and nutrition max.
            float nutritionEaten = Mathf.Clamp(nutritionAtStart * eatenFraction, 0f, nutritionAtStart);

            // Calculate remaining nutrition to not go below zero.
            float remainingNutrition = Mathf.Max(0f, nutritionAtStart - nutritionEaten);

            // Get map of pawn which is where destroyedFood resided before destruction
            Map map = pawn.Map;

            if (map == null || !foodCell.IsValid)
            {
                Log.Warning($"[FoodTracker] Invalid map or food cell. Map Null: {map == null}, Food Cell: {foodCell}");
                
                return;
            }

            // Initializing item stack where destroyedFood resided before eating event
            Thing survivingStack = null;

            // Find stack of destroyedFood
            foreach (Thing thing in foodCell.GetThingList(map))
            {
                if (thing == null || thing.Destroyed)
                    continue;

                if (thing.def != destroyedFood.def)
                    continue;

                if (!thing.def.IsNutritionGivingIngestible)
                    continue;

                // Store the leftover stack
                survivingStack = thing;
                break;
            }

            ThingDef mealDef = survivingStack?.def ?? destroyedFood.def;

            // If surviving stack cannot be found 
            if (survivingStack == null)
            {
                Log.Warning($"[FoodTracker] Could not find a surviving {mealDef} stack at {foodCell}.");
                
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
            Thing partialMeal =
                MealReplacement.CreateAndDropPartialMeal(
                pawn,
                mealDef,
                foodCell,
                remainingNutrition);

            if (partialMeal == null)
            {
                Log.Warning($"[FoodTracker] Failed to make partial {partialMeal?.Label ?? "NULL"} near {pawn.Label}. Food ID: {partialMeal?.thingIDNumber ?? 0}, Food Def: {partialMeal?.def.defName ?? "NULL"}.");

                return;
            }

            // Give the pawn and its records exactly the amount removed from the food.
            FoodTrackerMeal.ApplyNutritionToPawn(pawn, nutritionEaten);

        }

        public static void HandleDestroyedBatchFood(
            Pawn pawn,
            Thing destroyedFood,
            IntVec3 foodCell,
            int ingestCount,
            float nutritionPerItem,
            float eatenFraction)
        {

            // Validate inputs
            if (pawn == null || destroyedFood == null || !destroyedFood.Destroyed)
            {
                Log.Warning($"[FoodTracker] Inputs are not valid. Pawn: {pawn?.Label ?? "NULL"}, Food: {destroyedFood?.Label ?? "NULL"}, " +
                    $"Food ID: {destroyedFood?.thingIDNumber ?? 0}, Food Def: {destroyedFood?.def.defName}, Food Destroyed: {destroyedFood?.Destroyed ?? false}");

                return;
            }

            // Calculate the number of items eaten and nutrition eaten
            int itemsEaten = Mathf.Clamp(Mathf.FloorToInt(eatenFraction * ingestCount), 0, ingestCount);
            float nutritionOnStackEaten = itemsEaten * nutritionPerItem;

            // Get map of pawn which is where destroyedFood resided before destruction
            Map map = pawn.Map;

            if (map == null || !foodCell.IsValid)
            {
                Log.Warning($"[FoodTracker] Invalid map or food cell. Map Null: {map == null}, Food Cell: {foodCell}");
                
                return;
            }

            // Initializing item stack where destroyedFood resided before eating event
            Thing survivingStack = null;

            // Find stack of destroyedFood
            foreach (Thing thing in foodCell.GetThingList(map))
            {
                if (thing == null || thing.Destroyed)
                    continue;

                if (thing.def != destroyedFood.def)
                    continue;

                if (!thing.def.IsNutritionGivingIngestible)
                    continue;

                // Store the leftover stack
                survivingStack = thing;
                break;
            }

            ThingDef foodDef = survivingStack?.def ?? destroyedFood.def;

            // If surviving stack cannot be found 
            if (survivingStack == null)
            {
                Log.Warning($"[FoodTracker] Could not find a surviving {foodDef} stack at {foodCell}.");
                
                return;
            }

            // Only remove items from stack up to max stack size
            int itemsToRemove = Mathf.Min(itemsEaten, survivingStack.stackCount);
            survivingStack.stackCount -= itemsToRemove;

            // Give the pawn and its records exactly the amount removed from the food.
            FoodTrackerMeal.ApplyNutritionToPawn(pawn, nutritionOnStackEaten);

            if (FoodTrackerSettings.Verbose)
                {
                    Log.Message($"[FoodTracker] Removed {itemsToRemove} item(s) from surviving stack to compensate for " +
                        $"vanilla restoration. New Stack Count: {survivingStack.stackCount}");
                }
            

        }
        
    }
}