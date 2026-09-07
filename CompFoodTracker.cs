using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FoodTracker
{
    public class CompFoodTracker : ThingComp
    {
        // Singleton state: The nutrition remaining in this individual meal. -1f means this Thing is currently in stack state.
        private float thisMealFraction = -1f;

        // Stack state: One nutrition value for each meal represented by the stack.
        private List<float> remainingFractions = new List<float>();

        // Temporary migration fields
        private float oldNutritionThisMeal = -1f;
        private List<float> oldNutritionEntries = null;

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

            // SINGLETON STATE
            if (parent.stackCount == 1)
            {
                remainingFractions.Clear();

                if (thisMealFraction < 0f)
                {
                    thisMealFraction = 1f;
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
                remainingFractions.Add(1f);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Values.Look(ref thisMealFraction, "thisMealFraction", -1f);
            Scribe_Collections.Look(ref remainingFractions, "remainingFractions", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Values.Look(ref oldNutritionThisMeal, "nutritionThisMeal", -1f);
                Scribe_Collections.Look(ref oldNutritionEntries, "nutritionEntries", LookMode.Value);
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                remainingFractions ??= new List<float>();

                MigrateOldNutritionData();
            }
        }

        private void MigrateOldNutritionData()
        {
            // Nothing old was loaded.
            if (oldNutritionThisMeal < 0f && oldNutritionEntries == null || oldNutritionEntries.Count == 0)
                return;

            // Get original def to be safe and grab nutrition value from that def.
            ThingDef originalDef = FoodTrackingHelpers.GetOriginalDef(parent.def);
            float nutritionPerItem = originalDef.GetStatValueAbstract(StatDefOf.Nutrition);

            if (nutritionPerItem <= 0f)
                return;

            // Singleton state.
            if (oldNutritionThisMeal >= 0f && (oldNutritionEntries == null || oldNutritionEntries.Count == 0))
            {
                thisMealFraction = Mathf.Clamp01(oldNutritionThisMeal / nutritionPerItem);
            }
            // Stack state.
            else if (oldNutritionThisMeal < 0f && oldNutritionEntries != null && oldNutritionEntries.Count > 1)
            {
                remainingFractions.Clear();

                foreach (float oldNutrition in oldNutritionEntries)
                {
                    remainingFractions.Add(Mathf.Clamp01(oldNutrition / nutritionPerItem));
                }
            }

            // Defensive handling for an invalid legacy one-entry list.
            if (oldNutritionEntries != null && oldNutritionEntries.Count == 1)
            {
                thisMealFraction = Mathf.Clamp01(oldNutritionEntries[0] / nutritionPerItem);

                remainingFractions.Clear();
            }

            // Migration is complete.
            oldNutritionThisMeal = -1f;
            oldNutritionEntries?.Clear();
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