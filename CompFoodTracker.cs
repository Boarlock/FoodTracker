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

        // Temporary migration fields.
        private float oldNutritionThisMeal = -1f;
        private List<float> oldNutritionEntries = null;
        private float oldRemainingNutrition = -1f;

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

            CompFoodTrackerUtility.NormalizeState(parent);

        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Values.Look(ref thisMealFraction, "thisMealFraction", -1f);
            Scribe_Collections.Look(ref remainingFractions, "remainingFractions", LookMode.Value);

            if (remainingFractions == null)
                remainingFractions = new List<float>();

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                // Support for when FT stored nutrition singleton values and lists nutrition values.
                Scribe_Values.Look(ref oldNutritionThisMeal, "nutritionThisMeal", -1f);
                Scribe_Collections.Look(ref oldNutritionEntries, "nutritionEntries", LookMode.Value);

                // Support for when FT stored only nutrition singleton values.
                Scribe_Values.Look(ref oldRemainingNutrition, "remainingNutrition", -1f);

                MigrateOldNutritionData();
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                remainingFractions ??= new List<float>();
            }
        }

        private void MigrateOldNutritionData()
        {
            // Nothing old was loaded.
            if (oldNutritionThisMeal < 0f && (oldNutritionEntries == null || oldNutritionEntries?.Count == 0) && oldRemainingNutrition < 0f)
                return;

            if (parent == null || parent.def == null)
            {
                Log.Warning("[FoodTracker] Cannot migrate legacy nutrition data because parent ThingDef is not available yet.");
                return;
            }

            // Get original def to be safe.
            ThingDef originalDef = parent.def;

            if (originalDef.defName.StartsWith(DynamicMealDefFactory.Prefix))
            {
                string originalDefName = originalDef.defName.Substring(DynamicMealDefFactory.Prefix.Length);

                originalDef = DefDatabase<ThingDef>.GetNamedSilentFail(originalDefName);
            }
            if (originalDef == null)
            {
                Log.Warning($"[FoodTracker] Could not find original ThingDef while migrating legacy data for {parent.def.defName}.");
                return;
            }

            // Grab nutrition value from that def.
            float nutritionPerItem = originalDef.GetStatValueAbstract(StatDefOf.Nutrition);
            if (nutritionPerItem <= 0f)
            {
                Log.Warning($"[FoodTracker] Invalid nutrition value while migrating {parent.def.defName}: {nutritionPerItem}");
                return;
            }

            // Stack state, accounts for a singleton to be present.
            if (oldNutritionEntries != null && oldNutritionEntries.Count >= 2)
            {
                // Clear list to be fresh, set the list and clear singleton to be safe.
                remainingFractions.Clear();

                foreach (float oldNutrition in oldNutritionEntries)
                {
                    remainingFractions.Add(Mathf.Clamp01(oldNutrition / nutritionPerItem));
                }

                thisMealFraction = -1f;
            }
            // Singleton state, accounts for a single list item to be presented.
            else
            {

                float oldNutrition = oldNutritionThisMeal;

                if (oldNutritionEntries != null && oldNutritionEntries.Count == 1)
                    oldNutrition = oldNutritionEntries[0];

                if (oldRemainingNutrition >= 0f)
                    oldNutrition = oldRemainingNutrition;

                if (oldNutrition >= 0f)
                    thisMealFraction = Mathf.Clamp01(oldNutrition / nutritionPerItem);

                remainingFractions.Clear();
            }

            // Migration is complete.
            oldNutritionThisMeal = -1f;
            oldNutritionEntries = null;
        }
    }

    public static class CompFoodTrackerUtility
    {
        /// Enforces mutual exclusivity between PartialFraction and RemainingFractions, repairs invalid states.
        public static void NormalizeState(Thing thing)
        {
            CompFoodTracker tracker = thing.TryGetComp<CompFoodTracker>();

            if (tracker == null)
                return;

            // SINGLETON STATE
            if (thing.stackCount == 1)
            {
                tracker.RemainingFractions.Clear();
                if (tracker.PartialFraction <= 0f)
                {
                    tracker.PartialFraction = 1f;
                }
                return;
            }

            // STACK STATE
            tracker.PartialFraction = -1f;

            // Existing per-item nutrition array length matches physical stack
            if (tracker.RemainingFractions.Count == thing.stackCount)
                return;

            // Otherwise repair/initialize the stack
            tracker.RemainingFractions.Clear();
            for (int i = 0; i < thing.stackCount; i++)
            {
                tracker.RemainingFractions.Add(1f);
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