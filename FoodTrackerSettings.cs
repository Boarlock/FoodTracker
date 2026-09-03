using Verse;

namespace FoodTracker
{
    public class FoodTrackerSettings : ModSettings
    {
        // Enable or disable verbose logging for debugging purposes.
        public bool verboseLogging = false;

        // Provide a static property to access the verbose logging setting from other classes.
        public static bool Verbose =>
            FoodTrackerMod.settings != null &&
            FoodTrackerMod.settings.verboseLogging;

        // Persist the verbose logging setting across game sessions.
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(
                ref verboseLogging,
                "verboseLogging",
                false);
        }

    }
}