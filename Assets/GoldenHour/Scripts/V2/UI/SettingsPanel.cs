using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GoldenHour
{
    /// <summary>
    /// Full settings: audio (master, music, SFX), controls (sensitivity, key rebinding), gameplay (difficulty),
    /// accessibility (subtitles, colour-blind tags, text size, reduced motion, stress effects) and graphics
    /// (preset, resolution, fullscreen). Everything is saved immediately.
    /// </summary>
    public static class SettingsPanel
    {
        public static string RebindingAction { get; private set; }

        public static void Build(Transform view, System.Action refresh, System.Action back)
        {
            UiFactory.Text(view, "SETTINGS", 34, new Vector2(600f, 52f), new Vector2(0f, 382f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;

            // Left column: audio, controls.
            Header(view, "AUDIO", new Vector2(-310f, 320f));
            Row(view, "Master", new Vector2(-310f, 280f));
            UiFactory.VolumeSlider(view, new Vector2(-200f, 280f), new Vector2(240f, 30f));
            Row(view, "Music", new Vector2(-310f, 240f));
            UiFactory.Slider(view, "Music Volume", new Vector2(-200f, 240f), new Vector2(240f, 30f), 0f, 1f, GameSettings.MusicVolume)
                .onValueChanged.AddListener(v => GameSettings.MusicVolume = v);
            Row(view, "Effects", new Vector2(-310f, 200f));
            UiFactory.Slider(view, "SFX Volume", new Vector2(-200f, 200f), new Vector2(240f, 30f), 0f, 1f, GameSettings.SfxVolume)
                .onValueChanged.AddListener(v => GameSettings.SfxVolume = v);

            Header(view, "CONTROLS", new Vector2(-310f, 146f));
            Row(view, "Look sensitivity", new Vector2(-310f, 106f));
            UiFactory.SensitivitySlider(view, new Vector2(-200f, 106f), new Vector2(240f, 30f));
            IReadOnlyList<InputAction> actions = GameInput.Rebindable;
            for (int i = 0; i < actions.Count; i++)
            {
                InputAction action = actions[i];
                float y = 62f - i * 40f;
                Row(view, action.name, new Vector2(-310f, y));
                string label = RebindingAction == action.name ? "press a key..." : GameInput.KeyName(action);
                UiFactory.Button(view, label, new Vector2(-170f, y), new Vector2(170f, 34f), () =>
                {
                    RebindingAction = action.name;
                    refresh();
                    GameInput.Rebind(action, ok =>
                    {
                        RebindingAction = null;
                        refresh();
                    });
                }, Palette.Slate);
            }

            UiFactory.Button(view, "RESET KEYS", new Vector2(-40f, 62f - actions.Count * 40f), new Vector2(170f, 34f), () =>
            {
                GameInput.ResetBindings();
                refresh();
            }, new Color32(93, 49, 53, 255));

            // Right column: gameplay, accessibility, graphics.
            Header(view, "GAMEPLAY", new Vector2(240f, 320f));
            Choice(view, "Difficulty", new Vector2(240f, 280f), new[] { "Easy", "Normal", "Adaptive" }, (int)Map(GameSettings.Difficulty), i =>
            {
                GameSettings.Difficulty = i == 0 ? Difficulty.Easy : i == 1 ? Difficulty.Normal : Difficulty.Adaptive;
                refresh();
            });
            UiFactory.Text(view, DifficultyNote(), 14, new Vector2(560f, 40f), new Vector2(270f, 244f), TextAlignmentOptions.MidlineLeft, Palette.Warning);

            Header(view, "ACCESSIBILITY", new Vector2(240f, 200f));
            Toggle(view, "Subtitles", new Vector2(240f, 160f), GameSettings.Subtitles, v => { GameSettings.Subtitles = v; refresh(); });
            Toggle(view, "Colour-blind tags (shapes + numbers)", new Vector2(240f, 120f), GameSettings.ColourblindTags,
                v => { GameSettings.ColourblindTags = v; refresh(); });
            Toggle(view, "Reduce motion", new Vector2(240f, 80f), GameSettings.ReduceMotion, v => { GameSettings.ReduceMotion = v; refresh(); });
            Toggle(view, "Stress effects (heartbeat, vignette)", new Vector2(240f, 40f), GameSettings.StressEffects,
                v => { GameSettings.StressEffects = v; refresh(); });
            float[] scales = { 0.9f, 1f, 1.15f, 1.3f };
            int scaleIndex = System.Array.FindIndex(scales, s => Mathf.Abs(s - GameSettings.TextScale) < 0.03f);
            Choice(view, "Text size", new Vector2(240f, 0f), new[] { "S", "M", "L", "XL" }, scaleIndex < 0 ? 1 : scaleIndex, i =>
            {
                GameSettings.TextScale = scales[i];
                GraphicsApplier.Apply();
                refresh();
            });

            Header(view, "GRAPHICS", new Vector2(240f, -50f));
            Choice(view, "Preset", new Vector2(240f, -90f), new[] { "Low", "Medium", "High" }, (int)GameSettings.Graphics, i =>
            {
                GameSettings.Graphics = (GraphicsPreset)i;
                GraphicsApplier.Apply();
                refresh();
            });
            List<Resolution> resolutions = UniqueResolutions();
            int current = resolutions.FindIndex(r => r.width == Screen.width && r.height == Screen.height);
            Row(view, "Resolution", new Vector2(240f, -130f));
            UiFactory.Button(view, Screen.width + " x " + Screen.height, new Vector2(395f, -130f), new Vector2(230f, 34f), () =>
            {
                if (resolutions.Count == 0) return;
                Resolution next = resolutions[(current + 1 + resolutions.Count) % resolutions.Count];
                Screen.SetResolution(next.width, next.height, Screen.fullScreenMode);
                refresh();
            }, Palette.Slate);
            Toggle(view, "Fullscreen", new Vector2(240f, -170f), Screen.fullScreenMode != FullScreenMode.Windowed, v =>
            {
                Screen.fullScreenMode = v ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                refresh();
            });

            UiFactory.Text(view, "Saved automatically. Gamepad: left stick move, right stick look, A interact, B kneel, D-pad radio/shout/gloves, Start pause.",
                14, new Vector2(1100f, 34f), new Vector2(0f, -330f), TextAlignmentOptions.Center, Palette.LightGrey);
            UiFactory.Button(view, "BACK", new Vector2(0f, -375f), new Vector2(240f, 50f), back, Palette.Slate);
        }

        private static int Map(Difficulty difficulty) => difficulty == Difficulty.Easy ? 0 : difficulty == Difficulty.Normal ? 1 : 2;

        private static string DifficultyNote()
        {
            switch (GameSettings.Difficulty)
            {
                case Difficulty.Easy: return "Easy: slower deterioration and START hints.";
                case Difficulty.Normal: return "Normal: standard deterioration, no hints.";
                default: return "Adaptive: deterioration speed follows your last three results.";
            }
        }

        private static List<Resolution> UniqueResolutions()
        {
            List<Resolution> list = new List<Resolution>();
            foreach (Resolution resolution in Screen.resolutions)
            {
                if (resolution.width < 1024) continue;
                if (!list.Exists(r => r.width == resolution.width && r.height == resolution.height)) list.Add(resolution);
            }

            return list;
        }

        private static void Header(Transform view, string text, Vector2 position)
        {
            UiFactory.Text(view, "<b>" + text + "</b>", 20, new Vector2(560f, 34f), position + new Vector2(30f, 0f), TextAlignmentOptions.MidlineLeft,
                new Color32(240, 120, 120, 255));
        }

        private static void Row(Transform view, string text, Vector2 position)
        {
            UiFactory.Text(view, text, 16, new Vector2(230f, 32f), position + new Vector2(-150f, 0f), TextAlignmentOptions.MidlineRight, Palette.LightGrey);
        }

        private static void Toggle(Transform view, string text, Vector2 position, bool value, System.Action<bool> changed)
        {
            UiFactory.Button(view, (value ? "[X]  " : "[  ]  ") + text, position + new Vector2(130f, 0f), new Vector2(480f, 34f), () => changed(!value),
                value ? new Color32(52, 92, 70, 255) : Palette.Slate);
        }

        private static void Choice(Transform view, string label, Vector2 position, string[] options, int selected, System.Action<int> chosen)
        {
            Row(view, label, position);
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                float width = options.Length > 3 ? 80f : 110f;
                UiFactory.Button(view, options[i], position + new Vector2(20f + width * 0.5f + i * (width + 8f), 0f), new Vector2(width, 34f),
                    () => chosen(index), i == selected ? Palette.EmergencyRed : Palette.Slate);
            }
        }
    }
}
