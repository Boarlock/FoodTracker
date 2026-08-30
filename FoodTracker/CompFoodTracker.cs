using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using System.Linq;

namespace FoodTracker
{
    public class CompFoodTracker : ThingComp
    {

        public List<float> NutritionEntries = new List<float>();

        public float RemainingNutrition
        {
            get
            {
                if (NutritionEntries.Count == 0)
                {
                    ThingDef originalDef = FoodTrackingHelpers.GetOriginalMealDef(parent.def);

                    float nutrition = originalDef?.GetStatValueAbstract(StatDefOf.Nutrition) ?? 0f;

                    NutritionEntries.Add(nutrition);
                }

                return NutritionEntries[0];
            }
        }

        public void SetRemainingNutrition(float nutrition)
        {
            if (NutritionEntries.Count == 0)
            {
                NutritionEntries.Add(Mathf.Max(0f, nutrition));
                return;
            }

            NutritionEntries[0] = Mathf.Max(0f, nutrition);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Collections.Look(ref NutritionEntries, "nutritionEntries", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && NutritionEntries == null)
            {
                NutritionEntries = new List<float>();
            }
        }
    }

    public class CompProperties_FoodTracker : CompProperties
    {
        public CompProperties_FoodTracker()
        {
            compClass = typeof(CompFoodTracker);
        }
    }
}