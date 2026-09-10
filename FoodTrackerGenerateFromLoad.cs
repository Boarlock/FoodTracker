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

        public static void Prefix(Game __instance)
        {
            if (Scribe.mode != LoadSaveMode.LoadingVars)
                return;

            Log.Message("[FoodTracker][0] Early save restoration hook reached.");

            FoodTrackerGameComponent.RestoreGeneratedDefsFromSave(__instance);
        }

        public static void Postfix(Game __instance)
        {
            if (Scribe.mode != LoadSaveMode.LoadingVars)
                return;

            FoodTrackerGameComponent component = __instance.GetComponent<FoodTrackerGameComponent>();

            if (component == null)
                return;

            if (FoodTrackerGameComponent.DiscoveredGeneratedDefs.Count == 0)
                return;

            foreach (string defName in FoodTrackerGameComponent.DiscoveredGeneratedDefs)
            {
                if (!component.GeneratedDefNames.Contains(defName))
                    component.GeneratedDefNames.Add(defName);
            }

            FoodTrackerGameComponent.DiscoveredGeneratedDefs.Clear();
        }
    }

    public class FoodTrackerGameComponent : GameComponent
    {
        // List to store all the dynamic FoodTracker defs generated on a save file.
        public List<string> GeneratedDefNames = new List<string>();

        // Used to not generate any duplicates during re-generation.
        public static readonly HashSet<string> DiscoveredGeneratedDefs = new HashSet<string>();


        public override void ExposeData()
        {
            base.ExposeData();

            bool hasRecoveredDefs = DiscoveredGeneratedDefs.Count > 0;

            Scribe_Collections.Look(ref GeneratedDefNames, "generatedDefs", LookMode.Value);

            if (GeneratedDefNames == null)
                GeneratedDefNames = new List<string>();

        }


        // Game component constructor which is mandatory for Game Component class.
        public FoodTrackerGameComponent(Game game) { }

        // Restores dynamically generated FoodTracker meal definitions when loading a save.
        public static void RestoreGeneratedDefsFromSave(Game game)
        {

            // Get the save file node that contains game components.
            XmlNode componentsNode = Scribe.loader.curXmlParent?["components"];

            // No game component node exists, so recover the FoodTracker component and scan the saved world for legacy generated definitions.
            if (componentsNode == null)
            {
                RestoreLegacyGeneratedDefs(game);
                return;
            }

            // Examine each game component in the save.
            foreach (XmlNode componentNode in componentsNode.ChildNodes)
            {
                // Ignore components that are not FoodTrackerGameComponent instances.
                if (componentNode.Attributes?["Class"]?.Value != typeof(FoodTrackerGameComponent).FullName)
                    continue;

                // Get the XML node containing the names of generated definitions.
                XmlNode generatedDefsNode = componentNode["generatedDefs"];

                if (generatedDefsNode == null || generatedDefsNode.ChildNodes.Count == 0)
                {
                    // Save was created before GeneratedDefNames existed.
                    RestoreLegacyGeneratedDefs(game);
                    return;
                }

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
                        Log.Warning($"[FoodTracker] Invalid generated ThingDef name in save: {generatedDefName}");
                        continue;
                    }

                    // Remove the generated definition prefix to recover the original ThingDef name.
                    string originalDefName = generatedDefName.Substring(DynamicMealDefFactory.Prefix.Length);

                    // Find the original def in the DefDatabase and pull it.
                    ThingDef originalDef = DefDatabase<ThingDef>.GetNamedSilentFail(originalDefName);

                    if (originalDef == null)
                    {
                        Log.Warning($"[FoodTracker] Could not find original ThingDef {originalDefName} while restoring generated def.");

                        continue;
                    }

                    // Recreate the FoodTracker version of the original definition. The loadingFromSave argument
                    // prevents the game component from being modified while its saved data is still being loaded.
                    DynamicMealDefFactory.CreateTrackerMeal(originalDef, true);
                }

                return;
            }
            // After searching all game components, no FT component exists so this creates one.
            RestoreLegacyGeneratedDefs(game);
            return;
        }


        private static void RestoreLegacyGeneratedDefs(Game game)
        {
            Log.Message("[FoodTracker] Possible legacy save detected. Scanning saved Things for generated FoodTracker defs.");

            // If FT game component doesn't exist this creates one.
            FoodTrackerGameComponent component = game.GetComponent<FoodTrackerGameComponent>();
            if (component == null)
            {
                component = new FoodTrackerGameComponent(game);
                game.components.Add(component);
            }

            // If generated Def names don't exist this creates them.
            component.GeneratedDefNames ??= new List<string>();

            // Find the Food Restriction Database node in the save file.
            XmlNode foodRestrictionDatabase = Scribe.loader.curXmlParent?["foodRestrictionDatabase"];
            if (foodRestrictionDatabase != null)
            {
                // Find the Food Restriction node in the save file.
                XmlNode foodRestrictions = foodRestrictionDatabase["foodRestrictions"];
                if (foodRestrictions != null)
                {
                    // Iterate through the children node(s) of Food Restriction.
                    foreach (XmlNode restrictionNode in foodRestrictions.ChildNodes)
                    {
                        if (restrictionNode?.Name != "li")
                            continue;

                        // Enter the Filter node(s).
                        XmlNode filterNode = restrictionNode["filter"];

                        if (filterNode == null)
                            continue;

                        // Enter allowed defs node(s).
                        XmlNode allowedDefsNode = filterNode["allowedDefs"];

                        if (allowedDefsNode == null)
                            continue;

                        // Iterate through allowed defs node(s) looking for FT defs.
                        foreach (XmlNode defNode in allowedDefsNode.ChildNodes)
                        {
                            if (defNode?.Name != "li")
                                continue;

                            string generatedDefName = defNode.InnerText;

                            if (string.IsNullOrEmpty(generatedDefName))
                                continue;

                            if (!generatedDefName.StartsWith(DynamicMealDefFactory.Prefix))
                                continue;

                            // Add FT defs to the HashSet.
                            if (!DiscoveredGeneratedDefs.Add(generatedDefName))
                                continue;

                            Log.Message($"[FoodTracker] Found legacy generated def in food restrictions: {generatedDefName}");

                            // Get the Non-FT defname.
                            string originalDefName = generatedDefName.Substring(DynamicMealDefFactory.Prefix.Length);

                            // Get the original def from the Database to send to the factory.
                            ThingDef originalDef = DefDatabase<ThingDef>.GetNamedSilentFail(originalDefName);

                            if (originalDef == null)
                            {
                                Log.Warning($"[FoodTracker] Could not find original ThingDef {originalDefName} while restoring legacy generated def.");
                                continue;
                            }

                            DynamicMealDefFactory.CreateTrackerMeal(originalDef, true);
                        }
                    }
                }
            }

            // Find the map node in the save file, if it doesn't exiswt then exist early.
            XmlNode mapsNode = Scribe.loader.curXmlParent?["maps"];
            if (mapsNode == null)
            {
                Log.Warning("[FoodTracker] Could not find <maps> while restoring legacy generated defs.");
                return;
            }

            // Iterate through all children maps.
            XmlNodeList mapChildren = mapsNode.ChildNodes;
            foreach (XmlNode mapNode in mapChildren)
            {

                if (mapNode == null)
                    continue;

                if (mapNode.Name != "li")
                    continue;

                // Enter the Things node(s).
                XmlNode thingsNode = mapNode["things"];

                if (thingsNode == null)
                    continue;

                // Iterate through Thing node(s) inside Things looking for FT defs.
                foreach (XmlNode thingNode in thingsNode.ChildNodes)
                {

                    if (thingNode?.Name != "thing")
                        continue;

                    // Enter the def node(s).
                    XmlNode defNode = thingNode["def"];

                    if (defNode == null)
                        continue;

                    string generatedDefName = defNode.InnerText;

                    if (string.IsNullOrEmpty(generatedDefName))
                        continue;

                    if (!generatedDefName.StartsWith(DynamicMealDefFactory.Prefix))
                        continue;

                    // Add FT def to hashset.
                    if (!DiscoveredGeneratedDefs.Add(generatedDefName))
                        continue;

                    Log.Message($"[FoodTracker] Found legacy generated def in map Things: {generatedDefName}");

                    // Get the Non-FT defname.
                    string originalDefName = generatedDefName.Substring(DynamicMealDefFactory.Prefix.Length);

                    // Get the original def from the Database to send to the factory.
                    ThingDef originalDef = DefDatabase<ThingDef>.GetNamedSilentFail(originalDefName);

                    if (originalDef == null)
                    {
                        Log.Warning($"[FoodTracker] Could not find original ThingDef {originalDefName} while restoring legacy generated def.");
                        continue;
                    }

                    DynamicMealDefFactory.CreateTrackerMeal(originalDef, true);
                }
            }
        }
    }
}