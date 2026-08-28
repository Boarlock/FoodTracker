using HarmonyLib;
using Verse;
using RimWorld;

namespace FoodTracker
{
    [HarmonyPatch(typeof(StatWorker), nameof(StatWorker.GetValue), new[] { typeof(Thing), typeof(bool), typeof(int) })]
    public static class NutritionStatPatch
    {
        static bool Prefix(StatWorker __instance, Thing thing, ref float __result, StatDef ___stat)
        {
            if (___stat != StatDefOf.Nutrition)
                return true;

            if (thing == null || thing.Destroyed)
                return true;

            if (thing.def.defName.StartsWith("FoodTracker_") == false)
                return true;

            CompPartialNutrition comp = thing.TryGetComp<CompPartialNutrition>();

            if (comp == null)
                return true;

            __result = comp.RemainingNutrition;
            return false;
        }
    }
}