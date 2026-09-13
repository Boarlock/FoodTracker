using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using static RimWorld.Planet.DaysWorthOfFoodCalculator;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using Verse;

namespace FoodTracker
{
    
    public class FoodPatch
    {
        public int Total;
        public float TotalFractions;

        public FoodPatch Clone()
        {
            return (FoodPatch)this.MemberwiseClone();
        }
    }

    [HarmonyPatch(typeof(DaysWorthOfFoodCalculator), "ApproxDaysWorthOfFood", new[] { typeof(List<Pawn>), typeof(List<ThingDefCount>), typeof(PlanetTile),
        typeof(IgnorePawnsInventoryMode), typeof(Faction), typeof(WorldPath), typeof(float), typeof(int), typeof(bool)})]
    public static class FoodPatch_DaysWorthOfFoodCalculator
    {
        private static int runCounter = 0;

        private delegate int BestEverEdibleFoodIndexFor(Pawn pawn, List<ThingDefCount> food);
        private static BestEverEdibleFoodIndexFor CallBestEverEdibleFoodIndexFor;

        [HarmonyPrefix]
        public static bool Prefix(
            List<Pawn> pawns,
            List<ThingDefCount> extraFood,
            PlanetTile tile,
            IgnorePawnsInventoryMode ignoreInventory,
            Faction faction,
            WorldPath path,
            float nextTileCostLeft,
            int caravanTicksPerMove,
            bool assumeCaravanMoving,
            ref float __result,
            List<ThingDefCount> ___tmpFood,
            List<float> ___cachedMaxFoodLevel,
            List<float> ___cachedNutritionBetweenHungryAndFed,
            List<int> ___cachedTicksUntilHungryWhenFed,
            List<float> ___tmpDaysWorthOfFoodForPawn,
            List<ThingDefCount> ___tmpFood2,
            List<Pawn> ___tmpLactatingPawns,
            List<(PlanetTile, int)> ___tmpTicksToArrive,
            HashSet<Pawn> ___babiesWithFeeders)

        {
            runCounter++;
            int currentRunId = runCounter;

            // BestEverEdibleFoodIndexFor is private in vanilla. Create the delegate once,
            // then reuse it for every food-consumption check.
            if (CallBestEverEdibleFoodIndexFor == null)
            {
                MethodInfo methodInfo = AccessTools.Method(typeof(DaysWorthOfFoodCalculator), "BestEverEdibleFoodIndexFor", new[] { typeof(Pawn), typeof(List<ThingDefCount>) });
                CallBestEverEdibleFoodIndexFor = AccessTools.MethodDelegate<BestEverEdibleFoodIndexFor>(methodInfo);
            }

            // No food-eating pawns means the caravan has effectively infinite food.
            if (!AnyFoodEatingPawn(pawns))
            {
                __result = 600f;
                return false;
            }

            // A stationary caravan does not use travel-time calculations.
            if (!assumeCaravanMoving)
            {
                path = null;
            }

            // DICTIONARY CREATION, HASHSET CREATION
            // FOODTRACKER LOGIC BEGINS HERE
            // Dictionary to store each ThingDef, it's total count and total fractional amount.
            Dictionary<ThingDef, FoodPatch> ftFoodTotalsBaseline = new Dictionary<ThingDef, FoodPatch>();

            // HashSet to not add duplicate Thing ID's to the simulations
            HashSet<int> addedThingIDs = new HashSet<int>();

            // Collect food from the explicit extra food list and from pawn inventories.
            ___tmpFood.Clear();

            // Extra food we don't need to account for as it has the same source from our transferables.
            if (extraFood != null)
            {
                for (int extraFoodIndex = 0; extraFoodIndex < extraFood.Count; extraFoodIndex++)
                {
                    ThingDefCount foodCount = extraFood[extraFoodIndex];
                    if (foodCount.ThingDef.IsNutritionGivingIngestible && foodCount.Count > 0)
                    {
                        ___tmpFood.Add(foodCount);
                    }
                }
            }

            // We need to account for food in pawn's inventories.
            for (int pawnIndex = 0; pawnIndex < pawns.Count; pawnIndex++)
            {
                Pawn pawn = pawns[pawnIndex];
                if (InventoryCalculatorsUtility.ShouldIgnoreInventoryOf(pawn, ignoreInventory))
                {
                    continue;
                }

                ThingOwner<Thing> inventory = pawn.inventory.innerContainer;
                for (int i = 0; i < inventory.Count; i++)
                {
                    Thing thing = inventory[i];
                    
                    if (thing.def.IsNutritionGivingIngestible)
                    {
                        ___tmpFood.Add(new ThingDefCount(thing.def, thing.stackCount));
                    }

                    CompFoodTracker tracker = thing.TryGetComp<CompFoodTracker>();

                    if (tracker == null)
                        continue;

                    if (!addedThingIDs.Add(thing.thingIDNumber))
                        continue;

                    if (ftFoodTotalsBaseline.TryGetValue(thing.def, out FoodPatch totals))
                    {
                        if (tracker.RemainingFractions.Count > 0)
                        {
                            for (int j = 0; j < tracker.RemainingFractions.Count; j++)
                            {
                                totals.TotalFractions += tracker.RemainingFractions[j];
                                totals.Total++;
                            }
                        }
                        else
                        {
                            totals.TotalFractions += tracker.PartialFraction;
                            totals.Total++;
                        }
                    }
                    else
                    {
                        FoodPatch newTotal = new FoodPatch
                        {
                            Total = 0,
                            TotalFractions = 0f
                        };

                        if (tracker.RemainingFractions.Count > 0)
                        {
                            for (int j = 0; j < tracker.RemainingFractions.Count; j++)
                            {
                                newTotal.TotalFractions += tracker.RemainingFractions[j];
                                newTotal.Total++;
                            }
                        }
                        else
                        {
                            newTotal.TotalFractions += tracker.PartialFraction;
                            newTotal.Total++;
                        }

                        ftFoodTotalsBaseline.Add(thing.def, newTotal);
                    }
                }
            }

            // Merge duplicate food definitions so each definition has one total count.
            ___tmpFood2.Clear();
            ___tmpFood2.AddRange(___tmpFood);
            ___tmpFood.Clear();

            for (int i = 0; i < ___tmpFood2.Count; i++)
            {
                ThingDefCount copiedFood = ___tmpFood2[i];
                bool foodDefinitionAlreadyAdded = false;
                for (int j = 0; j < ___tmpFood.Count; j++)
                {
                    ThingDefCount existingFood = ___tmpFood[j];
                    if (existingFood.ThingDef == copiedFood.ThingDef)
                    {
                        ___tmpFood[j] = existingFood.WithCount(existingFood.Count + copiedFood.Count);
                        foodDefinitionAlreadyAdded = true;
                        break;
                    }
                }

                if (!foodDefinitionAlreadyAdded)
                {
                    ___tmpFood.Add(copiedFood);
                }
            }

            // Prepare one accumulated food-days value for each pawn.
            ___tmpDaysWorthOfFoodForPawn.Clear();
            for (int pawnIndex = 0; pawnIndex < pawns.Count; pawnIndex++)
            {
                ___tmpDaysWorthOfFoodForPawn.Add(0f);
            }

            // Calculate where the caravan will be at each point in the simulation.
            int ticksAbs = Find.TickManager.TicksAbs;
            ___tmpTicksToArrive.Clear();
            if (path != null && path.Found)
            {
                CaravanArrivalTimeEstimator.EstimatedTicksToArriveToEvery(tile, path.LastNode, path, nextTileCostLeft, caravanTicksPerMove, ticksAbs, ___tmpTicksToArrive);
            }

            // Cache each pawn's food-need values. The lists must remain aligned with pawns.
            ___cachedNutritionBetweenHungryAndFed.Clear();
            ___cachedTicksUntilHungryWhenFed.Clear();
            ___cachedMaxFoodLevel.Clear();
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn.RaceProps.EatsFood && pawn.needs.food != null)
                {
                    Need_Food foodNeed = pawn.needs.food;
                    ___cachedNutritionBetweenHungryAndFed.Add(foodNeed.NutritionBetweenHungryAndFed);
                    ___cachedTicksUntilHungryWhenFed.Add(foodNeed.TicksUntilHungryWhenFedIgnoringMalnutrition);
                    ___cachedMaxFoodLevel.Add(foodNeed.MaxLevel);
                }
                else
                {
                    ___cachedNutritionBetweenHungryAndFed.Add(0f);
                    ___cachedTicksUntilHungryWhenFed.Add(0);
                    ___cachedMaxFoodLevel.Add(0f);
                }
            }

            float previousDaysWorthOfFood = 0f;
            float currentDaysWorthOfFood = 0f;
            float accumulatedForageTicks = 0f;
            bool ranOutOfFood = false;
            WorldGrid worldGrid = Find.WorldGrid;

            // Remove babies who already have an available feeder from the food calculation.
            ___babiesWithFeeders.Clear();
            ___tmpLactatingPawns.Clear();
            ___tmpLactatingPawns.AddRange(pawns);
            ___tmpLactatingPawns.RemoveAll((Pawn mom) => !ChildcareUtility.CanBreastfeed(mom, out var _));

            // Vanilla adds one extra feeder slot for each lactating pawn.
            int initialLactatingPawnCount = ___tmpLactatingPawns.Count;
            for (int lactatingPawnIndex = 0; lactatingPawnIndex < initialLactatingPawnCount; lactatingPawnIndex++)
            {
                ___tmpLactatingPawns.Add(___tmpLactatingPawns[lactatingPawnIndex]);
            }

            foreach (Pawn baby in pawns)
            {
                if (ChildcareUtility.CanSuckle(baby, out var _))
                {
                    int feederIndex = ___tmpLactatingPawns.FindIndex((Pawn feeder) => ChildcareUtility.CanMomBreastfeedBaby(feeder, baby, out var _) && baby.mindState.AutofeedSetting(feeder) != AutofeedMode.Never);
                    if (feederIndex >= 0)
                    {
                        ___tmpLactatingPawns[feederIndex] = null;
                        ___babiesWithFeeders.Add(baby);
                    }
                }
            }

            // THINGDEF HASHSET CREATION, TRANSFERABLE POPULATION
            // FOODTRACKER LOGIC BEGINS HERE
            // Collect FT ThingDefs into a HashSet for O(1) fast lookups. HashSet eliminates the need to index or iterate over thingDefList later.
            HashSet<ThingDef> ftThingDefs = new HashSet<ThingDef>();

            // Compile each ThingDef in ___tmpFood.
            foreach (ThingDefCount item in ___tmpFood)
            {
                if (item.ThingDef.defName.StartsWith(DynamicMealDefFactory.Prefix))
                {
                    ftThingDefs.Add(item.ThingDef);
                }
            }

            // Loop through trackedTransferables to track all possible FT meals that can exist in a given simulation.
            foreach (var pair in CaravanPatch.trackedTransferables)
            {
                List<Thing> things = pair.Value;

                if (things == null)
                    continue;

                for (int i = 0; i < things.Count; i++)
                {
                    Thing thing = things[i];

                    if (!addedThingIDs.Add(thing.thingIDNumber))
                        continue;

                    // Fast O(1) hash set check.
                    if (ftThingDefs.Contains(thing.def))
                    {
                        // If our dictionary contains this ThingDef.
                        if (ftFoodTotalsBaseline.TryGetValue(thing.def, out FoodPatch totals))
                        {

                            CompFoodTracker tracker = thing.TryGetComp<CompFoodTracker>();

                            if (tracker != null)
                            {
                                // Add tracked nutrition data to the total.
                                if (tracker.RemainingFractions.Count > 0)
                                {
                                    for (int j = 0; j < tracker.RemainingFractions.Count; j++)
                                    {
                                        totals.TotalFractions += tracker.RemainingFractions[j];
                                        totals.Total++;
                                    }
                                }
                                else
                                {
                                    totals.TotalFractions += tracker.PartialFraction;
                                    totals.Total++;
                                }
                            }

                            continue;
                        }
                        // If our dictionary doesn't contain this ThingDef add it.
                        else
                        {
                            FoodPatch newTotal = new FoodPatch
                            {
                                Total = 0,
                                TotalFractions = 0f
                            };

                            CompFoodTracker tracker = thing.TryGetComp<CompFoodTracker>();

                            if (tracker != null)
                            {
                                // Add tracked nutrition data to the total.
                                if (tracker.RemainingFractions.Count > 0)
                                    for (int j = 0; j < tracker.RemainingFractions.Count; j++)
                                    {
                                        newTotal.TotalFractions += tracker.RemainingFractions[j];
                                        newTotal.Total++;
                                    }
                                else
                                {
                                    newTotal.TotalFractions += tracker.PartialFraction;
                                    newTotal.Total++;
                                }
                            }

                            ftFoodTotalsBaseline.Add(thing.def, newTotal);
                        }
                    }
                }
            }

            // WORKING DICTIONARY CREATION
            // FOODTRACKER LOGIC BEGINS HERE
            Dictionary<ThingDef, FoodPatch> ftFoodTotalWorkingSet = ftFoodTotalsBaseline.ToDictionary(entry => entry.Key, entry => entry.Value.Clone());

            // Simulate food consumption one food-day at a time until every pawn is fed,
            // food runs out, or the result exceeds the vanilla upper bound.
            bool foodWasConsumed;

            do
            {

                foodWasConsumed = false;
                int ticksAtCurrentFoodDay = ticksAbs + (int)(currentDaysWorthOfFood * 60000f);
                PlanetTile tileAtCurrentTime = path != null
                    ? CaravanArrivalTimeEstimator.TileIllBeInAt(ticksAtCurrentFoodDay, ___tmpTicksToArrive, ticksAbs)
                    : tile;
                bool isRestingAtTile = CaravanNightRestUtility.WouldBeRestingAt(tileAtCurrentTime, ticksAtCurrentFoodDay);
                float progressPerTick = ForagedFoodPerDayCalculator.GetProgressPerTick(assumeCaravanMoving && !isRestingAtTile, isRestingAtTile);
                float ticksPerForageInterval = 1f / progressPerTick;
                bool canEatVirtualPlants = VirtualPlantsUtility.EnvironmentAllowsEatingVirtualPlantsAt(tileAtCurrentTime, ticksAtCurrentFoodDay);

                // Convert newly simulated food-days into foraged food when appropriate.
                float additionalFoodDays = currentDaysWorthOfFood - previousDaysWorthOfFood;
                if (additionalFoodDays > 0f)
                {
                    accumulatedForageTicks += additionalFoodDays * 60000f;
                    if (accumulatedForageTicks >= ticksPerForageInterval)
                    {
                        BiomeDef primaryBiome = worldGrid[tileAtCurrentTime].PrimaryBiome;

                        // Amount and type of food vanilla is adding.
                        int foragedFoodCount = Mathf.RoundToInt(ForagedFoodPerDayCalculator.GetForagedFoodCountPerInterval(pawns, primaryBiome, faction));
                        ThingDef foragedFood = primaryBiome.foragedFood;

                        // Bool for FT foods.
                        bool isFTFood = false;

                        if (foragedFood.defName.StartsWith(DynamicMealDefFactory.Prefix))
                        {
                            isFTFood = true;
                        }

                        while (accumulatedForageTicks >= ticksPerForageInterval)
                        {

                            accumulatedForageTicks -= ticksPerForageInterval;

                            if (foragedFoodCount <= 0)
                            {
                                continue;
                            }

                            if (isFTFood)
                            {
                                float randomFraction = 0f;

                                if (ftFoodTotalWorkingSet.TryGetValue(foragedFood, out FoodPatch totals))
                                {
                                    // We generate a randomized nutrition value for each scavenged food item.
                                    for (int i = 0; i < foragedFoodCount; i++)
                                    {
                                        randomFraction = UnityEngine.Random.value;
                                        totals.TotalFractions += randomFraction;
                                        totals.Total++;
                                    }
                                }
                                else
                                {
                                    FoodPatch newTotal = new FoodPatch
                                    {
                                        Total = 0,
                                        TotalFractions = 0f
                                    };

                                    // We generate a randomized nutrition value for each scavenged food item.
                                    for (int i = 0; i < foragedFoodCount; i++)
                                    {
                                        randomFraction = UnityEngine.Random.value;
                                        newTotal.TotalFractions += randomFraction;
                                        newTotal.Total++;
                                    }

                                    ftFoodTotalWorkingSet.Add(foragedFood, newTotal);
                                }
                            }

                            bool foragedFoodAlreadyAdded = false;
                            for (int i = ___tmpFood.Count - 1; i >= 0; i--)
                            {
                                ThingDefCount existingFood = ___tmpFood[i];
                                if (existingFood.ThingDef == foragedFood)
                                {
                                    ___tmpFood[i] = existingFood.WithCount(existingFood.Count + foragedFoodCount);
                                    foragedFoodAlreadyAdded = true;
                                    break;
                                }
                            }

                            if (!foragedFoodAlreadyAdded)
                            {
                                ___tmpFood.Add(new ThingDefCount(foragedFood, foragedFoodCount));
                            }
                        }
                    }
                }

                previousDaysWorthOfFood = currentDaysWorthOfFood;

                // Give each pawn enough food to reach the current simulation point.
                for (int pawnIndex = 0; pawnIndex < pawns.Count; pawnIndex++)
                {
                    Pawn pawn = pawns[pawnIndex];
                    if (!pawn.RaceProps.EatsFood || pawn.needs?.food == null || ___babiesWithFeeders.Contains(pawn))
                    {
                        continue;
                    }

                    if (canEatVirtualPlants && VirtualPlantsUtility.CanEverEatVirtualPlants(pawn))
                    {
                        if (___tmpDaysWorthOfFoodForPawn[pawnIndex] < currentDaysWorthOfFood)
                        {
                            ___tmpDaysWorthOfFoodForPawn[pawnIndex] = currentDaysWorthOfFood;
                        }
                        else
                        {
                            ___tmpDaysWorthOfFoodForPawn[pawnIndex] += 0.45f;
                        }

                        foodWasConsumed = true;
                    }
                    else
                    {
                        float nutritionNeeded = ___cachedNutritionBetweenHungryAndFed[pawnIndex];
                        int ticksUntilHungry = ___cachedTicksUntilHungryWhenFed[pawnIndex];
                        do
                        {

                            int foodIndex = CallBestEverEdibleFoodIndexFor(pawn, ___tmpFood);

                            if (foodIndex < 0)
                            {
                                if (___tmpDaysWorthOfFoodForPawn[pawnIndex] < currentDaysWorthOfFood)
                                {
                                    ranOutOfFood = true;
                                }
                                break;
                            }

                            // Here vanilla grabs a specific food from the list.
                            ThingDefCount food = ___tmpFood[foodIndex];

                            // FOODTRACKER LOGIC BEGINS HERE
                            bool isFTFood = food.ThingDef.defName.StartsWith(DynamicMealDefFactory.Prefix);
                            FoodPatch ftTotal = null;

                            float nutritionPerItem;

                            // Get our independent FT simulation totals.
                            ftFoodTotalWorkingSet.TryGetValue(food.ThingDef, out ftTotal);

                            if (isFTFood)
                            {

                                // FT says this food does not exist in the simulation. Remove it from vanilla's working food list.
                                if (ftTotal == null || ftTotal.Total <= 0)
                                {
                                    ___tmpFood[foodIndex] = food.WithCount(0);
                                    continue;
                                }

                                // FT Total is the absolute maximum number of units that can exist
                                // in this simulation. Vanilla's working count cannot exceed it.
                                if (food.Count > ftTotal.Total)
                                {
                                    food = food.WithCount(ftTotal.Total);
                                    ___tmpFood[foodIndex] = food;
                                }

                                // Calculate an Actual Nutrition and Average Nutrition value from our independent FT totals.
                                float maxNutrition = food.ThingDef.GetStatValueAbstract(StatDefOf.Nutrition);
                                float actualNutrition = ftTotal.TotalFractions * maxNutrition;
                                float averageNutrition = actualNutrition / ftTotal.Total;

                                nutritionPerItem = Mathf.Min(averageNutrition, nutritionNeeded);

                            }
                            else
                            {
                                float maxNutrition = food.ThingDef.ingestible.CachedNutrition;

                                nutritionPerItem = Mathf.Min(maxNutrition, nutritionNeeded);
                            }

                            // Leave this calculation alone, it already factors in our modifed Nutrition Per Item.
                            float foodDaysPerItem = nutritionPerItem / nutritionNeeded * ticksUntilHungry / 60000f;

                            // Vanilla's item count calculation remains intact.
                            int itemCount = Mathf.Min(Mathf.CeilToInt(Mathf.Min(0.2f, ___cachedMaxFoodLevel[pawnIndex]) / nutritionPerItem), food.Count);

                            // Actual food-days calculation.
                            ___tmpDaysWorthOfFoodForPawn[pawnIndex] += foodDaysPerItem * itemCount;

                            // Vanilla removes itemCount from its working simulation.
                            ___tmpFood[foodIndex] = food.WithCount(food.Count - itemCount);

                            if (isFTFood)
                            {
                                // 1. Calculate the proportional fraction weight for the items being consumed
                                float averageFraction = ftTotal.Total > 0 ? (ftTotal.TotalFractions / ftTotal.Total) : 0f;
                                float fractionsToRemove = averageFraction * itemCount;

                                // 2. Update ONLY the working set copy for subsequent days in this simulation
                                ftTotal.Total -= itemCount;
                                ftTotal.TotalFractions -= fractionsToRemove;

                                // 3. Clamp to prevent underflow
                                if (ftTotal.Total <= 0 || ftTotal.TotalFractions <= 0f)
                                {
                                    ftTotal.Total = 0;
                                    ftTotal.TotalFractions = 0f;
                                }
                            }

                            foodWasConsumed = true;
                        }
                        while (___tmpDaysWorthOfFoodForPawn[pawnIndex] < currentDaysWorthOfFood);
                    }

                    if (ranOutOfFood)
                    {
                        break;
                    }

                    currentDaysWorthOfFood = Mathf.Max(currentDaysWorthOfFood, ___tmpDaysWorthOfFoodForPawn[pawnIndex]);
                }
                
            }
            while (!(!foodWasConsumed | ranOutOfFood) && !(currentDaysWorthOfFood > 601f));

            // The caravan's food supply is limited by the hungriest food-eating pawn.
            float minimumDaysWorthOfFood = 600f;
            for (int pawnIndex = 0; pawnIndex < pawns.Count; pawnIndex++)
            {
                Pawn pawn = pawns[pawnIndex];
                if (pawn.RaceProps.EatsFood && pawn.needs?.food != null && !___babiesWithFeeders.Contains(pawn))
                {
                    minimumDaysWorthOfFood = Mathf.Min(minimumDaysWorthOfFood, ___tmpDaysWorthOfFoodForPawn[pawnIndex]);
                }
            }

            __result = minimumDaysWorthOfFood;

            // The result above replaces the vanilla result, so skip the original method.
            return false;
        }
    }

    [HarmonyPatch(typeof(TransferableOneWayWidget), "DrawItemNutrition")]
    public static class FoodPatch_DrawItemNutrition
    {

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> FoodLabel_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo toString = AccessTools.Method(typeof(float), nameof(float.ToString), new[] { typeof(string) });

            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count - 2; i++)
            {
                if (codes[i].Calls(toString))
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
