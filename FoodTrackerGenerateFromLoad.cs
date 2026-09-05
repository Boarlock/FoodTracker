using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Xml;
using Verse;

namespace FoodTracker
{

    // Patch to have partial drugs work because DrugPolicy defs are loaded before Game Component
    // ExposeData is called, now FoodTracker defs are loaded before all small game components.
    [HarmonyPatch(typeof(Game), "ExposeSmallComponents")]
    public static class GameExposeSmallComponentsPatch
    {
        public static void Prefix()
        {
            if (Scribe.mode != LoadSaveMode.LoadingVars)
                return;

            Log.Message("[FoodTracker][0] Early save restoration hook reached.");

            FoodTrackerGameComponent.RestoreGeneratedDefsFromSave();
        }
    }

    public class FoodTrackerGameComponent : GameComponent
    {
        // List to store all the dynamic FoodTracker defs generated on a save file.
        public List<string> GeneratedDefNames = new List<string>();

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref GeneratedDefNames, "generatedDefs", LookMode.Value);
        }

        // Game component constructor which is mandatory for Game Component class.
        public FoodTrackerGameComponent(Game game) { }

        // Restores dynamically generated FoodTracker meal definitions when loading a save.
        public static void RestoreGeneratedDefsFromSave()
        {
            // Get the save file node that contains serialized game components.
            XmlNode componentsNode = Scribe.loader.curXmlParent?["components"];

            if (componentsNode == null)
                return;

            // Examine each serialized game component in the save.
            foreach (XmlNode componentNode in componentsNode.ChildNodes)
            {
                // Ignore components that are not FoodTrackerGameComponent instances.
                if (componentNode.Attributes?["Class"]?.Value != typeof(FoodTrackerGameComponent).FullName)
                    continue;

                // Get the XML node containing the names of generated definitions.
                XmlNode generatedDefsNode = componentNode["generatedDefs"];

                if (generatedDefsNode == null)
                    return;

                // Examine each saved generated definition entry.
                foreach (XmlNode defNode in generatedDefsNode.ChildNodes)
                {
                    // Read the generated ThingDef name from the XML entry.
                    string generatedDefName = defNode.InnerText;

                    if (string.IsNullOrEmpty(generatedDefName))
                        continue;

                    // Verify that the saved name uses FoodTracker's generated-definition prefix.
                    if (!generatedDefName.StartsWith(DynamicMealDefFactory.Prefix))
                    {
                        Log.Error($"[FoodTracker] Invalid generated ThingDef name in save: {generatedDefName}");
                        continue;
                    }

                    // Remove the generated definition prefix to recover the original ThingDef name.
                    string originalDefName = generatedDefName.Substring(DynamicMealDefFactory.Prefix.Length);

                    // Find the original def in the DefDatabase and pull it.
                    ThingDef originalDef = DefDatabase<ThingDef>.GetNamedSilentFail(originalDefName);

                    if (originalDef == null)
                    {
                        Log.Error($"[FoodTracker] Could not find original ThingDef {originalDefName} while restoring generated def.");

                        continue;
                    }

                    // Recreate the FoodTracker version of the original definition. The loadingFromSave argument
                    // prevents the game component from being modified while its saved data is still being loaded.
                    DynamicMealDefFactory.CreateTrackerMeal(originalDef, loadingFromSave: true);
                }

                return;

            }
        }
    }
}