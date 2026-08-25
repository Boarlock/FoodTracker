using RimWorld;
using UnityEngine;
using Verse;

namespace FoodTracker
{
    public class CompPartialNutrition : ThingComp
    {

        // If remainingNutrition is negative, it indicates that the value has not been initialized yet.
        private float remainingNutrition = -1f;

        // This method consumes a specified amount of nutrition from the food item, ensuring that it does not exceed the remaining nutrition. It returns the actual amount of nutrition consumed.
        public float ConsumeNutrition(float nutritionToConsume)
        {
            float nutritionRemoved = Mathf.Clamp(
                nutritionToConsume,
                0f,
                RemainingNutrition);

            remainingNutrition -= nutritionRemoved;

            return nutritionRemoved;
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
                // If remainingNutrition is negative, it means it hasn't been set yet, so we initialize it with the full nutrition value of the parent Thing.
                if (remainingNutrition < 0f)
                    remainingNutrition = parent.GetStatValue(StatDefOf.Nutrition);

                return remainingNutrition;
            }
        }

        // Expose the remaining nutrition value for saving and loading. This ensures that the state of the food item is preserved across game sessions.
        public override void PostExposeData()
        {
            Scribe_Values.Look(
                ref remainingNutrition,
                "remainingNutrition",
                -1f);
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