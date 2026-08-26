using System.Collections.Generic;
using Verse;

namespace FoodTracker
{
    public class IngestionState
    {
        public Pawn Pawn;
        public Thing Food;
        public int TotalTicks;
        public float NutritionAtStart;
        public float NutritionPerItem;
        public float HungerAtStart;
        public int IngestCount;
        public int StartingStackCount;
        public IntVec3 FoodCell;
        public ThingDef MealDef;
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