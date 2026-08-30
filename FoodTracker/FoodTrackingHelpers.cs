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
        public static ThingDef GetOriginalMealDef(IngestionState state)
        {
            if (state.FoodDef == null)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Input is not valid. ThingDef Null: {state.FoodDef == null}");

                return null;
            }

            if (!state.FoodDef.defName.StartsWith(DynamicMealDefFactory.Prefix))
                return state.FoodDef;

            string originalDefName = state.FoodDef.defName[DynamicMealDefFactory.Prefix.Length..];

            return DefDatabase<ThingDef>.GetNamedSilentFail(originalDefName);
        }

        // Determines if the target food is a batch food item that should not be subdivided into partials.
        public static bool IsBatchFood(IngestionState state)
        {
            if (state.BaseDef == null)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Input is not valid. ThingDef Null: {state.BaseDef == null}");

                return false;
            }

            float nutrition = state.BaseDef?.GetStatValueAbstract(StatDefOf.Nutrition) ?? 0f;

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
        public static float GetRemainingNutrition(IngestionState state)
        {
            if (state.Food == null || state.Food.Destroyed)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Input is not valid. Food Null: {state.Food == null} | Food Destroyed: {state.Food?.Destroyed ?? false}");

                return 0f;
            }

            CompPartialNutrition nutritionTracker = state.Food.TryGetComp<CompPartialNutrition>();

            if (nutritionTracker == null)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Component missing from {state.Food?.def?.defName ?? "NULL"} (ID {state.Food.thingIDNumber})");

                return 0f;
            }

            return nutritionTracker.RemainingNutrition;
        }

        // This calls a method in CompPartialNutrition to set the remaining nutrition value of a food Thing.
        // If the Thing is null, destroyed, or does not have a CompPartialNutrition component, it does nothing.
        public static void SetRemainingNutrition(IngestionState state, float nutrition)
        {
            if (state.Food == null || state.Food.Destroyed || nutrition < 0f)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Inputs are not valid. Food Null: {state.Food?.def?.defName ?? "NULL"} " +
                    $"| Food Destroyed: {state.Food?.Destroyed ?? false} | Nutrition: {nutrition:F4}");

                return;
            }
            CompPartialNutrition nutritionTracker = state.Food.TryGetComp<CompPartialNutrition>();

            if (nutritionTracker == null)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Component missing from {state.Food?.def?.defName ?? "NULL"} (ID {state.Food.thingIDNumber})");

                return;
            }

            nutritionTracker.SetRemainingNutrition(nutrition);
        }

        // Method to check pawn and nutrition for invalid values, and apply nutrition to pawn.
        public static void ApplyNutritionToPawn(IngestionState state, float nutrition)
        {
            if (state.Pawn == null || nutrition < 0f)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Inputs are not valid. Pawn: {state.Pawn == null} | Nutrition: {nutrition:F4}");

                return;
            }

            if (state.Pawn.needs?.food == null || state.Pawn.records == null || state.Pawn.needs.food.CurLevel < 0f)
            {
                Log.Warning($"[FoodTracker][T{state.TraceID}] Cannot apply nutrition to {state.Pawn.LabelShort}. Pawn Needs Null: {state.Pawn.needs?.food == null} " +
                    $"| Pawn Records Null: {state.Pawn.records == null}| Pawn Needs Level: {state.Pawn.needs.food.CurLevel:F4}");

                return;
            }
            
            float currentHungerLevel = state.Pawn.needs.food.CurLevel;
            float maxHungerLevel = state.Pawn.needs.food.MaxLevel; // 1.0 for humans
            float roomInStomach = maxHungerLevel - currentHungerLevel;

            float actualNutritionEaten = Mathf.Min(nutrition, roomInStomach);

            state.Pawn.needs.food.CurLevel += actualNutritionEaten;

            state.Pawn.records.AddTo(RecordDefOf.NutritionEaten, actualNutritionEaten);

        }
    }
}