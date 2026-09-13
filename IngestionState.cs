using System.Collections.Generic;
using Verse;
using static RimWorld.FleshTypeDef;

namespace FoodTracker
{

    // Snapshot of ingestion job 
    public class IngestionState
    {
        // Internal ID to track each ingestion.
        public int TraceID;
        public bool HandlingInterruption;
        public List<float> RemainingFractionsBefore;
        public List<ThingDef> IngredientsBefore;
        public List<Thing> ThingsToDestroy = new List<Thing>();

        // Captured during Prefix while the Food Thing is still reliable.
        public Pawn Pawn;
        public bool IsDrug;
        public Thing PostIngestObject;
        public ThingDef ObjectDef;
        public ThingDef ObjectGameDef;
        public ThingDef ObjectTrackerDef;
        public int IngestCount;
        public int PreStackCount;
        public int ThingID;
        public float TotalFraction;

        // Captured after vanilla initializes the toil.
        public int StartTick;
        public int TotalTicks;

        // Runtime state.
        public float IngestedFraction;
        public bool DestroyFoodAfterIngestion;
    }

    public static class FoodTrackerIngestionTracker
    {
        private static readonly Dictionary<Pawn, IngestionState> active = new Dictionary<Pawn, IngestionState>();

        public static void Register(IngestionState state)
        {
            if (state?.Pawn == null)
                return;

            active[state.Pawn] = state;
        }

        public static bool TryGet(Pawn pawn, out IngestionState state)
        {
            bool found = active.TryGetValue(pawn, out state);

            return found;
        }

        public static void Remove(Pawn pawn)
        {
            if (pawn != null)
                active.Remove(pawn);
        }


        public static bool IsBeingIngested(Thing thing, out IngestionState state)
        {
            state = null;
            if (thing == null) return false;

            foreach (var kvp in active)
            {
                if (kvp.Value != null && kvp.Value.ThingID == thing.thingIDNumber)
                {
                    state = kvp.Value;
                    return true;
                }
            }
            return false;
        }
    }
}