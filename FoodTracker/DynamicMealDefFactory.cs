using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FoodTracker
{
    public static class DynamicMealDefFactory
    {
        public const string Prefix = "FoodTracker_";

        public static ThingDef CreateTrackerMeal(IngestionState state)
        {
            if (state.FoodDef == null)
                return null;

            // Make the new tracker def name
            string newDefName = Prefix + state.FoodDef.defName;

            // Already a generated FoodTracker def.
            if (state.FoodDef.defName.StartsWith(Prefix))
                return state.FoodDef;

            // Look for the canonical generated def for the ORIGINAL food.
            ThingDef existingDef = DefDatabase<ThingDef>.GetNamedSilentFail(newDefName);
            if (existingDef != null)
                return existingDef;

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{state.TraceID}] ThingDef cloning is underway. {state.FoodDef.defName} has been received, {newDefName} will be created.");

            // Clone the ThingDef with all of it's references, fields, data, etc..
            ThingDef childDef = Gen.MemberwiseClone(state.FoodDef);

            // Make the partial meal Un-Stackable, set the name, append partial to description
            childDef.defName = newDefName;
//            childDef.stackLimit = 1;
            childDef.description = state.FoodDef.description + " (Partial)";

            // Set the tracking component
            if (state.FoodDef.comps != null)
                childDef.comps = new List<CompProperties>(state.FoodDef.comps);
            else
                childDef.comps = new List<CompProperties>();

            childDef.comps.Add(new CompProperties_FoodTracker());

            RegisterGeneratedThingDef(childDef);
            
            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{state.TraceID}] ThingDef {childDef.defName} has been successfully created.");

            return childDef;
        }

        private static void RegisterGeneratedThingDef(ThingDef childDef)
        {

            // A new Def needs its own hash.
            childDef.shortHash = 0;
            childDef.ResolveDefNameHash();
            childDef.ResolveReferences();
            childDef.generated = true;

            // Add the new FoodTracker Def to the Database
            DefDatabase<ThingDef>.Add(childDef);

            // Register the new ThingDef with its ThingCategoryDefs
            foreach (ThingCategoryDef category in childDef.thingCategories)
            {
                if (!category.childThingDefs.Contains(childDef))
                {
                    category.childThingDefs.Add(childDef);
                }

                // Rebuild the category's cached ThingDef lists
                category.ResolveReferences();
            }

            // Because meals are counted as resources we need to update resource center with our new ThingDef
            ResourceCounter.ResetDefs();
        }
    }
}