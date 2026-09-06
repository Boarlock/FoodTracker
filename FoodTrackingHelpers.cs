using RimWorld;
using UnityEngine;
using Verse;

namespace FoodTracker
{
    public static class FoodTrackingHelpers

    {
        // Amount of nutrition this mod considers irrelevant and therefore doesn't track.
        public const float MinimumPartialNutrition = 0.01f;

        public static FoodTrackerDrugEffects.FoodTrackerDrugType GetDrugType(ThingDef def)
        {
            if (def == null || def.ingestible == null)
                return FoodTrackerDrugEffects.FoodTrackerDrugType.Unsupported;

            switch (def.defName)
            {
                case "Ambrosia":
                    return FoodTrackerDrugEffects.FoodTrackerDrugType.Ambrosia;

                case "Beer":
                    return FoodTrackerDrugEffects.FoodTrackerDrugType.Beer;

                case "PsychiteTea":
                    return FoodTrackerDrugEffects.FoodTrackerDrugType.PsychiteTea;

                case "SmokeleafJoint":
                    return FoodTrackerDrugEffects.FoodTrackerDrugType.Smokeleaf;

                case "GoJuice":
                    return FoodTrackerDrugEffects.FoodTrackerDrugType.GoJuice;

                case "Flake":
                    return FoodTrackerDrugEffects.FoodTrackerDrugType.Flake;

                default:
                    return FoodTrackerDrugEffects.FoodTrackerDrugType.Unsupported;
            }
        }

        public static int GetDrugBaseIngestTicks(ThingDef def)
        {
            switch (def.defName)
            {
                case "Ambrosia":
                    return 100;

                case "Beer":
                    return 120;

                case "PsychiteTea":
                    return 210;

                case "SmokeleafJoint":
                    return 720;

                case "GoJuice":
                    return 100;

                case "Flake":
                    return 650;

                default:
                    return 0;
            }
        }

        public static float GetFoodTrackerNutritionValue(ThingDef def)
        {
            ThingDef originalDef = GetOriginalMealDef(def);

            if (originalDef == null)
                return 0f;

            if (GetDrugType(originalDef) != FoodTrackerDrugEffects.FoodTrackerDrugType.Unsupported)
                return originalDef.GetStatValueAbstract(StatDefOf.Mass);

            return originalDef.GetStatValueAbstract(StatDefOf.Nutrition);
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

        // Method to check pawn and nutrition for invalid values, and apply nutrition to pawn.
        public static void ApplyNutritionToPawn(IngestionState state, float nutrition)
        {
            if (state.Pawn == null || nutrition < 0f)
            {
                Log.Warning($"[FoodTracker] Inputs are not valid. Pawn: {state.Pawn == null} | Nutrition: {nutrition:F4}");

                return;
            }

            if (state.Pawn.needs?.food == null || state.Pawn.records == null || state.Pawn.needs.food.CurLevel < 0f)
            {
                Log.Warning($"[FoodTracker] Cannot apply nutrition to {state.Pawn.LabelShort}. Pawn Needs Null: {state.Pawn.needs?.food == null} " +
                    $"| Pawn Records Null: {state.Pawn.records == null}| Pawn Needs Level: {state.Pawn.needs.food.CurLevel:F4}");

                return;
            }
            
            // Calculate current hunger level and max total hunger to see how much the pawn could eat.
            float currentHungerLevel = state.Pawn.needs.food.CurLevel;
            float maxHungerLevel = state.Pawn.needs.food.MaxLevel; // 1.0 for humans
            float roomInStomach = maxHungerLevel - currentHungerLevel;

            // Then this caps the max amount eaten to what the pawn can eat.
            float actualNutritionEaten = Mathf.Min(nutrition, roomInStomach);

            // Add the true amount eaten to the pawns current hunger and lifetime records.
            state.Pawn.needs.food.CurLevel += actualNutritionEaten;
            state.Pawn.records.AddTo(RecordDefOf.NutritionEaten, actualNutritionEaten);

        }
    }
}