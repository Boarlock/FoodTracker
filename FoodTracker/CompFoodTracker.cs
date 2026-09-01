using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FoodTracker
{
    public class CompFoodTracker : ThingComp
    {
        // Singleton state: The nutrition remaining in this individual meal. -1f means this Thing is currently in stack state.
        private float nutritionThisMeal = -1f;

        // Stack state: One nutrition value for each meal represented by the stack.
        private List<float> nutritionEntries = new List<float>();


        public float PartialNutrition
        {
            get
            {
                return nutritionThisMeal;
            }
            set
            {
                nutritionThisMeal = value;
            }
        }


        public List<float> NutritionEntries
        {
            get
            {
                return nutritionEntries;
            }
            set
            {
                nutritionEntries = value;
            }
        }


        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            if (parent.stackCount <= 0)
                return;

            ThingDef originalDef = FoodTrackingHelpers.GetOriginalMealDef(parent.def);
            float nutrition = originalDef?.GetStatValueAbstract(StatDefOf.Nutrition) ?? 0f;

            // SINGLETON STATE
            if (parent.stackCount == 1)
            {
                nutritionEntries.Clear();

                // Only initialize the singleton nutrition if it has not already been initialized.
                if (nutritionThisMeal < 0f)
                {
                    nutritionThisMeal = nutrition;
                }

                return;
            }

            // STACK STATE
            nutritionThisMeal = -1f;

            // If we already have the correct number of entries, leave the existing nutrition values alone.
            if (nutritionEntries.Count == parent.stackCount)
                return;

            // Otherwise initialize the stack.
            nutritionEntries.Clear();

            for (int i = 0; i < parent.stackCount; i++)
            {
                nutritionEntries.Add(nutrition);
            }
        }


        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Values.Look(ref nutritionThisMeal, "nutritionThisMeal", -1f);

            Scribe_Collections.Look(ref nutritionEntries, "nutritionEntries", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                nutritionEntries ??= new List<float>();
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