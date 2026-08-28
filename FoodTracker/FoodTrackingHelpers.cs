using RimWorld;
using UnityEngine;
using Verse;

namespace FoodTracker
{
    public static class FoodTrackingHelpers
    {
        // If nutrition is below this threshold treat as interrupted, otherwise FoodTracker does not interfere with ingestion completion
        public const float MealCompletionThreshold = 0.995f;

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
                    return Get("FoodTracker_MealSimple");

                case "MealSurvivalPack":
                    return Get("FoodTracker_MealSurvivalPack");

                case "FoodTracker_MealSurvivalPack":
                    return Get("FoodTracker_MealSurvivalPack");

                case "MealNutrientPaste":
                    return Get("FoodTracker_MealNutrientPaste");

                case "FoodTracker_MealNutrientPaste":
                    return Get("FoodTracker_MealNutrientPaste");

                case "MealFine":
                    return Get("FoodTracker_MealFine");

                case "FoodTracker_MealFine":
                    return Get("FoodTracker_MealFine");

                case "MealFine_Veg":
                    return Get("FoodTracker_MealFine_Veg");

                case "FoodTracker_MealFine_Veg":
                    return Get("FoodTracker_MealFine_Veg");

                case "MealFine_Meat":
                    return Get("FoodTracker_MealFine_Meat");

                case "FoodTracker_MealFine_Meat":
                    return Get("FoodTracker_MealFine_Meat");

                case "MealLavish":
                    return Get("FoodTracker_MealLavish");

                case "FoodTracker_MealLavish":
                    return Get("FoodTracker_MealLavish");

                case "MealLavish_Veg":
                    return Get("FoodTracker_MealLavish_Veg");

                case "FoodTracker_MealLavish_Veg":
                    return Get("FoodTracker_MealLavish_Veg");

                case "MealLavish_Meat":
                    return Get("FoodTracker_MealLavish_Meat");

                case "FoodTracker_MealLavish_Meat":
                    return Get("FoodTracker_MealLavish_Meat");

                default:
                    return null;
            }
        }

        // This method checks if a given Thing is tracked by the FoodTracker mod. 
        // It returns false if the Thing is null, destroyed, or does not have a corresponding meal definition. 
        // It also checks if the Thing's definition name starts with "FoodTracker_" to determine if it is a tracked item.
        public static bool IsTracked(Thing food)
        {
            if (food == null || food.Destroyed)
            {
                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker] Input is not valid. Food: {food?.def?.defName ?? "NULL"}, Food ID: " +
                        $"{food?.thingIDNumber ?? 0}, Food Destroyed: {food?.Destroyed ?? false}");

                return false;
            }

            return GetMealDef(food.def) != null && food.def.defName.StartsWith("FoodTracker_");
        }

        // Determines if the target food is a batch food item that can be consumed in multiple portions.
        public static bool IsBatchFood(Thing food)
        {
            if (food == null || food.Destroyed)
            {
                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker] Input is not valid. Food: {food?.def?.defName ?? "NULL"}, Food ID: " +
                        $"{food?.thingIDNumber ?? 0}, Food Destroyed: {food?.Destroyed ?? false}");

                return false;
            }

            return food.def.ingestible.maxNumToIngestAtOnce != 1;
        }

        public static bool ValidateFoodEatingAttempt(Pawn pawn, Thing food)
        {

            if (pawn == null || food == null || food.Destroyed)
            {
                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker] Inputs are not valid. Pawn: {pawn?.Label ?? "NULL"}, Food: {food?.def?.defName ?? "NULL"}, " +
                        $"Food ID: {food?.thingIDNumber ?? 0}, Food Destroyed: {food?.Destroyed ?? false}");

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
                    Log.Message($"[FoodTracker] {food?.def?.defName ?? "NULL"} gives no nutrition, returning to EatingPatch.");

                return false;
            }

            return true;
        }

        // This method retrieves the remaining nutrition value of a given food Thing. It returns 0 if the Thing is null,
        // destroyed or if the Thing has a CompPartialNutrition component.  It returns the remaining nutrition value from that component. 
        public static float GetRemainingNutrition(Thing food)
        {
            if (food == null || food.Destroyed)
            {
                Log.Warning($"[FoodTracker] Input is not valid. Food: {food?.def?.defName ?? "NULL"}, Food ID: " +
                    $"{food?.thingIDNumber ?? 0}, Food Destroyed: {food?.Destroyed ?? false}");

                return 0f;
            }

            CompPartialNutrition nutritionTracker = food.TryGetComp<CompPartialNutrition>();

            if (nutritionTracker == null)
            {
                Log.Warning($"[FoodTracker] Component missing from {food?.def?.defName ?? "NULL"}. Food ID: {food.thingIDNumber}");

                return 0f;
            }

            return nutritionTracker.RemainingNutrition;
        }

        // This calls a method in CompPartialNutrition to set the remaining nutrition value of a food Thing.
        // If the Thing is null, destroyed, or does not have a CompPartialNutrition component, it does nothing.
        public static void SetRemainingNutrition(Thing food, float nutrition)
        {
            if (food == null || food.Destroyed)
            {
                Log.Warning($"[FoodTracker] Input is not valid. Food: {food?.def?.defName ?? "NULL"}, Food ID: " +
                    $"{food?.thingIDNumber ?? 0}, Food Destroyed: {food?.Destroyed ?? false}");

                return;
            }
            CompPartialNutrition nutritionTracker = food.TryGetComp<CompPartialNutrition>();

            if (nutritionTracker == null)
            {
                Log.Warning($"[FoodTracker] Component missing from {food?.def?.defName ?? "NULL"}. Food ID: {food.thingIDNumber}");

                return;
            }

            nutritionTracker.SetRemainingNutrition(nutrition);
        }

        // This calls a method in CompPartialNutrition to consume a specified amount of nutrition from a food Thing.
        // It returns the actual amount of nutrition consumed, which may be less than requested if the food does not have enough remaining nutrition.
        // If the food is null, destroyed, or does not have a CompPartialNutrition component, it returns 0.
        public static float ConsumeNutritionFromFood(Thing food, float nutritionToConsume)
        {
            if (food == null || food.Destroyed)
            {
                Log.Warning($"[FoodTracker] Input is not valid. Food: {food?.def?.defName ?? "NULL"}, Food ID: " +
                    $"{food?.thingIDNumber ?? 0}, Food Destroyed: {food?.Destroyed ?? false}");

                return 0f;

            }

            CompPartialNutrition nutritionTracker = food.TryGetComp<CompPartialNutrition>();

            if (nutritionTracker == null)
            {
                Log.Warning($"[FoodTracker] Component missing from {food?.def?.defName ?? "NULL"}. Food ID: {food.thingIDNumber}");

                return 0f;
            }

            return nutritionTracker.ConsumeNutrition(nutritionToConsume);
        }

        // Method to check pawn and nutrition for invalid values, and apply nutrition to pawn.
        public static void ApplyNutritionToPawn(Pawn pawn, float nutrition)
        {
            if (pawn == null || nutrition < 0f)
            {
                Log.Warning($"[FoodTracker] Input is not valid. Pawn: {pawn?.Label ?? "NULL"}, Nutrition: {nutrition}");

                return;
            }

            if (pawn.needs?.food == null || pawn.needs.food.CurLevel < 0f || pawn.records == null)
            {
                Log.Warning($"[FoodTracker] Cannot apply nutrition bookkeeping to {pawn.Label}.");

                return;
            }

            float currentHungerLevel = pawn.needs.food.CurLevel;
            float maxHungerLevel = pawn.needs.food.MaxLevel; // 1.0 for humans
            float roomInStomach = maxHungerLevel - currentHungerLevel;

            float actualNutritionEaten = Mathf.Min(nutrition, roomInStomach);

            pawn.needs.food.CurLevel += actualNutritionEaten;

            pawn.records.AddTo(RecordDefOf.NutritionEaten, actualNutritionEaten);

        }
    }
}