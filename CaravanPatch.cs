using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Verse;

namespace FoodTracker
{
    public static class CaravanPatch
    {
        // Primary lookup: Maps each TransferableOneWay to its underlying Thing instances.
        public static Dictionary<TransferableOneWay, List<Thing>> trackedTransferables = new Dictionary<TransferableOneWay, List<Thing>>();

        // Registers or updates a Thing under its corresponding TransferableOneWay.
        public static void Track(TransferableOneWay transferable, Thing thing)
        {

            if (transferable == null || thing == null)
                return;

            CompFoodTracker tracker = thing.TryGetComp<CompFoodTracker>();

            if (tracker == null)
                return;

            // Get or initialize the Thing list for this transferable
            if (!CaravanPatch.trackedTransferables.TryGetValue(transferable, out List<Thing> things))
            {
                things = new List<Thing>();
                CaravanPatch.trackedTransferables.Add(transferable, things);
                Log.Message($"[FoodTracker] Track | Created NEW Transferable tracking bucket for: {transferable.ThingDef?.defName}");
            }

            // Prevent duplicate tracking if AddToTransferables fires twice for the same Thing reference
            if (things.Contains(thing))
            {
                Log.Message($"[FoodTracker] Track | Thing Already Registered: {thing.def} (ID: {thing.thingIDNumber}");

                return;
            }

            things.Add(thing);
        }

        // Resets tracking data when a caravan dialog opens or rebuilds.
        public static void Clear()
        {
            CaravanPatch.trackedTransferables.Clear();

            Log.Message("[FoodTracker] Tracking dictionary cleared for fresh caravan session.");
        }

        public static string GetCaravanLabel(string vanillaLabel, TransferableOneWay trad)
        {
            if (trad?.AnyThing?.TryGetComp<CompFoodTracker>() != null)
            {
                return "(varies)";
            }

            // Vanilla fallback
            return vanillaLabel;
        }
    }

    [HarmonyPatch]
    public static class CaravanPatch_TransferableMatching
    {
        // Dynamically specify the exact generic instantiation: TransferableMatching<TransferableOneWay>
        public static MethodBase TargetMethod()
        {
            MethodInfo genericMethod = typeof(TransferableUtility).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == nameof(TransferableUtility.TransferableMatching) && m.IsGenericMethod);

            return genericMethod?.MakeGenericMethod(typeof(TransferableOneWay));
        }

        [HarmonyPostfix]
        public static void Postfix(Thing thing, TransferableOneWay __result)
        {
            if (thing == null || __result == null)
                return;

            // Auto-track the Thing under its matched TransferableOneWay
            CaravanPatch.Track(__result, thing);
        }
    }

    [HarmonyPatch]
    public static class CaravanPatch_CalculateAndRecacheTransferables
    {
        // Dynamically return all recalculation methods across RimWorld caravan/transfer dialogs
        public static IEnumerable<MethodBase> TargetMethods()
        {
            Type[] targetTypes = new[]
            {
            typeof(Dialog_FormCaravan),
            typeof(Dialog_SplitCaravan),
            typeof(Dialog_EnterPortal),
            typeof(Dialog_LoadTransporters)
            };

            foreach (Type type in targetTypes)
            {
                yield return AccessTools.Method(type, "CalculateAndRecacheTransferables");
            }
        }

        [HarmonyPrefix]
        public static void Prefix()
        {
            // Safe to clear before any of these dialogs rebuild their transferables
            CaravanPatch.trackedTransferables.Clear();
        }
    }
}
