using RimWorld;
using UnityEngine;
using Verse;

namespace FoodTracker
{
    public static class FoodTrackerMeal
    {

        // This method retrieves a ThingDef by its definition name. It returns null if no definition is found with the given name.
        private static ThingDef Get(string defName)
        {
            return DefDatabase<ThingDef>.GetNamedSilentFail(defName);
        }

        // This method retrieves the corresponding meal definition for a given ThingDef. It returns null if the input is null or if no corresponding meal definition is found.
        public static ThingDef GetMealDef(ThingDef def)
        {
            if (def == null)
                return null;

            switch (def.defName)
            {
                case "MealSimple":
                    return Get("FoodTracker_MealSimple");

                case "FoodTracker_MealSimple":
                    return Get("MealSimple");

                case "MealSurvivalPack":
                    return Get("FoodTracker_MealSurvivalPack");

                case "FoodTracker_MealSurvivalPack":
                    return Get("MealSurvivalPack");

                case "MealNutrientPaste":
                    return Get("FoodTracker_MealNutrientPaste");

                case "FoodTracker_MealNutrientPaste":
                    return Get("MealNutrientPaste");

                case "MealFine":
                    return Get("FoodTracker_MealFine");

                case "FoodTracker_MealFine":
                    return Get("MealFine");

                case "MealFine_Veg":
                    return Get("FoodTracker_MealFine_Veg");

                case "FoodTracker_MealFine_Veg":
                    return Get("MealFine_Veg");

                case "MealFine_Meat":
                    return Get("FoodTracker_MealFine_Meat");

                case "FoodTracker_MealFine_Meat":
                    return Get("MealFine_Meat");

                case "MealLavish":
                    return Get("FoodTracker_MealLavish");

                case "FoodTracker_MealLavish":
                    return Get("MealLavish");

                case "MealLavish_Veg":
                    return Get("FoodTracker_MealLavish_Veg");

                case "FoodTracker_MealLavish_Veg":
                    return Get("MealLavish_Veg");

                case "MealLavish_Meat":
                    return Get("FoodTracker_MealLavish_Meat");

                case "FoodTracker_MealLavish_Meat":
                    return Get("MealLavish_Meat");

                default:
                    return null;
            }
        }

        // A small threshold to determine if a food item has been fully consumed.
        public const float NutritionEpsilon = 0.005f;

        // This method checks if a given Thing is tracked by the FoodTracker mod. 
        // It returns false if the Thing is null, destroyed, or does not have a corresponding meal definition. 
        // It also checks if the Thing's definition name starts with "FoodTracker_" to determine if it is a tracked item.
        public static bool IsTracked(Thing food)
        {
            if (food == null || food.Destroyed)
            {
                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker] Inputs are not valid. Food: {food?.Label ?? "NULL"}, Food ID: " +
                        $"{food?.thingIDNumber ?? 0}, Food Def: {food?.def.defName ?? "NULL"}, Food Destroyed: {food?.Destroyed ?? false}");

                return false;
            }

            return GetMealDef(food.def) != null && food.def.defName.StartsWith("FoodTracker_");
        }

        public static bool ValidateFood(Pawn pawn, Thing food)
        {

            if (pawn == null || food == null || food.Destroyed)
            {
                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker] Inputs are not valid. Pawn: {pawn?.Label ?? "NULL"}, Food: {food?.Label ?? "NULL"}, " +
                        $"Food ID: {food?.thingIDNumber ?? 0}, Food Def: {food?.def.defName ?? "NULL"}, Food Destroyed: {food?.Destroyed ?? false}");

                return false;
            }

            if (!pawn.RaceProps.Humanlike)
            {
                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker] {pawn.Label} is not human, returning to EatingPatch.");

                return false;
            }

            if (!food.def.IsNutritionGivingIngestible)
            {
                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker] {food.Label} gives no nutrition, returning to EatingPatch.");

                return false;
            }

            return true;
        }

        // This method retrieves the full nutrition value of a given food Thing. It returns 0 if the Thing is null or destroyed,
        // and otherwise uses the StatDefOf.Nutrition stat to get the full nutrition value.
        public static float GetFullNutrition(Thing food)
        {
            if (food == null || food.Destroyed)
            {
                Log.Warning($"[FoodTracker] Input is not valid. Food: {food?.Label ?? "NULL"}, Food ID: " +
                    $"{food?.thingIDNumber ?? 0}, Food Def: {food?.def.defName ?? "NULL"}, Food Destroyed: {food?.Destroyed ?? false}");

                return 0f;
            }

            return food.GetStatValue(StatDefOf.Nutrition);
        }

        // This method retrieves the remaining nutrition value of a given food Thing. It returns 0 if the Thing is null,
        // destroyed or if the Thing has a CompPartialNutrition component.  It returns the remaining nutrition value from that component. 
        public static float GetRemainingNutrition(Thing food)
        {
            if (food == null || food.Destroyed)
            {
                Log.Warning($"[FoodTracker] Input is not valid. Food: {food?.Label ?? "NULL"}, Food ID: " +
                    $"{food?.thingIDNumber ?? 0}, Food Def: {food?.def.defName ?? "NULL"}, Food Destroyed: {food?.Destroyed ?? false}");

                return 0f;
            }

            CompPartialNutrition nutritionTracker =
                food.TryGetComp<CompPartialNutrition>();

            if (nutritionTracker == null)
            {
                Log.Warning($"[FoodTracker] Component missing from {food.Label}. Food ID: {food.thingIDNumber}, Food Def: {food.def.defName}.");

                return 0f;
            }

            return nutritionTracker.RemainingNutrition;
        }

        // This calls a method in CompPartialNutrition to set the remaining nutrition value of a food Thing.
        // If the Thing is null, destroyed, or does not have a CompPartialNutrition component, it does nothing.
        public static void SetRemainingNutrition(
            Thing food,
            float nutrition)
        {
            if (food == null || food.Destroyed)
            {
                Log.Warning($"[FoodTracker] Input is not valid. Food: {food?.Label ?? "NULL"}, Food ID: " +
                    $"{food?.thingIDNumber ?? 0}, Food Def: {food?.def.defName ?? "NULL"}, Food Destroyed: {food?.Destroyed ?? false}");

                return;
            }
            CompPartialNutrition nutritionTracker =
                food.TryGetComp<CompPartialNutrition>();

            if (nutritionTracker == null)
            {
                Log.Warning($"[FoodTracker] Component missing from {food.Label}. Food ID: {food.thingIDNumber}, Food Def: {food.def.defName}.");

                return;
            }

            nutritionTracker.SetRemainingNutrition(nutrition);
        }

        // This calls a method in CompPartialNutrition to consume a specified amount of nutrition from a food Thing.
        // It returns the actual amount of nutrition consumed, which may be less than requested if the food does not have enough remaining nutrition.
        // If the food is null, destroyed, or does not have a CompPartialNutrition component, it returns 0.
        public static float ConsumeNutrition(
            Thing food,
            float nutritionToConsume)
        {
            if (food == null || food.Destroyed)
            {
                Log.Warning($"[FoodTracker] Input is not valid. Food: {food?.Label ?? "NULL"}, Food ID: " +
                    $"{food?.thingIDNumber ?? 0}, Food Def: {food?.def.defName ?? "NULL"}, Food Destroyed: {food?.Destroyed ?? false}");

                return 0f;
                
            }

            CompPartialNutrition nutritionTracker =
                food.TryGetComp<CompPartialNutrition>();

            if (nutritionTracker == null)
            {
                Log.Warning($"[FoodTracker] Component missing from {food.Label}. Food ID: {food.thingIDNumber}, Food Def: {food.def.defName}.");

                return 0f;
            }

            return nutritionTracker.ConsumeNutrition(nutritionToConsume);
        }

        // Method to check pawn and nutrition for invalid values, and apply nutrition to pawn.
        public static void ApplyNutritionToPawn(
            Pawn pawn,
            float nutrition)
        {
            if (pawn == null || nutrition <= 0f)
            {
                Log.Warning($"[FoodTracker] Input is not valid. Pawn: {pawn?.Label ?? "NULL"}, Nutrition: {nutrition}");

                return;
            }

            if (pawn.needs?.food == null || pawn.records == null)
            {
                Log.Warning($"[FoodTracker] Cannot apply nutrition bookkeeping to {pawn.Label}.");

                return;
            }

            pawn.needs.food.CurLevel += nutrition;
            pawn.records.AddTo(
                RecordDefOf.NutritionEaten,
                nutrition);

        }
    }
}