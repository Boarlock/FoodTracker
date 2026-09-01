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
            CompFoodTracker sourceTracker = __state.SourceTracker;
            CompFoodTracker resultTracker = __result.TryGetComp<CompFoodTracker>();

            if (sourceTracker == null || resultTracker == null)
                return;

            // Vanilla should have reduced the original stack by exactly the amount that was split off.
            if (__instance.stackCount != __state.SourceStackBefore - count)
                return;

            // We should only be handling an actual split
            if (__result == __instance)
                return;

            // Setting target to singleton mode.
            if (__result.stackCount == 1)
            {

                resultTracker.PartialNutrition = sourceTracker.NutritionEntries[0];

                resultTracker.NutritionEntries.Clear();

                sourceTracker.NutritionEntries.RemoveAt(0);

            }
            else
            {

                // Add as many entries to __result list that changed in source list.
                for (int i = 0; i < count; i++)
                {
                    resultTracker.NutritionEntries.Add(sourceTracker.NutritionEntries[0]);

                    sourceTracker.NutritionEntries.RemoveAt(0);
                }

                resultTracker.PartialNutrition = -1f;

            }

            // Setting source to singleton mode.
            if (__instance.stackCount == 1)
            {

                sourceTracker.PartialNutrition = sourceTracker.NutritionEntries[0];

                sourceTracker.NutritionEntries.Clear();

            }

            int tListCount = resultTracker.NutritionEntries.Count;
            int sListCount = resultTracker.NutritionEntries.Count;

            string combinedTarget = string.Join(", ", resultTracker.NutritionEntries);
            string combinedSource = string.Join(", ", __state.SourceTracker.NutritionEntries);

            Log.Message(
                $"[FoodTracker][SPLIT] " +
                $"Target Stack={__result.stackCount} " +
                $"Target Partial={resultTracker.PartialNutrition} " +
                $"Target List Count={tListCount} " +
                $"Target List=[{combinedTarget}] | " +
                $"Source Stack={__instance.stackCount} " +
                $"Source Partial={sourceTracker.PartialNutrition} " +
                $"Source List Count={sourceTracker.NutritionEntries.Count} " +
                $"Source List=[{combinedSource}] | " +
                $"Items Moved={count} | " +
                $"Source Before={__state.SourceStackBefore}");

            // Validation
            if (__result.stackCount != tListCount)
            {
                if (__result.stackCount == 1 && resultTracker.PartialNutrition > 0)
                {
                    resultTracker.NutritionEntries.Clear();

                    return;
                }
                int diff = __result.stackCount - tListCount;
                if (diff > 0)
                {
                    Log.Warning($"SplitOff() Discrepency Reported. Target stack count is less than list count. " +
                        $"Stack Count: {__result.stackCount} | List Count: {tListCount}");

                    for (int i = 0; i < diff; i++)
                    {
                        __result.stackCount--;
                    }
                }
                else
                {
                    Log.Warning($"SplitOff() Discrepency Reported. Target stack count is more than list count. " +
                        $"Stack Count: {__result.stackCount} | List Count: {tListCount}");

                    for (int i = 0; i < -diff; i++)
                    {
                        resultTracker.NutritionEntries.RemoveAt(0);
                    }
                }

            }

            if (__instance.stackCount != sListCount)
            {
                if (__instance.stackCount == 0 || (__instance.stackCount == 1 && sourceTracker.PartialNutrition > 0))
                {
                    sourceTracker.NutritionEntries.Clear();

                    return;
                }
                int diff = __instance.stackCount - sListCount;
                if (diff > 0)
                {
                    Log.Warning($"SplitOff() Discrepency Reported. Source stack count is less than list count. " +
                        $"Stack Count: {__instance.stackCount} | List Count: {sListCount}");

                    for (int i = 0; i < diff; i++)
                    {
                        __instance.stackCount--;
                    }
                }
                else
                {
                    Log.Warning($"SplitOff() Discrepency Reported. Source stack count is more than list count. " +
                        $"Stack Count: {__instance.stackCount} | List Count: {sListCount}");

                    for (int i = 0; i < -diff; i++)
                    {
                        resultTracker.NutritionEntries.RemoveAt(0);
                    }
                }
            }
        }
    }

    public class StackMergeState
    {
        public CompFoodTracker TargetTracker;
        public CompFoodTracker SourceTracker;

        public int TargetStackBefore;
        public int SourceStackBefore;
    }

}






