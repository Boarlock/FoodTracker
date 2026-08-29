using System.Collections.Generic;
using Verse;

namespace FoodTracker
{

    // Snapshot of ingestion job 
    public class IngestionState
    {
        // Captured during Prefix while the Food Thing is still reliable.
        public Thing Food;
        public ThingDef MealDef;
        public ThingDef TrackerDef;
        public float NutritionAtStart;
        public float NutritionPerItem;
        public float TotalNutrition;
        public int StartingStackCount;
        public int IngestCount;

        // Captured after vanilla initializes the toil.
        public Pawn Pawn;
        public int TotalTicks;
        public float HungerAtStart;
        public IntVec3 FoodCell;

        // Runtime state.
        public float EatenFraction;
        public bool Finalized;
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