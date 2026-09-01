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
                Log.Warning($"[FoodTracker][T{state?.TraceID.ToString() ?? "?"}] Inputs are not valid. State Null: {state == null} " +
                    $"| Pawn Null: {state?.Pawn == null} | ThingDef Null: {state?.FoodDef == null}");

                return;
            }

            float nutritionEaten = state.TotalNutrition * state.EatenFraction;
            float exactItemsEaten = nutritionEaten / state.NutritionPerItem;
            int wholeItemsEaten = Mathf.FloorToInt(exactItemsEaten);
            float nutritionIntoPartial = state.TotalNutrition - nutritionEaten;
            int itemsRemoved = Mathf.CeilToInt(exactItemsEaten);

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

                return;
            }

            // Create the partial meal and drop at specified cell
            Thing partialMeal = PartialMealFactory.CreateAndDropPartialMeal(state, nutritionIntoPartial, state.FoodCell);

            if (partialMeal == null)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Failed to make {partialMeal?.def.defName ?? "NULL"} (ID {partialMeal?.thingIDNumber ?? 0})");

                float nutritionCorrection = itemsRemoved * state.NutritionPerItem;

                itemsRemoved--;

                if (itemsRemoved >= survivingStack.stackCount)
                {
                    state.FoodToDestroy = survivingStack;
                    state.DestroyFoodAfterIngestion = true;
                }
                else
                    survivingStack.stackCount -= itemsRemoved;

                // Give the pawn and its records exactly the amount removed from the food.
                FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionCorrection);

                return;
            }

            // Remove the consumed physical meals from the stack.
            if (itemsRemoved >= survivingStack.stackCount)
            {
                state.FoodToDestroy = survivingStack;
                state.DestroyFoodAfterIngestion = true;
            }
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

        // Handle edge cases where vanilla destroys the food instance before we can instantiate a partial
        public static void HandleDestroyedFoodTrackerMeal(IngestionState state)
        {
            // Validate inputs
            if (state == null || state.Pawn == null || state.FoodDef == null)
            {
                Log.Warning($"[FoodTracker][T{state?.TraceID.ToString() ?? "?"}] Inputs are not valid. State Null: {state == null} " +
                    $"| Pawn Null: {state?.Pawn == null} | ThingDef Null: {state?.FoodDef == null}");

                return;
            }

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

                return;
            }

            CompFoodTracker tracker = survivingStack.TryGetComp<CompFoodTracker>();

            float nutritionEaten = state.TotalNutrition * state.EatenFraction;
            float nutritionRemainder = nutritionEaten;
            int itemsRemoved = 0;
            float nextItem = 0f;

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
            if (itemsRemoved >= survivingStack.stackCount)
            {
                state.FoodToDestroy = survivingStack;
                state.DestroyFoodAfterIngestion = true;
            }
            else
            {
                survivingStack.stackCount -= itemsRemoved;
            }

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{state.TraceID}] Eating interrupted: {state.FoodDef.defName} (ID {state?.Food?.thingIDNumber ?? 0}) " +
                    $"Pawn: {state.Pawn.LabelShort} | Ingest Count: {state.IngestCount} | Eaten: {state.EatenFraction:P0} " +
                    $"| Total Nutrition: {state.TotalNutrition:F2} | Total Consumed: {nutritionEaten:F2} | Total Remaining: {(state.TotalNutrition - nutritionEaten):F2} " +
                    $"| Whole Items Remaining: {(state.IngestCount - itemsRemoved) - 1}");

            // Give the pawn and its records exactly the amount removed from the food.
            FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionEaten);

        }

        public static void HandleDestroyedBatchFood(IngestionState state)
        {
            if (state == null || state.Pawn == null || state.FoodDef == null)
            {
                Log.Warning($"[FoodTracker][T{state?.TraceID.ToString() ?? "?"}] Inputs are not valid. State Null: {state == null} " +
                    $"| Pawn Null: {state?.Pawn == null} | ThingDef Null: {state?.FoodDef == null}");

                return;
            }

            float nutritionEaten = state.TotalNutrition * state.EatenFraction;
            float exactItemsEaten = nutritionEaten / state.NutritionPerItem;
            int wholeItemsEaten = Mathf.FloorToInt(exactItemsEaten);

            // Vanilla restored the food to the stack and no whole batch items were consumed.
            if (wholeItemsEaten <= 0.004f)
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
            {
                state.FoodToDestroy = survivingStack;
                state.DestroyFoodAfterIngestion = true;
            }
            else
                survivingStack.stackCount -= itemsRemoved;

            // Give the pawn and its records exactly the amount removed from the food.
            FoodTrackingHelpers.ApplyNutritionToPawn(state, nutritionEaten);

        }
    }
}