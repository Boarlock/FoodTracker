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
                Log.Warning($"[FoodTracker] Inputs are not valid. State Null: {state == null}, Pawn Null: {state?.Pawn == null}, " +
                    $"Food Null: {state?.Food == null}, Food Destroyed: {state?.Food?.Destroyed ?? false}");

                return null;
            }

            // Get the partial meal definition corresponding to the vanilla meal. If no partial meal definition is found, return null.
            ThingDef partialDef = DynamicMealDefFactory.CreateTrackerMeal(state.MealDef);

            if (partialDef == null)
            {
                Log.Warning($"[FoodTracker] No corresponding partial meal found for {state.Food.def.defName}, Food ID: {state.Food.thingIDNumber}.");

                return null;
            }

            // Create a new partial meal Thing using the partial meal definition.
            Thing food = ThingMaker.MakeThing(partialDef);

            if (FoodTrackerSettings.Verbose)
            {
                Log.Message($"[FoodTracker] New partial {food.def.defName}, Food ID: {food.thingIDNumber} Thing created.");
            }

            // If created item doesn't for any reason contain our component then delete it.
            if (food.TryGetComp<CompPartialNutrition>() == null)
            {
                Log.Warning($"[FoodTracker] Component missing from {food.def.defName}, Food ID: {food.thingIDNumber}.");

                if (!food.Destroyed)
                {
                    Log.Message($"[FoodTracker] Destroying untracked partial meal.");

                    food.Destroy(DestroyMode.Vanish);
                }

                return null;
            }

            // Set the remaining nutrition of the partial meal to the provided value.
            FoodTrackingHelpers.SetRemainingNutrition(food, remainingNutrition);

            // Get the cell of the pawn to determine where to drop the new partial meal.
            IntVec3 dropCell = state.Pawn.Position;

            if (FoodTrackerSettings.Verbose)
            {
                Log.Message($"[FoodTracker] Preparing to drop partial {food.def.defName}, Food ID: {food.thingIDNumber}. " +
                    $"Remaining Nutrition: {remainingNutrition:F2}, Intended Drop Cell: {dropCell}");
            }

            if (!GenDrop.TryDropSpawn(food, dropCell, state.Pawn.Map, ThingPlaceMode.Near, out Thing resultingThing))

            {
                Log.Warning($"[FoodTracker] Failed to drop partial {food.def.defName}, Food ID: {food.thingIDNumber} near {state.Pawn.Label}.");

                // If the drop failed, don't leave a ghost item in the world. Destroy the partial meal to prevent it from lingering.
                if (!food.Destroyed)
                {
                    Log.Message($"[FoodTracker] Destroying partial meal.");

                    food.Destroy(DestroyMode.Vanish);
                }

                return null;
            }

            // Destroy the vanilla meal only if stack count is 1
            if (state.Food.stackCount > 1)
                state.Food.stackCount--;
            else
                state.Food.Destroy(DestroyMode.Vanish);

            if (FoodTrackerSettings.Verbose)
            {
                Log.Message($"[FoodTracker] {state.MealDef.defName}, {state.Food.thingIDNumber} has been replaced by {resultingThing?.def?.defName ?? "NULL"}, " +
                    $"{resultingThing?.thingIDNumber ?? 0}. Is Tracked: {!(resultingThing?.TryGetComp<CompPartialNutrition>() == null)}");
            }

            // Return the newly created partial meal.
            return resultingThing;
        }

        // Create and drop a partial meal when original vanilla meal has already been destroyed
        public static Thing CreateAndDropPartialMeal(IngestionState state, float remainingNutrition)
        {

            // Validate the input parameters.
            if (state == null || state.Pawn == null || state.MealDef == null)
            {
                Log.Warning($"[FoodTracker] Inputs are not valid. State Null: {state == null}, Pawn Null: {state?.Pawn == null}, ThingDef Null: {state?.MealDef == null}");

                return null;
            }

            // Get the partial meal definition corresponding to the original vanilla meal definition.
            ThingDef partialDef = DynamicMealDefFactory.CreateTrackerMeal(state.MealDef);

            if (partialDef == null)
            {

                Log.Warning($"[FoodTracker] No corresponding partial meal found for {state?.MealDef?.defName ?? "NULL"}, {state?.Food?.thingIDNumber ?? 0}.");

                return null;
            }

            // Create the new partial meal.
            Thing food = ThingMaker.MakeThing(partialDef);

            if (FoodTrackerSettings.Verbose)
            {
                Log.Message($"[FoodTracker] New partial {food.def.defName}, {food.thingIDNumber} Thing created.");
            }

            // Verify the component exists before attempting to use it.
            if (food.TryGetComp<CompPartialNutrition>() == null)
            {

                Log.Warning($"[FoodTracker] Component missing from {food.def.defName}, {food.thingIDNumber}.");

                if (!food.Destroyed)
                {
                    Log.Message($"[FoodTracker] Destroying untracked partial meal.");

                    food.Destroy(DestroyMode.Vanish);
                }

                return null;
            }

            // Store the nutrition remaining in the partial meal.
            FoodTrackingHelpers.SetRemainingNutrition(food, remainingNutrition);

            if (FoodTrackerSettings.Verbose)
            {
                Log.Message($"[FoodTracker] Preparing to drop partial {food.def.defName}, {food.thingIDNumber}. " +
                    $"Remaining Nutrition: {remainingNutrition:F2}, Intended Drop Cell: {state.FoodCell}");
            }

            // Drop the new partial meal into the world.
            if (!GenDrop.TryDropSpawn(food, state.FoodCell, state.Pawn.Map, ThingPlaceMode.Near, out Thing resultingThing))
            {
                Log.Warning($"[FoodTracker] Failed to drop partial {food.def.defName}, {food.thingIDNumber} near {state.Pawn.Label}");

                if (!food.Destroyed)
                {
                    Log.Message($"[FoodTracker] Destroying partial meal.");

                    food.Destroy(DestroyMode.Vanish);
                }

                return null;
            }

            if (FoodTrackerSettings.Verbose)
            {
                Log.Message($"[FoodTracker] {state?.MealDef?.defName ?? "NULL"}, {state?.Food?.thingIDNumber ?? 0} has been replaced by {resultingThing?.def?.defName ?? "NULL"}, " +
                    $"{resultingThing?.thingIDNumber ?? 0}. Is Tracked: {!(resultingThing?.TryGetComp<CompPartialNutrition>() == null)}");
            }

            return resultingThing;
        }
    }
}