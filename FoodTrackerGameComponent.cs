using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FoodTracker
{
    public class FoodTrackerGameComponent : GameComponent
    {
        // List to store all the dynamic FoodTracker defs generated on a save file.
        public List<string> GeneratedDefNames = new List<string>();

        // Game component constructor which is mandatory for Game Component class.
        public FoodTrackerGameComponent(Game game) { }

        // Expose the list so it's saved.
        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref GeneratedDefNames, "generatedDefs", LookMode.Value);

            // On loading from a save the dynamic defs need to be re-generated while Rimworld is still resolving it's references.
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                foreach (string mealDefName in GeneratedDefNames)
                {
                    if (string.IsNullOrEmpty(mealDefName))
                        continue;

                    if (!mealDefName.StartsWith(DynamicMealDefFactory.Prefix))
                        continue;

                    // We need the original defs for def generation, this gets the original def names.
                    string originalDefName = mealDefName.Substring(DynamicMealDefFactory.Prefix.Length);

                    // Find the original def in the DefDatabase and pull it.
                    ThingDef originalDef = DefDatabase<ThingDef>.GetNamedSilentFail(originalDefName);

                    if (originalDef == null)
                    {
                        Log.Error($"[FoodTracker] Could not find original ThingDef {originalDefName} while restoring generated def.");

                        continue;
                    }

                    // Re-Generate the FoodTracker variant.
                    DynamicMealDefFactory.CreateTrackerMeal(originalDef, loadingFromSave: true);
                }
            }
        }
    }
}