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
        public List<float> SourceNutritionHistory;
        public float SourcePartialNutrition;
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

            if (sourceTracker.NutritionEntries == null)
                return;

            __state = new SplitOffState

            {
                SourceTracker = sourceTracker,
                SourceStackBefore = __instance.stackCount,

                SourceNutritionHistory = new List<float>(sourceTracker.NutritionEntries),
                SourcePartialNutrition = sourceTracker.PartialNutrition
            };

            FoodTrackerStackOperations.SplitInProgress = true;
        }

        public static void Postfix(Thing __instance, int count, Thing __result, SplitOffState __state)
        {

            if (__state == null || __result == null)
                return;

            List<float> resultNutritionHistory = null;
            float resultPartialNutrition = -1f;

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

                resultNutritionHistory = new List<float>(resultTracker.NutritionEntries);
                resultPartialNutrition = resultTracker.PartialNutrition;

                // Setting result to singleton mode, if result is stack count of 1.
                if (__result.stackCount == 1)
                {
                    resultPartialNutrition = __state.SourceNutritionHistory[0];
                    __state.SourceNutritionHistory.RemoveAt(0);

                    resultNutritionHistory.Clear();
                }
                else
                {

                    while (diff > 0 && __state.SourceNutritionHistory.Count > 0)
                    {
                        resultNutritionHistory.Insert(0, __state.SourceNutritionHistory[0]);
                        __state.SourceNutritionHistory.RemoveAt(0);
                        diff--;
                    }

                    // Resulting stack is now in multi-item stack mode, so we need to reset its PartialNutrition value.
                    resultPartialNutrition = -1f;

                    //Resetting source stack's PartialNutrition value, sanity check.
                    __state.SourcePartialNutrition = -1f;

                }
                // Setting source to singleton mode.
                if (__instance.stackCount == 1)
                {
                    __state.SourcePartialNutrition = __state.SourceNutritionHistory[0];
                    __state.SourceNutritionHistory.Clear();
                }
                // Resetting source, sanity check.
                if (__instance.stackCount == 0)
                {
                    __state.SourcePartialNutrition = -1f;
                    __state.SourceNutritionHistory.Clear();
                }


            }
            finally
            {

                resultTracker.NutritionEntries = resultNutritionHistory;
                resultTracker.PartialNutrition = resultPartialNutrition;

                __state.SourceTracker.NutritionEntries = __state.SourceNutritionHistory;
                __state.SourceTracker.PartialNutrition = __state.SourcePartialNutrition;

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

        public float TargetPartialNutrition;
        public float SourcePartialNutrition;

        public List<float> TargetNutritionHistory;
        public List<float> SourceNutritionHistory;
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

            if (targetTracker.NutritionEntries == null || sourceTracker.NutritionEntries == null)
                return;

            __state = new StackMergeState
            {
                TargetTracker = targetTracker,
                SourceTracker = sourceTracker,

                TargetStackBefore = __instance.stackCount,
                SourceStackBefore = other.stackCount,

                TargetNutritionHistory = new List<float>(targetTracker.NutritionEntries),
                SourceNutritionHistory = new List<float>(sourceTracker.NutritionEntries),

                TargetPartialNutrition = targetTracker.PartialNutrition,
                SourcePartialNutrition = sourceTracker.PartialNutrition
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
                if (__state.TargetNutritionHistory.Count == 0 && __state.TargetPartialNutrition < 0f)
                {
                    ThingDef originalDef = FoodTrackingHelpers.GetOriginalMealDef(__instance.def);
                    __state.TargetPartialNutrition = originalDef?.GetStatValueAbstract(StatDefOf.Nutrition) ?? 0f;
                }
                else if
                    (__state.SourceNutritionHistory.Count == 0 && __state.SourcePartialNutrition < 0f)
                {
                    ThingDef originalDef = FoodTrackingHelpers.GetOriginalMealDef(other.def);
                    __state.SourcePartialNutrition = originalDef?.GetStatValueAbstract(StatDefOf.Nutrition) ?? 0f;
                }

                // Stack merge case: If both stacks had more than one item, we need to append the NutritionEntries lists both ways..
                if (__state.TargetStackBefore > 1 && __state.SourceStackBefore > 1)
                {
                    if (diff > 0)
                    {

                        while (diff > 0 && __state.SourceNutritionHistory.Count > 0)
                        {
                            __state.TargetNutritionHistory.Insert(0, __state.SourceNutritionHistory[0]);
                            __state.SourceNutritionHistory.RemoveAt(0);
                            diff--;

                        }
                        // If the source stack is now a singleton.
                        if (__state.SourceNutritionHistory.Count == 1)
                        {
                            // Target list should already have its PartialNutrition value set, but we reset it just in case.
                            __state.TargetPartialNutrition = -1f;

                            // Source stack has become a singleton, so we need to set its PartialNutrition value and clear its NutritionEntries list.
                            __state.SourcePartialNutrition = __state.SourceNutritionHistory[0];
                            __state.SourceNutritionHistory.Clear();

                        }
                        // Source stack has become empty, sanity check.
                        else if (__state.SourceNutritionHistory.Count == 0)
                        {
                            // Target list should already have its PartialNutrition value set, but we reset it just in case.
                            __state.TargetPartialNutrition = -1f;

                            // Source stack has become empty, so we need to reset its PartialNutrition value and clear its NutritionEntries list.
                            __state.SourcePartialNutrition = -1f;
                            __state.SourceNutritionHistory.Clear();

                        }
                        // Target stack and Source stack are both still multi-item stacks, reset both PartialNutrition values, sanity check.
                        else
                        {
                            __state.TargetPartialNutrition = -1f;
                            __state.SourcePartialNutrition = -1f;
                        }
                    }
                    else
                    {
                        while (diff > 0 && __state.TargetNutritionHistory.Count > 0)
                        {
                            __state.SourceNutritionHistory.Insert(0, __state.TargetNutritionHistory[0]);
                            __state.TargetNutritionHistory.RemoveAt(0);
                            diff--;

                        }
                        // If the target stack is now a singleton.
                        if (__state.TargetNutritionHistory.Count == 1)
                        {
                            // Source list should already have its PartialNutrition value set, but we reset it just in case.
                            __state.SourcePartialNutrition = -1f;

                            // Target stack has become a singleton, so we need to set its PartialNutrition value and clear its NutritionEntries list.
                            __state.TargetPartialNutrition = __state.TargetNutritionHistory[0];
                            __state.TargetNutritionHistory.Clear();

                        }
                        // Target stack has become empty, sanity check.
                        else if (__state.TargetNutritionHistory.Count == 0)
                        {
                            // Source list should already have its PartialNutrition value set, but we reset it just in case.
                            __state.SourcePartialNutrition = -1f;

                            // Target stack has become empty, so we need to reset its PartialNutrition value and clear its NutritionEntries list.
                            __state.TargetPartialNutrition = -1f;
                            __state.TargetNutritionHistory.Clear();

                        }
                        // Target stack and Source stack are both still multi-item stacks, reset both PartialNutrition values, sanity check.
                        else
                        {
                            __state.TargetPartialNutrition = -1f;
                            __state.SourcePartialNutrition = -1f;
                        }
                    }

                    return;

                }

                // Append singleton merge case: If one stack was a singleton and the other stack had more than one item,
                // we need to add the PartialNutrition value of the singleton to the NutritionEntries list of the resulting stack.
                if ((__state.TargetStackBefore > 1 && __state.SourceStackBefore == 1) || (__state.SourceStackBefore > 1 && __state.TargetStackBefore == 1))
                {
                    // Target singleton became the 10-stack while Source became singleton.
                    if (__state.TargetStackBefore == 1 && targetStackAfter > 1 && sourceStackAfter == 1)
                    {
                        // Add Target's singleton nutrition into its list.
                        __state.TargetNutritionHistory.Insert(0, __state.TargetPartialNutrition);

                        __state.TargetPartialNutrition = -1f;

                        // Transfer the additional items from Source.
                        while (diff > 0 && __state.SourceNutritionHistory.Count > 0)
                        {
                            __state.TargetNutritionHistory.Insert(0, __state.SourceNutritionHistory[0]);
                            __state.SourceNutritionHistory.RemoveAt(0);
                            diff--;

                        }

                        // Source is now the singleton.
                        __state.SourcePartialNutrition = __state.SourceNutritionHistory[0];
                        __state.SourceNutritionHistory.Clear();
                    }
                    // Source singleton became the 10-stack while Target became singleton.
                    else if (__state.SourceStackBefore == 1 && sourceStackAfter > 1 && targetStackAfter == 1)
                    {
                        // Add Source's singleton nutrition into its list.
                        __state.SourceNutritionHistory.Insert(0, __state.SourcePartialNutrition);
                        __state.SourcePartialNutrition = -1f;

                        // Transfer the additional items from Target.
                        while (diff > 0 && __state.TargetNutritionHistory.Count > 0)
                        {
                            __state.SourceNutritionHistory.Insert(0, __state.TargetNutritionHistory[0]);
                            __state.TargetNutritionHistory.RemoveAt(0);
                            diff--;
                        }

                        // Target is now the singleton.
                        __state.TargetPartialNutrition = __state.TargetNutritionHistory[0];
                        __state.TargetNutritionHistory.Clear();
                    }
                    // Source large stack was absorbed into target singleton.
                    else if (__state.TargetStackBefore == 1 && __state.SourceStackBefore > 1 && targetStackAfter > 1 && sourceStackAfter == 0)
                    {
                        // Target needs to add its own singleton entry.
                        __state.TargetNutritionHistory.Insert(0, __state.TargetPartialNutrition);
                        __state.TargetPartialNutrition = -1f;

                        while (diff > 0 && __state.SourceNutritionHistory.Count > 0)
                        {
                            __state.TargetNutritionHistory.Insert(0, __state.SourceNutritionHistory[0]);
                            __state.SourceNutritionHistory.RemoveAt(0);
                            diff--;

                        }

                        // Source entry and source list needs to be reset.
                        __state.SourcePartialNutrition = -1f;
                        __state.SourceNutritionHistory.Clear();

                    }
                    // Target large stack was absorbed into source singleton.
                    else if (__state.SourceStackBefore == 1 && __state.TargetStackBefore > 1 && sourceStackAfter > 1 && targetStackAfter == 0)
                    {
                        // Source needs to add its own singleton entry.
                        __state.SourceNutritionHistory.Insert(0, __state.SourcePartialNutrition);
                        __state.SourcePartialNutrition = -1f;

                        while (diff > 0 && __state.TargetNutritionHistory.Count > 0)
                        {
                            __state.SourceNutritionHistory.Insert(0, __state.TargetNutritionHistory[0]);
                            __state.TargetNutritionHistory.RemoveAt(0);
                            diff--;

                        }

                        // Target entry and target list needs to be reset.
                        __state.TargetPartialNutrition = -1f;
                        __state.TargetNutritionHistory.Clear();

                    }
                    // Target singleton was absorbed by larger source.
                    else if (__state.TargetStackBefore == 1 && __state.SourceStackBefore > 1 && targetStackAfter == 0 && sourceStackAfter > 1)
                    {
                        // Soruce list absorbs targets singleton entry.
                        __state.SourceNutritionHistory.Insert(0, __state.TargetPartialNutrition);

                        // Source list should already have its PartialNutrition value set, but we reset it just in case.
                        __state.SourcePartialNutrition = -1f;

                        // Target entry and target list needs to be reset.
                        __state.TargetPartialNutrition = -1f;
                        __state.TargetNutritionHistory.Clear();

                    }
                    // Source singleton was absorbed by larger target.
                    else if (__state.SourceStackBefore == 1 && __state.TargetStackBefore > 1 && sourceStackAfter == 0 && targetStackAfter > 1)
                    {
                        // Target list absorbs sources singleton entry.
                        __state.TargetNutritionHistory.Insert(0, __state.SourcePartialNutrition);

                        // Target list should already have its PartialNutrition value set, but we reset it just in case.
                        __state.TargetPartialNutrition = -1f;

                        // Source entry and source list needs to be reset.
                        __state.SourcePartialNutrition = -1f;
                        __state.SourceNutritionHistory.Clear();

                    }

                    return;

                }

                // Singleton merge case: If either stack was a singleton, we need to add.
                // the PartialNutrition values to the NutritionEntries list of the resulting stack.
                if ((targetStackAfter == 2 && sourceStackAfter == 0) || (sourceStackAfter == 2 && targetStackAfter == 0))
                {
                    // If target became the double stack.
                    if (targetStackAfter == 2)
                    {
                        // Target list absorbs it's own entry and sources entry.
                        __state.TargetNutritionHistory.Insert(0, __state.TargetPartialNutrition);
                        __state.TargetNutritionHistory.Insert(0, __state.SourcePartialNutrition);

                        // Target entry needs to be reset.
                        __state.TargetPartialNutrition = -1f;

                        // Source entry and source list needs to be reset.
                        __state.SourcePartialNutrition = -1f;
                        __state.SourceNutritionHistory.Clear();

                    }
                    // If source became the double stack.
                    else
                    {
                        // Source list absorbs it's own entry and targets entry.
                        __state.SourceNutritionHistory.Insert(0, __state.SourcePartialNutrition);
                        __state.SourceNutritionHistory.Insert(0, __state.TargetPartialNutrition);

                        // Source entry needs to be reset.
                        __state.SourcePartialNutrition = -1f;

                        // Target entry and target list needs to be reset.
                        __state.TargetPartialNutrition = -1f;
                        __state.TargetNutritionHistory.Clear();

                    }

                    return;

                }
            }
            finally
            {

                if (__state != null)
                {
                    __state.TargetTracker.NutritionEntries.Clear();
                    __state.TargetTracker.NutritionEntries.AddRange(__state.TargetNutritionHistory);

                    __state.SourceTracker.NutritionEntries.Clear();
                    __state.SourceTracker.NutritionEntries.AddRange(__state.SourceNutritionHistory);

                    __state.TargetTracker.PartialNutrition = __state.TargetPartialNutrition;
                    __state.SourceTracker.PartialNutrition = __state.SourcePartialNutrition;
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
            int listCount = tracker.NutritionEntries.Count;

            if (stack == 0)
            {
                if (listCount != 0 || tracker.PartialNutrition >= 0f)
                {
                    Log.Warning($"[FoodTracker][VALIDATION] INVALID EMPTY STATE | Thing={thing.def.defName} " +
                        $"ID={thing.thingIDNumber} Stack={stack} Partial={tracker.PartialNutrition} ListCount={listCount}"
                    );
                }

                return;
            }

            if (stack == 1)
            {
                if (listCount != 0 || tracker.PartialNutrition < 0f)
                {
                    Log.Warning($"[FoodTracker][VALIDATION] INVALID SINGLETON STATE | Thing={thing.def.defName} " +
                        $"ID={thing.thingIDNumber} Stack={stack} Partial={tracker.PartialNutrition} ListCount={listCount}"
                    );
                }

                return;
            }

            if (listCount != stack)
            {
                Log.Warning($"[FoodTracker][VALIDATION] INVALID STACK STATE | Thing={thing.def.defName} ID={thing.thingIDNumber} Stack={stack} " +
                    $"Partial={tracker.PartialNutrition} ListCount={listCount} List=[{string.Join(", ", tracker.NutritionEntries)}]"
                );
            }

            if (tracker.PartialNutrition >= 0f)
            {
                Log.Warning($"[FoodTracker][VALIDATION] MULTI-STACK HAS ACTIVE SINGLETON | Thing={thing.def.defName} " +
                    $"ID={thing.thingIDNumber} Stack={stack} Partial={tracker.PartialNutrition} ListCount={listCount}"
                );
            }
        }
    }
}






