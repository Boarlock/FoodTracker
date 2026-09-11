using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public List<float> SourceFractionHistory;
        public float SourcePartialFraction;
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.SplitOff))]
    public static class SplitOffPatch
    {
        public static void Prefix(Thing __instance, int count, out SplitOffState __state)
        {

            __state = null;

            if (__instance == null)
                return;

            if (FoodTrackerStackOperations.MergeInProgress || FoodTrackerStackOperations.SplitInProgress)
                return;

            // Get our list component from the Thing object.
            CompFoodTracker sourceTracker = __instance.TryGetComp<CompFoodTracker>();

            // If the stack doesn't have FoodTracker item(s) then we have nothing to do here.
            if (sourceTracker == null)
                return;

            // If count >= stackCount, vanilla returns this Thing itself, no new stack is created, so there is nothing for us to transfer.
            if (count >= __instance.stackCount)
                return;

            if (sourceTracker.RemainingFractions == null)
                return;

            __state = new SplitOffState

            {
                SourceTracker = sourceTracker,
                SourceStackBefore = __instance.stackCount,

                SourceFractionHistory = new List<float>(sourceTracker.RemainingFractions),
                SourcePartialFraction = sourceTracker.PartialFraction
            };

            FoodTrackerStackOperations.SplitInProgress = true;
        }

        public static void Postfix(Thing __instance, int count, Thing __result, SplitOffState __state)
        {

            if (__state == null || __result == null)
                return;

            List<float> resultFractionHistory = null;
            float resultPartialFraction = -1f;

            // Vanilla should have reduced the original stack by exactly the amount that was split off.
            if (__instance.stackCount != __state.SourceStackBefore - count)
                return;

            // We should only be handling an actual split
            if (__result == __instance)
                return;

            // We get the FT component for both the source stack and the resulting stack after the split.
            CompFoodTracker resultTracker = __result.TryGetComp<CompFoodTracker>();
            CompFoodTracker sourceTracker = __state.SourceTracker;

            if (sourceTracker == null || resultTracker == null)
                return;

            try
            {
                
                int diff = Math.Abs(__state.SourceStackBefore - __instance.stackCount);

                resultFractionHistory = new List<float>(resultTracker.RemainingFractions);
                resultPartialFraction = resultTracker.PartialFraction;

                // Setting result to singleton mode, if result is stack count of 1.
                if (__result.stackCount == 1)
                {
                    resultPartialFraction = __state.SourceFractionHistory[0];
                    __state.SourceFractionHistory.RemoveAt(0);

                    resultFractionHistory.Clear();
                }
                else
                {

                    while (diff > 0 && __state.SourceFractionHistory.Count > 0)
                    {
                        resultFractionHistory.Insert(0, __state.SourceFractionHistory[0]);
                        __state.SourceFractionHistory.RemoveAt(0);
                        diff--;
                    }

                    // Resulting stack is now in multi-item stack mode, so we need to reset its PartialFraction value.
                    resultPartialFraction = -1f;

                    //Resetting source stack's PartialFraction value, sanity check.
                    __state.SourcePartialFraction = -1f;

                }
                // Setting source to singleton mode.
                if (__instance.stackCount == 1)
                {
                    __state.SourcePartialFraction = __state.SourceFractionHistory[0];
                    __state.SourceFractionHistory.Clear();
                }
                // Resetting source, sanity check.
                if (__instance.stackCount == 0)
                {
                    __state.SourcePartialFraction = -1f;
                    __state.SourceFractionHistory.Clear();
                }


            }
            finally
            {

                resultTracker.RemainingFractions = resultFractionHistory;
                resultTracker.PartialFraction = resultPartialFraction;

                __state.SourceTracker.RemainingFractions = __state.SourceFractionHistory;
                __state.SourceTracker.PartialFraction = __state.SourcePartialFraction;

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

        public float TargetPartialFraction;
        public float SourcePartialFraction;

        public List<float> TargetFractionHistory;
        public List<float> SourceFractionHistory;
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.TryAbsorbStack))]

    public class StackMergePatch
    {
        public static void Prefix(Thing __instance, Thing other, out StackMergeState __state)
        {
            __state = null;

            if (__instance == null || other == null)
                return;

            if (FoodTrackerStackOperations.MergeInProgress || FoodTrackerStackOperations.SplitInProgress)
                return;

            CompFoodTracker targetTracker = __instance.TryGetComp<CompFoodTracker>();
            CompFoodTracker sourceTracker = other.TryGetComp<CompFoodTracker>();

            if (targetTracker == null || sourceTracker == null)
                return;

            if (targetTracker.RemainingFractions == null || sourceTracker.RemainingFractions == null)
                return;

            __state = new StackMergeState
            {
                TargetTracker = targetTracker,
                SourceTracker = sourceTracker,

                TargetStackBefore = __instance.stackCount,
                SourceStackBefore = other.stackCount,

                TargetFractionHistory = new List<float>(targetTracker.RemainingFractions),
                SourceFractionHistory = new List<float>(sourceTracker.RemainingFractions),

                TargetPartialFraction = targetTracker.PartialFraction,
                SourcePartialFraction = sourceTracker.PartialFraction
            };

            FoodTrackerStackOperations.MergeInProgress = true;

        }
        public static void Postfix(Thing __instance, Thing other, StackMergeState __state)
        {

            if (__state == null)
                return;

            if (__state.TargetTracker == null || __state.SourceTracker == null)
                return;

            int targetStackAfter = __instance.stackCount;
            int sourceStackAfter = other.stackCount;
            int diff = Math.Abs(targetStackAfter - __state.TargetStackBefore);

            // In case the vanilla method fired but no merger happened, we don't want to do anything.
            if (__state.TargetStackBefore == targetStackAfter && __state.SourceStackBefore == sourceStackAfter)
                return;

            try
            {

                // Bootstrap any uninitialized singleton FT meals. This mirrors CompFoodTracker.PostSpawnSetup initialization.
                CompFoodTrackerUtility.NormalizeState(__instance);
                CompFoodTrackerUtility.NormalizeState(other);

                // Stack merge case: If both stacks had more than one item, we need to append the FractionEntries lists both ways..
                if (__state.TargetStackBefore > 1 && __state.SourceStackBefore > 1)
                {
                    if (diff > 0)
                    {

                        while (diff > 0 && __state.SourceFractionHistory.Count > 0)
                        {
                            __state.TargetFractionHistory.Insert(0, __state.SourceFractionHistory[0]);
                            __state.SourceFractionHistory.RemoveAt(0);
                            diff--;

                        }
                        // If the source stack is now a singleton.
                        if (__state.SourceFractionHistory.Count == 1)
                        {
                            // Target list should already have its PartialFraction value set, but we reset it just in case.
                            __state.TargetPartialFraction = -1f;

                            // Source stack has become a singleton, so we need to set its PartialFraction value and clear its FractionEntries list.
                            __state.SourcePartialFraction = __state.SourceFractionHistory[0];
                            __state.SourceFractionHistory.Clear();

                        }
                        // Source stack has become empty, sanity check.
                        else if (__state.SourceFractionHistory.Count == 0)
                        {
                            // Target list should already have its PartialFraction value set, but we reset it just in case.
                            __state.TargetPartialFraction = -1f;

                            // Source stack has become empty, so we need to reset its PartialFraction value and clear its FractionEntries list.
                            __state.SourcePartialFraction = -1f;
                            __state.SourceFractionHistory.Clear();

                        }
                        // Target stack and Source stack are both still multi-item stacks, reset both PartialFraction values, sanity check.
                        else
                        {
                            __state.TargetPartialFraction = -1f;
                            __state.SourcePartialFraction = -1f;
                        }
                    }
                    else
                    {
                        while (diff > 0 && __state.TargetFractionHistory.Count > 0)
                        {
                            __state.SourceFractionHistory.Insert(0, __state.TargetFractionHistory[0]);
                            __state.TargetFractionHistory.RemoveAt(0);
                            diff--;

                        }
                        // If the target stack is now a singleton.
                        if (__state.TargetFractionHistory.Count == 1)
                        {
                            // Source list should already have its PartialFraction value set, but we reset it just in case.
                            __state.SourcePartialFraction = -1f;

                            // Target stack has become a singleton, so we need to set its PartialFraction value and clear its FractionEntries list.
                            __state.TargetPartialFraction = __state.TargetFractionHistory[0];
                            __state.TargetFractionHistory.Clear();

                        }
                        // Target stack has become empty, sanity check.
                        else if (__state.TargetFractionHistory.Count == 0)
                        {
                            // Source list should already have its PartialFraction value set, but we reset it just in case.
                            __state.SourcePartialFraction = -1f;

                            // Target stack has become empty, so we need to reset its PartialFraction value and clear its FractionEntries list.
                            __state.TargetPartialFraction = -1f;
                            __state.TargetFractionHistory.Clear();

                        }
                        // Target stack and Source stack are both still multi-item stacks, reset both PartialFraction values, sanity check.
                        else
                        {
                            __state.TargetPartialFraction = -1f;
                            __state.SourcePartialFraction = -1f;
                        }
                    }

                    return;

                }

                // Append singleton merge case: If one stack was a singleton and the other stack had more than one item,
                // we need to add the PartialFraction value of the singleton to the FractionEntries list of the resulting stack.
                if ((__state.TargetStackBefore > 1 && __state.SourceStackBefore == 1) || (__state.SourceStackBefore > 1 && __state.TargetStackBefore == 1))
                {
                    // Target singleton became the 10-stack while Source became singleton.
                    if (__state.TargetStackBefore == 1 && targetStackAfter > 1 && sourceStackAfter == 1)
                    {
                        // Add Target's singleton nutrition into its list.
                        __state.TargetFractionHistory.Insert(0, __state.TargetPartialFraction);

                        __state.TargetPartialFraction = -1f;

                        // Transfer the additional items from Source.
                        while (diff > 0 && __state.SourceFractionHistory.Count > 0)
                        {
                            __state.TargetFractionHistory.Insert(0, __state.SourceFractionHistory[0]);
                            __state.SourceFractionHistory.RemoveAt(0);
                            diff--;

                        }

                        // Source is now the singleton.
                        __state.SourcePartialFraction = __state.SourceFractionHistory[0];
                        __state.SourceFractionHistory.Clear();
                    }
                    // Source singleton became the 10-stack while Target became singleton.
                    else if (__state.SourceStackBefore == 1 && sourceStackAfter > 1 && targetStackAfter == 1)
                    {
                        // Add Source's singleton nutrition into its list.
                        __state.SourceFractionHistory.Insert(0, __state.SourcePartialFraction);
                        __state.SourcePartialFraction = -1f;

                        // Transfer the additional items from Target.
                        while (diff > 0 && __state.TargetFractionHistory.Count > 0)
                        {
                            __state.SourceFractionHistory.Insert(0, __state.TargetFractionHistory[0]);
                            __state.TargetFractionHistory.RemoveAt(0);
                            diff--;
                        }

                        // Target is now the singleton.
                        __state.TargetPartialFraction = __state.TargetFractionHistory[0];
                        __state.TargetFractionHistory.Clear();
                    }
                    // Source large stack was absorbed into target singleton.
                    else if (__state.TargetStackBefore == 1 && __state.SourceStackBefore > 1 && targetStackAfter > 1 && sourceStackAfter == 0)
                    {
                        // Target needs to add its own singleton entry.
                        __state.TargetFractionHistory.Insert(0, __state.TargetPartialFraction);
                        __state.TargetPartialFraction = -1f;

                        while (diff > 0 && __state.SourceFractionHistory.Count > 0)
                        {
                            __state.TargetFractionHistory.Insert(0, __state.SourceFractionHistory[0]);
                            __state.SourceFractionHistory.RemoveAt(0);
                            diff--;

                        }

                        // Source entry and source list needs to be reset.
                        __state.SourcePartialFraction = -1f;
                        __state.SourceFractionHistory.Clear();

                    }
                    // Target large stack was absorbed into source singleton.
                    else if (__state.SourceStackBefore == 1 && __state.TargetStackBefore > 1 && sourceStackAfter > 1 && targetStackAfter == 0)
                    {
                        // Source needs to add its own singleton entry.
                        __state.SourceFractionHistory.Insert(0, __state.SourcePartialFraction);
                        __state.SourcePartialFraction = -1f;

                        while (diff > 0 && __state.TargetFractionHistory.Count > 0)
                        {
                            __state.SourceFractionHistory.Insert(0, __state.TargetFractionHistory[0]);
                            __state.TargetFractionHistory.RemoveAt(0);
                            diff--;

                        }

                        // Target entry and target list needs to be reset.
                        __state.TargetPartialFraction = -1f;
                        __state.TargetFractionHistory.Clear();

                    }
                    // Target singleton was absorbed by larger source.
                    else if (__state.TargetStackBefore == 1 && __state.SourceStackBefore > 1 && targetStackAfter == 0 && sourceStackAfter > 1)
                    {
                        // Soruce list absorbs targets singleton entry.
                        __state.SourceFractionHistory.Insert(0, __state.TargetPartialFraction);

                        // Source list should already have its PartialFraction value set, but we reset it just in case.
                        __state.SourcePartialFraction = -1f;

                        // Target entry and target list needs to be reset.
                        __state.TargetPartialFraction = -1f;
                        __state.TargetFractionHistory.Clear();

                    }
                    // Source singleton was absorbed by larger target.
                    else if (__state.SourceStackBefore == 1 && __state.TargetStackBefore > 1 && sourceStackAfter == 0 && targetStackAfter > 1)
                    {
                        // Target list absorbs sources singleton entry.
                        __state.TargetFractionHistory.Insert(0, __state.SourcePartialFraction);

                        // Target list should already have its PartialFraction value set, but we reset it just in case.
                        __state.TargetPartialFraction = -1f;

                        // Source entry and source list needs to be reset.
                        __state.SourcePartialFraction = -1f;
                        __state.SourceFractionHistory.Clear();

                    }

                    return;

                }

                // Singleton merge case: If either stack was a singleton, we need to add.
                // the PartialFraction values to the FractionEntries list of the resulting stack.
                if ((targetStackAfter == 2 && sourceStackAfter == 0) || (sourceStackAfter == 2 && targetStackAfter == 0))
                {
                    // If target became the double stack.
                    if (targetStackAfter == 2)
                    {
                        // Target list absorbs it's own entry and sources entry.
                        __state.TargetFractionHistory.Insert(0, __state.TargetPartialFraction);
                        __state.TargetFractionHistory.Insert(0, __state.SourcePartialFraction);

                        // Target entry needs to be reset.
                        __state.TargetPartialFraction = -1f;

                        // Source entry and source list needs to be reset.
                        __state.SourcePartialFraction = -1f;
                        __state.SourceFractionHistory.Clear();

                    }
                    // If source became the double stack.
                    else
                    {
                        // Source list absorbs it's own entry and targets entry.
                        __state.SourceFractionHistory.Insert(0, __state.SourcePartialFraction);
                        __state.SourceFractionHistory.Insert(0, __state.TargetPartialFraction);

                        // Source entry needs to be reset.
                        __state.SourcePartialFraction = -1f;

                        // Target entry and target list needs to be reset.
                        __state.TargetPartialFraction = -1f;
                        __state.TargetFractionHistory.Clear();

                    }

                    return;

                }
            }
            finally
            {

                if (__state != null)
                {
                    __state.TargetTracker.RemainingFractions.Clear();
                    __state.TargetTracker.RemainingFractions.AddRange(__state.TargetFractionHistory);

                    __state.SourceTracker.RemainingFractions.Clear();
                    __state.SourceTracker.RemainingFractions.AddRange(__state.SourceFractionHistory);

                    __state.TargetTracker.PartialFraction = __state.TargetPartialFraction;
                    __state.SourceTracker.PartialFraction = __state.SourcePartialFraction;
                }

                ValidateTrackerState(__instance, __state.TargetTracker);
                ValidateTrackerState(other, __state.SourceTracker);

                FoodTrackerStackOperations.MergeInProgress = false; 
            }
        }

        private static void ValidateTrackerState(Thing thing, CompFoodTracker tracker)
        {
            if (thing == null || tracker == null)
                return;

            int stack = thing.stackCount;
            int listCount = tracker.RemainingFractions.Count;

            if (stack == 0)
            {
                if (listCount != 0 || tracker.PartialFraction >= 0f)
                {
                    Log.Warning($"[FoodTracker][VALIDATION] INVALID EMPTY STATE | Thing={thing.def.defName} " +
                        $"ID={thing.thingIDNumber} Stack={stack} Partial={tracker.PartialFraction} ListCount={listCount}"
                    );
                }

                return;
            }

            if (stack == 1)
            {
                if (listCount != 0 || tracker.PartialFraction < 0f)
                {
                    Log.Warning($"[FoodTracker][VALIDATION] INVALID SINGLETON STATE | Thing={thing.def.defName} " +
                        $"ID={thing.thingIDNumber} Stack={stack} Partial={tracker.PartialFraction} ListCount={listCount}"
                    );
                }

                return;
            }

            if (listCount != stack)
            {
                Log.Warning($"[FoodTracker][VALIDATION] INVALID STACK STATE | Thing={thing.def.defName} ID={thing.thingIDNumber} Stack={stack} " +
                    $"Partial={tracker.PartialFraction} ListCount={listCount} List=[{string.Join(", ", tracker.RemainingFractions)}]"
                );
            }

            if (tracker.PartialFraction >= 0f)
            {
                Log.Warning($"[FoodTracker][VALIDATION] MULTI-STACK HAS ACTIVE SINGLETON | Thing={thing.def.defName} " +
                    $"ID={thing.thingIDNumber} Stack={stack} Partial={tracker.PartialFraction} ListCount={listCount}"
                );
            }
        }
    }
}






