using System.Collections.Generic;
using Verse;

namespace FoodTracker
{

    // Snapshot of ingestion job 
    public class IngestionState
    {
        // Internal ID to track each ingestion.
        public int TraceID;
        public List<float> NutritionEntriesBefore;
        public List<ThingDef> IngredientsBefore;

        // Captured during Prefix while the Food Thing is still reliable.
        public Pawn Pawn;
        public Thing Food;
        public ThingDef FoodDef;
        public ThingDef BaseDef;
        public ThingDef TrackerDef;
        public int StartingStackCount;
        public int IngestCount;
        public float TotalNutrition;
        public float NutritionPerItem;

        // Captured after vanilla initializes the toil.
        public int TotalTicks;
        public float HungerAtStart;
        public IntVec3 FoodCell;

        // Runtime state.
        public float EatenFraction;
        public bool Finalized;
        public bool DestroyFoodAfterIngestion;
        public Thing FoodToDestroy;
    }

    public static class FoodTrackerIngestionTracker
    {
        private static readonly Dictionary<Pawn, IngestionState> active =
            new Dictionary<Pawn, IngestionState>();

        public static void Register(IngestionState state)
        {
            if (state?.Pawn == null)
                return;

            active[state.Pawn] = state;
        }

        public static bool TryGet(Pawn pawn, out IngestionState state)
        {
            return active.TryGetValue(pawn, out state);
        }

        public static void Remove(Pawn pawn)
        {
            if (pawn != null)
                active.Remove(pawn);
        }
    }
}