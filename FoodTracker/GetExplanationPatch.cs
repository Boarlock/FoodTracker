using HarmonyLib;
using RimWorld;
using System.Text;
using Verse;

namespace FoodTracker
{
    [HarmonyPatch(typeof(StatDrawEntry), nameof(StatDrawEntry.GetExplanationText))]
    public static class GetExplanationPatch
    {
        public static void Postfix(StatDrawEntry __instance, StatRequest optionalReq, ref string __result)
        {
            // Only modify the Nutrition tooltip.
            if (__instance.stat != StatDefOf.Nutrition)
                return;

            if (optionalReq.Thing == null)
                return;

            // Check to see if Thing has our tracker list.
            CompFoodTracker tracker = optionalReq.Thing.TryGetComp<CompFoodTracker>();

            if (tracker == null)
                return;

            // Build the 'Base value: X' text on the GetExplanation screen.
            string descriptionBaseText = "How nutritious this food is.\n\nBase value: ";

            // Get the nutrition per item and change it into type string.
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
}