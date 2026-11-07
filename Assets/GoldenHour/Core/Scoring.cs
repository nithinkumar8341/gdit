using System;
using System.Collections.Generic;

namespace GoldenHour
{
    /// <summary>All score values in one place (documented in the GDD).</summary>
    public static class ScoreRules
    {
        public const int TriageCorrect = 100;
        public const int OverTriage = -20;
        public const int UnderTriage = -60;
        public const int WrongExpectant = -100;
        public const int JustifiedRetriage = 40;
        public const int TreatmentEffective = 50;
        public const int TreatmentNotIndicated = -15;
        public const int NoGloves = -25;
        public const int PreventableDeath = -150;
        public const int HandoverCorrect = 40;
        public const int HandoverWrong = -30;
        public const int HazardMarked = 20;
        public const int DispatchField = 15;
        public const int DispatchFields = 4;
        public const int WalkingCommand = 30;
        public const int BystanderCalmed = 10;
        public const int DangerZone = -10;
        public const int MaxTimeBonus = 100;
        public const int TimeBonusPerMinute = 2;

        /// <summary>Scores the first tag for a casualty against the START category at that moment.</summary>
        public static int FirstTag(TriageTag chosen, TriageTag truth)
        {
            if (chosen == truth) return TriageCorrect;
            if (chosen == TriageTag.Black) return WrongExpectant;
            if (truth == TriageTag.Black) return OverTriage;
            return StartTriage.Priority(chosen) < StartTriage.Priority(truth) ? OverTriage : UnderTriage;
        }

        public static string FirstTagVerdict(TriageTag chosen, TriageTag truth)
        {
            if (chosen == truth) return "correct";
            if (chosen == TriageTag.Black) return "tagged BLACK while still salvageable";
            if (truth == TriageTag.Black) return "over-triage";
            return StartTriage.Priority(chosen) < StartTriage.Priority(truth) ? "over-triage" : "under-triage";
        }

        /// <summary>A retriage earns credit only when the condition really changed and the new tag is right.</summary>
        public static int Retag(TriageTag chosen, TriageTag truthNow, TriageTag truthAtPreviousTag)
        {
            return truthNow != truthAtPreviousTag && chosen == truthNow ? JustifiedRetriage : 0;
        }

        public static int TimeBonus(float remainingMinutes)
        {
            return Math.Max(0, Math.Min(MaxTimeBonus, (int)Math.Floor(remainingMinutes) * TimeBonusPerMinute));
        }

        /// <summary>1-3 stars. Three stars also require no preventable deaths.</summary>
        public static int Stars(int score, int maximum, int preventableDeaths)
        {
            float ratio = maximum > 0 ? (float)score / maximum : 0f;
            int stars = ratio >= 0.85f ? 3 : ratio >= 0.65f ? 2 : ratio >= 0.4f ? 1 : 0;
            if (preventableDeaths > 0 && stars > 2) stars = 2;
            return Math.Max(1, stars);
        }
    }

    [Serializable]
    public sealed class ScoreEntry
    {
        public float minute;
        public string category;
        public int points;
        public string reason;
        public string casualtyId;
    }

    /// <summary>Every point change with its reason (drives the HUD score, the debrief timeline and telemetry).</summary>
    public sealed class ScoreLedger
    {
        private readonly List<ScoreEntry> entries = new List<ScoreEntry>();

        public IReadOnlyList<ScoreEntry> Entries => entries;
        public int Total { get; private set; }

        public ScoreEntry Add(float minute, string category, int points, string reason, string casualtyId = "")
        {
            ScoreEntry entry = new ScoreEntry
            {
                minute = minute, category = category, points = points, reason = reason, casualtyId = casualtyId ?? string.Empty
            };
            entries.Add(entry);
            Total += points;
            return entry;
        }

        public int Sum(string category)
        {
            int sum = 0;
            foreach (ScoreEntry entry in entries)
            {
                if (entry.category == category) sum += entry.points;
            }

            return sum;
        }

        public int Count(string category, bool negativeOnly = false)
        {
            int count = 0;
            foreach (ScoreEntry entry in entries)
            {
                if (entry.category == category && (!negativeOnly || entry.points < 0)) count++;
            }

            return count;
        }
    }

    public enum TagRequestResult
    {
        FirstTag,
        Retag,
        RejectedNeedsReexamination,
        RejectedSameTag,
        RejectedNoExamination
    }

    /// <summary>
    /// Triage tag history for one casualty. A tag needs at least one examination; a retriage is allowed only
    /// after re-examining since the last tag. Only the first tag is scored in full.
    /// </summary>
    public sealed class TriageRecord
    {
        private readonly List<string> log = new List<string>();

        public TriageTag FirstTag { get; private set; }
        public TriageTag FirstTruth { get; private set; }
        public StartStep FirstTruthStep { get; private set; }
        public float FirstTagMinute { get; private set; } = -1f;
        public float FirstExamMinute { get; private set; } = -1f;
        public TriageTag CurrentTag { get; private set; }
        public TriageTag TruthAtLastTag { get; private set; }
        public float LastTagMinute { get; private set; } = -1f;
        public float LastExamMinute { get; private set; } = -1f;
        public int Retags { get; private set; }
        public bool HasTag => CurrentTag != TriageTag.None;
        public bool CanRetag => HasTag && LastExamMinute > LastTagMinute;
        public IReadOnlyList<string> Log => log;

        /// <summary>Decision time: from the first examination action to the first tag.</summary>
        public float DecisionMinutes => FirstTagMinute >= 0f && FirstExamMinute >= 0f ? FirstTagMinute - FirstExamMinute : -1f;

        public void RecordExamination(float minute, string action)
        {
            if (FirstExamMinute < 0f) FirstExamMinute = minute;
            LastExamMinute = minute;
            log.Add(minute.ToString("0.0") + " min: " + action);
        }

        public TagRequestResult Check(TriageTag chosen)
        {
            if (LastExamMinute < 0f) return TagRequestResult.RejectedNoExamination;
            if (!HasTag) return TagRequestResult.FirstTag;
            if (!CanRetag) return TagRequestResult.RejectedNeedsReexamination;
            return chosen == CurrentTag ? TagRequestResult.RejectedSameTag : TagRequestResult.Retag;
        }

        /// <summary>Applies a tag and returns the points it earns (0 if rejected).</summary>
        public int ApplyTag(float minute, TriageTag chosen, TriageTag truth, StartStep truthStep, out TagRequestResult result)
        {
            result = Check(chosen);
            int points = 0;
            if (result == TagRequestResult.FirstTag)
            {
                FirstTag = chosen;
                FirstTruth = truth;
                FirstTruthStep = truthStep;
                FirstTagMinute = minute;
                points = ScoreRules.FirstTag(chosen, truth);
                log.Add(minute.ToString("0.0") + " min: tagged " + chosen + " (START: " + truth + ", " +
                        ScoreRules.FirstTagVerdict(chosen, truth) + ")");
            }
            else if (result == TagRequestResult.Retag)
            {
                points = ScoreRules.Retag(chosen, truth, TruthAtLastTag);
                Retags++;
                log.Add(minute.ToString("0.0") + " min: re-tagged " + CurrentTag + " -> " + chosen + " (START now: " + truth +
                        (points > 0 ? ", justified by a change in condition)" : ", no change in condition - no credit)"));
            }
            else
            {
                return 0;
            }

            CurrentTag = chosen;
            TruthAtLastTag = truth;
            LastTagMinute = minute;
            return points;
        }
    }

    /// <summary>Ambulance handover: the chosen casualty must have the highest START priority still waiting.</summary>
    public static class HandoverRules
    {
        public static bool IsCorrect(TriageTag chosenTruth, IEnumerable<TriageTag> waitingTruths)
        {
            if (chosenTruth == TriageTag.Black || chosenTruth == TriageTag.None) return false;
            int chosen = StartTriage.Priority(chosenTruth);
            foreach (TriageTag tag in waitingTruths)
            {
                if (tag != TriageTag.Black && StartTriage.Priority(tag) < chosen) return false;
            }

            return true;
        }
    }

    /// <summary>The scored dispatch report given to the emergency number.</summary>
    [Serializable]
    public sealed class DispatchReport
    {
        public int locationIndex = -1;
        public int casualtyCount = -1;
        public int hazardMask;
        public string emergencyNumber = string.Empty;

        /// <summary>Counts correct fields: location, number of casualties (±1), hazards, emergency number 112.</summary>
        public int CorrectFields(int correctLocation, int actualCasualties, int actualHazardMask)
        {
            int fields = 0;
            if (locationIndex == correctLocation) fields++;
            if (casualtyCount >= 0 && Math.Abs(casualtyCount - actualCasualties) <= 1) fields++;
            if (hazardMask == actualHazardMask) fields++;
            if (emergencyNumber == "112") fields++;
            return fields;
        }
    }

    /// <summary>
    /// Adaptive difficulty: the mean score ratio of the last three runs selects a deterioration speed.
    /// High performers (mean ≥ 0.8) get faster deterioration and no hints; players who struggle (≤ 0.45)
    /// get slower deterioration and hints.
    /// </summary>
    public static class AdaptiveDifficulty
    {
        public const int Window = 3;

        public static float DeteriorationScale(IReadOnlyList<float> recentRatios, out string band)
        {
            if (recentRatios == null || recentRatios.Count == 0)
            {
                band = "Standard";
                return 1f;
            }

            float sum = 0f;
            int count = 0;
            for (int i = recentRatios.Count - 1; i >= 0 && count < Window; i--, count++) sum += recentRatios[i];
            float mean = sum / count;
            if (mean >= 0.8f)
            {
                band = "Challenge";
                return 1.2f;
            }

            if (mean <= 0.45f)
            {
                band = "Support";
                return 0.85f;
            }

            band = "Standard";
            return 1f;
        }
    }
}
