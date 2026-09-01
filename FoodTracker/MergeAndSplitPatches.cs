using HarmonyLib;
using RimWorld;
using System;
using Verse;

namespace FoodTracker
{
    public static class FoodTrackerStackOperations
    {
        public static bool MergeInProgress;
        public static bool SplitInProgress;
    }

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

            FoodTrackerStackOperations.SplitInProgress = true;

            try
            {

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
            finally
            {
                FoodTrackerStackOperations.SplitInProgress = false;
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

    [HarmonyPatch(typeof(Thing), nameof(Thing.TryAbsorbStack))]

    public class StackMergePatch
    {
        public static void Prefix(Thing __instance, Thing other, bool respectStackLimit, out StackMergeState __state)
        {
            __state = null;

            if (__instance == null || other == null)
                return;

            if (FoodTrackerStackOperations.MergeInProgress)
                return;

            CompFoodTracker targetTracker = __instance.TryGetComp<CompFoodTracker>();
            CompFoodTracker sourceTracker = other.TryGetComp<CompFoodTracker>();

            if (targetTracker == null || sourceTracker == null)
                return;

            __state = new StackMergeState
            {
                TargetTracker = targetTracker,
                SourceTracker = sourceTracker,

                TargetStackBefore = __instance.stackCount,
                SourceStackBefore = other.stackCount,
            };

            FoodTrackerStackOperations.MergeInProgress = true;

        }
        public static void Postfix(Thing __instance, Thing other, bool respectStackLimit, StackMergeState __state)
        {
            if (__state == null)
                return;

            if (__state.TargetTracker == null || __state.SourceTracker == null)
                return;

            try
            {
                int targetStackAfter = __instance.stackCount;
                int sourceStackAfter = other.stackCount;
                int diff = Math.Abs(targetStackAfter - __state.TargetStackBefore);

                // In case the vanilla method fired but no merger happened, we don't want to do anything.
                if (__state.TargetStackBefore == targetStackAfter && __state.SourceStackBefore == sourceStackAfter)
                    return;

                // Bootstrap any uninitialized singleton FT meals. This mirrors CompFoodTracker.PostSpawnSetup initialization.
                if (__state.TargetTracker.NutritionEntries.Count == 0 && __state.TargetTracker.PartialNutrition < 0f)
                {
                    ThingDef originalDef = FoodTrackingHelpers.GetOriginalMealDef(__instance.def);
                    __state.TargetTracker.PartialNutrition = originalDef?.GetStatValueAbstract(StatDefOf.Nutrition) ?? 0f;
                }
                else if
                    (__state.SourceTracker.NutritionEntries.Count == 0 && __state.SourceTracker.PartialNutrition < 0f)
                {
                    ThingDef originalDef = FoodTrackingHelpers.GetOriginalMealDef(other.def);
                    __state.SourceTracker.PartialNutrition = originalDef?.GetStatValueAbstract(StatDefOf.Nutrition) ?? 0f;
                }

                // Singleton merge case: If either stack was a singleton, we need to add
                // the PartialNutrition values to the NutritionEntries list of the resulting stack.
                if ((targetStackAfter == 2 && sourceStackAfter == 0) || (sourceStackAfter == 2 && targetStackAfter == 0))
                {

                    if (targetStackAfter == 2)
                    {
                        __state.TargetTracker.NutritionEntries.Insert(0, __state.TargetTracker.PartialNutrition);
                        __state.TargetTracker.NutritionEntries.Insert(0, __state.SourceTracker.PartialNutrition);

                        Log.Message($"[FoodTracker][MERGE] Singleton merge detected, Target has absorbed Source. Target stack before: {__state.TargetStackBefore}, " +
                            $"Source stack before: {__state.SourceStackBefore}, Target stack after: {targetStackAfter}, Source stack after: {sourceStackAfter}, " +
                            $"Target List Count: {__state.TargetTracker.NutritionEntries.Count}, Source List Count: {__state.SourceTracker.NutritionEntries.Count}, " +
                            $"Target Singleton State: {__state.TargetTracker.PartialNutrition}, Source Singleton State: {__state.SourceTracker.PartialNutrition}");
                    }
                    else
                    {
                        __state.SourceTracker.NutritionEntries.Insert(0, __state.SourceTracker.PartialNutrition);
                        __state.SourceTracker.NutritionEntries.Insert(0, __state.TargetTracker.PartialNutrition);

                        Log.Message($"[FoodTracker][MERGE] Singleton merge detected, Source has absorbed Target. Target stack before: {__state.TargetStackBefore}, " +
                            $"Source stack before: {__state.SourceStackBefore}, Target stack after: {targetStackAfter}, Source stack after: {sourceStackAfter}, " +
                            $"Target List Count: {__state.TargetTracker.NutritionEntries.Count}, Source List Count: {__state.SourceTracker.NutritionEntries.Count}, " +
                            $"Target Singleton State: {__state.TargetTracker.PartialNutrition}, Source Singleton State: {__state.SourceTracker.PartialNutrition}");
                    }
                    // Vanilla should've destroyed the other stack so we do not need to clear its list or reset its PartialNutrition.

                    return;

                }

                // Append singleton merge case: If one stack was a singleton and the other stack had more than one item,
                // we need to add the PartialNutrition value of the singleton to the NutritionEntries list of the resulting stack.
                if ((__state.TargetStackBefore > 1 && __state.SourceStackBefore == 1) || (__state.SourceStackBefore > 1 && __state.TargetStackBefore == 1))
                {

                    if (__state.TargetStackBefore == 1 && targetStackAfter == 0)
                    {
                        __state.SourceTracker.NutritionEntries.Insert(0, __state.TargetTracker.PartialNutrition);

                        Log.Message($"[FoodTracker][MERGE] Append singleton merge detected, Source has absorbed Target. Target stack before: {__state.TargetStackBefore}, " +
                            $"Source stack before: {__state.SourceStackBefore}, Target stack after: {targetStackAfter}, Source stack after: {sourceStackAfter}, " +
                            $"Target List Count: {__state.TargetTracker.NutritionEntries.Count}, Source List Count: {__state.SourceTracker.NutritionEntries.Count}, " +
                            $"Target Singleton State: {__state.TargetTracker.PartialNutrition}, Source Singleton State: {__state.SourceTracker.PartialNutrition}");
                    }
                    else
                    {
                        __state.TargetTracker.NutritionEntries.Insert(0, __state.SourceTracker.PartialNutrition);

                        Log.Message($"[FoodTracker][MERGE] Append singleton merge detected, Target has absorbed Source. Target stack before: {__state.TargetStackBefore}, " +
                            $"Source stack before: {__state.SourceStackBefore}, Target stack after: {targetStackAfter}, Source stack after: {sourceStackAfter}, " +
                            $"Target List Count: {__state.TargetTracker.NutritionEntries.Count}, Source List Count: {__state.SourceTracker.NutritionEntries.Count}, " +
                            $"Target Singleton State: {__state.TargetTracker.PartialNutrition}, Source Singleton State: {__state.SourceTracker.PartialNutrition}");
                    }
                    // Vanilla should've destroyed the other stack so we do not need to clear its list or reset its PartialNutrition.

                    return;

                }

                // Stack merge case: If both stacks had more than one item, we need to append the NutritionEntries lists both ways..
                if (__state.TargetStackBefore > 1 && __state.SourceStackBefore > 1)
                {
                    if (diff > 0)
                    {

                        while (diff > 0 && __state.SourceTracker.NutritionEntries.Count > 0)
                        {
                            __state.TargetTracker.NutritionEntries.Insert(0, __state.SourceTracker.NutritionEntries[0]);
                            __state.SourceTracker.NutritionEntries.RemoveAt(0);
                            diff--;

                            Log.Message($"[FoodTracker][MERGE] Stack merge detected, Target has absorbed Source. Target stack before: {__state.TargetStackBefore}, " +
                                $"Source stack before: {__state.SourceStackBefore}, Target stack after: {targetStackAfter}, Source stack after: {sourceStackAfter}, " +
                                $"Target List Count: {__state.TargetTracker.NutritionEntries.Count}, Source List Count: {__state.SourceTracker.NutritionEntries.Count}, " +
                                $"Target Singleton State: {__state.TargetTracker.PartialNutrition}, Source Singleton State: {__state.SourceTracker.PartialNutrition}");
                        }
                        // We need to check for singleton conversion after the merge. If the target stack is now a singleton,
                        // we need to set its PartialNutrition value and clear its NutritionEntries list.
                        if (__state.SourceTracker.NutritionEntries.Count == 1)
                        {
                            __state.SourceTracker.PartialNutrition = __state.SourceTracker.NutritionEntries[0];
                            __state.SourceTracker.NutritionEntries.Clear();

                            Log.Message($"[FoodTracker][MERGE] Singleton conversion detected, Source has become a singleton. Source stack before: " +
                                $"{__state.SourceStackBefore}, Source stack after: {sourceStackAfter}, Source List Count: {__state.SourceTracker.NutritionEntries.Count}, " +
                                $"Source Singleton State: {__state.SourceTracker.PartialNutrition}");
                        }
                    }
                    else
                    {
                        while (diff > 0 && __state.TargetTracker.NutritionEntries.Count > 0)
                        {
                            __state.SourceTracker.NutritionEntries.Insert(0, __state.TargetTracker.NutritionEntries[0]);
                            __state.TargetTracker.NutritionEntries.RemoveAt(0);
                            diff--;

                            Log.Message($"[FoodTracker][MERGE] Stack merge detected, Source has absorbed Target. Target stack before: {__state.TargetStackBefore}, " +
                                $"Source stack before: {__state.SourceStackBefore}, Target stack after: {targetStackAfter}, Source stack after: {sourceStackAfter}, " +
                                $"Target List Count: {__state.TargetTracker.NutritionEntries.Count}, Source List Count: {__state.SourceTracker.NutritionEntries.Count}, " +
                                $"Target Singleton State: {__state.TargetTracker.PartialNutrition}, Source Singleton State: {__state.SourceTracker.PartialNutrition}");
                        }
                        // Same for Target stack as well.
                        if (__state.TargetTracker.NutritionEntries.Count == 1)
                        {
                            __state.TargetTracker.PartialNutrition = __state.TargetTracker.NutritionEntries[0];
                            __state.TargetTracker.NutritionEntries.Clear();

                            Log.Message($"[FoodTracker][MERGE] Singleton conversion detected, Target has become a singleton. Target stack before: " +
                                $"{__state.TargetStackBefore}, Target stack after: {targetStackAfter}, Target List Count: {__state.TargetTracker.NutritionEntries.Count}, " +
                                $"Target Singleton State: {__state.TargetTracker.PartialNutrition}");
                        }
                    }

                    return;

                }
            }
            finally
            {
                FoodTrackerStackOperations.MergeInProgress = false; 
            }
        }
    }
}






