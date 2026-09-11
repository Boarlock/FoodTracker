using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using static RimWorld.Planet.DaysWorthOfFoodCalculator;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using Verse;

namespace FoodTracker
{

    [HarmonyPatch(typeof(DaysWorthOfFoodCalculator), "ApproxDaysWorthOfFood", new[] { typeof(List<Pawn>), typeof(List<ThingDefCount>), typeof(PlanetTile),
        typeof(IgnorePawnsInventoryMode), typeof(Faction), typeof(WorldPath), typeof(float), typeof(int), typeof(bool)})]
    public static class DaysWorthOfFoodCalculatorPatch
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
            Dictionary<ThingDef, FTFoodTotal> ftFoodTotalsBaseline = new Dictionary<ThingDef, FTFoodTotal>();

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

                    if (ftFoodTotalsBaseline.TryGetValue(thing.def, out FTFoodTotal totals))
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

                        Log.Message($"[Run #{currentRunId}] INVENTORIES - ThingDef: {thing.def} - Total Count: {totals.Total} Fraction Total: {totals.TotalFractions}");
                    }
                    else
                    {
                        FTFoodTotal newTotal = new FTFoodTotal
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
                        Log.Message($"[Run #{currentRunId}] INVENTORIES - New ThingDef Added: {thing.def} - Total Count: {newTotal.Total} Fraction Total: {newTotal.TotalFractions}");

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
                        if (ftFoodTotalsBaseline.TryGetValue(thing.def, out FTFoodTotal totals))
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
                            Log.Message($"[Run #{currentRunId}] TRANSFERABLES - ThingDef: {thing.def} - Total Count: {totals.Total} Fraction Total: {totals.TotalFractions}");

                            continue;
                        }
                        // If our dictionary doesn't contain this ThingDef add it.
                        else
                        {
                            FTFoodTotal newTotal = new FTFoodTotal
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
                            Log.Message($"[Run #{currentRunId}] TRANSFERABLES - New ThingDef Added: {thing.def} - Total Count: {newTotal.Total} Fraction Total: {newTotal.TotalFractions}");

                            ftFoodTotalsBaseline.Add(thing.def, newTotal);
                        }
                    }
                }
            }

            // WORKING DICTIONARY CREATION
            // FOODTRACKER LOGIC BEGINS HERE
            Dictionary<ThingDef, FTFoodTotal> ftFoodTotalWorkingSet = ftFoodTotalsBaseline.ToDictionary(entry => entry.Key, entry => entry.Value.Clone());

            int dayCount = 0;

            foreach (var (key, value) in ftFoodTotalWorkingSet)
                Log.Message($"[Run #{currentRunId}] Day {dayCount} WORKING DICTIONARY CREATION - ThingDef: {key} - Total Count: {value.Total} - Fraction Total: {value.TotalFractions}");

            // Simulate food consumption one food-day at a time until every pawn is fed,
            // food runs out, or the result exceeds the vanilla upper bound.
            bool foodWasConsumed;
            do
            {
                dayCount++;

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
                        int foragedFoodIteration = 0;

                        // Bool for FT foods.
                        bool isFTFood = false;

                        if (foragedFood.defName.StartsWith(DynamicMealDefFactory.Prefix))
                        {
                            isFTFood = true;
                        }

                        while (accumulatedForageTicks >= ticksPerForageInterval)
                        {
                            foragedFoodIteration++;

                            accumulatedForageTicks -= ticksPerForageInterval;

                            if (foragedFoodCount <= 0)
                            {
                                continue;
                            }

                            if (isFTFood)
                            {
                                float randomFraction = 0f;

                                if (ftFoodTotalWorkingSet.TryGetValue(foragedFood, out FTFoodTotal totals))
                                {
                                    // We generate a randomized nutrition value for each scavenged food item.
                                    for (int i = 0; i < foragedFoodCount; i++)
                                    {
                                        randomFraction = UnityEngine.Random.value;
                                        totals.TotalFractions += randomFraction;
                                        totals.Total++;

                                        Log.Message($"[Run #{currentRunId}] Day {dayCount} Foraged Food {foragedFoodIteration} - ThingDef: {foragedFood} - Items Added: {foragedFoodCount} - " +
                                            $"Total Count: {totals.Total} - Fraction Total: {totals.TotalFractions} - Random Fraction Generated: {randomFraction}");
                                    }
                                }
                                else
                                {
                                    FTFoodTotal newTotal = new FTFoodTotal
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

                                        Log.Message($"[Run #{currentRunId}] Day {dayCount} Foraged Food {foragedFoodIteration} NEW THINGDEF ADDED - ThingDef: {foragedFood} - " +
                                            $"Items Added: {foragedFoodCount} - Total Count: {newTotal.Total} - Fraction Total: {newTotal.TotalFractions} - Random Fraction Generated: {randomFraction}");
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
                            FTFoodTotal ftTotal = null;

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

                                Log.Message($"[Run #{currentRunId}] Day {dayCount} PRE-COUNT CLAMP - Vanilla's Sim Count: {food.Count} - Total Count: {ftTotal.Total}");

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

                                Log.Message($"[Run #{currentRunId}] Day {dayCount} FINISHED - Remaining Total: {ftTotal.Total} - Remaining Fractions: {ftTotal.TotalFractions} - " +
                                $"Items Removed: {itemCount} - ThingDef: {food.ThingDef} - Average Fraction: {averageFraction} - Fractions Removed: {fractionsToRemove} - " +
                                $"Nutrition Per Item: {nutritionPerItem} - Nutrition Needed: {nutritionNeeded} - Vanilla's Sim Count: {food.Count}");

                            }

                            Log.Message($"[Run #{currentRunId}] Day {dayCount} FINISHED - Items Removed: {itemCount} - ThingDef: {food.ThingDef} - " +
                                $"Nutrition Per Item: {nutritionPerItem} - Nutrition Needed: {nutritionNeeded} - Vanilla's Sim Count: {food.Count}");

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

            Log.Message($"[Run #{currentRunId}] END - Total Days: {dayCount}\n");
            __result = minimumDaysWorthOfFood;

            // The result above replaces the vanilla result, so skip the original method.
            return false;
        }
    }

    /*[HarmonyPatch(typeof(CollectionsMassCalculator))]
    public static class CaravanPatch_MassUsage
    {
        // Target both overloads.
        [HarmonyPatch(nameof(CollectionsMassCalculator.MassUsage), new[] { typeof(List<ThingCount>), typeof(IgnorePawnsInventoryMode), typeof(bool), typeof(bool) })]
        [HarmonyPatch(nameof(CollectionsMassCalculator.MassUsage), new[] { typeof(ThingOwner), typeof(IgnorePawnsInventoryMode), typeof(bool), typeof(bool) })]

        // Replace the Mass stat calculation with our fractional-mass contribution.
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> PatchMassUsage(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            // Bool to track which overload we're on.
            bool isThingCountOverload = __originalMethod.GetParameters()[0].ParameterType == typeof(List<ThingCount>);

            int thingLocal = isThingCountOverload ? 3 : 2;
            int countLocal = isThingCountOverload ? 2 : 3;

            // Create target method and target field for transpiler splice.
            MethodInfo statValue = AccessTools.Method(typeof(StatExtension), nameof(StatExtension.GetStatValue), new[] { typeof(Thing), typeof(StatDef), typeof(bool), typeof(int) });
            FieldInfo massFieldInfo = AccessTools.Field(typeof(RimWorld.StatDefOf), "Mass");

            var codes = new List<CodeInstruction>(instructions);

            for (int i = 1; i < codes.Count - 7; i++)
            {

                // OLD C#: num += thing.GetStatValue(StatDefOf.Mass) * (float)count;
                // NEW C#: num += CaravanPatch.GetActualMassContribution(thing, count);
                if (codes[i + 3].Calls(statValue) && codes[i].LoadsField(massFieldInfo) && LoadsTargetLocal(codes[i - 1], thingLocal))
                {

                    // Replace IL_0083/IL_0074: (ldsfld) .StatDefOf::Mass with ldloc.2/ldloc.3.
                    codes[i] = CodeInstruction.LoadLocal(countLocal);

                    // Replace IL_0088/IL_0079: (ldc.i4.1) with .Call our method.
                    codes[i + 1] = CodeInstruction.Call(typeof(CaravanPatch), nameof(CaravanPatch.GetActualMassContribution));

                    // Remove IL_0089/IL_007a, IL_008a/IL_007b, IL_008f/IL_0080, IL_0090/IL_0081, IL_0091/IL_0082.
                    codes.RemoveRange((i + 2), 5);
                    break;
                }
            }
            return codes;
        }

        private static bool LoadsTargetLocal(CodeInstruction instruction, int localIndex)
        {
            if (localIndex == 2)
                return instruction.opcode == OpCodes.Ldloc_2;

            if (localIndex == 3)
                return instruction.opcode == OpCodes.Ldloc_3;

            return false;
        }
    }*/

    class FTFoodTotal
    {
        public int Total;
        public float TotalFractions;

        public FTFoodTotal Clone()
        {
            return (FTFoodTotal)this.MemberwiseClone();
        }
    }

    [HarmonyPatch]
    public static class TransferableUtility_MatchingPatch
    {
        // Dynamically specify the exact generic instantiation: TransferableMatching<TransferableOneWay>
        public static MethodBase TargetMethod()
        {
            MethodInfo genericMethod = typeof(TransferableUtility)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
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
    public static class MultiDialog_Recache_Patch
    {
        // Dynamically return all recalculation methods across RimWorld caravan/transfer dialogs
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Dialog_FormCaravan), "CalculateAndRecacheTransferables");

            Type splitDialog = AccessTools.TypeByName("RimWorld.Planet.Dialog_SplitCaravan");
            if (splitDialog != null)
                yield return AccessTools.Method(splitDialog, "CalculateAndRecacheTransferables");

            Type portalDialog = AccessTools.TypeByName("RimWorld.Dialog_EnterPortal");
            if (portalDialog != null)
                yield return AccessTools.Method(portalDialog, "CalculateAndRecacheTransferables");

            Type transporterDialog = AccessTools.TypeByName("RimWorld.Dialog_LoadTransporters");
            if (transporterDialog != null)
                yield return AccessTools.Method(transporterDialog, "CalculateAndRecacheTransferables");
        }

        [HarmonyPrefix]
        public static void Prefix()
        {
            // Safe to clear before any of these dialogs rebuild their transferables
            CaravanPatch.trackedTransferables.Clear();
        }
    }


    public static class CaravanPatch
    {
        // Primary lookup: Maps each TransferableOneWay to its underlying Thing instances
        public static Dictionary<TransferableOneWay, List<Thing>> trackedTransferables = new Dictionary<TransferableOneWay, List<Thing>>();

        // Resets tracking data when a caravan dialog opens or rebuilds.
        public static void Clear()
        {
            trackedTransferables.Clear();

            Log.Message("[FoodTracker] Tracking dictionary cleared for fresh caravan session.");
        }

        // Registers or updates a Thing under its corresponding TransferableOneWay.
        public static void Track(TransferableOneWay transferable, Thing thing)
        {

            if (transferable == null || thing == null)
                return;

            CompFoodTracker tracker = thing.TryGetComp<CompFoodTracker>();

            if (tracker == null)
                return;

            // Normalize nutrition values if stack count and tracked nutrition data don't match.
            CompFoodTrackerUtility.NormalizeState(thing);

            // Get or initialize the Thing bucket for this transferable
            if (!trackedTransferables.TryGetValue(transferable, out List<Thing> things))
            {
                things = new List<Thing>();
                trackedTransferables.Add(transferable, things);
                Log.Message($"[FoodTracker] Track | Created NEW Transferable tracking bucket for: {transferable.ThingDef?.defName}");
            }

            // Prevent duplicate tracking if AddToTransferables fires twice for the same Thing reference
            if (things.Contains(thing))
            {
                Log.Message($"[FoodTracker] Track | Thing Already Registered: {thing.def} (ID: {thing.thingIDNumber}");

                return;
            }

            int totalCount = 0;

            foreach (Thing t in things)
            {
                CompFoodTracker tr = t.TryGetComp<CompFoodTracker>();

                if (tr.RemainingFractions.Count > 0)
                {
                    foreach (int i in tr.RemainingFractions)
                        totalCount++;
                }
                else
                    totalCount++;
            }

            if (tracker.RemainingFractions.Count > 0)
            {
                foreach (int i in tracker.RemainingFractions)
                    totalCount++;
            }

            things.Add(thing);
            Log.Message($"[FoodTracker] Track | Registered Thing: {thing.LabelCap} (ID: {thing.thingIDNumber}) | StackCount: {thing.stackCount} | " +
                $"Total Fractions: {totalCount} | Total List Values: {things.Count} | Total Keys: {CaravanPatch.trackedTransferables.Count}");
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
                return tracker.PartialFraction > 0f ? fullMass * tracker.PartialFraction : 0f;
            }

            float totalMass = 0f;
            int limit = Mathf.Min(count, tracker.RemainingFractions.Count);

            for (int i = 0; i < limit; i++)
            {
                totalMass += fullMass * tracker.RemainingFractions[i];
            }

            return totalMass;
        }
    }
}
