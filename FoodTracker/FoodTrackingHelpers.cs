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