using UnityEngine;

namespace GoldenHour
{
    public enum Difficulty
    {
        Normal = 0,
        Easy = 1
    }

    /// <summary>Player preferences shared by the menu, pause menu and gameplay (saved in PlayerPrefs).</summary>
    public static class GameSettings
    {
        public const string VolumeKey = "GoldenHour.Volume";
        public const string SensitivityKey = "GoldenHour.MouseSensitivity";
        public const string DifficultyKey = "GoldenHour.Difficulty";

        public const float DefaultVolume = 0.8f;
        public const float DefaultSensitivity = 1f;
        public const float MinSensitivity = 0.2f;
        public const float MaxSensitivity = 3f;

        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, DefaultVolume);
            set
            {
                AudioListener.volume = Mathf.Clamp01(value);
                Save(VolumeKey, Mathf.Clamp01(value));
            }
        }

        /// <summary>Multiplier applied to the base mouse-look sensitivity.</summary>
        public static float MouseSensitivity
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity), MinSensitivity, MaxSensitivity);
            set => Save(SensitivityKey, Mathf.Clamp(value, MinSensitivity, MaxSensitivity));
        }

        public static Difficulty Difficulty
        {
            get => PlayerPrefs.GetInt(DifficultyKey, 0) == 1 ? Difficulty.Easy : Difficulty.Normal;
            set
            {
                PlayerPrefs.SetInt(DifficultyKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        private static void Save(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
        }
    }
}
