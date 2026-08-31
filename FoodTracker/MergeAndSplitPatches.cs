using HarmonyLib;
using RimWorld;
using System;
using Verse;

namespace FoodTracker
{

    public class SplitOffState
    {
        public CompFoodTracker SourceTracker;
        public int SourceStackBefore;
    }

    public class StackMergeState
    {
        public CompFoodTracker TargetTracker;
        public CompFoodTracker SourceTracker;

        public int TargetStackBefore;
        public int SourceStackBefore;
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.SplitOff))]
    public static class SplitOffPatch
    {
        public static void Prefix(Thing __instance, int count, out SplitOffState __state)
        {
            __state = null;

            if (__instance == null)
                return;

            // Get our list component from the Thing object.
            CompFoodTracker sourceTracker = __instance.TryGetComp<CompFoodTracker>();

            // If the stack doesn't have FoodTracker item(s) then we have nothing to do here.
            if (sourceTracker == null)
                return;

            // If count >= stackCount, vanilla returns this Thing itself, no new stack is created, so there is nothing for us to transfer.
            if (count >= __instance.stackCount)
                return;

            __state = new SplitOffState
            {
                SourceTracker = sourceTracker,
                SourceStackBefore = __instance.stackCount
            };
        }

        public static void Postfix(Thing __instance, int count, Thing __result, SplitOffState __state)
        {
            if (__state == null || __result == null)
                return;

            // We get the FT component for both the source stack and the resulting stack after the split.
            CompFoodTracker resultTracker = __result.TryGetComp<CompFoodTracker>();

            if (resultTracker == null)
                return;

            // Sanity check, Vanilla should have reduced the original stack by exactly the amount that was split off.
            if (__instance.stackCount != __state.SourceStackBefore - count)
                return;

            Log.Message("State of lists and counts before splitting off.");
            Log.Message($"Source Stack Before: _{__state.SourceStackBefore} | Source Stack After: {__instance.stackCount} | " +
                $"Items Moved: {count} | Target List Count: {resultTracker.NutritionEntries.Count} | Source List Count: {__state.SourceTracker.NutritionEntries.Count}");

            // If more items are split off than our list contains we need to correct.
            if (__state.SourceTracker.NutritionEntries.Count < count)
            {
                Log.Message("Source List Count is less than items moved.  Correcting nutrition lists.");

                FoodTrackingHelpers.CorrectNutritionList(__instance, __state.SourceTracker);

                // If after correction we still don't have enough entries then don't attempot GetRange().
                if (__state.SourceTracker.NutritionEntries.Count < count)
                    return;
            }
            int itemsRmoved = __state.SourceStackBefore - __instance.stackCount;
            // Add as many entries to __result list that changed in source list.
            for (int i = 0; i < itemsRmoved; i++)
            {
                resultTracker.NutritionEntries.Add(__state.SourceTracker.NutritionEntries[0]);
                __state.SourceTracker.NutritionEntries.RemoveAt(0);
            }


            Log.Message("Splitting off list values into new list.");

            string combinedTarget = string.Join(", ", resultTracker.NutritionEntries);
            string combinedSource = string.Join(", ", __state.SourceTracker.NutritionEntries);
            Log.Message($"Target List Count: {resultTracker.NutritionEntries.Count} | Source List Count: {__state.SourceTracker.NutritionEntries.Count} | " +
                $"Target List Items: {combinedTarget} | Source List Items {combinedSource}");
        }
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
            CompFoodTracker targetTracker = __instance.TryGetComp<CompFoodTracker>();
            CompFoodTracker sourceTracker = other.TryGetComp<CompFoodTracker>();

            // If FT component doesn't exist for either stack then we don't need to worry about merging.
            if (targetTracker == null || sourceTracker == null)
                return;

            __state = new StackMergeState
            {
                TargetTracker = targetTracker,
                SourceTracker = sourceTracker,
                TargetStackBefore = __instance.stackCount,
                SourceStackBefore = other.stackCount
            };
        }

        public static void Postfix(Thing __instance, Thing other, StackMergeState __state)
        {

            if (__state == null || __state?.TargetTracker == null || __state?.SourceTracker == null)
                return;

            // How many item(s) were transferred from source stack to target stack.
            int targetReceived = __instance.stackCount - __state.TargetStackBefore;

            // How many item(s) are remaining in the source stack.
            int sourceRemaining = other.Destroyed ? 0 : other.stackCount;

            // How many element(s) are left in each list.
            int targetListCount = __state.TargetTracker.NutritionEntries.Count;
            int sourceListCount = __state.SourceTracker.NutritionEntries.Count;

            // If the Target list is already equal to it's stack size OR if it's stack before was size
            // 1 and targets list is currently empty, mark as valid. Do the same for source stack.
            bool targetListValid = targetListCount == __state.TargetStackBefore || (__state.TargetStackBefore == 1 && targetListCount == 0);
            bool sourceListValid = sourceListCount == __state.SourceStackBefore || (__state.SourceStackBefore == 1 && sourceListCount == 0);

            Log.Message("State of lists and counts before list merging.");
            Log.Message($"Source Stack Before: _{__state.SourceStackBefore} | Source Stack After: {sourceRemaining} | " +
                $"Target Stack Before: {__state.TargetStackBefore} | Target Stack After: {__instance.stackCount} | " +
                $"Items Moved: {targetReceived} | Target List Count: {targetListCount} | Source List Count: {sourceListCount} |" +
                $"Target Is Valid List: {targetListValid} | Source Is Valid List: {sourceListValid}");

            string combinedTargetBefore = string.Join(", ", __state.TargetTracker.NutritionEntries);
            string combinedSourceBefore = string.Join(", ", __state.SourceTracker.NutritionEntries);

            Log.Message($"Target List Items: {combinedTargetBefore} | Source List Items {combinedSourceBefore}");

            // If our list is not aligned with the stack count we re-populate the list with full nutrition values.
            if (!targetListValid || !sourceListValid)
            {
                Log.Message("Either Target or Source are not valid. Correcting nutrition lists.");

                FoodTrackingHelpers.CorrectNutritionList(__instance, __state.TargetTracker);

                if (!other.Destroyed)
                    FoodTrackingHelpers.CorrectNutritionList(other, __state.SourceTracker);

                return;
            }

            // If either Target or Source stack has a singleton and un-initialized list, we simply add it to the list.
            if (__state.TargetStackBefore == 1 && targetListCount == 0)
            {
                Log.Message("Detecting target list un-initialized with single nutrition value in stack.");

                string combinedTarget = string.Join(", ", __state.TargetTracker.NutritionEntries);
                string combinedSource = string.Join(", ", __state.SourceTracker.NutritionEntries);

                Log.Message($"Target List Count: {targetListCount} | Source List Count: {sourceListCount} | " +
                    $"Target List Items: {combinedTarget} | Source List Items {combinedSource}");

                __state.TargetTracker.NutritionEntries.Insert(0, __state.TargetTracker.RemainingNutrition);
            }
            if (__state.SourceStackBefore == 1 && sourceListCount == 0)
            {
                Log.Message("Detecting source list un-initialized with single nutrition value in stack.");

                string combinedTarget = string.Join(", ", __state.TargetTracker.NutritionEntries);
                string combinedSource = string.Join(", ", __state.SourceTracker.NutritionEntries);

                Log.Message($"Target List Count: {targetListCount} | Source List Count: {sourceListCount} | " +
                    $"Target List Items: {combinedTarget} | Source List Items {combinedSource}");

                __state.SourceTracker.NutritionEntries.Insert(0, __state.SourceTracker.RemainingNutrition);
            }

            // Re-Update lists counts to reflect changes
            int targetListCountAfterInit = __state.TargetTracker.NutritionEntries.Count;
            int sourceListCountAfterInit = __state.SourceTracker.NutritionEntries.Count;

            // If both components contain a list we add entries on the source list up to the targetRecieved.
            if (targetListCountAfterInit >= 1 && sourceListCountAfterInit >= 1)
            {
                Log.Message("Detecting multiple items has transferred.");

                for (int i = sourceListCountAfterInit - 1; i >= 0; i--)
                {
                    __state.TargetTracker.NutritionEntries.Insert(0, __state.SourceTracker.NutritionEntries[i]);
                }

                string combinedTarget = string.Join(", ", __state.TargetTracker.NutritionEntries);
                string combinedSource = string.Join(", ", __state.SourceTracker.NutritionEntries);

                Log.Message($"Target List Count: {targetListCountAfterInit} | Source List Count: {sourceListCountAfterInit} | " +
                    $"Target List Items: {combinedTarget} | Source List Items {combinedSource}");

                return;

            }
        }
    }
}
