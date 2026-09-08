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
    // OLD C#: int num19 = Mathf.Min(Mathf.CeilToInt(Mathf.Min(0.2f, cachedMaxFoodLevel[num13]) / num17), thingDefCount3.Count);
    //         tmpDaysWorthOfFoodForPawn[num13] += num18 * (float)num19;

    // NEW C#: int num19 = CaravanPatch.GetRequiredItemCount(Mathf.Min(0.2f, cachedMaxFoodLevel[num13]), thingDefCount3);
    //         tmpDaysWorthOfFoodForPawn[num13] += CaravanPatch.GetActualFoodDaysContribution(num18, num17);

    [HarmonyPatch(typeof(DaysWorthOfFoodCalculator), "ApproxDaysWorthOfFood", new[] { typeof(List<Pawn>), typeof(List<ThingDefCount>), typeof(PlanetTile),
        typeof(IgnorePawnsInventoryMode), typeof(Faction), typeof(WorldPath), typeof(float), typeof(int), typeof(bool)})]
    public static class CaravanPatch_DaysWorthOfFoodCalculator
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {

            // Target method Verse.ThingDefCount.get_ThingDef()
            MethodInfo minInt = AccessTools.Method(typeof(Mathf), nameof(Mathf.Min), new[] { typeof(float), typeof(float) });
            MethodInfo listGetItem = AccessTools.Method(typeof(List<float>), "get_Item");

            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                // IL_0691: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Min(float32, float32). Begin after this.
                if (codes[i - 1].opcode == OpCodes.Call && codes[i - 1].operand is MethodInfo method && method.Name == nameof(Mathf.Min) &&
                    method.DeclaringType == typeof(Mathf) && (codes[i].opcode == OpCodes.Ldloc_S || codes[i].opcode == OpCodes.Ldloc))
                {
                    // Verify local index 60 regardless of operand box type (LocalBuilder, LocalVariableInfo, int, etc.)
                    int localIndex = -1;
                    if (codes[i].operand is LocalVariableInfo lvi) 
                        localIndex = lvi.LocalIndex;
                    else if (codes[i].operand != null && int.TryParse(codes[i].operand.ToString(), out int parsed)) 
                        localIndex = parsed;

                    if (localIndex == 60)
                    {
                        // Leave IL_0696 (ldloc.s 60) as a fallback if not FT Item (num17).

                        // Replace IL_0698 (div) with ldloc.s 59 (thingDefCount3)
                        codes[i + 1] = CodeInstruction.LoadLocal(59);

                        // Remove the 3 instructions (IL_0699, IL_069e, IL_06a0)
                        codes.RemoveRange((i + 2), 3);

                        // Replace IL_06a5 (Mathf.Min int, int) now shifted down to index (i + 1) with method call
                        codes[i + 1] = CodeInstruction.Call(typeof(CaravanPatch), nameof(CaravanPatch.GetRequiredItemCount));

                        break;
                    }
                }

                if (codes[i - 1].opcode == OpCodes.Callvirt && codes[i - 1].operand is MethodInfo method2 && method2.Name == "get_Item" &&
                    method2.DeclaringType == typeof(List<float>) && (codes[i].opcode == OpCodes.Ldloc_S || codes[i].opcode == OpCodes.Ldloc))
                {
                    // Verify local index 61 regardless of operand box type (LocalBuilder, LocalVariableInfo, int, etc.)
                    int localIndex = -1;
                    if (codes[i].operand is LocalVariableInfo lvi)
                        localIndex = lvi.LocalIndex;
                    else if (codes[i].operand != null && int.TryParse(codes[i].operand.ToString(), out int parsed))
                        localIndex = parsed;

                    if (localIndex == 61)
                    {
                        // Don't touch IL_06c4, vanilla loads num18 for us.

                        // Replace IL_06c6 (ldloc.s 62) with ldloc.s 60 (num17)
                        codes[i + 1] = CodeInstruction.LoadLocal(60);

                        // Replace IL_06c8 (conv.r4) with our method call
                        codes[i + 2] = CodeInstruction.Call(typeof(CaravanPatch), nameof(CaravanPatch.GetActualFoodDaysContribution), new[] { typeof(float), typeof(float) });

                        // Remove IL_06c9 (mul)
                        codes.RemoveAt(i + 3);
                    }
                }
            }
            return codes;
        }
    }

    [HarmonyPatch(typeof(CollectionsMassCalculator), nameof(CollectionsMassCalculator.MassUsage), new[] { typeof(List<ThingCount>), typeof(IgnorePawnsInventoryMode), typeof(bool), typeof(bool) })]
    public static class CaravanPatch_MassUsage
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {

            MethodInfo statValue = AccessTools.Method(typeof(StatExtension), nameof(StatExtension.GetStatValue), new[] { typeof(Thing), typeof(StatDef), typeof(bool), typeof(int)});

            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i + 3].opcode == OpCodes.Call && codes[i + 3].operand is MethodInfo method && method.Name == nameof(StatExtension.GetStatValue) &&
                    method.DeclaringType == typeof(StatExtension) && (codes[i - 1].opcode == OpCodes.Ldloc_S || codes[i - 1].opcode == OpCodes.Ldloc))
                {
                    // Verify local index 3 regardless of operand box type (LocalBuilder, LocalVariableInfo, int, etc.)
                    int localIndex = -1;
                    if (codes[i - 1].operand is LocalVariableInfo lvi)
                        localIndex = lvi.LocalIndex;
                    else if (codes[i - 1].operand != null && int.TryParse(codes[i - 1].operand.ToString(), out int parsed))
                        localIndex = parsed;

                    if (localIndex == 3)
                    {
                        // Replace IL_0083: ldsfld .StatDefOf::Mass with ldloc.2
                        codes[i] = CodeInstruction.LoadLocal(2);

                        // Replace IL_0088: ldc.i4.1 with .Call our method.
                        codes[i + 1] = CodeInstruction.Call(typeof(CaravanPatch), nameof(CaravanPatch.GetActualMassContribution));

                        // Remove IL_0089, IL_008a, IL_008f, IL_0090, IL_0091
                        codes.RemoveRange((i + 2), 5);
                    }
                }
            }
            return codes;
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
