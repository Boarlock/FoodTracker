using RimWorld;
using System.Collections.Generic;
using System.Reflection.Emit;
using Verse;

namespace FoodTracker
{
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

            // Already exists.
            ThingDef existingDef = DefDatabase<ThingDef>.GetNamedSilentFail(newDefName);

            if (existingDef != null)
                return existingDef;

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker] ThingDef cloning is underway. {mealDef.defName} has been received, {newDefName} will be created.");

            ThingDef childDef = Gen.MemberwiseClone(mealDef);

            childDef.defName = newDefName;
            childDef.description = mealDef.description + " (Partial)";
            childDef.label = mealDef.label + " (Partial)";
            childDef.ClearCachedData();

            if (mealDef.comps != null)
                childDef.comps = new List<CompProperties>(mealDef.comps);
            else
                childDef.comps = new List<CompProperties>();

            childDef.comps.Add(new CompProperties_FoodTracker());

            RegisterGeneratedThingDef(childDef, loadingFromSave);

            // Do not touch the GameComponent while it is currently being loaded from the save.
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

        // Everything needed to resolve references, short hash, adding the def to the database, and repopulating ThingCategory's
        private static void RegisterGeneratedThingDef(ThingDef childDef, bool loadingFromSave)
        {
            childDef.shortHash = 0;
            childDef.ResolveDefNameHash();
            childDef.ResolveReferences();
            childDef.generated = true;

            DefDatabase<ThingDef>.Add(childDef);

            AssignShortHash(childDef);
            DefDatabase<ThingDef>.InitializeShortHashDictionary();

            foreach (ThingCategoryDef category in childDef.thingCategories)
            {
                if (!category.childThingDefs.Contains(childDef))
                category.childThingDefs.Add(childDef);

                category.ResolveReferences();
            }

            if (!loadingFromSave)
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