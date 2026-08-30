using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FoodTracker
{
    public class FoodTrackerMod : Mod
    {

        public FoodTrackerMod(ModContentPack content) : base(content)
        {
            // 1. Initialize Settings
            settings = GetSettings<FoodTrackerSettings>();

            // 2. Initialize Harmony
            var harmony = new Harmony("b0arl0ck.foodtracker");
            harmony.PatchAll();

            // 3. Log Initialization
            Log.Message($"[FoodTracker][T0] Initialization completed.");

        }
        public int TraceId { get; set; }
        public static FoodTrackerSettings settings;

        public override string SettingsCategory() => "Food Tracker";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("Enable Developer Logging", ref settings.verboseLogging);
            listing.End();
            base.DoSettingsWindowContents(inRect);
        }
    }
}