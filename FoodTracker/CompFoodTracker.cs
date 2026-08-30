using System.Collections.Generic;
using Verse;

namespace FoodTracker
{
    public class CompFoodTracker : ThingComp
    {

        public List<float> NutritionEntries = new List<float>();

    }

    public class CompProperties_FoodTracker : CompProperties
    {
        public CompProperties_FoodTracker()
        {
            compClass = typeof(CompFoodTracker);
        }
    }
}