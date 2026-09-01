using RimWorld;
using Verse;

namespace FoodTracker
{
    public static class CompDumper
    {
        public static void DumpMealComps(Thing food)
        {
            if (food == null)
            {
                Log.Message("[FoodTracker][COMP DUMP] Food is NULL");
                return;
            }

            Log.Message(
                $"[FoodTracker][COMP DUMP] " +
                $"Def={food.def.defName} | " +
                $"ID={food.thingIDNumber} | " +
                $"Stack={food.stackCount}"
            );

            ThingWithComps thingWithComps = food as ThingWithComps;

            if (thingWithComps == null)
            {
                Log.Message("[FoodTracker][COMP DUMP] Food is not ThingWithComps.");
                return;
            }

            foreach (ThingComp comp in thingWithComps.AllComps)
            {
                if (comp == null)
                    continue;

                Log.Message(
                    $"[FoodTracker][COMP DUMP] Comp={comp.GetType().FullName}"
                );

                CompIngredients ingredients = comp as CompIngredients;

                if (ingredients != null)
                {
                    Log.Message(
                        $"[FoodTracker][COMP DUMP]   CompIngredients count={ingredients.ingredients.Count}"
                    );

                    foreach (ThingDef ingredient in ingredients.ingredients)
                    {
                        Log.Message(
                            $"[FoodTracker][COMP DUMP]   Ingredient={ingredient.defName}"
                        );
                    }
                }
            }
        }
    }
}