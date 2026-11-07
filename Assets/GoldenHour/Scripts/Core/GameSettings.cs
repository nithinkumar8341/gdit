using UnityEngine;

namespace GoldenHour
{
    public enum Difficulty
    {
        Normal = 0,
        Easy = 1,
        Adaptive = 2
    }

    public enum GraphicsPreset
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    /// <summary>Player preferences shared by menus, pause menu and gameplay (saved in PlayerPrefs).</summary>
    public static class GameSettings
    {
        public const string VolumeKey = "GoldenHour.Volume";
        public const string MusicKey = "GoldenHour.MusicVolume";
        public const string SfxKey = "GoldenHour.SfxVolume";
        public const string SensitivityKey = "GoldenHour.MouseSensitivity";
        public const string DifficultyKey = "GoldenHour.Difficulty";
        public const string GraphicsKey = "GoldenHour.Graphics";
        public const string SubtitlesKey = "GoldenHour.Subtitles";
        public const string ColourblindKey = "GoldenHour.ColourblindTags";
        public const string TextScaleKey = "GoldenHour.TextScale";
        public const string ReduceMotionKey = "GoldenHour.ReduceMotion";
        public const string StressKey = "GoldenHour.StressEffects";

        public const float DefaultVolume = 0.8f;
        public const float DefaultSensitivity = 1f;
        public const float MinSensitivity = 0.2f;
        public const float MaxSensitivity = 3f;

        public static event System.Action Changed;

        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, DefaultVolume);
            set
            {
                AudioListener.volume = Mathf.Clamp01(value);
                SaveFloat(VolumeKey, Mathf.Clamp01(value));
            }
        }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MusicKey, 0.7f);
            set => SaveFloat(MusicKey, Mathf.Clamp01(value));
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxKey, 1f);
            set => SaveFloat(SfxKey, Mathf.Clamp01(value));
        }

        /// <summary>Multiplier applied to the base mouse/stick look sensitivity.</summary>
        public static float MouseSensitivity
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity), MinSensitivity, MaxSensitivity);
            set => SaveFloat(SensitivityKey, Mathf.Clamp(value, MinSensitivity, MaxSensitivity));
        }

        public static Difficulty Difficulty
        {
            get => (Difficulty)Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, (int)Difficulty.Adaptive), 0, 2);
            set => SaveInt(DifficultyKey, (int)value);
        }

        public static GraphicsPreset Graphics
        {
            get => (GraphicsPreset)Mathf.Clamp(PlayerPrefs.GetInt(GraphicsKey, (int)GraphicsPreset.Medium), 0, 2);
            set => SaveInt(GraphicsKey, (int)value);
        }

        public static bool Subtitles
        {
            get => PlayerPrefs.GetInt(SubtitlesKey, 1) == 1;
            set => SaveInt(SubtitlesKey, value ? 1 : 0);
        }

        /// <summary>Adds shapes, stripes and priority numbers to triage tags.</summary>
        public static bool ColourblindTags
        {
            get => PlayerPrefs.GetInt(ColourblindKey, 0) == 1;
            set => SaveInt(ColourblindKey, value ? 1 : 0);
        }

        /// <summary>UI text scale: 0.9, 1.0, 1.15 or 1.3.</summary>
        public static float TextScale
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(TextScaleKey, 1f), 0.85f, 1.35f);
            set => SaveFloat(TextScaleKey, Mathf.Clamp(value, 0.85f, 1.35f));
        }

        public static bool ReduceMotion
        {
            get => PlayerPrefs.GetInt(ReduceMotionKey, 0) == 1;
            set => SaveInt(ReduceMotionKey, value ? 1 : 0);
        }

        /// <summary>Heartbeat, breathing and vignette pulse under time pressure.</summary>
        public static bool StressEffects
        {
            get => PlayerPrefs.GetInt(StressKey, 1) == 1;
            set => SaveInt(StressKey, value ? 1 : 0);
        }

        private static void SaveFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static void SaveInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
