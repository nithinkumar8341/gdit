using System;
using System.IO;
using UnityEngine;

namespace GoldenHour
{
    /// <summary>State carried between scenes: the chosen scenario and the last finished run.</summary>
    public static class Session
    {
        public static string ScenarioId { get; set; } = "tutorial";
        public static RunRecord LastRun { get; set; }
        public static string LastRunPath { get; set; }

        /// <summary>Fixed seed for reproducible runs (tests). Null = random seed and shuffled positions.</summary>
        public static int? SeedOverride { get; set; }

        public static Difficulty? DifficultyOverride { get; set; }

        /// <summary>Time scale applied when a scenario starts (tests use values above 1). 0 = normal.</summary>
        public static float TimeScaleOverride { get; set; }
    }

    /// <summary>Saves the player profile and run telemetry as JSON under the persistent data folder.</summary>
    public static class ProfileStore
    {
        private static PlayerProfileData cached;

        /// <summary>Set by the automated playtest so test runs never touch the real profile.</summary>
        public static string OverrideDirectory { get; set; }

        public static string Directory
        {
            get
            {
                string root = string.IsNullOrEmpty(OverrideDirectory) ? Application.persistentDataPath : OverrideDirectory;
                System.IO.Directory.CreateDirectory(root);
                return root;
            }
        }

        public static string ProfilePath => Path.Combine(Directory, "profile.json");

        public static PlayerProfileData Load()
        {
            if (cached != null) return cached;
            try
            {
                if (File.Exists(ProfilePath))
                {
                    cached = JsonUtility.FromJson<PlayerProfileData>(File.ReadAllText(ProfilePath));
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Golden Hour profile could not be read, starting fresh: " + exception.Message);
            }

            if (cached == null) cached = new PlayerProfileData();
            return cached;
        }

        public static void Save()
        {
            if (cached == null) return;
            File.WriteAllText(ProfilePath, JsonUtility.ToJson(cached, true));
        }

        public static void Reset()
        {
            cached = new PlayerProfileData();
        }

        public static string SaveRun(RunRecord run)
        {
            string folder = Path.Combine(Directory, "Runs");
            System.IO.Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "run_" + run.scenarioId + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(run, true));
            return path;
        }
    }
}
