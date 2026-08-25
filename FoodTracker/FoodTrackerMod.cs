using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FoodTracker
{
    public class FoodTrackerMod : Mod
    {
        public static FoodTrackerSettings settings;

        public FoodTrackerMod(ModContentPack content) : base(content)
        {
            // 1. Initialize Settings
            settings = GetSettings<FoodTrackerSettings>();

            // 2. Initialize Harmony
            var harmony = new Harmony("b0arl0ck.FoodTracker");
            harmony.PatchAll();

            // 3. Log Initialization
            if (FoodTrackerSettings.Verbose)
            {
                Log.Message($"[FoodTracker] Initialization completed.");
            }

        }

        public override string SettingsCategory() => "Food Tracker - Partial Meals";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("Enable Verbose/Developer Logging", ref settings.verboseLogging);
            listing.End();
            base.DoSettingsWindowContents(inRect);
        }
    }
}