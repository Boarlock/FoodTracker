using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace FoodTracker
{

    [HarmonyPatch(typeof(Thing), nameof(Thing.SplitOff))]
    public static class SplitOffPatch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo thingMaker = AccessTools.Method(typeof(ThingMaker), nameof(ThingMaker.MakeThing), new[] { typeof(ThingDef), typeof(ThingDef) });

            List<CodeInstruction> newInstructions = new List<CodeInstruction>
            {
                CodeInstruction.LoadArgument(1),
                CodeInstruction.Call(typeof(SplitOffPatch), nameof(ProcessSplit)),
                CodeInstruction.StoreArgument(1),
                CodeInstruction.LoadLocal(0),
                CodeInstruction.LoadArgument(1)
            };

            var codes = new List<CodeInstruction>(instructions);

            for (int i = 2; i < codes.Count - 1; i++)
            {
                if (codes[i - 2].Calls(thingMaker) && codes[i + 1].IsLdarg(1))
                {
                    codes[i] = CodeInstruction.LoadArgument(0);
                    codes[i + 1] = CodeInstruction.LoadLocal(0);
                    codes.InsertRange(i + 2, newInstructions);

                    break;
                }
            }
            return codes;
        }

        public static int ProcessSplit(Thing oldThing, Thing newThing, int count)
        {
            if (oldThing == null || newThing == null)
                return count;

            CompFoodTracker oldTracker = oldThing.TryGetComp<CompFoodTracker>();
            CompFoodTracker newTracker = newThing.TryGetComp<CompFoodTracker>();

            if (oldTracker == null || newTracker == null)
                return count;

            int splitAmount = count;

            if (splitAmount <= 0)
                return count;

            // VALIDATION

            // Normalize FT lists of count 1 so they are correctly identified as Partials.
            if (oldTracker.RemainingFractions.Count == 1)
            {
                oldTracker.PartialFraction = oldTracker.RemainingFractions[0];
                oldTracker.RemainingFractions.Clear();
            }

            // Create bools for current representation.
            bool oldIsList = oldTracker.RemainingFractions.Count > 1;
            bool oldIsSingleton = oldTracker.PartialFraction > 0f && oldTracker.PartialFraction <= 1f;

            // Normalize representation.
            if (oldIsList)
                oldTracker.PartialFraction = -1f;
            else
                oldTracker.RemainingFractions.Clear();

            // Completely broken representation.
            if (!oldIsList && !oldIsSingleton)
                oldTracker.PartialFraction = 0.01f;

            // If either are lists their count is equal to list count, otherwise it's 1.
            int oldCount = oldIsList ? oldTracker.RemainingFractions.Count : 1;

            // Comparison Block for Old Thing.
            if (oldCount != oldThing.stackCount)
            {
                // Calculate differences.
                int oldDifference = oldThing.stackCount - oldCount;

                // Vanilla's stack count is less than our representation of that stack, remove our entries until they match.
                if (oldDifference < 0)
                {
                    // It's impossible for our representation to be Singleton and Vanilla have less than that, so we only mutate list here.
                    for (int i = 0; i < -oldDifference; i++)
                    {
                        oldTracker.RemainingFractions.RemoveAt(0);
                    }
                    // Check to see if the list became a Singleton.
                    if (oldTracker.RemainingFractions.Count == 1)
                    {
                        oldTracker.PartialFraction = oldTracker.RemainingFractions[0];

                        oldTracker.RemainingFractions.Clear();
                    }
                }
                // Vanilla's stack count is greater than our representation of that stack, pad our entries until they match.
                else
                {
                    // If our representation started off as a Singleton we first turn it into a list.
                    if (oldTracker.PartialFraction > 0f)
                    {
                        oldTracker.RemainingFractions.Add(oldTracker.PartialFraction);

                        oldTracker.PartialFraction = -1f;
                    }

                    for (int i = oldDifference - 1; i >= 0; i--)
                    {
                        oldTracker.RemainingFractions.Insert(0, 0.01f);
                    }
                }
            }

            // ACTUAL SPLIT

            if (splitAmount == 1)
            {
                newTracker.PartialFraction = oldTracker.RemainingFractions[0];
                oldTracker.RemainingFractions.RemoveAt(0);
            }
            else
            {
                for (int i = splitAmount - 1; i >= 0; i--)
                {
                    newTracker.RemainingFractions.Insert(0, oldTracker.RemainingFractions[i]);
                    oldTracker.RemainingFractions.RemoveAt(i);
                }
            }

            // Demote old source to singleton if one item remains.
            if (oldTracker.RemainingFractions.Count == 1)
            {
                oldTracker.PartialFraction = oldTracker.RemainingFractions[0];

                oldTracker.RemainingFractions.Clear();
            }

            return splitAmount;
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.TryAbsorbStack))]
    public static class TryAbsorbStackPatch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo ceilToInt = AccessTools.Method(typeof(UnityEngine.Mathf), nameof(UnityEngine.Mathf.CeilToInt), new[] { typeof(float) });

            List<CodeInstruction> newInstructions = new List<CodeInstruction>
            {
                CodeInstruction.LoadArgument(0),
                CodeInstruction.LoadArgument(1),
                CodeInstruction.LoadLocal(0),
                CodeInstruction.Call(typeof(TryAbsorbStackPatch), nameof(ProcessMerge)),
                CodeInstruction.StoreLocal(0)
            };

            var codes = new List<CodeInstruction>(instructions);

            for (int i = 2; i < codes.Count; i++)
            {
                if (codes[i - 2].Calls(ceilToInt) && codes[i].IsLdarg(0))
                {
                    codes.InsertRange(i, newInstructions);

                    break;
                }
            }
            return codes;
        }

        public static int ProcessMerge(Thing thing, Thing other, int result)
        {

            if (thing == null || other == null)
            {
                return result;
            }

            CompFoodTracker targetTracker = thing.TryGetComp<CompFoodTracker>();
            CompFoodTracker sourceTracker = other.TryGetComp<CompFoodTracker>();

            if (targetTracker == null || sourceTracker == null)
            {
                return result;
            }

            int transferAmount = result;

            if (transferAmount <= 0)
            {
                return result;
            }

            // VALIDATION

            // Normalize FT lists of count 1 so they are correctly identified as Partials.
            if (targetTracker.RemainingFractions.Count == 1)
            {
                targetTracker.PartialFraction = targetTracker.RemainingFractions[0];
                targetTracker.RemainingFractions.Clear();
            }

            if (sourceTracker.RemainingFractions.Count == 1)
            {
                sourceTracker.PartialFraction = sourceTracker.RemainingFractions[0];
                sourceTracker.RemainingFractions.Clear();
            }

            // Create some bools to get the current representation of things.
            bool targetIsList = targetTracker.RemainingFractions.Count > 1;
            bool sourceIsList = sourceTracker.RemainingFractions.Count > 1;
            bool targetIsSingleton = targetTracker.PartialFraction > 0f && targetTracker.PartialFraction <= 1f;
            bool sourceIsSingleton = sourceTracker.PartialFraction > 0f && sourceTracker.PartialFraction <= 1f;

            // Normalize entries so FT lists don't have Partial Values and Partials have no listed values.
            if (targetIsList)
                targetTracker.PartialFraction = -1f;
            else
                targetTracker.RemainingFractions.Clear();

            if (sourceIsList)
                sourceTracker.PartialFraction = -1f;
            else
                sourceTracker.RemainingFractions.Clear();

            // If FT representation is completely broken then create an almost empty meal.
            if (!targetIsList && !targetIsSingleton)
                targetTracker.PartialFraction = 0.01f;

            if (!sourceIsList && !sourceIsSingleton)
                sourceTracker.PartialFraction = 0.01f;

            // If the either are lists their count is equal to list count, otherwise it's 1.
            int targetCount = (targetIsList) ? targetTracker.RemainingFractions.Count : 1;
            int sourceCount = (sourceIsList) ? sourceTracker.RemainingFractions.Count : 1;

            // Comparison Block for Target.
            if (targetCount != thing.stackCount)
            {
                // Calculate differences.
                int targetDifference = thing.stackCount - targetCount;

                // Vanilla's stack count is less than our representation of that stack, remove our entries until they match.
                if (targetDifference < 0)
                {
                    // It's impossible for our representation to be Singleton and Vanilla have less than that, so we only mutate list here.
                    for (int i = 0; i < -targetDifference; i++)
                    {
                        targetTracker.RemainingFractions.RemoveAt(0);
                    }

                    // Check to see if the list became a Singleton.
                    if (targetTracker.RemainingFractions.Count == 1)
                    {
                        targetTracker.PartialFraction = targetTracker.RemainingFractions[0];
                        targetTracker.RemainingFractions.Clear();
                    }
                }
                // Vanilla's stack count is greater than our representation of that stack, pad our entries until they match.
                else
                {
                    // If our representation started off as a Singleton we first turn it into a list.
                    if (targetTracker.PartialFraction > 0f)
                    {
                        targetTracker.RemainingFractions.Add(targetTracker.PartialFraction);
                        targetTracker.PartialFraction = -1f;
                    }

                    for (int i = targetDifference - 1; i >= 0; i--)
                    {
                        targetTracker.RemainingFractions.Insert(0, 0.01f);
                    }
                }
            }

            // Comparison Block for Source.
            if (sourceCount != other.stackCount)
            {
                // Calculate differences.
                int sourceDifference = other.stackCount - sourceCount;

                // Vanilla's stack count is less than our representation of that stack, remove our entries until they match.
                if (sourceDifference < 0)
                {
                    // It's impossible for our representation to be Singleton and Vanilla have less than that, so we only mutate list here.
                    for (int i = 0; i < -sourceDifference; i++)
                    {
                        sourceTracker.RemainingFractions.RemoveAt(0);
                    }

                    // Check to see if the list became a Singleton.
                    if (sourceTracker.RemainingFractions.Count == 1)
                    {
                        sourceTracker.PartialFraction = sourceTracker.RemainingFractions[0];
                        sourceTracker.RemainingFractions.Clear();
                    }
                }
                // Vanilla's stack count is greater than our representation of that stack, pad our entries until they match.
                else
                {
                    // If our representation started off as a Singleton we first turn it into a list.
                    if (sourceTracker.PartialFraction > 0f)
                    {
                        sourceTracker.RemainingFractions.Add(sourceTracker.PartialFraction);
                        sourceTracker.PartialFraction = -1f;
                    }

                    for (int i = sourceDifference - 1; i >= 0; i--)
                    {
                        sourceTracker.RemainingFractions.Insert(0, 0.01f);
                    }
                }
            }

            // ACTUAL MERGER

            targetIsList = targetTracker.RemainingFractions.Count > 1;
            sourceIsList = sourceTracker.RemainingFractions.Count > 1;

            // Prepare Target: If Target is currently a singleton, convert it into a list representation first.
            if (!targetIsList)
            {
                targetTracker.RemainingFractions.Insert(0, targetTracker.PartialFraction);
                targetTracker.PartialFraction = -1f;
            }

            // Perform Transfer based on Source's current state.
            if (sourceIsList)
            {
                // Transfer items while items remain in source list up to transferAmount
                for (int i = transferAmount - 1; i >= 0; i--)
                {
                    targetTracker.RemainingFractions.Insert(0, sourceTracker.RemainingFractions[i]);
                    sourceTracker.RemainingFractions.RemoveAt(i);

                }

                // If source was reduced to exactly 1 element, demote to singleton.
                if (sourceTracker.RemainingFractions.Count == 1)
                {
                    sourceTracker.PartialFraction = sourceTracker.RemainingFractions[0];
                    sourceTracker.RemainingFractions.Clear();
                }
            }
            else
            {
                // Source is a singleton.
                targetTracker.RemainingFractions.Insert(0, sourceTracker.PartialFraction);
            }

            return transferAmount;
        }
    }
}
   






