using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace FoodTracker
{
    [HarmonyPatch(typeof(Toils_Ingest), nameof(Toils_Ingest.FinalizeIngest))]
    public static class Patch_Toils_Ingest_FinalizeIngest
    {

        public static void Postfix(
            Toil __result,
            Pawn ingester,
            TargetIndex ingestibleInd)
        {

            // Save vanilla's completion action before replacing the toil's initialization callback.
            Action vanillaFinalize = __result.initAction;

            // Wrap the callback so partial meals can be finalized with their remaining nutrition.
            __result.initAction = () =>
            {

                // Get the food Thing and Pawn for use later.
                Thing food = ingester.CurJob?.GetTarget(ingestibleInd).Thing;

                // If the food Thing is not tracked by FoodTracker or null, retain vanilla's normal completion behavior.
                if (food == null || !FoodTrackerMeal.IsTracked(food))
                {

                    if (FoodTrackerSettings.Verbose)
                    {
                        Log.Message($"[FoodTracker] {food?.Label ?? "NULL"} is not tracked, letting vanilla handle it. Food ID: {food?.thingIDNumber ?? 0}, Food Def: {food?.def.defName ?? "NULL"}");
                    }

                    vanillaFinalize?.Invoke();
                    return;
                }

                Pawn pawn = ingester;
                float remainingNutrition = FoodTrackerMeal.GetRemainingNutrition(food);

                // Credit the remaining nutrition and matching record.
                FoodTrackerMeal.ApplyNutritionToPawn(pawn, remainingNutrition);

                // Reproduce vanilla mood memories for eating without a table or in an impressive dining room.
                if (pawn.needs.mood != null && food.def.IsNutritionGivingIngestible && food.def.ingestible.chairSearchRadius > 10f)
                {
                    if (!(pawn.Position + pawn.Rotation.FacingCell).HasEatSurface(pawn.Map) && pawn.GetPosture() == PawnPosture.Standing && !pawn.IsWildMan() && food.def.ingestible.tableDesired)
                    {
                        pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.AteWithoutTable);
                    }
                    Room room = pawn.GetRoom();
                    if (room != null)
                    {
                        int scoreStageIndex = RoomStatDefOf.Impressiveness.GetScoreStageIndex(room.GetStat(RoomStatDefOf.Impressiveness));
                        if (ThoughtDefOf.AteInImpressiveDiningRoom.stages[scoreStageIndex] != null)
                        {
                            pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtMaker.MakeThought(ThoughtDefOf.AteInImpressiveDiningRoom, scoreStageIndex));
                        }
                    }
                }

                // Destroy the food Thing to prevent further consumption.
                if (food != null && !food.Destroyed)

                    if (FoodTrackerSettings.Verbose)
                    {
                        Log.Message($"[FoodTracker] Destroying {food.Label}, nutrition has been exhausted. Nutrition Consumed: " +
                            $"{remainingNutrition:F3}, Food ID: {food.thingIDNumber}, Food Def: {food.def.defName}.");
                    }

                    food.Destroy(DestroyMode.Vanish);

            };
        }
    }
}