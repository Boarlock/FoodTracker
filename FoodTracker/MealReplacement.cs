using RimWorld;
using Verse;

namespace FoodTracker
{
    public static class MealReplacement
    {
        // Replace a vanilla meal with its corresponding partial meal definition, dropping the new item.
        public static Thing ReplaceAndDropPartialMeal(
            Pawn pawn,
            Thing vanillaMeal,
            float remainingNutrition)
        {
            // Validate the input parameters.
            if (pawn == null || vanillaMeal == null || vanillaMeal.Destroyed)
            {
                Log.Warning($"[FoodTracker] Inputs are not valid. Pawn: {pawn?.Label ?? "NULL"}, Food: {vanillaMeal?.Label ?? "NULL"}, " +
                    $"Food ID: {vanillaMeal?.thingIDNumber ?? 0}, Food Def: {vanillaMeal?.def.defName}, Food Destroyed: {vanillaMeal?.Destroyed ?? false}");

                return null;
            }

            // Get the partial meal definition corresponding to the vanilla meal. If no partial meal definition is found, return null.
            ThingDef partialDef = FoodTrackerMeal.GetMealDef(vanillaMeal.def);

            if (partialDef == null)
            {
                Log.Warning($"[FoodTracker] No corresponding partial meal found for {vanillaMeal.Label}. " +
                    $"Food ID: {vanillaMeal.thingIDNumber}, Food Def: {vanillaMeal.def.defName}.");

                return null;
            }

            // Create a new partial meal Thing using the partial meal definition.
            Thing food = ThingMaker.MakeThing(partialDef);

            // Set the stack count of the partial meal to 1, to ensure it is treated as a single item.
            food.stackCount = 1;

            if (FoodTrackerSettings.Verbose)
            {
                Log.Message($"[FoodTracker] New partial {food.Label} created. Food ID: {food.thingIDNumber}, Food Def: {food.def.defName}");
            }

            if (!FoodTrackerMeal.IsTracked(food))
            {
                Log.Warning($"[FoodTracker] Component missing from {food.Label}. Food ID: {food.thingIDNumber}, Food Def: {food.def.defName}.");

                // If component is missing from partial meal then destroy it
                if (!food.Destroyed)
                {
                    Log.Message($"[FoodTracker] Destroying untracked partial meal.");

                    food.Destroy(DestroyMode.Vanish);
                }

                return null;
            }

            // Set the remaining nutrition of the partial meal to the provided value.
            FoodTrackerMeal.SetRemainingNutrition(food, remainingNutrition);

            // Get the cell of the pawn to determine where to drop the new partial meal.
            IntVec3 dropCell = pawn.Position;

            if (FoodTrackerSettings.Verbose)
            {
                Log.Message($"[FoodTracker] Preparing to drop partial {vanillaMeal.Label}. Remaining Nutrition: {remainingNutrition}, " +
                    $"Intended Drop Cell: {dropCell}, Food: {food.Label}, Food ID: {food.thingIDNumber}, Food Def: {food.def.defName}");
            }

            if (!GenDrop.TryDropSpawn(
                food,
                dropCell,
                pawn.Map,
                ThingPlaceMode.Near,
                out Thing resultingThing))

            {
                Log.Warning($"[FoodTracker] Failed to drop partial {food.Label} near {pawn.Label}. Food ID: {food.thingIDNumber}, Food Def: {food.def.defName}.");

                // If the drop failed, don't leave a ghost item in the world. Destroy the partial meal to prevent it from lingering.
                if (!food.Destroyed)
                {
                    Log.Message($"[FoodTracker] Destroying partial {food.Label} reference.");

                    food.Destroy(DestroyMode.Vanish);
                }

                return null;
            }

            if (FoodTrackerSettings.Verbose)
            {
                Log.Message($"[FoodTracker] New partial {resultingThing?.Label ?? "NULL"} created successfully, destroying vanilla {vanillaMeal.Label}. Food Def: {resultingThing?.def.defName ?? "NULL"}, Food ID: {resultingThing?.thingIDNumber ?? 0}, " +
                    $"Remaining Nutrition: {FoodTrackerMeal.GetRemainingNutrition(resultingThing):F3}, Is Tracked: {FoodTrackerMeal.IsTracked(resultingThing)}");
            }

            // Destroy the vanilla meal using the Vanish mode, which removes it from the game without leaving any remnants.
            vanillaMeal.Destroy(DestroyMode.Vanish);

            // Return the newly created partial meal.
            return resultingThing;
        }

        // Create and drop a partial meal when original vanilla meal has already been destroyed
        public static Thing CreateAndDropPartialMeal(
            Pawn pawn,
            ThingDef vanillaMealDef,
            IntVec3 dropCell,
            float remainingNutrition)
        {

            // Validate the input parameters.
            if (pawn == null || vanillaMealDef == null)
            {
                Log.Warning($"[FoodTracker] Inputs are not valid. Pawn: {pawn?.Label ?? "NULL"}, Food Def: {vanillaMealDef?.defName ?? "NULL"}");

                return null;
            }

            // Get the partial meal definition corresponding to the original vanilla meal definition.
            ThingDef partialDef = FoodTrackerMeal.GetMealDef(vanillaMealDef);

            if (partialDef == null)
            {

                Log.Warning($"[FoodTracker] No corresponding partial meal found for {vanillaMealDef.defName}.");

                return null;
            }

            // Create the new partial meal.
            Thing food = ThingMaker.MakeThing(partialDef);

            food.stackCount = 1;

            if (FoodTrackerSettings.Verbose)
            {
                Log.Message($"[FoodTracker] New partial {food.Label} created. Food ID: {food.thingIDNumber}, Food Def: {food.def.defName}");
            }

            // Verify the component exists before attempting to use it.
            if (!FoodTrackerMeal.IsTracked(food))
            {

                Log.Warning($"[FoodTracker] Component missing from {food.Label}. Food ID: {food.thingIDNumber}, Food Def: {food.def.defName}.");

                if (!food.Destroyed)
                {
                    Log.Message($"[FoodTracker] Destroying untracked partial {food.Label}.");

                    food.Destroy(DestroyMode.Vanish);
                }

                return null;
            }

            // Store the nutrition remaining in the partial meal.
            FoodTrackerMeal.SetRemainingNutrition(food, remainingNutrition);

            if (FoodTrackerSettings.Verbose)
            {
                Log.Message($"[FoodTracker] Preparing to drop partial {vanillaMealDef.defName}. Remaining Nutrition: {remainingNutrition}, " +
                    $"Intended Drop Cell: {dropCell}, Food: {food.Label}, Food ID: {food.thingIDNumber}, Food Def: {food.def.defName}");
            }

            // Drop the new partial meal into the world.
            if (!GenDrop.TryDropSpawn(
                food,
                dropCell,
                pawn.Map,
                ThingPlaceMode.Near,
                out Thing resultingThing))
            {
                Log.Warning($"[FoodTracker] Failed to drop partial {food.Label} near {pawn.Label}. Food ID: {food.thingIDNumber}, Food Def: {food.def.defName}.");

                if (!food.Destroyed)
                {
                    Log.Message($"[FoodTracker] Destroying partial {food.Label} reference.");

                    food.Destroy(DestroyMode.Vanish);
                }

                return null;
            }

            if (FoodTrackerSettings.Verbose)
            {
                Log.Message($"[FoodTracker] New partial {resultingThing?.Label ?? "NULL"} created successfully. Food Def: {resultingThing?.def.defName ?? "NULL"}, Food ID: {resultingThing?.thingIDNumber ?? 0}, " +
                    $"Remaining Nutrition: {FoodTrackerMeal.GetRemainingNutrition(resultingThing):F3}, Is Tracked: {FoodTrackerMeal.IsTracked(resultingThing)}");
            }

            return resultingThing;
        }
    }
}