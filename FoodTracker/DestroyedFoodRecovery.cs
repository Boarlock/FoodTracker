using RimWorld;
using System.Diagnostics;
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
            if (state == null || state.Pawn == null || state.FoodDef == null)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Inputs are not valid. State Null: {state == null} " +
                    $"| Pawn Null: {state.Pawn == null} | ThingDef Null: {state.FoodDef == null}");

                return;
            }

            float nutritionEaten = state.TotalNutrition * state.EatenFraction;
            float exactItemsEaten = nutritionEaten / state.NutritionPerItem;
            int wholeItemsEaten = Mathf.FloorToInt(exactItemsEaten);
            float nutritionIntoPartial = nutritionEaten - (wholeItemsEaten * state.NutritionPerItem);
            int itemsRemoved = Mathf.CeilToInt(exactItemsEaten);

            // Vanilla restored the food to the stack and no whole batch items were consumed.
            if (wholeItemsEaten <= 0)
                return;

            // Get map of pawn which is where destroyedFood resided before destruction.
            Map map = state.Pawn.Map;

            if (map == null || !state.FoodCell.IsValid)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Invalid map or food cell. Map Null: {map == null} | Food Cell Invalid: {!state.FoodCell.IsValid}");

                return;
            }

            // Initializing item stack where destroyedFood resided before eating.
            Thing survivingStack = null;

            foreach (Thing thing in state.FoodCell.GetThingList(map))
            {
                if (thing == null || thing.Destroyed)
                    continue;

                if (thing.def != state.FoodDef)
                    continue;

                if (!thing.def.IsNutritionGivingIngestible)
                    continue;

                // Store the leftover stack
                survivingStack = thing;
                break;
            }

            if (survivingStack == null)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Could not find a surviving {state?.FoodDef?.defName ?? "NULL"} (ID {state?.Food?.thingIDNumber ?? 0}) stack at {state.FoodCell}.");

                float nutritionCorrection = itemsRemoved * state.NutritionPerItem;

                itemsRemoved--;

                if (itemsRemoved >= survivingStack.stackCount)
                    survivingStack.Destroy(DestroyMode.Vanish);
                else
                    survivingStack.stackCount -= itemsRemoved;

                // Give the pawn and its records exactly the amount removed from the food.
                FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionCorrection);

                return;
            }

            CompFoodTracker tracker = survivingStack.TryGetComp<CompFoodTracker>();

            if (tracker != null)
            {

                nutritionEaten = state.TotalNutrition * state.EatenFraction;
                float nutritionRemainder = nutritionEaten;

                itemsRemoved = 0;

                while (nutritionRemainder > 0f && state.NutritionEntriesBefore.Count > 0)
                {
                    float nextItem = state.NutritionEntriesBefore[0];

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
                if (itemsRemoved >= survivingStack.stackCount)
                    survivingStack.Destroy(DestroyMode.Vanish);
                else
                    survivingStack.stackCount -= itemsRemoved;

                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker][T{state.TraceID}] Eating interrupted: {state.FoodDef.defName} (ID {state.Food?.thingIDNumber ?? 0}) " +
                        $"| Pawn: {state.Pawn.LabelShort} | Ingest Count: {state.IngestCount} | Eaten: {state.EatenFraction:P0} " +
                        $"| Total Nutrition: {state.TotalNutrition:F2} | Total Consumed: {nutritionEaten:F2} | Total Remaining: {(state.TotalNutrition - nutritionEaten):F2} " +
                        $"| Partial Nutrition: {nutritionIntoPartial} | Whole Items Remaining: {(state.StartingStackCount - itemsRemoved)}");

                // Give the pawn and its records exactly the amount removed from the food.
                FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionEaten);

                return;
            }

            // Create the partial meal and drop at specified cell
            Thing partialMeal = PartialMealFactory.CreateAndDropPartialMeal(state, nutritionIntoPartial);

            if (partialMeal == null)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Failed to make {partialMeal?.def.defName ?? "NULL"} (ID {partialMeal?.thingIDNumber ?? 0})");

                float nutritionCorrection = itemsRemoved * state.NutritionPerItem;

                itemsRemoved--;

                if (itemsRemoved >= survivingStack.stackCount)
                    survivingStack.Destroy(DestroyMode.Vanish);
                else
                    survivingStack.stackCount -= itemsRemoved;

                // Give the pawn and its records exactly the amount removed from the food.
                FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionCorrection);

                return;
            }

            // Remove the consumed physical meals from the stack.
            if (itemsRemoved >= survivingStack.stackCount)
                survivingStack.Destroy(DestroyMode.Vanish);
            else
                survivingStack.stackCount -= itemsRemoved;

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{state.TraceID}] Eating interrupted: {state.FoodDef.defName} (ID {state?.Food?.thingIDNumber ?? 0}) " +
                    $"Pawn: {state.Pawn.LabelShort} | Ingest Count: {state.IngestCount} | Eaten: {state.EatenFraction:P0} " +
                    $"| Total Nutrition: {state.TotalNutrition:F2} | Total Consumed: {nutritionEaten:F2} | Total Remaining: {(state.TotalNutrition - nutritionEaten):F2} " +
                    $"| Partial Meal: {partialMeal.def.defName} (ID {partialMeal.thingIDNumber}) | Partial Nutrition: {nutritionIntoPartial:F2} " +
                    $"| Whole Items Remaining: {(state.IngestCount - wholeItemsEaten)}");

            // Give the pawn and its records exactly the amount removed from the food.
            FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionEaten);

        }

        public static void HandleDestroyedBatchFood(IngestionState state)
        {
            if (state == null || state.Pawn == null || state.FoodDef == null)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Inputs are not valid. State Null: {state == null} " +
                    $"| Pawn Null: {state.Pawn == null} | ThingDef Null: {state.FoodDef == null}");

                return;
            }

            float nutritionEaten = state.TotalNutrition * state.EatenFraction;
            float exactItemsEaten = nutritionEaten / state.NutritionPerItem;
            int wholeItemsEaten = Mathf.FloorToInt(exactItemsEaten);

            // Vanilla restored the food to the stack and no whole batch items were consumed.
            if (wholeItemsEaten <= 0)
                return;

            // Get map of pawn which is where destroyedFood resided before destruction
            Map map = state.Pawn.Map;

            if (map == null || !state.FoodCell.IsValid)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Invalid map or food cell. Map Null: {map == null} | Food Cell Invalid: {!state.FoodCell.IsValid}");

                return;
            }

            // Initializing item stack where destroyedFood resided before eating event
            Thing survivingStack = null;

            // Find stack of destroyedFood
            foreach (Thing thing in state.FoodCell.GetThingList(map))
            {
                if (thing == null || thing.Destroyed)
                    continue;

                if (thing.def != state.FoodDef)
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
                Log.Warning($"[FoodTracker][T{state.TraceID}] Could not find a surviving {state?.FoodDef?.defName ?? "NULL"} (ID {state?.Food?.thingIDNumber ?? 0}) stack at {state.FoodCell}.");

                return;
            }

            int itemsRemoved = Mathf.RoundToInt(exactItemsEaten);
            nutritionEaten = itemsRemoved * state.NutritionPerItem;

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{state.TraceID}] Eating interrupted: {state.FoodDef.defName} (ID {state?.Food?.thingIDNumber ?? 0}) " +
                    $"Pawn: {state.Pawn.LabelShort} | Ingest Count: {state.IngestCount} | Eaten: {state.EatenFraction:P0} " +
                    $"| Total Nutrition: {state.TotalNutrition:F2} | Total Consumed: {nutritionEaten:F2}");

            if (itemsRemoved >= survivingStack.stackCount)
                survivingStack.Destroy(DestroyMode.Vanish);
            else
                survivingStack.stackCount -= itemsRemoved;

            // Give the pawn and its records exactly the amount removed from the food.
            FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionEaten);

        }
    }
}