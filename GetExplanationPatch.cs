using HarmonyLib;
using RimWorld;
using System.Diagnostics;
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

            if (optionalReq.Thing == null)
                return;

            Thing thing = optionalReq.Thing;
            CompFoodTracker tracker = thing.TryGetComp<CompFoodTracker>();

            // Check to see if Thing has our tracker and return if it doesn't.
            if (tracker == null)
                return;

            // Checking to see if it's a tracked food item or tracked drug item.
            if (__instance.stat == StatDefOf.Nutrition)
            {
                // Modify the Nutrition tooltip.
                if (FoodTrackingHelpers.GetDrugType(thing.def) == FoodTrackerDrugEffects.FoodTrackerDrugType.Unsupported)
                {
                    float nutritionPerItem;
                    float nutrition;

                    // Build the 'Base value: ' text on the GetExplanation screen and the default text that shows.
                    string descriptionBaseText = "How nutritious this food is.\n\nBase value: ";

                    // Get the base nutrition for the Thing and change it into our formatted string then append the descriptor base text.
                    float baseValue = thing.def.GetStatValueAbstract(StatDefOf.Nutrition);
                    string baseValueString = baseValue.ToString("N2");
                    string baseFinal = descriptionBaseText + baseValueString + "\n\n";

                    // If tracked item is a singleton.
                    if (tracker.RemainingFractions.Count == 0)
                    {
                        // Get the base nutrition per item, multiply that with tracked fraction.
                        nutritionPerItem = thing.def.GetStatValueAbstract(StatDefOf.Nutrition);
                        float totalFraction = tracker.PartialFraction;
                        nutrition = totalFraction * nutritionPerItem;

                        // Build "Final value: X" string.
                        string single = $"Final Value: {nutrition:N2}";
                        __result = baseFinal + single;

                        return;
                    }
                    // If tracked item is a stack.
                    else
                    {
                        // Get the base nutrition per item, and initialize string builder.
                        nutritionPerItem = thing.def.GetStatValueAbstract(StatDefOf.Nutrition);
                        StringBuilder itemList = new StringBuilder();
                        float partialFraction;

                        for (int i = 0; i < tracker.RemainingFractions.Count; i++)
                        {
                            partialFraction = tracker.RemainingFractions[i];
                            nutrition = partialFraction * nutritionPerItem;

                            itemList.AppendLine($"Item {i + 1}: {nutrition:0.00}");
                        }

                        __result = baseFinal + itemList.ToString();

                        return;
                    }
                }
            }
            // It is a tracked item but not a food item.
            else if (__instance.stat == StatDefOf.Mass)
            {
                __result = GetMassInspectDescription(thing, tracker);
            }
            // If the player is looking at market value tooltip.
            else if (__instance.stat == StatDefOf.MarketValue)
            {
                __result = GetValueInspectDescription(thing, tracker);
            }
            
        }

        private static string GetValueInspectDescription(Thing thing, CompFoodTracker tracker)
        {
            float valuePerItem;
            float value;

            // Build the 'Base value: ' text on the GetExplanation screen and the default text that shows.
            string descriptionBaseText = "The market value of an object.\n\nThe actual trade price will be adjusted by negotiation skill, " +
                "relationship status, and other contextual factors.\n\nBase value: $";

            // Get the base nutrition for the Thing and change it into our formatted string then append the descriptor base text.
            float baseValue = thing.def.GetStatValueAbstract(StatDefOf.MarketValue);
            string baseValueString = baseValue.ToString("N2");
            string baseFinal = descriptionBaseText + baseValueString + "\n\n";

            string __result;

            // If tracked item is a singleton.
            if (tracker.RemainingFractions.Count == 0)
            {
                // Get the base value for the Thing, multiply that with tracked fraction.
                valuePerItem = thing.def.GetStatValueAbstract(StatDefOf.MarketValue);
                float totalFraction = tracker.PartialFraction;
                value = totalFraction * valuePerItem;

                // Build "Final value: X" string.
                string single = $"Final Value: ${value:N2}";

                __result = baseFinal + single;

            }
            // If tracked item is a stack.
            else
            {
                // Get the base value for the Thing, and initialize string builder.
                valuePerItem = thing.def.GetStatValueAbstract(StatDefOf.MarketValue);
                StringBuilder itemList = new StringBuilder();
                float partialFraction;

                for (int i = 0; i < tracker.RemainingFractions.Count; i++)
                {
                    partialFraction = tracker.RemainingFractions[i];
                    value = partialFraction * valuePerItem;

                    itemList.AppendLine($"Item {i + 1}: ${value:0.00}");
                }

                __result = baseFinal + itemList.ToString();

            }

            return __result;

        }

        private static string GetMassInspectDescription(Thing thing, CompFoodTracker tracker)
        {
            float massPerItem;
            float mass;

            // Build the 'Base value: ' text on the GetExplanation screen and the default text that shows.
            string descriptionBaseText = "The physical mass of an object or creature.\n\nBase value: ";

            // Get the base nutrition for the Thing and change it into our formatted string then append the descriptor base text.
            float baseValue = thing.def.GetStatValueAbstract(StatDefOf.Mass);
            string baseValueString = baseValue.ToString("N2");
            string baseFinal = descriptionBaseText + baseValueString + " kg\n\n";

            string __result;

            // If tracked item is a singleton.
            if (tracker.RemainingFractions.Count == 0)
            {
                // Get the base mass for the Thing, multiply that with tracked fraction.
                massPerItem = thing.def.GetStatValueAbstract(StatDefOf.Mass);
                float totalFraction = tracker.PartialFraction;
                mass = totalFraction * massPerItem;

                // Build "Final value: X" string.
                string single = $"Final Value: {mass:N2} kg";

                __result = baseFinal + single;

            }
            // If tracked item is a stack.
            else
            {
                // Get the base mass for the Thing, and initialize string builder.
                massPerItem = thing.def.GetStatValueAbstract(StatDefOf.Mass);
                StringBuilder itemList = new StringBuilder();
                float partialFraction;

                for (int i = 0; i < tracker.RemainingFractions.Count; i++)
                {
                    partialFraction = tracker.RemainingFractions[i];
                    mass = partialFraction * massPerItem;

                    itemList.AppendLine($"Item {i + 1}: {mass:0.00} kg");
                }

                __result = baseFinal + itemList.ToString();

            }

            return __result;

        }
    }

    [HarmonyPatch(typeof(StatWorker), nameof(StatWorker.GetStatDrawEntryLabel))]

    // Patch for the left side of the inspect screen so it displays the actual nutrition, mass, or market value represented by the tracked fractions.
    public static class GetStatDrawEntryLabelPatch
    {
        public static bool Prefix(StatDef stat, ToStringNumberSense numberSense, StatRequest optionalReq, bool finalized, ref string __result)
        {
            // Only modify Nutrition, Mass, and Market Value.
            if (stat != StatDefOf.Nutrition && stat != StatDefOf.Mass && stat != StatDefOf.MarketValue)
                return true;

            if (!optionalReq.HasThing)
                return true;

            Thing thing = optionalReq.Thing;
            CompFoodTracker tracker = thing.TryGetComp<CompFoodTracker>();

            // Check to see if Thing has our tracker and return if it doesn't.
            if (tracker == null)
                return true;

            // Tracked drugs don't have a Nutrition stat.
            if (stat == StatDefOf.Nutrition &&
                FoodTrackingHelpers.GetDrugType(thing.def) != FoodTrackerDrugEffects.FoodTrackerDrugType.Unsupported)
                return true;

            // Get the total normalized fraction represented by this Thing.
            float totalFraction;

            // Singleton item.
            if (tracker.RemainingFractions.Count == 0)
            {
                totalFraction = tracker.PartialFraction;
            }
            // Stack item.
            else
            {
                totalFraction = 0f;

                for (int i = 0; i < tracker.RemainingFractions.Count; i++)
                    totalFraction += tracker.RemainingFractions[i];
            }

            // Convert the normalized fraction into the actual physical stat value.
            float baseValue = thing.def.GetStatValueAbstract(stat);
            float finalValue = totalFraction * baseValue;

            __result = stat.ValueToString(finalValue, numberSense, finalized);

            return false;
        }
    }
}