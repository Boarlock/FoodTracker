using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FoodTracker
{
    public class FoodTrackerGameComponent : GameComponent
    {
        public List<string> GeneratedDefNames = new List<string>();

        public FoodTrackerGameComponent(Game game) { }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(
                ref GeneratedDefNames, "generatedDefs", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                foreach (string mealDefName in GeneratedDefNames)
                {
                    if (string.IsNullOrEmpty(mealDefName))
                        continue;

                    if (!mealDefName.StartsWith(DynamicMealDefFactory.Prefix))
                        continue;

                    string originalDefName =
                        mealDefName.Substring(DynamicMealDefFactory.Prefix.Length);

                    ThingDef originalDef =
                        DefDatabase<ThingDef>.GetNamedSilentFail(originalDefName);

                    if (originalDef == null)
                    {
                        Log.Error($"[FoodTracker] Could not find original ThingDef {originalDefName} while restoring generated def.");

                        continue;
                    }

                    DynamicMealDefFactory.CreateTrackerMeal(originalDef, loadingFromSave: true);
                }
            }
        }
    }
}