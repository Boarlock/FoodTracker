using Verse;
using System.Collections.Generic;

namespace FoodTracker
{
    internal class DeferredFoodDestruction
    {
        public static void Schedule(List<Thing> food)
        {

            if (food == null)
                return;

            CompFoodTracker tracker = food[0].TryGetComp<CompFoodTracker>();

            if (tracker != null)
            {
                tracker.NutritionEntries.Clear();
                tracker.PartialNutrition = -1f;
            }

            foreach (Thing thing in food) 
            {
                if (thing != null && !thing.Destroyed)
                    thing.Destroy();


            }
        }
    }
}
