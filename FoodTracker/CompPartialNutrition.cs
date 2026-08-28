using RimWorld;
using UnityEngine;
using Verse;

namespace FoodTracker
{
    public class CompPartialNutrition : ThingComp
    {

        // If remainingNutrition is negative, it indicates that the value has not been initialized yet.
        private float remainingNutrition = -1f;

        // Due to the patch on StatWorker.GetValue(), you can no longer use .GetStatValue(StatDefOf.Nutrition) to get base nutrition
        private float GetBaseNutrition()
        {
            StatModifier nutritionStat = parent.def.statBases?.FirstOrDefault(x => x.stat == StatDefOf.Nutrition);

            return nutritionStat?.value ?? 0f;
        }

        // Set the remaining nutrition of the food item, ensuring it doesn't go below zero.
        public void SetRemainingNutrition(float nutrition)
        {
            remainingNutrition = Mathf.Max(0f, nutrition);
        }

        // Global property to access the remaining nutrition of the food item. If it hasn't been set yet, it initializes it with the full nutrition value of the parent Thing.
        public float RemainingNutrition
        {
            get
            {
                if (remainingNutrition < 0f)
                    remainingNutrition = GetBaseNutrition();

                return remainingNutrition;
            }
        }

        // Expose the remaining nutrition value for saving and loading. This ensures that the state of the food item is preserved across game sessions.
        public override void PostExposeData()
        {
            Scribe_Values.Look(ref remainingNutrition, "remainingNutrition", -1f);
        }
    }

    public class CompProperties_PartialNutrition : CompProperties
    {
        public CompProperties_PartialNutrition()
        {
            compClass = typeof(CompPartialNutrition);
        }
    }
}