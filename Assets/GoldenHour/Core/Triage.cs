using System;

namespace GoldenHour
{
    /// <summary>START triage categories (simplified educational model).</summary>
    public enum TriageTag
    {
        None = 0,
        Green = 1,
        Yellow = 2,
        Red = 3,
        Black = 4
    }

    /// <summary>The START step that decided a category; used by feedback and the debrief.</summary>
    public enum StartStep
    {
        None,
        Deceased,
        Walking,
        NoBreathingAfterAirwayOpening,
        BreathesOnlyAfterAirwayOpening,
        RespiratoryRateOver30,
        PoorCirculation,
        CannotFollowCommands,
        AllChecksPassed
    }

    public enum BleedingLevel
    {
        None = 0,
        Minor = 1,
        Severe = 2
    }

    /// <summary>
    /// The START (Simple Triage And Rapid Treatment) algorithm as a simplified educational model:
    /// walking → GREEN; not breathing after airway opening → BLACK; breathing only after the
    /// airway is opened → RED; breathing over 30/min → RED; poor circulation (capillary refill
    /// over 2 s or no radial pulse) → RED; cannot follow simple commands → RED; otherwise YELLOW.
    /// Not medical training: the content has not been medically reviewed.
    /// </summary>
    public static class StartTriage
    {
        public const int RespiratoryRateLimit = 30;
        public const float CapillaryRefillLimit = 2f;

        public static TriageTag Evaluate(PhysiologyState state)
        {
            return Evaluate(state, out _);
        }

        public static TriageTag Evaluate(PhysiologyState state, out StartStep step)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.deceased)
            {
                step = StartStep.Deceased;
                return TriageTag.Black;
            }

            if (state.canWalk)
            {
                step = StartStep.Walking;
                return TriageTag.Green;
            }

            if (state.respiratoryRate <= 0)
            {
                step = StartStep.NoBreathingAfterAirwayOpening;
                return TriageTag.Black;
            }

            if (state.airwayObstructed)
            {
                step = StartStep.BreathesOnlyAfterAirwayOpening;
                return TriageTag.Red;
            }

            if (state.respiratoryRate > RespiratoryRateLimit)
            {
                step = StartStep.RespiratoryRateOver30;
                return TriageTag.Red;
            }

            if (state.PoorCirculation)
            {
                step = StartStep.PoorCirculation;
                return TriageTag.Red;
            }

            if (!state.followsCommands)
            {
                step = StartStep.CannotFollowCommands;
                return TriageTag.Red;
            }

            step = StartStep.AllChecksPassed;
            return TriageTag.Yellow;
        }

        /// <summary>Lower number = transported/treated first. BLACK is never prioritised.</summary>
        public static int Priority(TriageTag tag)
        {
            switch (tag)
            {
                case TriageTag.Red: return 0;
                case TriageTag.Yellow: return 1;
                case TriageTag.Green: return 2;
                case TriageTag.Black: return 4;
                default: return 3;
            }
        }

        public static string Label(TriageTag tag)
        {
            switch (tag)
            {
                case TriageTag.Red: return "RED | IMMEDIATE";
                case TriageTag.Yellow: return "YELLOW | DELAYED";
                case TriageTag.Green: return "GREEN | MINOR";
                case TriageTag.Black: return "BLACK | DECEASED / EXPECTANT";
                default: return "NOT TAGGED";
            }
        }

        public static string Explain(StartStep step)
        {
            switch (step)
            {
                case StartStep.Deceased: return "The person had died: BLACK.";
                case StartStep.Walking: return "START step 1 - able to walk: GREEN (minor), re-check later.";
                case StartStep.NoBreathingAfterAirwayOpening:
                    return "START step 2 - no breathing even after opening the airway: BLACK (deceased/expectant).";
                case StartStep.BreathesOnlyAfterAirwayOpening:
                    return "START step 2 - breathing started only after opening the airway: RED (immediate).";
                case StartStep.RespiratoryRateOver30:
                    return "START step 2 - breathing faster than 30 per minute: RED (immediate).";
                case StartStep.PoorCirculation:
                    return "START step 3 - capillary refill over 2 seconds or no radial pulse: RED (immediate).";
                case StartStep.CannotFollowCommands:
                    return "START step 4 - cannot follow a simple command: RED (immediate).";
                case StartStep.AllChecksPassed:
                    return "START step 4 - breathing under 30, good circulation, follows commands, cannot walk: YELLOW (delayed).";
                default: return string.Empty;
            }
        }
    }
}
