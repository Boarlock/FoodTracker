using HarmonyLib;
using Verse;

namespace FoodTracker
{
    public class StackMergeState
    {
        public CompFoodTracker TargetComp;
        public CompFoodTracker SourceComp;

        public int TargetStackBefore;
        public int SourceStackBefore;
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.TryAbsorbStack))]
    public static class TryAbsorbStackPatch
    {
        public static void Prefix(Thing __instance, Thing other, out StackMergeState __state)
        {

            __state = null;

            if (__instance == null || other == null)
                return;

            // Check for FoodTracker component on either stack.
            CompFoodTracker targetComp = __instance.TryGetComp<CompFoodTracker>();
            CompFoodTracker sourceComp = other.TryGetComp<CompFoodTracker>();

            // If FT component doesn't exist for either stack then we don't need to worry about merging.
            if (targetComp == null || sourceComp == null)
                return;

            // Instantiate our Merge State
            __state = new StackMergeState
            {
                TargetComp = targetComp,
                SourceComp = sourceComp,
                TargetStackBefore = __instance.stackCount,
                SourceStackBefore = other.stackCount
            };
        }

        public static void Postfix(Thing __instance, Thing other, StackMergeState __state)
        {
            if (__state == null)
                return;

            int targetTransferred = __instance.stackCount - __state.TargetStackBefore;

            int sourceRemaining = other.Destroyed ? 0 : other.stackCount;

            int sourceTransferred = __state.SourceStackBefore - sourceRemaining;

            Log.Message(
                $"[FoodTracker] STACK MERGE | " +
                $"Target: {__instance.ThingID} " +
                $"Before: {__state.TargetStackBefore} " +
                $"After: {__instance.stackCount} " +
                $"| Source: {other.ThingID} " +
                $"Before: {__state.SourceStackBefore} " +
                $"After: {sourceRemaining} " +
                $"| T Transferred: {targetTransferred} " +
                $"S Transferred: {sourceTransferred}");
        }

    }
}
