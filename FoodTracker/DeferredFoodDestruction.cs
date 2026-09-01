using Verse;

namespace FoodTracker
{
    internal class DeferredFoodDestruction
    {
        public static void Schedule(Thing food)
        {

            if (food == null)
                return;

            CompFoodTracker tracker = food.TryGetComp<CompFoodTracker>();

            if (tracker != null)
            {
                tracker.NutritionEntries.Clear();
            }

            if (food.Destroyed != true)
                food.Destroy(DestroyMode.Vanish);

        }
    }
}
