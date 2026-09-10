using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FoodTracker
{
    [HarmonyPatch(typeof(ThingFilter), nameof(ThingFilter.Allows), typeof(ThingDef))]
    public static class FoodTrackerPatch_ThingFilterAllows
    {
        public static bool Prefix(ThingDef def, ref bool __result)
        {
            if (def != null && def.defName.StartsWith(DynamicMealDefFactory.Prefix))
            {
                __result = true;
                return false;
            }

            return true;
        }
    }

    public static class DynamicMealDefFactory
    {
        public const string Prefix = "FoodTracker_";

        public static ThingDef CreateTrackerMeal(ThingDef mealDef, bool loadingFromSave = false)
        {
            if (mealDef == null)
                return null;

            string newDefName = Prefix + mealDef.defName;

            // Already a generated FoodTracker def.
            if (mealDef.defName.StartsWith(Prefix))
                return mealDef;

            // Check if it exists in the def database.
            ThingDef existingDef = DefDatabase<ThingDef>.GetNamedSilentFail(newDefName);

            if (existingDef != null)
                return existingDef;

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker] ThingDef cloning is underway. {mealDef.defName} has been received, {newDefName} will be created.");

            ThingDef childDef = Gen.MemberwiseClone(mealDef);

            // Create a new FT defName, description and label.
            childDef.defName = newDefName;
            childDef.description = mealDef.description + " (Partial)";
            childDef.label = mealDef.label + " (Partial)";
            childDef.ClearCachedData();

            // Add the FT tracking component.
            if (mealDef.comps != null)
                childDef.comps = new List<CompProperties>(mealDef.comps);
            else
                childDef.comps = new List<CompProperties>();

            childDef.comps.Add(new CompProperties_FoodTracker());

            // Add new def to the virtual defs list of it's parent.
            childDef.virtualDefs = new List<ThingDef>();

            if (!mealDef.virtualDefs.Contains(childDef))
                mealDef.virtualDefs.Add(childDef);

            childDef.virtualDefParent = mealDef;

            // Access the Food Restriction database and iterate through Food Restrictions to allow FT defs.
            FoodRestrictionDatabase database = Current.Game.foodRestrictionDatabase;

            foreach (FoodPolicy policy in database.AllFoodRestrictions)
            {
                policy.filter.SetAllow(mealDef, true);
            }

            // Register the defs short hash, references, and thing categories.
            RegisterGeneratedThingDef(childDef);

            if (!loadingFromSave)
            {
                FoodTrackerGameComponent component = Current.Game.GetComponent<FoodTrackerGameComponent>();

                if (!component.GeneratedDefNames.Contains(childDef.defName))
                    component.GeneratedDefNames.Add(childDef.defName);
            }

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker] ThingDef {childDef.defName} has been successfully created.");

            return childDef;
        }

        // Everything needed to resolve references, short hash, adding the def to the database, and repopulating Thing Categories.
        private static void RegisterGeneratedThingDef(ThingDef childDef)
        {

            childDef.shortHash = 0;
            childDef.ResolveDefNameHash();
            childDef.ResolveReferences();
            childDef.generated = true;

            DefDatabase<ThingDef>.Add(childDef);

            AssignShortHash(childDef);
            DefDatabase<ThingDef>.InitializeShortHashDictionary();

            // Re-generate Thing categorys and resolve references after each.
            foreach (ThingCategoryDef category in childDef.thingCategories)
            {
                if (!category.childThingDefs.Contains(childDef))
                    category.childThingDefs.Add(childDef);

                category.ResolveReferences();
            }
            
            // Update resource center with the new def.
            ResourceCounter.ResetDefs();
        }

        // Reproducing vanilla's exact short has algorithm
        private static void AssignShortHash(ThingDef def)
        {
            HashSet<ushort> takenHashes = new HashSet<ushort>();

            foreach (ThingDef existingDef in DefDatabase<ThingDef>.AllDefs)
            {
                if (existingDef == def)
                    continue;

                if (existingDef.shortHash != 0)
                    takenHashes.Add(existingDef.shortHash);
            }

            ushort hash = (ushort)(GenText.StableStringHash(def.defName) % 65535);
            int attempts = 0;

            while (hash == 0 || takenHashes.Contains(hash))
            {
                hash++;
                attempts++;

                if (attempts > 5000)
                    Log.Message("[FoodTracker] Short hashes are saturated. There are probably too many ThingDefs.");
            }

            def.shortHash = hash;
        }
    }
}