using System;
using System.Collections.Generic;

namespace GoldenHour
{
    [Serializable]
    public sealed class TelemetryEvent
    {
        public float minute;
        public float realSeconds;
        public string type;
        public string casualtyId;
        public string detail;
    }

    [Serializable]
    public sealed class PathSample
    {
        public float minute;
        public float x;
        public float z;
    }

    [Serializable]
    public sealed class CasualtyOutcome
    {
        public string casualtyId;
        public string displayName;
        public string firstTag;
        public string startTruthAtFirstTag;
        public string startStep;
        public string finalTag;
        public string finalTruth;
        public string outcome;
        public float decisionMinutes;
        public int retags;
        public string treatments;
        public List<string> history = new List<string>();
    }

    /// <summary>One run, saved as JSON for the debrief and for playtest analysis.</summary>
    [Serializable]
    public sealed class RunRecord
    {
        public string scenarioId;
        public string scenarioTitle;
        public int seed;
        public string difficultyBand;
        public float deteriorationScale;
        public string startedUtc;
        public float durationMinutes;
        public float realSeconds;
        public int score;
        public int maximumScore;
        public int stars;
        public int preventableDeaths;
        public int survivors;
        public List<ScoreEntry> ledger = new List<ScoreEntry>();
        public List<TelemetryEvent> events = new List<TelemetryEvent>();
        public List<PathSample> path = new List<PathSample>();
        public List<CasualtyOutcome> casualties = new List<CasualtyOutcome>();
        public List<string> feedback = new List<string>();
    }

    [Serializable]
    public sealed class ScenarioProgress
    {
        public string scenarioId;
        public bool unlocked;
        public int bestScore;
        public int bestStars;
        public int plays;
    }

    /// <summary>Saved player profile: campaign progress, unlocks, best scores and recent performance.</summary>
    [Serializable]
    public sealed class PlayerProfileData
    {
        public int version = 2;
        public List<ScenarioProgress> scenarios = new List<ScenarioProgress>();
        public List<float> recentRatios = new List<float>();
        public int totalRuns;

        public ScenarioProgress Get(string scenarioId)
        {
            foreach (ScenarioProgress progress in scenarios)
            {
                if (progress.scenarioId == scenarioId) return progress;
            }

            ScenarioProgress created = new ScenarioProgress { scenarioId = scenarioId };
            scenarios.Add(created);
            return created;
        }

        /// <summary>Records a run; unlocks <paramref name="nextScenarioId"/> on at least one star.</summary>
        public void RecordRun(string scenarioId, int score, int maximum, int stars, string nextScenarioId)
        {
            ScenarioProgress progress = Get(scenarioId);
            progress.unlocked = true;
            progress.plays++;
            progress.bestScore = Math.Max(progress.bestScore, score);
            progress.bestStars = Math.Max(progress.bestStars, stars);
            if (stars >= 1 && !string.IsNullOrEmpty(nextScenarioId)) Get(nextScenarioId).unlocked = true;
            recentRatios.Add(maximum > 0 ? Math.Max(0f, (float)score / maximum) : 0f);
            while (recentRatios.Count > 10) recentRatios.RemoveAt(0);
            totalRuns++;
        }
    }
}
