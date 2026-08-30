using HarmonyLib;
using System;
using Verse;

namespace FoodTracker
{
    public class StackMergeState
    {
        public CompPartialNutrition TargetPTComp;
        public CompPartialNutrition SourcePTComp;

        public int TargetStackBefore;
        public int SourceStackBefore;
        public float TargetNutrition;
        public float SourceNutrition;
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.TryAbsorbStack))]
    public static class TryAbsorbStackPatch
    {
        public static void Prefix(Thing __instance, Thing other, out StackMergeState __state)
        {

            __state = null;

            if (__instance == null || other == null)
                return;

            // Check for FoodTracker component on either stack.
            CompPartialNutrition targetPTComp = __instance.TryGetComp<CompPartialNutrition>();
            CompPartialNutrition sourcePTComp = other.TryGetComp<CompPartialNutrition>();

            // If PT component doesn't exist for either stack then we don't need to worry about merging.
            if (targetPTComp == null || sourcePTComp == null)
                return;

            // Instantiate our Merge State
            __state = new StackMergeState
            {
                TargetPTComp = targetPTComp,
                SourcePTComp = sourcePTComp,
                TargetStackBefore = __instance.stackCount,
                SourceStackBefore = other.stackCount,
                TargetNutrition = targetPTComp.RemainingNutrition,
                SourceNutrition = sourcePTComp.RemainingNutrition
            };
        }

        public static void Postfix(Thing __instance, Thing other, StackMergeState __state)
        {

            // Access the CompFoodTracker component to acces the pre-instantiated lists.
            CompFoodTracker targetFTComp = __instance.TryGetComp<CompFoodTracker>();
            CompFoodTracker sourceFTComp = other.TryGetComp<CompFoodTracker>();

            if (__state == null || targetFTComp == null || sourceFTComp == null)
                return;

            int targetRecieved = __instance.stackCount - __state.TargetStackBefore;
            int sourceRemaining = other.Destroyed ? 0 : other.stackCount;

            int targetListCount = targetFTComp.NutritionEntries.Count;
            int sourceListCount = sourceFTComp.NutritionEntries.Count;

            // If only 1 item was transferred between stacks, we need to create a new list and populate with both their nutrition values.
            if (targetRecieved == 1 && sourceRemaining == 0)
            {

                // Add both stacks to the target list and clear anything from the source.
                targetFTComp.NutritionEntries.Add(__state.TargetNutrition);
                targetFTComp.NutritionEntries.Add(__state.SourceNutrition);

                return;
            }

            // If both components contain a list we add entries on the source list up to the targetRecieved.
            if (targetListCount >= 1 && sourceListCount >= 1)
            {

                // Remove as many entries from the source list, that target recieved.
                targetFTComp.NutritionEntries.AddRange(sourceFTComp.NutritionEntries.GetRange(0, targetRecieved));
                sourceFTComp.NutritionEntries.RemoveRange(0, targetRecieved);

                return;

            }

            // If our list is never aligned with the stack count, 
            if (__instance.stackCount != targetListCount || other.stackCount != sourceListCount)
            {

                CorrectNutritionList(__instance, targetFTComp, __state.TargetNutrition);

                if (!other.Destroyed)
                    CorrectNutritionList(other, sourceFTComp, __state.SourceNutrition);

                return;
            }
        }

        private static void CorrectNutritionList(Thing thing, CompFoodTracker foodTracker, float fullNutrition)
        {
            foodTracker.NutritionEntries.Clear();

            for (int i = 0; i < thing.stackCount; i++)
            {
                foodTracker.NutritionEntries.Add(fullNutrition);
            }
        }

    }
}
