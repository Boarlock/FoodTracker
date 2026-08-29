using RimWorld;
using UnityEngine;
using Verse;

namespace FoodTracker
{
    public static class FoodTrackingHelpers
    {
        // If nutrition is below this threshold treat as interrupted, otherwise FoodTracker does not interfere with ingestion completion.
        public const float MealCompletionThreshold = 0.99f;

        // This is a number we use internally to classify if something is treated as a meal or a batch food item (to make a partial variant or not to).
        public const float MealQualifierThreshold = 0.225f;

        // This is a number we use to scale eating duration time.
        public const float NutritionConsumptionRateMultiplier = 0.90f;

        // Does the reverse operation of calling DynamicMealDefFactory.CreateTrackerMeal(def), this returns the base meal type def.
        public static ThingDef GetOriginalMealDef(ThingDef foodDef)
        {
            if (foodDef == null)
            {
                Log.Warning($"[FoodTracker] Input is not valid. ThingDef Null: {foodDef == null}");

                return null;
            }

            if (!foodDef.defName.StartsWith(DynamicMealDefFactory.Prefix))
                return foodDef;

            string originalDefName = foodDef.defName[DynamicMealDefFactory.Prefix.Length..];

            return DefDatabase<ThingDef>.GetNamedSilentFail(originalDefName);
        }

        // Determines if the target food is a batch food item that should not be subdivided into partials.
        public static bool IsBatchFood(ThingDef foodDef)
        {
            if (foodDef == null)
            {
                Log.Warning($"[FoodTracker] Input is not valid. ThingDef Null: {foodDef == null}");

                return false;
            }

            ThingDef originalDef = GetOriginalMealDef(foodDef);
            float nutrition = originalDef?.GetStatValueAbstract(StatDefOf.Nutrition) ?? 0f;

            return nutrition < MealQualifierThreshold;
        }

        public static bool ValidateFoodEatingAttempt(Pawn pawn, Thing food)
        {

            if (pawn == null || food == null || food.Destroyed)
            {
                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker] Inputs are not valid. Pawn Null: {pawn == null} | Food Null: {food == null} " +
                        $"| Food Destroyed: {food?.Destroyed ?? false}");

                return false;
            }

            if (!pawn.RaceProps.Humanlike)
                return false;

            if (!food.def.IsNutritionGivingIngestible)
                return false;

            return true;
        }

        // This method retrieves the remaining nutrition value of a given food Thing. It returns 0 if the Thing is null,
        // destroyed or if the Thing does not have a CompPartialNutrition component.  It returns the remaining nutrition value from that component. 
        public static float GetRemainingNutrition(Thing food)
        {
            if (food == null || food.Destroyed)
            {
                Log.Warning($"[FoodTracker] Input is not valid. Food Null: {food == null} | Food Destroyed: {food?.Destroyed ?? false}");

                return 0f;
            }

            CompPartialNutrition nutritionTracker = food.TryGetComp<CompPartialNutrition>();

            if (nutritionTracker == null)
            {
                Log.Warning($"[FoodTracker] Component missing from {food?.def?.defName ?? "NULL"} (ID {food.thingIDNumber})");

                return 0f;
            }

            return nutritionTracker.RemainingNutrition;
        }

        // This calls a method in CompPartialNutrition to set the remaining nutrition value of a food Thing.
        // If the Thing is null, destroyed, or does not have a CompPartialNutrition component, it does nothing.
        public static void SetRemainingNutrition(Thing food, float nutrition)
        {
            if (food == null || food.Destroyed || nutrition <= 0f)
            {
                Log.Warning($"[FoodTracker] Inputs are not valid. Food Null: {food?.def?.defName ?? "NULL"} " +
                    $"| Food Destroyed: {food?.Destroyed ?? false} | Nutrition: {nutrition:F4}");

                return;
            }
            CompPartialNutrition nutritionTracker = food.TryGetComp<CompPartialNutrition>();

            if (nutritionTracker == null)
            {
                Log.Warning($"[FoodTracker] Component missing from {food?.def?.defName ?? "NULL"} (ID {food.thingIDNumber})");

                return;
            }

            nutritionTracker.SetRemainingNutrition(nutrition);
        }

        // Method to check pawn and nutrition for invalid values, and apply nutrition to pawn.
        public static void ApplyNutritionToPawn(Pawn pawn, float nutrition)
        {
            if (pawn == null || nutrition < 0f)
            {
                Log.Warning($"[FoodTracker] Inputs are not valid. Pawn: {pawn == null} | Nutrition: {nutrition:F4}");

                return;
            }

            if (pawn.needs?.food == null || pawn.records == null || pawn.needs.food.CurLevel < 0f)
            {
                Log.Warning($"[FoodTracker] Cannot apply nutrition to {pawn.LabelShort}. Pawn Needs Null: {pawn.needs?.food == null} " +
                    $"| Pawn Records Null: {pawn.records == null}| Pawn Needs Level: {pawn.needs.food.CurLevel:F4}");

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