using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace FoodTracker
{

    [HarmonyPatch]
    public static class MassPatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            // Patch both Mass Usage overloads relevant to the Caravan calculations.

            yield return AccessTools.Method(typeof(CollectionsMassCalculator), nameof(CollectionsMassCalculator.MassUsage), new[] {
                typeof(List<ThingCount>), typeof(IgnorePawnsInventoryMode), typeof(bool), typeof(bool)});

            yield return AccessTools.Method(typeof(CollectionsMassCalculator), nameof(CollectionsMassCalculator.MassUsage), new[] {
                typeof(ThingOwner), typeof(IgnorePawnsInventoryMode), typeof(bool), typeof(bool)});
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> MassCalculator_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo getStatValue = AccessTools.Method(typeof(StatExtension), nameof(StatExtension.GetStatValue));
            FieldInfo mass = AccessTools.Field(typeof(StatDefOf), nameof(StatDefOf.Mass));

            var codes = new List<CodeInstruction>(instructions);

            for (int i = 1; i < codes.Count - 3; i++)
            {
                if (codes[i - 1].opcode == OpCodes.Ldloc_3 && codes[i].LoadsField(mass) && codes[i + 3].Calls(getStatValue))
                {
                    codes.RemoveRange(i, 7);
                    codes.Insert(i, CodeInstruction.LoadLocal(2));
                    codes.Insert(i + 1, CodeInstruction.Call(typeof(MassPatch), nameof(GetFoodTrackerMass)));

                    break;
                }
                else if (codes[i - 1].opcode == OpCodes.Ldloc_2 && codes[i].LoadsField(mass) && codes[i + 3].Calls(getStatValue))
                {
                    codes.RemoveRange(i, 7);
                    codes.Insert(i, CodeInstruction.LoadLocal(3));
                    codes.Insert(i + 1, CodeInstruction.Call(typeof(MassPatch), nameof(GetFoodTrackerMass)));

                    break;
                }
            }
            return codes;
        }

        public static float GetFoodTrackerMass(Thing thing, int stackCount)
        {
            // Inputs not valid.
            if (thing == null || stackCount <= 0)
                return 0f;

            // Get Vanilla's result to return if not our FT item.
            float mass = thing.GetStatValue(StatDefOf.Mass);

            CompFoodTracker tracker = thing.TryGetComp<CompFoodTracker>();

            Log.Message(
                $"[FoodTracker] MASS: def={thing.def.defName}, " +
                $"physicalStack={thing.stackCount}, passedCount={stackCount}, " +
                $"baseMass={mass}, " +
                $"tracker={(tracker != null)}, " +
                $"partial={(tracker != null ? tracker.PartialFraction : -1f)}, " +
                $"listCount={(tracker != null ? tracker.RemainingFractions.Count : -1)}");

            if (tracker == null)
                return mass * stackCount;

            float totalFraction = 0f;
            float totalMass = 0f;

            if (tracker.RemainingFractions.Count > 0)
            {
                for (int i = 0; i < stackCount; i++)
                {
                    totalFraction += tracker.RemainingFractions[i];
                }
            }
            else
            {
                totalFraction = tracker.PartialFraction;
            }

            totalMass += totalFraction * mass;

            Log.Message(
                $"[FoodTracker] MASS RESULT: def={thing.def.defName}, " +
                $"totalFraction={totalFraction}, result={totalMass}");

            return totalMass;
        }
    }

    [HarmonyPatch(typeof(TransferableOneWayWidget), "DrawMass")]
    public static class MassPatch_DrawMass
    {

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> MassLabel_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo toStringMass = AccessTools.Method(typeof(GenText), nameof(GenText.ToStringMass), new[] { typeof(float) });

            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(toStringMass))
                {
                    codes.InsertRange(i + 1, new[]
                    {
                        CodeInstruction.LoadArgument(2),
                        CodeInstruction.Call(typeof(CaravanPatch), nameof(CaravanPatch.GetCaravanLabel))
                    });

                    break;
                }
            }
            return codes;
        }
    }
}
