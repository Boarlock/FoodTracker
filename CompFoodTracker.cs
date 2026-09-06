using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FoodTracker
{
    public class CompFoodTracker : ThingComp
    {
        // Singleton state: The nutrition remaining in this individual meal. -1f means this Thing is currently in stack state.
        private float thisMealFraction = -1f;

        // Stack state: One nutrition value for each meal represented by the stack.
        private List<float> remainingFractions = new List<float>();

        public float PartialFraction
        {
            get
            {
                return thisMealFraction;
            }
            set
            {
                thisMealFraction = value;
            }
        }

        public List<float> RemainingFractions
        {
            get
            {
                return remainingFractions;
            }
            set
            {
                remainingFractions = value;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            if (respawningAfterLoad )
                return;

            if (FoodTrackerStackOperations.MergeInProgress || FoodTrackerStackOperations.SplitInProgress)
                return;

            if (parent.stackCount <= 0)
                return;

            float nutrition = FoodTrackingHelpers.GetFoodTrackerNutritionValue(parent.def);

            // SINGLETON STATE
            if (parent.stackCount == 1)
            {
                remainingFractions.Clear();

                if (thisMealFraction < 0f)
                {
                    thisMealFraction = nutrition;
                }

                return;
            }

            // STACK STATE
            thisMealFraction = -1f;

            // Existing per-item nutrition is already valid.
            if (remainingFractions.Count == parent.stackCount)
                return;

            // Otherwise initialize the stack.
            remainingFractions.Clear();

            for (int i = 0; i < parent.stackCount; i++)
            {
                remainingFractions.Add(nutrition);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Values.Look(ref thisMealFraction, "nutritionThisMeal", -1f);

            Scribe_Collections.Look(ref remainingFractions, "nutritionEntries", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                remainingFractions ??= new List<float>();
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