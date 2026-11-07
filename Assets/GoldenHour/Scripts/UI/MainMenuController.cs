using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoldenHour
{
    /// <summary>
    /// Main menu over a live 3D accident backdrop: Campaign (scenario select with locks, stars and best
    /// scores), How to Play (with disclaimer), Settings, Playtest Feedback, Credits and Exit.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        public const string GameScene = "Samplespace";
        private static readonly Color PanelColor = new Color32(18, 24, 32, 236);

        public ScenarioCatalog catalog;
        public Material baseMaterial;
        public Material unlitMaterial;
        public Camera menuCamera;

        private Canvas canvas;
        private GameObject currentView;
        private float orbit;

        public string CurrentView => currentView != null ? currentView.name : string.Empty;

        private void Awake()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Application.targetFrameRate = 60;
            GameInput.Ensure();
            GraphicsApplier.Apply();
            AudioListener.volume = GameSettings.Volume;
            MaterialFactory.Template = baseMaterial;
            MaterialFactory.UnlitTemplate = unlitMaterial;
            GoldenHourAudio audioManager = GetComponent<GoldenHourAudio>();
            UiFactory.ButtonPressed = audioManager != null ? audioManager.PlayButton : (System.Action)null;
            ButtonFeedback.Hovered = audioManager != null ? audioManager.PlayHover : (System.Action)null;
            if (audioManager != null) audioManager.StartMusic();

            AccidentEnvironment backdrop = GetComponent<AccidentEnvironment>();
            if (backdrop != null) backdrop.Build();
            canvas = UiFactory.CreateCanvas("Golden Hour Main Menu");
            canvas.gameObject.AddComponent<GamepadSelector>();
            UiFactory.Panel(canvas.transform, "Header Stripe", new Vector2(4000f, 10f), new Vector2(0f, 445f), Palette.EmergencyRed);
            ShowHome();
            SceneFader.FadeIn();
        }

        private void Update()
        {
            if (menuCamera == null) return;
            orbit += Time.deltaTime * (GameSettings.ReduceMotion ? 0f : 3f);
            Vector3 centre = new Vector3(0.5f, 1.2f, 5f);
            Vector3 offset = Quaternion.Euler(0f, -60f + orbit, 0f) * new Vector3(0f, 0f, -13f);
            menuCamera.transform.position = centre + offset + Vector3.up * 3.2f;
            menuCamera.transform.LookAt(centre);
        }

        private GameObject NewView(string viewName, Vector2 size, Vector2 position)
        {
            if (currentView != null)
            {
                currentView.SetActive(false);
                Destroy(currentView);
            }

            currentView = UiFactory.Panel(canvas.transform, viewName, size, position, PanelColor);
            return currentView;
        }

        public void ShowHome()
        {
            GameObject view = NewView("Main Menu", new Vector2(560f, 760f), new Vector2(-470f, 0f));
            UiFactory.Text(view.transform, "GOLDEN HOUR", 56, new Vector2(520f, 80f), new Vector2(0f, 300f), TextAlignmentOptions.Center)
                .fontStyle = FontStyles.Bold;
            UiFactory.Text(view.transform, "MASS-CASUALTY TRIAGE SIMULATION", 18, new Vector2(520f, 36f), new Vector2(0f, 250f),
                TextAlignmentOptions.Center, new Color32(240, 120, 120, 255));
            UiFactory.Button(view.transform, "CAMPAIGN", new Vector2(0f, 160f), new Vector2(380f, 64f), ShowCampaign);
            UiFactory.Button(view.transform, "HOW TO PLAY", new Vector2(0f, 84f), new Vector2(380f, 56f), ShowHowToPlay, Palette.Slate);
            UiFactory.Button(view.transform, "SETTINGS", new Vector2(0f, 18f), new Vector2(380f, 56f), ShowSettings, Palette.Slate);
            UiFactory.Button(view.transform, "PLAYTEST FEEDBACK", new Vector2(0f, -48f), new Vector2(380f, 56f), ShowFeedback, Palette.Slate);
            UiFactory.Button(view.transform, "CREDITS", new Vector2(0f, -114f), new Vector2(380f, 56f), ShowCredits, Palette.Slate);
            UiFactory.Button(view.transform, "EXIT", new Vector2(0f, -180f), new Vector2(380f, 56f), Quit, new Color32(93, 49, 53, 255));
            UiFactory.Text(view.transform,
                "Educational simulation only - not medical training or advice. Content NOT medically reviewed; check it with a qualified first-aid trainer.",
                15, new Vector2(500f, 80f), new Vector2(0f, -300f), TextAlignmentOptions.Center, Palette.LightGrey);
        }

        public void ShowCampaign()
        {
            GameObject view = NewView("Campaign", new Vector2(1180f, 780f), Vector2.zero);
            UiFactory.Text(view.transform, "CAMPAIGN", 38, new Vector2(800f, 60f), new Vector2(0f, 340f), TextAlignmentOptions.Center).fontStyle =
                FontStyles.Bold;
            PlayerProfileData profile = ProfileStore.Load();
            profile.Get("tutorial").unlocked = true;
            float y = 220f;
            if (catalog != null)
            {
                foreach (ScenarioData scenario in catalog.scenarios)
                {
                    ScenarioProgress progress = profile.Get(scenario.scenarioId);
                    bool unlocked = progress.unlocked || scenario.order == 0;
                    string status = unlocked
                        ? (progress.plays > 0 ? DebriefScreen.Stars(progress.bestStars) + "   best " + progress.bestScore : "<color=#9AA4AD>not played</color>")
                        : "<color=#9AA4AD>locked - earn 1 star in the previous scenario</color>";
                    ScenarioData current = scenario;
                    Button button = UiFactory.Button(view.transform, (scenario.order + 1) + ".  " + scenario.title.ToUpperInvariant(),
                        new Vector2(-300f, y), new Vector2(460f, 64f), () => StartScenario(current), unlocked ? Palette.EmergencyRed : Palette.Slate);
                    button.interactable = unlocked;
                    UiFactory.Text(view.transform, scenario.description + "\n" + status, 16, new Vector2(620f, 92f), new Vector2(260f, y),
                        TextAlignmentOptions.MidlineLeft, Palette.LightGrey);
                    y -= 120f;
                }
            }

            AdaptiveDifficulty.DeteriorationScale(profile.recentRatios, out string band);
            UiFactory.Text(view.transform, "Difficulty: " + GameSettings.Difficulty + (GameSettings.Difficulty == Difficulty.Adaptive ? " (" + band + ")" : "") +
                                           "   |   Runs played: " + profile.totalRuns, 16, new Vector2(900f, 34f), new Vector2(0f, -268f),
                TextAlignmentOptions.Center, Palette.LightGrey);
            UiFactory.Button(view.transform, "BACK", new Vector2(0f, -330f), new Vector2(250f, 54f), ShowHome, Palette.Slate);
        }

        public void StartScenario(ScenarioData scenario)
        {
            Session.ScenarioId = scenario.scenarioId;
            string[] tips = scenario.loadingTips;
            SceneFader.LoadingText = "<b>" + scenario.title.ToUpperInvariant() + "</b>\n" +
                                     (tips != null && tips.Length > 0 ? "TIP: " + tips[Random.Range(0, tips.Length)] : string.Empty);
            SceneFader.LoadScene(GameScene);
        }

        /// <summary>Starts the scenario in <see cref="Session.ScenarioId"/> (used by tests and quick start).</summary>
        public void StartGame()
        {
            ScenarioData scenario = catalog != null ? catalog.Find(Session.ScenarioId) : null;
            if (scenario != null) StartScenario(scenario);
            else SceneFader.LoadScene(GameScene);
        }

        public void ShowHowToPlay()
        {
            GameObject view = NewView("How To Play", new Vector2(1180f, 800f), Vector2.zero);
            UiFactory.Text(view.transform, "HOW TO PLAY", 36, new Vector2(740f, 56f), new Vector2(0f, 352f), TextAlignmentOptions.Center).fontStyle =
                FontStyles.Bold;
            string instructions =
                "<b>MISSION</b>  You are first on scene at a mass-casualty incident. The <b>golden hour</b> clock never stops while you work.\n" +
                "1. <b>Scene safety</b>: collect the first-aid bag, put on gloves, spot and mark hazards, then radio the <b>112 dispatch report</b>.\n" +
                "2. <b>Walking wounded</b>: shout \"if you can walk, go to the green zone\".\n" +
                "3. <b>Examine by actions</b>: ask to walk, check breathing (open the airway if needed), check circulation, give a command. Each action costs time.\n" +
                "4. <b>Tag</b> with a physical triage tag. You may only change a tag after re-examining; only the first tag is fully scored.\n" +
                "5. <b>First aid</b>: hold a pressure dressing, roll into the recovery position, blankets for warmth. Supplies are limited.\n" +
                "6. <b>Hand over</b> to arriving ambulances in priority order. Untreated casualties deteriorate and can die.\n\n" +
                "<b>START (simplified educational model)</b>\n" +
                "Walks -> <color=#4CC07F>GREEN</color>. Not breathing after airway opening -> <b>BLACK</b>. Breathes only after airway opening, over 30 breaths/min, capillary refill over 2 s or no radial pulse, or cannot follow commands -> <color=#FF6B6B>RED</color>. Otherwise -> <color=#F2C14E>YELLOW</color>.\n" +
                "Simplifications: children are triaged with adult START here (real practice uses JumpSTART); timings and deterioration are illustrative.\n\n" +
                "<b>CONTROLS</b>  WASD/left stick move, mouse/right stick look, " + GameInput.KeyName(GameInput.Interact) + " interact, " +
                GameInput.KeyName(GameInput.Radio) + " radio, " + GameInput.KeyName(GameInput.Shout) + " shout, " + GameInput.KeyName(GameInput.Gloves) +
                " gloves, " + GameInput.KeyName(GameInput.Tablet) + " casualty tablet, " + GameInput.KeyName(GameInput.Crouch) + " kneel, " +
                GameInput.KeyName(GameInput.Primary) + " hold/compress, F10 photo mode, Esc pause. Keys can be rebound in Settings.\n\n" +
                "<b>DISCLAIMER</b>  Educational simulation only - not medical training, medical advice or a substitute for emergency services. " +
                "The content has NOT been medically reviewed and should be checked by a qualified first-aid trainer. Guidelines named in the game " +
                "(START triage; 112 as the European emergency number) should be verified against current official sources.";
            TMP_Text body = UiFactory.Text(view.transform, instructions, 18, new Vector2(1100f, 640f), new Vector2(0f, 4f), TextAlignmentOptions.TopLeft);
            body.enableAutoSizing = true;
            body.fontSizeMin = 12f;
            body.fontSizeMax = 18f;
            UiFactory.Button(view.transform, "BACK", new Vector2(0f, -352f), new Vector2(250f, 52f), ShowHome, Palette.Slate);
        }

        public void ShowSettings()
        {
            SettingsPanel.Build(NewView("Settings", new Vector2(1240f, 840f), Vector2.zero).transform, ShowSettings, ShowHome);
        }

        public void SetDifficulty(Difficulty difficulty)
        {
            GameSettings.Difficulty = difficulty;
            ShowSettings();
        }

        public void ShowCredits()
        {
            GameObject view = NewView("Credits", new Vector2(900f, 640f), Vector2.zero);
            UiFactory.Text(view.transform, "CREDITS", 36, new Vector2(700f, 56f), new Vector2(0f, 270f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            UiFactory.Text(view.transform,
                "Golden Hour - a Game Design / Immersive Technology course project.\n\n" +
                "Design, code and procedural content: the project team, with Claude Code (AI assistant).\n" +
                "3D models: generated with Blender 5.2 scripts in Assets/GoldenHour/Blender.\n" +
                "Audio: synthesised procedurally (generate_audio.py) - no third-party samples.\n" +
                "Engine: Unity 6.6 (URP, Input System, AI Navigation, TextMesh Pro with LiberationSans).\n\n" +
                "Full list: ASSET_CREDITS.md.\n\n" +
                "Not medical training. Content not medically reviewed.",
                19, new Vector2(800f, 420f), new Vector2(0f, 10f), TextAlignmentOptions.Center);
            UiFactory.Button(view.transform, "BACK", new Vector2(0f, -262f), new Vector2(250f, 52f), ShowHome, Palette.Slate);
        }

        public void ShowFeedback()
        {
            FeedbackForm.Build(NewView("Feedback", new Vector2(1200f, 820f), Vector2.zero).transform, ShowHome);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
