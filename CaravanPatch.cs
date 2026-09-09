using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace FoodTracker
{
    
    [HarmonyPatch(typeof(DaysWorthOfFoodCalculator), "ApproxDaysWorthOfFood", new[] { typeof(List<Pawn>), typeof(List<ThingDefCount>), typeof(PlanetTile),
        typeof(IgnorePawnsInventoryMode), typeof(Faction), typeof(WorldPath), typeof(float), typeof(int), typeof(bool)})]
    public static class CaravanPatch_DaysWorthOfFoodCalculator
    {

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {

            // Target method UnityEngine.Mathf.Min().
            MethodInfo minFloat = AccessTools.Method(typeof(Mathf), nameof(Mathf.Min), new[] { typeof(float), typeof(float) });

            var codes = new List<CodeInstruction>(instructions);

            for (int i = 3; i < codes.Count - 5; i++)
            {

                // OLD C#: int num19 = Mathf.Min(Mathf.CeilToInt(Mathf.Min(0.2f, cachedMaxFoodLevel[num13]) / num17), thingDefCount3.Count);
                // NEW C#: int num19 = CaravanPatch.GetRequiredItemCount(Mathf.Min(0.2f, cachedMaxFoodLevel[num13]), thingDefCount3);
                if (codes[i - 1].Calls(minFloat) && LoadsTargetLocal(codes[i], 60))
                {

                    // Leave IL_0696 (ldloc.s 60) as a fallback if not FT Item (num17).

                    // Replace IL_0698 (div) with ldloc.s 59 (thingDefCount3).
                    codes[i + 1] = CodeInstruction.LoadLocal(59);

                    // Remove the 3 instructions (IL_0699, IL_069e, IL_06a0).
                    codes.RemoveRange((i + 2), 3);

                    // Replace IL_06a5 (Mathf.Min int, int) now shifted down to index (i + 1) with method call.
                    codes[i + 1] = CodeInstruction.Call(typeof(CaravanPatch), nameof(CaravanPatch.GetRequiredItemCount));

                    break;
                    
                }

                // OLD C# tmpDaysWorthOfFoodForPawn[num13] += num18 * (float)num19;
                // NEW C# tmpDaysWorthOfFoodForPawn[num13] += CaravanPatch.GetActualFoodDaysContribution(num18, num17);
                if (LoadsTargetLocal(codes[i], 61) && LoadsTargetLocal(codes[i - 2], 55) && LoadsTargetLocal(codes[i - 3], 54))
                {

                    // Don't touch IL_06c4, vanilla loads num18 for us.

                    // Replace IL_06c6 (ldloc.s 62) with ldloc.s 60 (num17).
                    codes[i + 1] = CodeInstruction.LoadLocal(60);

                    // Replace IL_06c8 (conv.r4) with our method call.
                    codes[i + 2] = CodeInstruction.Call(typeof(CaravanPatch), nameof(CaravanPatch.GetActualFoodDaysContribution));

                    // Remove IL_06c9 (mul).
                    codes.RemoveAt(i + 3);

                    break;
                    
                }
            }
            return codes;
        }

        private static bool LoadsTargetLocal(CodeInstruction instruction, int localIndex)
        {
            if (instruction.opcode == OpCodes.Ldloc_S)
            {
                // Convert the operand safely; it could be a byte, sbyte, or LocalBuilder object.
                if (instruction.operand is byte b && b == localIndex) return true;
                else if (instruction.operand is sbyte sb && sb == localIndex) return true;
                else if (instruction.operand is IConvertible c && Convert.ToInt32(c) == localIndex) return true;
            }

            return false;
        }

    }

    [HarmonyPatch(typeof(CollectionsMassCalculator))]
    public static class CaravanPatch_MassUsage
    {
        // Target both overloads.
        [HarmonyPatch(nameof(CollectionsMassCalculator.MassUsage), new[] { typeof(List<ThingCount>), typeof(IgnorePawnsInventoryMode), typeof(bool), typeof(bool) })]
        [HarmonyPatch(nameof(CollectionsMassCalculator.MassUsage), new[] { typeof(ThingOwner), typeof(IgnorePawnsInventoryMode), typeof(bool), typeof(bool) })]

        // Replace the Mass stat calculation with our fractional-mass contribution.
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> PatchMassUsage(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            // Bool to track which overload we're on.
            bool isThingCountOverload = __originalMethod.GetParameters()[0].ParameterType == typeof(List<ThingCount>);

            int thingLocal = isThingCountOverload ? 3 : 2;
            int countLocal = isThingCountOverload ? 2 : 3;

            // Create target method and target field for transpiler splice.
            MethodInfo statValue = AccessTools.Method(typeof(StatExtension), nameof(StatExtension.GetStatValue), new[] { typeof(Thing), typeof(StatDef), typeof(bool), typeof(int) });
            FieldInfo massFieldInfo = AccessTools.Field(typeof(RimWorld.StatDefOf), "Mass");

            var codes = new List<CodeInstruction>(instructions);

            for (int i = 1; i < codes.Count - 7; i++)
            {

                // OLD C#: num += thing.GetStatValue(StatDefOf.Mass) * (float)count;
                // NEW C#: num += CaravanPatch.GetActualMassContribution(thing, count);
                if (codes[i + 3].Calls(statValue) && codes[i].LoadsField(massFieldInfo) && LoadsTargetLocal(codes[i - 1], thingLocal))
                {

                    // Replace IL_0083/IL_0074: (ldsfld) .StatDefOf::Mass with ldloc.2/ldloc.3.
                    codes[i] = CodeInstruction.LoadLocal(countLocal);

                    // Replace IL_0088/IL_0079: (ldc.i4.1) with .Call our method.
                    codes[i + 1] = CodeInstruction.Call(typeof(CaravanPatch), nameof(CaravanPatch.GetActualMassContribution));

                    // Remove IL_0089/IL_007a, IL_008a/IL_007b, IL_008f/IL_0080, IL_0090/IL_0081, IL_0091/IL_0082.
                    codes.RemoveRange((i + 2), 5);
                    break;
                }
            }
            return codes;
        }

        private static bool LoadsTargetLocal(CodeInstruction instruction, int localIndex)
        {
            if (localIndex == 2)
                return instruction.opcode == OpCodes.Ldloc_2;

            if (localIndex == 3)
                return instruction.opcode == OpCodes.Ldloc_3;

            return false;
        }
    }

    [HarmonyPatch(typeof(Dialog_FormCaravan), "AddToTransferables", new[] { typeof(Thing), typeof(bool) })]
    public static class CaravanPatch_AddToTransferables
    {
        
        public static void PostFix(Thing thing, List<TransferableOneWay> transferables)
        {
            if (thing == null || transferables == null) 
                return;

            CompFoodTracker tracker = thing.TryGetComp<CompFoodTracker>();

            if (tracker == null)
                return;

            TransferableOneWay transferable = TransferableUtility.TransferableMatching(thing, transferables, TransferAsOneMode.PodsOrCaravanPacking);

            if (transferable == null)
                return;

            CaravanPatch.Track(transferable, thing);
        }
    }

    public static class CaravanPatch
    {
        public static Dictionary<TransferableOneWay, HashSet<Thing>> trackedTransferables = new Dictionary<TransferableOneWay, HashSet<Thing>>();
        private static int ItemCountAvailable;
        private static float TotalFraction;
        private static float RequestedNutrition;
        private static ThingDef LastThingDef;
        private static bool FoundFTItem;

        public static void Track(TransferableOneWay transferable, Thing thing)
        {
            if (!trackedTransferables.TryGetValue(transferable, out var things))
            {
                things = new HashSet<Thing>();
                trackedTransferables.Add(transferable, things);
            }

            things.Add(thing);
        }

        public static int GetRequiredItemCount(float requestedNutrition, float num17, ThingDefCount item)
        {
            ItemCountAvailable = 0;
            TotalFraction = 0f;
            RequestedNutrition = requestedNutrition;
            LastThingDef = item.ThingDef;

            FoundFTItem = false;
            float totalNutrition = 0f;
            float partialFraction;
            float fullNutrition;
            int fractionIndex = 0;

            foreach (TransferableOneWay transferable in trackedTransferables.Keys)
            {
                if (totalNutrition >= requestedNutrition || ItemCountAvailable >= item.Count)
                    break;

                foreach (Thing thing in transferable.things)
                {
                    if (thing.def != item.ThingDef)
                        continue;

                    CompFoodTracker tracker = thing.TryGetComp<CompFoodTracker>();

                    if (tracker == null)
                        continue;

                    FoundFTItem = true;

                    if (tracker.RemainingFractions.Count <= 0 && tracker.PartialFraction <= 0f)
                        continue;

                    fullNutrition = thing.def.GetStatValueAbstract(StatDefOf.Nutrition);

                    if (fractionIndex < tracker.RemainingFractions.Count)
                    {
                        partialFraction = tracker.RemainingFractions[fractionIndex];
                    }
                    else if (tracker.PartialFraction > 0f)
                    {
                        partialFraction = tracker.PartialFraction;
                    }
                    else
                    {
                        continue;
                    }
                    
                    totalNutrition += fullNutrition * partialFraction;
                    ItemCountAvailable++;
                    fractionIndex++;

                    if (totalNutrition >= requestedNutrition || ItemCountAvailable >= item.Count)
                        break;
                }

                fractionIndex = 0;

            }

            // Not an FT item use vanilla calculation.
            if (!FoundFTItem)
            {
                ItemCountAvailable = Mathf.Min(Mathf.CeilToInt(RequestedNutrition / num17), item.Count);

                return ItemCountAvailable;
            }

            TotalFraction = totalNutrition;
            return ItemCountAvailable;

        }

        public static float GetActualMassContribution(Thing thing, int count)
        {
            CompFoodTracker tracker = thing.TryGetComp<CompFoodTracker>();
            float fullMass = 0f;

            if (tracker == null)
            {
                // Exact vanilla calculation.
                fullMass = StatExtension.GetStatValue(thing, StatDefOf.Mass, true, -1);

                return fullMass * count;
            }

            fullMass = StatExtension.GetStatValue(thing, StatDefOf.Mass, true, -1);

            if (tracker.RemainingFractions.Count <= 0)
            {
                return tracker.PartialFraction > 0f ? fullMass * tracker.PartialFraction: 0f;
            }

            float totalMass = 0f;
            int limit = Mathf.Min(count, tracker.RemainingFractions.Count);

            for (int i = 0; i < limit; i++)
            {
                totalMass += fullMass * tracker.RemainingFractions[i];
            }

            return totalMass;
        }

        public static float GetActualFoodDaysContribution(float foodDaysPerItem, float itemNutrition)
        {

            if (!FoundFTItem)
                return foodDaysPerItem * ItemCountAvailable;

            float actualNutrition = TotalFraction * LastThingDef.ingestible.CachedNutrition;

            return foodDaysPerItem / itemNutrition * actualNutrition;
        }
    }
}
