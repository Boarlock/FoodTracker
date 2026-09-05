using RimWorld;
using UnityEngine;
using Verse;

namespace FoodTracker
{
    public class FoodTrackerDrugEffects
    {
        // Supported partial drug list
        public enum FoodTrackerDrugType
        {
            Unsupported,
            Beer,
            Ambrosia,
            PsychiteTea,
            Smokeleaf,
            GoJuice,
            Flake
        }

        // Caches for drug defs
        private static readonly HediffDef AlcoholHigh = DefDatabase<HediffDef>.GetNamedSilentFail("AlcoholHigh");
        private static readonly HediffDef AmbrosiaHigh = DefDatabase<HediffDef>.GetNamedSilentFail("AmbrosiaHigh");
        private static readonly HediffDef PsychiteTeaHigh = DefDatabase<HediffDef>.GetNamedSilentFail("PsychiteTeaHigh");
        private static readonly HediffDef SmokeleafHigh = DefDatabase<HediffDef>.GetNamedSilentFail("SmokeleafHigh");
        private static readonly HediffDef GoJuiceHigh = DefDatabase<HediffDef>.GetNamedSilentFail("GoJuiceHigh");
        private static readonly HediffDef FlakeHigh = DefDatabase<HediffDef>.GetNamedSilentFail("FlakeHigh");

        public static void ApplyIngestionEffects(Pawn pawn, float ingestedFraction, FoodTrackerDrugType drugType, ThingDef drugDef)
        {
            // Get drug comp and the addiction if present.
            CompProperties_Drug drugProps = drugDef.GetCompProperties<CompProperties_Drug>();
            Hediff_Addiction addiction = AddictionUtility.FindAddictionHediff(pawn, drugProps.chemical);

            Log.Message($"[FoodTracker][Ingestion] START | Pawn={pawn.LabelShort} | Drug={drugDef.defName} | Type={drugType} | " +
                $"Fraction={ingestedFraction:P2} | Chemical={drugProps.chemical?.defName ?? "NULL"} | ExistingAddiction={(addiction != null)}");

            // ============================================================
            // Generate drug-specific effect values
            // ============================================================

            float chemNeed = 0f;
            float recreationNeed = 0f;
            float restNeed = 0f;

            float highSeverity = 0f;
            HediffDef highHediffDef = null;

            float toleranceSeverity = 0f;
            float addictionSeverityOffset = 0f;

            switch (drugType)
            {
                case FoodTrackerDrugType.Beer:
                    chemNeed = 0.2f;
                    recreationNeed = 0.17f;
                    // No rest effect for alcohol
                    highSeverity = 0.15f;
                    highHediffDef = AlcoholHigh;
                    toleranceSeverity = 0.016f;
                    addictionSeverityOffset = 0.20f;
                    break;

                case FoodTrackerDrugType.Ambrosia:
                    chemNeed = 0.2f;
                    recreationNeed = 0.50f;
                    // No rest effect for ambrosia
                    highSeverity = 0.50f;
                    highHediffDef = AmbrosiaHigh;
                    toleranceSeverity = 0.032f;
                    addictionSeverityOffset = 0.10f;
                    break;

                case FoodTrackerDrugType.PsychiteTea:
                    chemNeed = 0.2f;
                    recreationNeed = 0.40f;
                    restNeed = 0.10f;
                    highSeverity = 0.75f;
                    highHediffDef = PsychiteTeaHigh;
                    toleranceSeverity = 0.030f;
                    addictionSeverityOffset = 0.15f;
                    break;

                case FoodTrackerDrugType.Smokeleaf:
                    chemNeed = 0.2f;
                    recreationNeed = 0.80f;
                    restNeed = -0.10f;
                    highSeverity = 0.50f;
                    highHediffDef = SmokeleafHigh;
                    toleranceSeverity = 0.030f;
                    addictionSeverityOffset = 0.06f;
                    break;

                case FoodTrackerDrugType.GoJuice:
                    chemNeed = 0.3f;
                    recreationNeed = 0.40f;
                    restNeed = 0.40f;
                    highSeverity = 0.50f;
                    highHediffDef = GoJuiceHigh;
                    // GoJuice has no tolerance severity
                    addictionSeverityOffset = 0.20f;
                    break;

                case FoodTrackerDrugType.Flake:
                    chemNeed = 0.3f;
                    recreationNeed = 0.70f;
                    restNeed = 0.20f;
                    highSeverity = 0.75f;
                    highHediffDef = FlakeHigh;
                    toleranceSeverity = 0.040f;
                    addictionSeverityOffset = 0.30f;
                    break;
            }

            Log.Message($"[FoodTracker][Ingestion] Generated values | ChemNeed={chemNeed:F3} | Recreation={recreationNeed:F3} | Rest={restNeed:F3} | " +
                $"High={highSeverity:F3} | HighDef={highHediffDef?.defName ?? "NULL"} | Tolerance={toleranceSeverity:F3} | AddictionSeverityOffset={addictionSeverityOffset:F3}");

            // ============================================================
            // Chemical need
            // ============================================================

            if (pawn.story?.traits?.HasTrait(TraitDefOf.DrugDesire) == true && pawn.needs?.drugsDesire != null)
            {
                float before = pawn.needs.drugsDesire.CurLevel;
                float scaledEffect = chemNeed * ingestedFraction;

                Log.Message($"[FoodTracker][Ingestion] Chemical need BEFORE | Pawn={pawn.LabelShort} | Level={before:F4} | BaseEffect={chemNeed:F4} | ScaledEffect={scaledEffect:F4}");

                float maxIncrease = pawn.needs.drugsDesire.MaxLevel - pawn.needs.drugsDesire.CurLevel;
                pawn.needs.drugsDesire.CurLevel += Mathf.Min(scaledEffect, maxIncrease);

                Log.Message($"[FoodTracker][Ingestion] Chemical need AFTER | MaxIncrease={maxIncrease:F4} | Level={pawn.needs.drugsDesire.CurLevel:F4} | Delta={(pawn.needs.drugsDesire.CurLevel - before):F4}");
            }
            else
            {
                Log.Message(
                    $"[FoodTracker][Ingestion] Chemical need SKIPPED");
            }

            // ============================================================
            // Recreation need
            // ============================================================

            if (pawn.needs?.joy != null)
            {
                float before = pawn.needs.joy.CurLevel;
                float scaledEffect = recreationNeed * ingestedFraction;

                Log.Message($"[FoodTracker][Ingestion] Recreation BEFORE | Level={before:F4} | BaseEffect={recreationNeed:F4} | ScaledEffect={scaledEffect:F4}");

                float maxIncrease = pawn.needs.joy.MaxLevel - pawn.needs.joy.CurLevel;
                pawn.needs.joy.CurLevel += Mathf.Min(scaledEffect, maxIncrease);

                Log.Message($"[FoodTracker][Ingestion] Recreation AFTER | MaxIncrease={maxIncrease:F4} | Level={pawn.needs.joy.CurLevel:F4} | Delta={(pawn.needs.joy.CurLevel - before):F4}");
            }
            else
            {
                Log.Message($"[FoodTracker][Ingestion] Recreation SKIPPED");
            }

            // ============================================================
            // Rest need
            // ============================================================

            if (pawn.needs?.rest != null)
            {
                float before = pawn.needs.rest.CurLevel;
                float scaledEffect = restNeed * ingestedFraction;

                Log.Message($"[FoodTracker][Ingestion] Rest BEFORE | Level={before:F4} | BaseEffect={restNeed:F4} | ScaledEffect={scaledEffect:F4}");

                float maxIncrease = pawn.needs.rest.MaxLevel - pawn.needs.rest.CurLevel;
                pawn.needs.rest.CurLevel += Mathf.Min(scaledEffect, maxIncrease);

                Log.Message($"[FoodTracker][Ingestion] Rest AFTER | MaxIncrease={maxIncrease:F4} | Level={pawn.needs.rest.CurLevel:F4} | Delta={(pawn.needs.rest.CurLevel - before):F4}");
            }
            else
            {
                Log.Message($"[FoodTracker][Ingestion] Rest SKIPPED");
            }

            // ============================================================
            // Psyfocus
            // ============================================================

            if (drugType == FoodTrackerDrugType.GoJuice)
            {

                Pawn_PsychicEntropyTracker tracker = pawn.psychicEntropy;

                if (tracker != null)
                {
                    float psyfocusEffect = 0.15f * ingestedFraction;
                    float before = tracker.CurrentPsyfocus;

                    Log.Message($"[FoodTracker][Ingestion] Psyfocus BEFORE | Level={before:F4} | BaseEffect=0.1500 | ScaledEffect={psyfocusEffect:F4}");

                    tracker.OffsetPsyfocusDirectly(psyfocusEffect);
                    float after = tracker.CurrentPsyfocus;

                    Log.Message($"[FoodTracker][Ingestion] Psyfocus AFTER | Level={after:F4} | Delta={(after - before):F4}");
                }
                else
                {
                    Log.Message(
                        $"[FoodTracker][Ingestion] Psyfocus SKIPPED");
                }
            }

            // ============================================================
            // High severity
            // ============================================================

            if (highSeverity > 0f && highHediffDef != null)
            {

                float baseSeverity = highSeverity;

                Log.Message($"[FoodTracker][Ingestion] High generated | Hediff={highHediffDef.defName} | BaseSeverity={baseSeverity:F4}");

                highSeverity *= ingestedFraction;

                Log.Message($"[FoodTracker][Ingestion] High fraction scaled | ScaledSeverity={highSeverity:F4}");

                bool divideByBodySize = drugType != FoodTrackerDrugType.GoJuice;
                bool applyGeneToleranceFactor = drugType != FoodTrackerDrugType.GoJuice;

                float beforeModificationHigh = highSeverity;

                AddictionUtility.ModifyChemicalEffectForToleranceAndBodySize(pawn, drugProps.chemical, ref highSeverity, applyGeneToleranceFactor, divideByBodySize);

                Log.Message($"[FoodTracker][Ingestion] High modified | Before={beforeModificationHigh:F4} | After={highSeverity:F4} | GeneToleranceFactor={applyGeneToleranceFactor} | DivideByBodySize={divideByBodySize}");

                Hediff high = HediffMaker.MakeHediff(highHediffDef, pawn);
                high.Severity = highSeverity;
                pawn.health.AddHediff(high);

                Log.Message($"[FoodTracker][Ingestion] High APPLIED | Hediff={highHediffDef.defName} | Severity={high.Severity:F4}");
            }
            else
            {
                Log.Message($"[FoodTracker][Ingestion] High SKIPPED | Severity={highSeverity:F4} | HediffDef={highHediffDef?.defName ?? "NULL"}");
            }

            // ============================================================
            // Addiction chance
            // ============================================================

            if (pawn.RaceProps.IsFlesh && drugProps.Addictive)
            {

                Log.Message($"[FoodTracker][Ingestion] Addiction check | Flesh=true | Addictive=true | ExistingAddiction={(addiction != null)}");

                if (addiction == null)
                {

                    float tolerance = AddictionUtility.FindToleranceHediff(pawn, drugProps.chemical)?.Severity ?? 0f;
                    float addictiveness = DrugStatsUtility.GetAddictivenessAtTolerance(drugDef, tolerance);

                    float baseAddictiveness = addictiveness;

                    Log.Message($"[FoodTracker][Ingestion] Addiction chance generated | Tolerance={tolerance:F4} | BaseAddictiveness={baseAddictiveness:F6} | MinTolerance={drugProps.minToleranceToAddict:F4}");

                    if (pawn.genes != null)
                    {
                        float beforeGeneFactor = addictiveness;

                        addictiveness *= pawn.genes.AddictionChanceFactor(drugProps.chemical);

                        Log.Message($"[FoodTracker][Ingestion] Addiction gene modifier | Before={beforeGeneFactor:F6} | After={addictiveness:F6}");
                    }

                    float beforeFraction = addictiveness;

                    // Scale addiction chance to the amount actually consumed.
                    addictiveness *= ingestedFraction;

                    Log.Message($"[FoodTracker][Ingestion] Addiction fraction scaled | Before={beforeFraction:F6} | After={addictiveness:F6}");

                    float roll = Rand.Value;

                    bool toleranceMet = tolerance >= drugProps.minToleranceToAddict;
                    bool addictionTriggered = roll < addictiveness && toleranceMet;

                    Log.Message($"[FoodTracker][Ingestion] Addiction roll | Roll={roll:F6} | Chance={addictiveness:F6} | ToleranceMet={toleranceMet} | Triggered={addictionTriggered}");

                    if (addictionTriggered)
                    {
                        pawn.health.AddHediff(drugProps.chemical.addictionHediff);

                        Log.Message($"[FoodTracker][Ingestion] ADDICTION APPLIED | Hediff={drugProps.chemical.addictionHediff.defName}");
                    }
                    else
                    {
                        Log.Message($"[FoodTracker][Ingestion] Addiction chance SKIPPED");
                    }
                }
                else
                {
                    Log.Message($"[FoodTracker][Ingestion] Addiction check SKIPPED | Flesh={pawn.RaceProps.IsFlesh} | Addictive={drugProps.Addictive}");
                }
            }

            // ============================================================
            // Tolerance severity
            // ============================================================

            float baseToleranceSeverity = toleranceSeverity;

            Log.Message($"[FoodTracker][Ingestion] Tolerance generated | BaseSeverity={baseToleranceSeverity:F4}");

            toleranceSeverity *= ingestedFraction;

            Log.Message($"[FoodTracker][Ingestion] Tolerance fraction scaled | Severity={toleranceSeverity:F4}");

            float beforeModificationTolerance = toleranceSeverity;

            AddictionUtility.ModifyChemicalEffectForToleranceAndBodySize(pawn, drugProps.chemical, ref toleranceSeverity, applyGeneToleranceFactor: true, divideByBodySize: true);

            Log.Message($"[FoodTracker][Ingestion] Tolerance modified | Before={beforeModificationTolerance:F4} | After={toleranceSeverity:F4} | GeneToleranceFactor=true | DivideByBodySize=true");

            if (toleranceSeverity > 0f)
            {
                Hediff tolerance = HediffMaker.MakeHediff(drugProps.chemical.toleranceHediff, pawn);

                tolerance.Severity = toleranceSeverity;
                pawn.health.AddHediff(tolerance);

                float actualTolerance = AddictionUtility.FindToleranceHediff(pawn, drugProps.chemical)?.Severity ?? 0f;

                Log.Message($"[FoodTracker][Ingestion] Tolerance APPLIED | Hediff={drugProps.chemical.toleranceHediff.defName} | AddedSeverity={tolerance.Severity:F4} | CurrentTotalTolerance={actualTolerance:F4}");
            }
            else
            {
                Log.Message($"[FoodTracker][Ingestion] Tolerance SKIPPED | FinalSeverity={toleranceSeverity:F4}");
            }

            // ============================================================
            // Existing addiction
            // ============================================================

            if (addiction != null)
            {
                // Individual addiction need
                Need_Chemical need = addiction.Need;

                if (need != null)
                {
                    float before = need.CurLevel;
                    float addictionNeed = drugProps.needLevelOffset * 0.9f;
                    float scaledEffect = addictionNeed * ingestedFraction;

                    Log.Message($"[FoodTracker][Ingestion] Addiction need BEFORE | Level={before:F4} | NeedLevelOffset={drugProps.needLevelOffset:F4} | " +
                        $"BaseEffect={addictionNeed} | ScaledEffect={scaledEffect:F4}");

                    float maxIncrease = need.MaxLevel - need.CurLevel;
                    need.CurLevel += Mathf.Min(scaledEffect, maxIncrease);

                    Log.Message($"[FoodTracker][Ingestion] Addiction need AFTER | MaxIncrease={maxIncrease:F4} | Level={need.CurLevel:F4} | Delta={(need.CurLevel - before):F4}");
                }
                else
                {
                    Log.Message($"[FoodTracker][Ingestion] Addiction need SKIPPED");
                }

                // Addiction progress/severity
                float severityBefore = addiction.Severity;

                float severityReduction = addictionSeverityOffset * ingestedFraction;

                Log.Message($"[FoodTracker][Ingestion] Addiction severity BEFORE | Severity={severityBefore:F4} | BaseReduction={addictionSeverityOffset:F4} | " +
                    $"ScaledReduction={severityReduction:F4}");

                addiction.Severity -= severityReduction;

                Log.Message($"[FoodTracker][Ingestion] Addiction severity AFTER | Severity={addiction.Severity:F4} | Delta={(addiction.Severity - severityBefore):F4}");
            }
            else
            {
                Log.Message($"[FoodTracker][Ingestion] Existing addiction effects SKIPPED");
            }

            // ============================================================
            // Overdose / Major Overdose
            // ============================================================

            if (drugType == FoodTrackerDrugType.GoJuice || drugType == FoodTrackerDrugType.Flake)
            {

                float overdoseChanceFactor = 1f;
                bool hasChemicalDependency = false;

                if (ModsConfig.BiotechActive && pawn.genes != null)
                {
                    foreach (Gene gene in pawn.genes.GenesListForReading)
                    {
                        if (gene.Active && gene.def.chemical == drugProps.chemical)
                        {
                            float before = overdoseChanceFactor;

                            overdoseChanceFactor *= gene.def.overdoseChanceFactor;

                            Log.Message($"[FoodTracker][Ingestion] OD gene modifier | Gene={gene.def.defName} | Factor={gene.def.overdoseChanceFactor:F4} | " +
                                $"CombinedBefore={before:F4} | CombinedAfter={overdoseChanceFactor:F4}");
                        }
                        if (gene is Gene_ChemicalDependency chemicalDependency && chemicalDependency.def.chemical == drugProps.chemical)
                        {
                            hasChemicalDependency = true;

                            Log.Message($"[FoodTracker][Ingestion] Chemical dependency gene found | Gene={gene.def.defName} | Chemical={drugProps.chemical.defName}");
                        }
                    }
                }

                Log.Message($"[FoodTracker][Ingestion] OD setup | ChanceFactor={overdoseChanceFactor:F4} | ChemicalDependency={hasChemicalDependency}");

                float overdoseRoll = Rand.Value;
                bool overdoseTriggered = overdoseRoll < overdoseChanceFactor;

                Log.Message($"[FoodTracker][Ingestion] OD event roll | Roll={overdoseRoll:F6} | Chance={overdoseChanceFactor:F6} | Triggered={overdoseTriggered}");

                if (overdoseTriggered)
                {
                    // Current overdose severity
                    float currentOD = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.DrugOverdose)?.Severity ?? 0f;

                    Log.Message($"[FoodTracker][Ingestion] OD current severity | Severity={currentOD:F4}");

                    // ----------------------------------------------------
                    // Major overdose
                    // ----------------------------------------------------

                    float majorOverdoseChance = drugProps.largeOverdoseChance * ingestedFraction;

                    Log.Message($"[FoodTracker][Ingestion] Major OD chance generated | BaseChance={drugProps.largeOverdoseChance:F6}| ScaledChance={majorOverdoseChance:F6}");

                    float majorODRoll = Rand.Value;
                    bool majorODTriggered = currentOD < 0.9f && !hasChemicalDependency && majorODRoll < majorOverdoseChance;

                    Log.Message($"[FoodTracker][Ingestion] Major OD roll | Roll={majorODRoll:F6} | Chance={majorOverdoseChance:F6} | " +
                        $"CurrentODBelow0.9={(currentOD < 0.9f)} | ChemicalDependency={!hasChemicalDependency} | Triggered={majorODTriggered}");

                    if (majorODTriggered)
                    {
                        // Major OD brings overdose severity to a random value between 85% and 99%.
                        float targetOD = Rand.Range(0.85f, 0.99f);
                        float overdoseIncrease = targetOD - currentOD;

                        float unscaledIncrease = overdoseIncrease;
                        overdoseIncrease *= ingestedFraction;

                        Log.Message($"[FoodTracker][Ingestion] Major OD generated | CurrentOD={currentOD:F4} | TargetOD={targetOD:F4} | " +
                            $"RawIncrease={unscaledIncrease:F4} | ScaledIncrease={overdoseIncrease:F4}");

                        float before = currentOD;

                        HealthUtility.AdjustSeverity(pawn, HediffDefOf.DrugOverdose, overdoseIncrease);

                        float after = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.DrugOverdose)?.Severity ?? 0f;

                        Log.Message($"[FoodTracker][Ingestion] Major OD APPLIED | Before={before:F4} | After={after:F4} | Delta={(after - before):F4}");
                    }
                    else
                    {
                        // ------------------------------------------------
                        // Normal overdose
                        // ------------------------------------------------

                        float rawOverdoseSeverity = drugProps.overdoseSeverityOffset.RandomInRange;
                        float overdoseSeverity = rawOverdoseSeverity / pawn.BodySize;

                        Log.Message($"[FoodTracker][Ingestion] Normal OD generated | RawSeverity={rawOverdoseSeverity:F4} | BodySize={pawn.BodySize:F4} | AfterBodySize={overdoseSeverity:F4}");

                        overdoseSeverity *= ingestedFraction;

                        Log.Message($"[FoodTracker][Ingestion] Normal OD fraction scaled | ScaledSeverity={overdoseSeverity:F4}");

                        if (overdoseSeverity > 0f)
                        {

                            float before = currentOD;

                            HealthUtility.AdjustSeverity(pawn, HediffDefOf.DrugOverdose, overdoseSeverity);

                            float after = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.DrugOverdose)?.Severity ?? 0f;

                            Log.Message($"[FoodTracker][Ingestion] Normal OD APPLIED | Before={before:F4} | After={after:F4} | Delta={(after - before):F4}");
                        }
                        else
                        {
                            Log.Message($"[FoodTracker][Ingestion] Normal OD SKIPPED | FinalSeverity={overdoseSeverity:F4}");
                        }
                    }
                }
            }
            else
            {
                Log.Message($"[FoodTracker][Ingestion] OD SKIPPED | Drug={drugType} does not have overdose effects.");
            }

            Log.Message($"[FoodTracker][Ingestion] END | Pawn={pawn.LabelShort} | Drug={drugDef.defName} | Fraction={ingestedFraction:P2}");
        }
    }
}
