using HarmonyLib;
using System;
using UnityEngine;
using Verse;
using Verse.AI;
using RimWorld;

namespace FoodTracker
{
    
    [HarmonyPatch(typeof(Toils_Ingest), nameof(Toils_Ingest.ChewIngestible))]
    public static class Patch_Toils_Ingest_ChewIngestible
    {

        // Determines if the target food is a batch food item that can be consumed in multiple portions.
        private static bool IsBatchFood(Thing food)
        {

            return food?.def?.ingestible != null && food.def.IsNutritionGivingIngestible && food.def.ingestible.maxNumToIngestAtOnce != 1;

        }

        public static void Prefix(
            Pawn chewer,
            ref float durationMultiplier,
            TargetIndex ingestibleInd)
        {

            // Get food being consumed by current job and count of items to be consumed
            Job curJob = chewer.CurJob;

            Thing food = curJob?.GetTarget(ingestibleInd).Thing;
            int ingestCount = curJob?.count ?? 0;

            if (food == null || food.Destroyed)
            {
                if (FoodTrackerSettings.Verbose)
                    Log.Message($"[FoodTracker] Inputs are not valid. Food: {food?.Label ?? "NULL"}, Food ID: " +
                        $"{food?.thingIDNumber ?? 0}, Food Def: {food?.def.defName ?? "NULL"}, Food Destroyed: {food?.Destroyed ?? false}");

                return;
            }

            float nutritionPerItem = FoodTrackerMeal.GetFullNutrition(food);

            if (nutritionPerItem <= 0f || ingestCount <= 0)
            {
                Log.Warning($"[FoodTracker] Invalid nutrition/stack: Pawn: {chewer.Label}, Food: {food?.Label ?? "NULL"} " +
                    $"Food ID: {food?.thingIDNumber}, Nutrition Per Item: {nutritionPerItem}, Count: {ingestCount}");
                
                return;
            }

            // Eating time scales with the total nutrition being consumed.
            if (IsBatchFood(food))
            {
                float totalNutrition = nutritionPerItem * ingestCount;

                durationMultiplier *= Mathf.Max(0.01f, totalNutrition / 0.9f);

                if (FoodTrackerSettings.Verbose)
                {
                    Log.Message($"[FoodTracker] Batch food detected. Scaling eating duration to {durationMultiplier:P0}");
                }

                return;
            }

            // Eating time scales with the nutrition remaining.
            if (FoodTrackerMeal.IsTracked(food))
            {
                float remainingFraction = Mathf.Clamp01(FoodTrackerMeal.GetRemainingNutrition(food) / nutritionPerItem);

                durationMultiplier *= remainingFraction;

                if (FoodTrackerSettings.Verbose)
                {
                    Log.Message($"[FoodTracker] Meal food detected. Scaling eating duration to {durationMultiplier:P0}");
                }

                return;
            }

            // Everything else uses vanilla eating duration.

        }

        // Wraps the toil so an interrupted eating action can consume nutrition proportionally.
        public static void Postfix(
            Toil __result,
            Pawn chewer,
            float durationMultiplier,
            TargetIndex ingestibleInd)
        {

            Toil toil = __result;

            // These values are populated when the toil starts and used when it finishes.
            Thing food = null;
            Pawn pawn = null;
            int startingStackCount = 0;
            int ingestCount = 0;
            float nutritionAtStart = 0f;
            float nutritionPerItem = 0f;
            int vanillaTotalTicks = 1;
            int vanillaTicksLeft = 1;
            IntVec3 foodCell = IntVec3.Invalid;

            // Preserve vanilla initialization, then capture the actual pawn and food at toil start.
            Action originalInit = toil.initAction;
            toil.initAction = () =>
            {
                originalInit?.Invoke();

                vanillaTotalTicks = Math.Max(1, chewer.jobs?.curDriver?.ticksLeftThisToil ?? 1);

                // Get the target food Thing, stack count from ground and item count to be consumed from the pawn's current job.
                Thing targetFood = chewer.CurJob?.GetTarget(ingestibleInd).Thing;
                startingStackCount = targetFood?.stackCount ?? 0;
                ingestCount = chewer.CurJob?.count ?? 0;

                // Check if the target food is a valid nutrition-giving ingestible item, if not return early.
                if (!FoodTrackerMeal.ValidateFood(chewer, targetFood))
                    return;

                // Capture the pawn and food for use in the finish action.
                pawn = chewer;
                food = targetFood;

                // Capture the starting stack count and nutrition per item for dealing with stackable food items.
                nutritionPerItem = FoodTrackerMeal.GetFullNutrition(food);

                // Capture pawn position in case food instance is destroyed before finishAction() init and we need to restore it.
                foodCell = pawn.Position;

                // Capture the remaining nutrition at the start of the eating action for proportional consumption on interruption.
                nutritionAtStart = FoodTrackerMeal.GetRemainingNutrition(food);

                if (FoodTrackerSettings.Verbose)
                {
                    Log.Message($"[FoodTracker] Eating toil has started. Pawn: {pawn.Label}, Source Stack: {startingStackCount}, Ingest Count: {ingestCount}, " +
                        $"Remaining Nutrition: {nutritionAtStart:F3}, Food: {food.Label}, Food ID: {food.thingIDNumber}, Food Def: {food.def.defName}");
                }

            };

            Action<int> originalTickIntervalAction = toil.tickIntervalAction;

            toil.tickIntervalAction = delta =>
            {
                originalTickIntervalAction?.Invoke(delta);

                vanillaTicksLeft = chewer?.jobs?.curDriver?.ticksLeftThisToil ?? 0;
            };

            // Run when the eating toil ends, including when the pawn stops eating early.
            toil.finishActions.Insert(0, () =>
            {

                float eatenFraction = Mathf.Clamp01(1f - ((float)vanillaTicksLeft / vanillaTotalTicks));

                // Check if the food reference is null before proceeding with nutrition consumption.
                if (food == null)
                {
                    
                    Log.Warning($"[FoodTracker] Food reference is null. Food Null: {food == null}, Food Destroyed: {food?.Destroyed}, " +
                        $"Food Def: {food?.def?.defName ?? "NULL"}, Food ID: {food?.thingIDNumber ?? 0}");

                    return;
                }

                // A fraction below one means the eating toil was interrupted before completion
                if (eatenFraction < 1f)
                {

                    // Rimworld may destroy original Thing, particularly when Un-Drafting a pawn eating on a stack.
                    if (food.Destroyed)
                    {
                        if (FoodTrackerSettings.Verbose)
                        {

                            Log.Message($"[FoodTracker] {food.def?.defName ?? "NULL"} has been destroyed. Attempting to recover.");

                        }

                        // Checking to see if it has a corresponding meal def.
                        if (FoodTrackerMeal.GetMealDef(food.def) != null)
                        {
                            InterruptedEatingRecovery.HandleDestroyedMeal(
                                pawn,
                                food,
                                foodCell,
                                nutritionAtStart,
                                eatenFraction);

                            return;
                        }

                        // Otherwise processing as a batch food item.
                        else
                        {
                            InterruptedEatingRecovery.HandleDestroyedBatchFood(
                                pawn,
                                food,
                                foodCell,
                                ingestCount,
                                nutritionPerItem,
                                eatenFraction);

                            return;
                        }
                    }

                    // Test if food item is stackable and calculate the number of items and nutrition eaten based on the fraction of the chewing time completed.
                    if (IsBatchFood(food))
                    {
                        // Calculate the number of items eaten and removed and nutrition eaten
                        int itemsEaten = Mathf.Clamp(Mathf.FloorToInt(eatenFraction * ingestCount), 0, ingestCount);
                        int itemsToRemove = Mathf.Min(itemsEaten, food.stackCount);
                        float nutritionOnStackEaten = itemsToRemove * nutritionPerItem;

                        // Subtract the number of items eaten from the stack count and destroy the food if the batch is empty.
                        food.stackCount -= itemsToRemove;

                        // Give the pawn and its records exactly the amount removed from the food.
                        FoodTrackerMeal.ApplyNutritionToPawn(pawn, nutritionOnStackEaten);

                        if (FoodTrackerSettings.Verbose)
                        {
                            Log.Message($"[FoodTracker] Batch food interrupted. Nutrition Eaten: {nutritionOnStackEaten:F3}, " +
                                $"Old Stack Count: {startingStackCount}, New Stack Count: {food.stackCount}, " +
                                $"Items Eaten: {itemsEaten}, Food: {food.Label}, Food ID: {food.thingIDNumber}, Food Def: {food.def.defName}");
                        }

                        // Do NOT fall through to meal component consumption, as the stackable food has already been handled.
                        return;
                    }

                    // Calculate nutrition to consume based on the fraction of the chewing time completed and the nutrition at the start of the eating action.
                    float nutritionToConsume = nutritionAtStart * eatenFraction;

                    // Calculate the actual nutrition removed from the food item, which is clamped to prevent exceeding the remaining nutrition.
                    float nutritionRemoved = FoodTrackerMeal.ConsumeNutrition(food, nutritionToConsume);

                    // Give the pawn and its records exactly the amount removed from the food.
                    FoodTrackerMeal.ApplyNutritionToPawn(pawn, nutritionRemoved);

                    // Get the remaining nutrition after consumption to determine if the food should be destroyed or replaced with a partial meal.
                    float remainingNutrition = FoodTrackerMeal.GetRemainingNutrition(food);

                    if (FoodTrackerSettings.Verbose)
                    {
                        Log.Message($"[FoodTracker] Meal food interrupted. Nutrition Eaten: {nutritionRemoved:F3}, NutritionRemaining: {remainingNutrition:F3}, " +
                            $"Food: {food.Label}, Food ID: {food.thingIDNumber}, Food Def: {food.def.defName}");
                    }

                    if (FoodTrackerSettings.Verbose)
                    {
                        Log.Message($"[FoodTracker] Replacing {food.def.defName} with {FoodTrackerMeal.GetMealDef(food.def)}, Food ID: {food.thingIDNumber}");
                    }

                    // Create a new Thing to represent the new meal, and drop it in the world.
                    Thing newFood =
                        MealReplacement.ReplaceAndDropPartialMeal(
                            pawn,
                            food,
                            remainingNutrition);

                    if (newFood == null)
                    {
                        Log.Warning($"[FoodTracker] Failed to make partial {newFood?.Label ?? "NULL"} near {pawn.Label}. Food ID: {newFood?.thingIDNumber ?? 0}, Food Def: {food?.def.defName ?? "NULL"}.");

                    }

                }
                else
                {
                    if (FoodTrackerSettings.Verbose)
                    {
                        Log.Message($"[FoodTracker] Meal effectively completed, letting vanilla finish {food.Label}. Food ID: {food.thingIDNumber}, Food Def: {food.def.defName}");
                    }

                    return;
                }
            });
        }
    }
}