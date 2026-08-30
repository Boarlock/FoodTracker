using Verse;

namespace FoodTracker
{
    public static class PartialMealFactory
    {
        // Replace a vanilla meal with its corresponding partial meal definition, dropping the new item.
        public static Thing ReplaceAndDropPartialMeal(IngestionState state, float remainingNutrition)
        {
            // Validate the input parameters.
            if (state == null || state.Pawn == null || state.Food == null || state.Food.Destroyed)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Inputs are not valid. State Null: {state == null} | Pawn Null: {state?.Pawn == null} " +
                    $"| Food Null: {state?.Food == null} | Food Destroyed: {state?.Food?.Destroyed ?? false}");

                return null;
            }

            // Get the partial meal definition corresponding to the vanilla meal. If no partial meal definition is found, return null.
            ThingDef partialDef = DynamicMealDefFactory.CreateTrackerMeal(state);

            if (partialDef == null)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] No corresponding partial meal found for {state.Food.def.defName} (ID {state.Food.thingIDNumber}).");

                return null;
            }

            // Create a new partial meal Thing using the partial meal definition.
            Thing food = ThingMaker.MakeThing(partialDef);

            // If created item doesn't for any reason contain our component then delete it.
            if (food.TryGetComp<CompPartialNutrition>() == null)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Component missing from {food?.def?.defName ?? "NULL"} (ID {food.thingIDNumber})");

                if (!food.Destroyed)
                {
                    food.Destroy(DestroyMode.Vanish);
                }

                return null;
            }

            // Set the remaining nutrition of the partial meal to the provided value.
            FoodTrackingHelpers.SetRemainingNutrition(state, remainingNutrition);

            // Get the cell of the pawn to determine where to drop the new partial meal.
            IntVec3 dropCell = state.Pawn.Position;

            if (!GenDrop.TryDropSpawn(food, dropCell, state.Pawn.Map, ThingPlaceMode.Near, out Thing resultingThing))

            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Failed to drop {food.def.defName} (ID {food.thingIDNumber})");

                // If the drop failed, don't leave a ghost item in the world. Destroy the partial meal to prevent it from lingering.
                if (!food.Destroyed)
                {
                    food.Destroy(DestroyMode.Vanish);
                }

                return null;
            }

            if (resultingThing == null)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Failed to make {resultingThing?.def.defName ?? "NULL"} (ID {resultingThing?.thingIDNumber ?? 0})");

                return null;
            }

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{state.TraceID}] Partial meal created: {state.MealDef.defName} (ID {state.Food.thingIDNumber}) " +
                    $"| Nutrition: {remainingNutrition:F2}");

            return resultingThing;
        }

        // Create and drop a partial meal when original vanilla meal has already been destroyed
        public static Thing CreateAndDropPartialMeal(IngestionState state, float remainingNutrition)
        {

            // Validate the input parameters.
            if (state == null || state.Pawn == null || state.MealDef == null)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Inputs are not valid. State Null: {state == null} | Pawn Null: {state?.Pawn == null} | ThingDef Null: {state?.MealDef == null}");

                return null;
            }

            // Get the partial meal definition corresponding to the original vanilla meal definition.
            ThingDef partialDef = DynamicMealDefFactory.CreateTrackerMeal(state);

            if (partialDef == null)
            {

                Log.Warning($"[FoodTracker][T{state.TraceID}] No corresponding partial meal found for {state?.MealDef?.defName ?? "NULL"} (ID {state?.Food?.thingIDNumber ?? 0}).");

                return null;
            }

            // Create the new partial meal.
            Thing food = ThingMaker.MakeThing(partialDef);

            // Verify the component exists before attempting to use it.
            if (food.TryGetComp<CompPartialNutrition>() == null)
            {

                Log.Warning($"[FoodTracker][T{state.TraceID}] Component missing from {food?.def?.defName ?? "NULL"} (ID {food.thingIDNumber})");

                if (!food.Destroyed)
                {
                    food.Destroy(DestroyMode.Vanish);
                }

                return null;
            }

            // Store the nutrition remaining in the partial meal.
            FoodTrackingHelpers.SetRemainingNutrition(state, remainingNutrition);

            // Drop the new partial meal into the world.
            if (!GenDrop.TryDropSpawn(food, state.FoodCell, state.Pawn.Map, ThingPlaceMode.Near, out Thing resultingThing))
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Failed to drop {food.def.defName} (ID {food.thingIDNumber})");

                if (!food.Destroyed)
                {
                    food.Destroy(DestroyMode.Vanish);
                }

                return null;
            }

            if (resultingThing == null)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Failed to make {resultingThing?.def.defName ?? "NULL"} (ID {resultingThing?.thingIDNumber ?? 0})");

                return null;
            }

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{state.TraceID}] Partial meal created: {state.MealDef.defName} (ID {state.Food.thingIDNumber}) " +
                    $"| Nutrition: {remainingNutrition:F2}");

            return resultingThing;
        }
    }
}