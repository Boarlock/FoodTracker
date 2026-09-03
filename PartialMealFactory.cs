using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FoodTracker
{
    public static class PartialMealFactory
    {
        // Replace a vanilla meal with its corresponding partial meal definition, dropping the new item.
        public static Thing CreateAndDropPartialMeal(IngestionState state, float remainingNutrition, IntVec3 dropCell)
        {
            // Validate the input parameters.
            if (state == null || state.Pawn == null || state.PreFood == null)
            {
                Log.Warning($"[FoodTracker][T{state?.TraceID.ToString() ?? "?"}] Inputs are not valid. State Null: {state == null} | Pawn Null: {state?.Pawn == null} " +
                    $"| Food Null: {state?.PreFood == null}");

                return null;
            }

            // Try to get the partial meal definition corresponding to the vanilla meal. If no partial meal definition is found it returns.
            ThingDef partialDef = state.TrackerDef;

            if (partialDef == null)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] No corresponding partial meal found for {state.PreFood.def.defName} (ID {state.PreFood.thingIDNumber}).");

                return null;
            }

            // Create a new partial meal Thing using the partial meal definition and get the tracker comp for it.
            Thing partial = ThingMaker.MakeThing(partialDef);

            // Check to see if the new partial meal has a CompFoodTracker component. If it doesn't, log a warning and destroy the partial meal.
            CompFoodTracker tracker = partial.TryGetComp<CompFoodTracker>();

            // If created item doesn't for any reason contain our component then delete it.
            if (tracker == null && !partial.Destroyed)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Component missing from {state.TrackerDef.defName} (ID {partial?.thingIDNumber ?? 0})");

                state.ThingsToDestroy.Add(partial);
                state.DestroyFoodAfterIngestion = true;

                return null;
            }

            // Access the new partials ingredients comp and set the ingredients to match the original meal's ingredients.
            CompIngredients ingredients = partial.TryGetComp<CompIngredients>();

            if (ingredients != null && state.IngredientsBefore != null)
            {
                ingredients.ingredients = new List<ThingDef>(state.IngredientsBefore);
            }

            // Set the new food's Partial Nutrition component.
            tracker.PartialNutrition = remainingNutrition;
            tracker.NutritionEntries.Clear();

            if (!GenDrop.TryDropSpawn(partial, dropCell, state.Pawn.Map, ThingPlaceMode.Near, out Thing resultingThing))

            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Failed to drop {resultingThing?.def.defName ?? "NULL"} (ID {resultingThing?.thingIDNumber ?? 0})");

                // If the drop failed, don't leave a ghost item in the world. Destroy the partial meal to prevent it from lingering.
                if (resultingThing == null && !resultingThing.Destroyed)
                {
                    Log.Warning($"[FoodTracker][T{state.TraceID}] Failed to make {resultingThing?.def.defName ?? "NULL"} (ID {resultingThing?.thingIDNumber ?? 0})");

                    state.ThingsToDestroy.Add(resultingThing);
                    state.DestroyFoodAfterIngestion = true;

                    return null;
                }

                return null;
            }

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{state.TraceID}] Partial meal created: {resultingThing.def.defName} (ID {resultingThing.thingIDNumber}) | Nutrition Remaining: {remainingNutrition:F2}");

            return resultingThing;
        }
    }
}
