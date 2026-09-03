using HarmonyLib;
using RimWorld;
using System.Text;
using Verse;

namespace FoodTracker
{
    [HarmonyPatch(typeof(StatDrawEntry), nameof(StatDrawEntry.GetExplanationText))]

    // Patch for the description screen when hovering over nutrition in item inspect.
    public static class GetExplanationPatch
    {
        public static void Postfix(StatDrawEntry __instance, StatRequest optionalReq, ref string __result)
        {

            // Only modify the Nutrition tooltip.
            if (__instance.stat != StatDefOf.Nutrition)
                return;

            if (optionalReq.Thing == null)
                return;

            // Check to see if Thing has our tracker and return if it doesn't.
            CompFoodTracker tracker = optionalReq.Thing.TryGetComp<CompFoodTracker>();

            if (tracker == null)
                return;

            // Build the 'Base value: ' text on the GetExplanation screen and the default text that shows.
            string descriptionBaseText = "How nutritious this food is.\n\nBase value: ";

            // Get the base nutrition for the Thing and change it into our formatted string then append the descriptor base text.
            float baseValue = optionalReq.Thing.GetStatValue(StatDefOf.Nutrition);
            string baseValueString = baseValue.ToString("N2");
            string baseFinal = descriptionBaseText + baseValueString + "\n\n";

            // SINGLETON STATE
            if (tracker.NutritionEntries.Count == 0)
            {
                string single = $"Final Value: {tracker.PartialNutrition:N2}";
                __result = baseFinal + single;

                return;
            }

            // STACK STATE
            StringBuilder mealList = new StringBuilder();

            for (int i = 0; i < tracker.NutritionEntries.Count; i++)
            {
                mealList.AppendLine(
                    $"Item {i + 1}: {tracker.NutritionEntries[i]:0.00}"
                );
            }

            __result = baseFinal + mealList.ToString();

        }
    }

    [HarmonyPatch(typeof(StatWorker), nameof(StatWorker.GetStatDrawEntryLabel))]

    // Patch for the left side of the inspect screen so it displays either the single nutrition value for singletons or the cumulative nutrition value of a stack of partials.
    public static class GetStatDrawEntryLabelPatch
    {
        public static bool Prefix(StatDef stat, ToStringNumberSense numberSense, StatRequest optionalReq, bool finalized, ref string __result)
        {

            if (stat != StatDefOf.Nutrition)
                return true;

            if (!optionalReq.HasThing)
                return true;

            Thing thing = optionalReq.Thing;

            // Check to see if Thing has our tracker and return if it doesn't.
            CompFoodTracker tracker = thing.TryGetComp<CompFoodTracker>();

            if (tracker == null)
                return true;

            float nutrition;

            // Singleton FT meal.
            if (tracker.NutritionEntries.Count == 0)
            {
                nutrition = tracker.PartialNutrition;
            }

            // Stack FT meal.
            else
            {
                nutrition = 0f;

                for (int i = 0; i < tracker.NutritionEntries.Count; i++)
                    nutrition += tracker.NutritionEntries[i];
            }

            __result = stat.ValueToString(nutrition, numberSense, finalized);

            return false;
        }
    }
}