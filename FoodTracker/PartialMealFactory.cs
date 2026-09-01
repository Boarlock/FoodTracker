using Verse;

namespace FoodTracker
{
    public static class PartialMealFactory
    {
        // Replace a vanilla meal with its corresponding partial meal definition, dropping the new item.
        public static Thing CreateAndDropPartialMeal(IngestionState state, float remainingNutrition, IntVec3 dropCell)
        {
            // Validate the input parameters.
            if (state == null || state.Pawn == null || state.Food == null || state.Food.Destroyed)
            {
                Log.Warning($"[FoodTracker][T{state?.TraceID.ToString() ?? "?"}] Inputs are not valid. State Null: {state == null} | Pawn Null: {state?.Pawn == null} " +
                    $"| Food Null: {state?.Food == null} | Food Destroyed: {state?.Food?.Destroyed ?? false}");

                return null;
            }

            // Get the partial meal definition corresponding to the vanilla meal. If no partial meal definition is found it returns.
            ThingDef partialDef = DynamicMealDefFactory.CreateTrackerMeal(state);

            if (partialDef == null)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] No corresponding partial meal found for {state.Food.def.defName} (ID {state.Food.thingIDNumber}).");

                return null;
            }

            // Create a new partial meal Thing using the partial meal definition and get the tracker comp for it.
            Thing food = ThingMaker.MakeThing(partialDef);

            CompFoodTracker tracker = food.TryGetComp<CompFoodTracker>();

            // If created item doesn't for any reason contain our component then delete it.
            if (tracker == null)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Component missing from {food?.def?.defName ?? "NULL"} (ID {food.thingIDNumber})");

                if (!food.Destroyed)
                {
                    state.FoodToDestroy = state.Food;
                    state.DestroyFoodAfterIngestion = true;
                }

                return null;
            }

            tracker.PartialNutrition = remainingNutrition;
            tracker.NutritionEntries.Clear();
            
            if (!GenDrop.TryDropSpawn(food, dropCell, state.Pawn.Map, ThingPlaceMode.Near, out Thing resultingThing))

            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Failed to drop {resultingThing?.def.defName ?? "NULL"} (ID {resultingThing?.thingIDNumber ?? 0})");

                // If the drop failed, don't leave a ghost item in the world. Destroy the partial meal to prevent it from lingering.
                if (!food.Destroyed)
                {
                    state.FoodToDestroy = state.Food;
                    state.DestroyFoodAfterIngestion = true;
                }

                return null;
            }

            if (resultingThing == null)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Failed to make {resultingThing?.def.defName ?? "NULL"} (ID {resultingThing?.thingIDNumber ?? 0})");

                return null;
            }

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{state.TraceID}] Partial meal created: {resultingThing.def.defName} (ID {resultingThing.thingIDNumber}) " +
                    $"| Nutrition: {remainingNutrition:F2}");

            return resultingThing;
        }
    }
}
        