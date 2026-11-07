using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GoldenHour
{
    /// <summary>
    /// End-to-end playtest of Golden Hour v2 in a player build. Inactive unless launched with
    /// <c>-goldenhour-autotest &lt;outputDirectory&gt;</c>. Plays every scenario to completion with a fixed seed,
    /// keeps an independent score tally from the published rules and compares it with the game's ledger,
    /// and checks deterioration/death, retriage rules, ambulance handover, menus and settings.
    /// Writes autotest_results.txt and screenshots, then exits with code 0 (all passed) or 1.
    /// </summary>
    public sealed partial class AutomatedPlaytest : MonoBehaviour
    {
        private const string Argument = "-goldenhour-autotest";
        private readonly List<string> lines = new List<string>();
        private string outputDirectory;
        private int failures;
        private int checks;
        private int errorsLogged;
        private readonly List<string> errorSamples = new List<string>();
        private float savedVolume;
        private float savedSensitivity;
        private Difficulty savedDifficulty;
        private bool savedSubtitles;
        private bool savedColourblind;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, Argument);
            if (index < 0 || index + 1 >= args.Length) return;
            GameObject runner = new GameObject("Automated Playtest");
            DontDestroyOnLoad(runner);
            runner.AddComponent<AutomatedPlaytest>().outputDirectory = Path.GetFullPath(args[index + 1]);
        }

        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            errorsLogged++;
            if (errorSamples.Count < 8) errorSamples.Add(type + ": " + condition);
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(outputDirectory);
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            savedVolume = GameSettings.Volume;
            savedSensitivity = GameSettings.MouseSensitivity;
            savedDifficulty = GameSettings.Difficulty;
            savedSubtitles = GameSettings.Subtitles;
            savedColourblind = GameSettings.ColourblindTags;
            string profileDirectory = Path.Combine(outputDirectory, "profile");
            if (Directory.Exists(profileDirectory)) Directory.Delete(profileDirectory, true);
            ProfileStore.OverrideDirectory = profileDirectory;
            ProfileStore.Reset();
            Session.SeedOverride = 4242;
            Session.DifficultyOverride = Difficulty.Normal;
            Cutscene.Enabled = true;
            Log("Golden Hour v2 automated playtest | " + DateTime.Now.ToString("u") + " | " + Screen.width + "x" + Screen.height +
                " | Unity " + Application.unityVersion);

            yield return MainMenuChecks();
            string[] scenarios = { "tutorial", "highway", "bus", "building" };
            for (int i = 0; i < scenarios.Length; i++)
            {
                yield return LoadScenario(scenarios[i], i == 0);
                yield return PlayScenario(scenarios[i]);
            }

            yield return ReturnToMenuChecks();

            GameSettings.Volume = savedVolume;
            GameSettings.MouseSensitivity = savedSensitivity;
            GameSettings.Difficulty = savedDifficulty;
            GameSettings.Subtitles = savedSubtitles;
            GameSettings.ColourblindTags = savedColourblind;
            GameInput.ResetBindings();
            Check("Zero errors or exceptions logged during the whole run", errorsLogged == 0,
                errorsLogged + (errorSamples.Count > 0 ? " | " + string.Join(" || ", errorSamples) : string.Empty));
            Log(failures == 0 ? "RESULT: ALL " + checks + " CHECKS PASSED" : "RESULT: " + failures + " OF " + checks + " CHECK(S) FAILED");
            File.WriteAllLines(Path.Combine(outputDirectory, "autotest_results.txt"), lines);
            Application.Quit(failures == 0 ? 0 : 1);
        }

        // ------------------------------------------------------------ main menu

        private IEnumerator MainMenuChecks()
        {
            MainMenuController menu = null;
            yield return WaitFor(() => (menu = FindAnyObjectByType<MainMenuController>()) != null, 15f);
            Check("Main menu scene loads first", menu != null && SceneManager.GetActiveScene().name == "MainMenu", SceneManager.GetActiveScene().name);
            if (menu == null) yield break;
            yield return WaitFor(() => SceneFader.Alpha < 0.01f, 4f);
            Check("Menu fades in from black over a live 3D backdrop", SceneFader.Alpha < 0.01f && GameObject.Find("Accident Scene") != null);
            Check("Main menu uses the Input System UI module", FindAnyObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() != null);
            Check("Menu disclaimer says content is not medically reviewed", ScreenTextContains("NOT medically reviewed"));

            ButtonFeedback feedback = FindAnyObjectByType<ButtonFeedback>();
            if (feedback != null)
            {
                feedback.OnPointerEnter(null);
                yield return new WaitForSecondsRealtime(0.2f);
                float hover = feedback.transform.localScale.x;
                feedback.OnPointerExit(null);
                Check("Buttons react to hover", hover > 1.02f, hover.ToString("0.000"));
            }

            yield return Screenshot("01_main_menu");
            menu.ShowCampaign();
            yield return null;
            Check("Campaign lists four scenarios", ScreenTextContains("TRAINING YARD") && ScreenTextContains("HIGHWAY COLLISION") &&
                                                   ScreenTextContains("NIGHT BUS ROLLOVER") && ScreenTextContains("COLLAPSED COMMUNITY HALL"));
            Button locked = FindButton("HIGHWAY COLLISION");
            Check("Later scenarios start locked on a fresh profile", locked != null && !locked.interactable);
            yield return Screenshot("02_campaign");

            menu.ShowHowToPlay();
            yield return null;
            Check("How to Play explains START and shows the disclaimer", ScreenTextContains("START") && ScreenTextContains("not medical training"));

            menu.ShowSettings();
            yield return null;
            Check("Settings has master, music, SFX and sensitivity sliders",
                FindSlider("Master Volume") != null && FindSlider("Music Volume") != null && FindSlider("SFX Volume") != null &&
                FindSlider("Mouse Sensitivity") != null);
            Slider volume = FindSlider("Master Volume");
            if (volume != null)
            {
                volume.value = 0.35f;
                Check("Volume slider changes AudioListener.volume", Mathf.Approximately(AudioListener.volume, 0.35f));
                volume.value = savedVolume;
            }

            Check("Settings has accessibility, graphics and key rebinding options",
                ScreenTextContains("Subtitles") && ScreenTextContains("Colour-blind") && ScreenTextContains("Reduce motion") &&
                ScreenTextContains("Text size") && ScreenTextContains("Preset") && ScreenTextContains("Resolution") && FindButton("RESET KEYS") != null);
            GameInput.SetKeyboardBinding(GameInput.Shout, "<Keyboard>/z");
            Check("Key rebinding changes the binding (Shout -> Z)", GameInput.KeyName(GameInput.Shout) == "Z", GameInput.KeyName(GameInput.Shout));
            GameInput.ResetBindings();
            Check("Reset restores default keys", GameInput.KeyName(GameInput.Shout) == "Q");
            GameSettings.Graphics = GraphicsPreset.Medium;
            GraphicsApplier.Apply();
            Check("Graphics preset Medium applies render scale 0.9", Mathf.Approximately(GraphicsApplier.RenderScale, 0.9f));
            yield return Screenshot("03_settings");

            menu.ShowFeedback();
            yield return null;
            FeedbackForm.SetRating(0, 5);
            string saved = FeedbackForm.Save("autotest comment");
            Check("Playtest feedback form saves answers to JSON", File.Exists(saved) && File.ReadAllText(saved).Contains("autotest comment"));
            yield return Screenshot("04_feedback_form");
            menu.ShowCredits();
            yield return null;
            Check("Credits screen opens", menu.CurrentView == "Credits");
            menu.ShowHome();
        }

        private IEnumerator LoadScenario(string scenarioId, bool fromMenu)
        {
            Session.ScenarioId = scenarioId;
            if (fromMenu)
            {
                MainMenuController menu = FindAnyObjectByType<MainMenuController>();
                ScenarioData scenario = menu.catalog.Find(scenarioId);
                menu.StartScenario(scenario);
                yield return null;
                Check("Loading screen shows a tip", SceneFader.IsLoading && ScreenTextContains("TIP:"));
                yield return Screenshot("05_loading_screen");
            }
            else
            {
                SceneFader.LoadScene("Samplespace");
            }

            yield return WaitFor(() => !SceneFader.IsLoading && IncidentDirector.Instance != null &&
                                       IncidentDirector.Instance.Scenario != null && IncidentDirector.Instance.Scenario.scenarioId == scenarioId, 30f);
        }

        private IEnumerator ReturnToMenuChecks()
        {
            IncidentDirector director = IncidentDirector.Instance;
            if (director == null) yield break;
            director.ReturnToMenu();
            MainMenuController menu = null;
            yield return WaitFor(() => (menu = FindAnyObjectByType<MainMenuController>()) != null && !SceneFader.IsLoading, 20f);
            Check("Main Menu returns to the menu with a visible cursor", menu != null && Cursor.visible && Cursor.lockState == CursorLockMode.None);
            if (menu == null) yield break;
            menu.ShowCampaign();
            yield return null;
            Button highway = FindButton("HIGHWAY COLLISION");
            Check("Completing scenarios unlocks the campaign", highway != null && highway.interactable);
            Check("Campaign shows best scores after playing", ScreenTextContains("best "));
            yield return Screenshot("90_campaign_after");
        }

        // -------------------------------------------------------------- helpers

        private static IEnumerator HoldKey(Key key, float seconds)
        {
            Keyboard keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return new WaitForSeconds(seconds);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }

        private static IEnumerator TapKey(Key key)
        {
            Keyboard keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        private static IEnumerator WaitFor(Func<bool> condition, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        private IEnumerator Screenshot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, name + ".png"));
            yield return null;
            yield return null;
            Log("  screenshot " + name + ".png");
        }

        private static bool ScreenTextContains(string value)
        {
            foreach (TMPro.TMP_Text text in FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Exclude))
            {
                if (text.text != null && text.text.Contains(value)) return true;
            }

            return false;
        }

        private static Slider FindSlider(string sliderName)
        {
            foreach (Slider slider in FindObjectsByType<Slider>(FindObjectsInactive.Exclude))
            {
                if (slider.name == sliderName) return slider;
            }

            return null;
        }

        private static Button FindButton(string contains)
        {
            foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Exclude))
            {
                TMPro.TMP_Text label = button.GetComponentInChildren<TMPro.TMP_Text>();
                if (label != null && label.text.Contains(contains)) return button;
            }

            return null;
        }

        private static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0f, value.z);

        private void Check(string name, bool passed, string detail = null)
        {
            checks++;
            if (!passed) failures++;
            Log((passed ? "PASS  " : "FAIL  ") + name + (string.IsNullOrEmpty(detail) ? string.Empty : "  [" + detail + "]"));
        }

        private void Log(string line)
        {
            lines.Add(line);
            Debug.Log("[Autotest] " + line);
        }
    }
}
