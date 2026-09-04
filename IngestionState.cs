using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace FoodTracker
{

    // Snapshot of ingestion job 
    public class IngestionState
    {
        // Internal ID to track each ingestion.
        public int TraceID;
        public List<float> NutritionEntriesBefore;
        public List<ThingDef> IngredientsBefore;
        public List<Thing> ThingsToDestroy = new List<Thing>();

        // Captured during Prefix while the Food Thing is still reliable.
        public Pawn Pawn;
        public Thing PreFood;
        public Thing PostFood;
        public ThingDef FoodDef;
        public ThingDef TrackerDef;
        public int IngestCount;
        public int PreStackCount;
        public float TotalNutrition;
        public float NutritionPerItem;

        // Captured after vanilla initializes the toil.
        public int StartTick;
        public int TotalTicks;
        public float HungerAtStart;
        public IntVec3 FoodCell;

        // Runtime state.
        public JobCondition EndCondition;
        public float EatenFraction;
        public bool DestroyFoodAfterIngestion;
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