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

        // Corrects a nutrition list or corrupted list if our list doesn't match the FoodTracker_ items in a stack.
        public static void CorrectNutritionList(Thing thing, CompFoodTracker foodTracker)
        {
            // Cllear the elements out of the corrupted list.
            foodTracker.NutritionEntries.Clear();

            // Return the original (base def) of each item in the list.
            ThingDef originalDef = FoodTrackingHelpers.GetOriginalMealDef(thing.def);

            // Store the full nutrition of each base item.
            float nutritionPerItem = originalDef?.GetStatValueAbstract(StatDefOf.Nutrition) ?? 0;

            for (int i = 0; i < thing.stackCount; i++)
            {
                // Store each nutrition value to their respective element in the list.
                foodTracker.NutritionEntries.Add(nutritionPerItem);
            }
        }

        // Does the reverse operation of calling DynamicMealDefFactory.CreateTrackerMeal(def), this returns the base meal type def.
        public static ThingDef GetOriginalMealDef(ThingDef mealDef)
        {
            if (mealDef == null)
            {
                Log.Warning($"[FoodTracker] Input is not valid. ThingDef Null: {mealDef == null}");

                return null;
            }

            if (!mealDef.defName.StartsWith(DynamicMealDefFactory.Prefix))
                return mealDef;

            string originalDefName = mealDef.defName[DynamicMealDefFactory.Prefix.Length..];

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