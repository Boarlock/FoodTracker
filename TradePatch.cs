using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace FoodTracker
{
    // Converts Vanilla instruction | label = ToStringMoney(priceFor, null) to label = GetTradePriceLabel(priceFor, trad);
    [HarmonyPatch(typeof(TradeUI), "DrawPrice")]
    public static class TradePatch_DrawPrice
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {

            // Target method Verse.GenText.ToStringMoney(float, string)
            MethodInfo targetMethod = AccessTools.Method(typeof(GenText), nameof(GenText.ToStringMoney), new[] { typeof(float), typeof(string) });

            var codes = new List<CodeInstruction>(instructions);

            // Iterate through the IL instructions in DrawPrice().
            for (int i = 0; i < codes.Count; i++)
            {
                CodeInstruction code = codes[i];

                // Find the target method inside the IL instructions.
                if (code.opcode == OpCodes.Call && code.operand is MethodInfo method && method.MetadataToken == targetMethod.MetadataToken)
                {
                    // Go back a code instruction to the ldnull to change it to ldarg.1, create a new CodeInstruction with OpCode of Ldarg.1
                    codes[i - 1] = new CodeInstruction(OpCodes.Ldarg_1);

                    // Create replacement CodeInstruction for the target method.
                    codes[i] = CodeInstruction.Call(typeof(TradePatch), nameof(TradePatch.GetTradePriceLabel));
                }
            }

            return codes;
        }
    }

    [HarmonyPatch(typeof(Tradeable), "CurTotalCurrencyCostForDestination", MethodType.Getter)]
    public static class TradePatch_Destination
    {
        public static void Postfix(Tradeable __instance, ref float __result)
        {
            float vanillaResult = __result;

            float ftPrice = TradePatch.CalculateFoodTrackerTradePrice(__instance, vanillaResult);

            if (ftPrice != 0f)
                __result = ftPrice;
        }
    }

    [HarmonyPatch(typeof(Tradeable), "CurTotalCurrencyCostForSource", MethodType.Getter)]
    public static class TradePatch_Source
    {
        public static void Postfix(Tradeable __instance, ref float __result)
        {
            float vanillaResult = __result;

            float ftPrice = TradePatch.CalculateFoodTrackerTradePrice(__instance, vanillaResult);

            if (ftPrice != 0f)
                __result = ftPrice;
        }
    }

    public static class TradePatch
    {
        public static string GetTradePriceLabel(float priceFor, Tradeable trad)
        {

            Thing thing = trad?.AnyThing;

            if (thing != null && thing?.TryGetComp<CompFoodTracker>() != null)
                return "(varies)";

            return priceFor.ToStringMoney(null);
        }

        public static float CalculateFoodTrackerTradePrice(Tradeable tradeable, float vanillaResult)
        {
            // Determine which direction the trade is going.
            TradeAction action = tradeable.ActionToDo;

            if (action == TradeAction.None)
                return 0f;

            List<Thing> thingList;
            int countToTransfer;

            if (action == TradeAction.PlayerSells)
            {
                thingList = tradeable.thingsColony;
                countToTransfer = tradeable.CountToTransferToDestination;
            }
            else if (action == TradeAction.PlayerBuys)
            {
                thingList = tradeable.thingsTrader;
                countToTransfer = tradeable.CountToTransferToSource;
            }
            else
            {
                return 0f;
            }

            float ftValue = CalculateFoodTrackerTotal(Mathf.Abs(countToTransfer), thingList);

            if (ftValue == 0f)
                return 0f;

            Thing ftThing = thingList.Find(thing => thing != null && thing.def != null && thing.def.defName.StartsWith(DynamicMealDefFactory.Prefix));

            if (ftThing == null)
                return 0f;

            string originalDefName = ftThing.def.defName.Substring(DynamicMealDefFactory.Prefix.Length);

            ThingDef originalDef = DefDatabase<ThingDef>.GetNamedSilentFail(originalDefName);

            if (originalDef == null)
                return 0f;

            float vanillaMarketValue = originalDef.GetStatValueAbstract(StatDefOf.MarketValue);

            float vanillaPrice = tradeable.GetPriceFor(action);

            float multiplier = vanillaPrice / vanillaMarketValue;

            float result = ftValue * multiplier;

            // Preserve the direction of the vanilla getter.
            if (vanillaResult < 0f)
                result = -result;

            return result;
        }

        public static float CalculateFoodTrackerTotal(int countToTransfer, List<Thing> things)
        {
            float ftValue = 0f;
            int num = countToTransfer;

            foreach (Thing thing in things)
            {
                if (num <= 0)
                    break;

                if (thing == null)
                    continue;

                CompFoodTracker tracker = thing.TryGetComp<CompFoodTracker>();

                if (tracker == null)
                    continue;

                ThingDef originalDef = null;

                if (thing.def.defName.StartsWith(DynamicMealDefFactory.Prefix))
                {
                    string originalDefName = thing.def.defName.Substring(DynamicMealDefFactory.Prefix.Length);

                    originalDef = DefDatabase<ThingDef>.GetNamedSilentFail(originalDefName);
                }

                float valuePerItem = originalDef.GetStatValueAbstract(StatDefOf.MarketValue);

                if (tracker.RemainingFractions == null)
                {
                    Log.Warning("[FoodTracker] RemainingFractions was unexpectedly null; initializing.");

                    tracker.RemainingFractions = new List<float>();
                }

                int num2 = Mathf.Min(num, thing.stackCount);

                if (num2 > 0)
                {
                    // FT item is a singleton.
                    if (tracker.PartialFraction > 0f && tracker.RemainingFractions.Count == 0)
                    {
                        ftValue += tracker.PartialFraction * valuePerItem;
                        num -= num2;

                        continue;
                    }
                    // FT item is a stack.
                    else if (tracker.PartialFraction < 0f && tracker.RemainingFractions.Count > 1)
                    {
                        for (int i = 0; i < num2; i++)
                        {
                            ftValue += tracker.RemainingFractions[i] * valuePerItem;
                        }

                        num -= num2;
                        continue;
                    }
                    // FT item is an incorrectly store stack item.
                    else if (tracker.PartialFraction < 0f && tracker.RemainingFractions.Count == 1)
                    {
                        ftValue += tracker.RemainingFractions[0] * valuePerItem;
                        num -= num2;

                        tracker.PartialFraction = tracker.RemainingFractions[0];
                        tracker.RemainingFractions.Clear();

                        continue;
                    }
                    else
                    {
                        ftValue += valuePerItem;
                        num -= num2;
                    }
                }
            }
            return ftValue;
        }
    }
}