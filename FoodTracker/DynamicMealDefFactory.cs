using System.IO;
using Verse;
using RimWorld;
using UnityEngine;
using System.Collections.Generic;

namespace FoodTracker
{
    public static class DynamicMealDefFactory
    {
        private const string Prefix = "FoodTracker_";

        public static ThingDef CreateTrackerMeal(ThingDef parentDef)
        {
            if (parentDef == null)
                return null;

            // Check to see if it's already a FoodTracker Meal Def
            if (parentDef.defName.StartsWith("FoodTracker_"))
                return parentDef;

            // Make the new tracker def name
            string newDefName = Prefix + parentDef.defName;

            if (FoodTrackerSettings.Verbose)
                Log.Message($"ThingDef creation is underway! {parentDef.defName} has been received, {newDefName} will be created.");

            // Clone the ThingDef with all of it's references, fields, data, etc..
            ThingDef childDef = Gen.MemberwiseClone(parentDef);

            // Make the partial meal Un-Stackable, set the name, append partial to description
            childDef.defName = newDefName;
            childDef.stackLimit = 1;
            childDef.description = parentDef.description + " (Partial)";

            // Set the tracking component
            if (parentDef.comps != null)
                childDef.comps = new List<CompProperties>(parentDef.comps);
            else
                childDef.comps = new List<CompProperties>();

            childDef.comps.Add(new CompProperties_PartialNutrition());

            // A new Def needs its own hash.
            childDef.shortHash = 0;
            childDef.ResolveDefNameHash();
            childDef.ResolveReferences();

            // Add the new MealDef to the Database
            DefDatabase<ThingDef>.Add(childDef);

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker] {childDef.defName} has been successfully created.");

            return childDef;
        }
    }
}