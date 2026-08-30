using System.IO;
using Verse;
using RimWorld;
using UnityEngine;
using System.Collections.Generic;

namespace FoodTracker
{
    public static class DynamicMealDefFactory
    {
        public const string Prefix = "FoodTracker_";

        public static ThingDef CreateTrackerMeal(IngestionState state)
        {
            if (state.MealDef == null)
                return null;

            // Make the new tracker def name
            string newDefName = Prefix + state.MealDef.defName;

            // Already a generated FoodTracker def.
            if (state.MealDef.defName.StartsWith(Prefix))
                return state.MealDef;

            // Look for the canonical generated def for the ORIGINAL food.
            ThingDef existingDef = DefDatabase<ThingDef>.GetNamedSilentFail(newDefName);
            if (existingDef != null)
                return existingDef;

            if (FoodTrackerSettings.Verbose)
                Log.Message($"[FoodTracker][T{state.TraceID}] ThingDef cloning is underway. {state.MealDef.defName} has been received, {newDefName} will be created.");

            // Clone the ThingDef with all of it's references, fields, data, etc..
            ThingDef childDef = Gen.MemberwiseClone(state.MealDef);

            // Make the partial meal Un-Stackable, set the name, append partial to description
            childDef.defName = newDefName;
            childDef.stackLimit = 1;
            childDef.description = state.MealDef.description + " (Partial)";

            // Set the tracking component
            if (state.MealDef.comps != null)
                childDef.comps = new List<CompProperties>(state.MealDef.comps);
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
                Log.Message($"[FoodTracker][T{state.TraceID}] ThingDef {childDef.defName} has been successfully created.");

            return childDef;
        }
    }
}