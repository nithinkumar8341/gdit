using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GoldenHour
{
    /// <summary>Main menu: Start, How to Play (with disclaimer), Settings (volume) and Exit.</summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        public const string GameScene = "Samplespace";
        private static readonly Color PanelColor = new Color32(22, 30, 39, 248);

        private Canvas canvas;
        private GameObject currentView;

        public string CurrentView => currentView != null ? currentView.name : string.Empty;

        private void Awake()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Application.targetFrameRate = 60;
            AudioListener.volume = PlayerPrefs.GetFloat(UiFactory.VolumePrefKey, 0.8f);
            GoldenHourAudio audioManager = GetComponent<GoldenHourAudio>();
            UiFactory.ButtonPressed = audioManager != null ? audioManager.PlayButton : (System.Action)null;
            canvas = UiFactory.CreateCanvas("Golden Hour Main Menu");
            UiFactory.Panel(canvas.transform, "Header Stripe", new Vector2(4000f, 14f),
                new Vector2(0f, 420f), Palette.EmergencyRed);
            ShowHome();
        }

        public void ShowHome()
        {
            GameObject view = NewView("Main Menu", new Vector2(620f, 700f));
            UiFactory.Text(view.transform, "GOLDEN HOUR", 54, new Vector2(560f, 80f),
                new Vector2(0f, 265f), TextAlignmentOptions.Center, Palette.White).fontStyle = FontStyles.Bold;
            UiFactory.Text(view.transform, "ROAD-ACCIDENT RESCUE & TRIAGE SIMULATION", 18,
                new Vector2(560f, 42f), new Vector2(0f, 210f), TextAlignmentOptions.Center,
                new Color32(240, 120, 120, 255));
            UiFactory.Button(view.transform, "START SCENARIO", new Vector2(0f, 120f),
                new Vector2(380f, 66f), StartGame);
            UiFactory.Button(view.transform, "HOW TO PLAY", new Vector2(0f, 38f),
                new Vector2(380f, 58f), ShowHowToPlay, Palette.Slate);
            UiFactory.Button(view.transform, "SETTINGS", new Vector2(0f, -36f),
                new Vector2(380f, 58f), ShowSettings, Palette.Slate);
            UiFactory.Button(view.transform, "EXIT", new Vector2(0f, -110f),
                new Vector2(380f, 58f), Quit, new Color32(93, 49, 53, 255));
            UiFactory.Text(view.transform,
                "Educational simulation only - not medical advice or training.",
                17, new Vector2(560f, 60f), new Vector2(0f, -270f), TextAlignmentOptions.Center,
                Palette.LightGrey);
        }

        public void ShowHowToPlay()
        {
            GameObject view = NewView("How To Play", new Vector2(980f, 780f));
            UiFactory.Text(view.transform, "HOW TO PLAY", 38, new Vector2(740f, 60f),
                new Vector2(0f, 340f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            string instructions =
                "<b>MISSION</b>\nYou arrive first at a road accident. Find the five people involved, examine each one, assign a triage priority, then use the first-aid kit to complete a simple simulated aid step for everyone.\n\n" +
                "<b>CONTROLS</b>\nWASD move  |  Mouse look  |  Shift walk faster  |  E examine / open kit  |  Esc pause or close a panel\n\n" +
                "<b>LEARNING TRIAGE MODEL (simplified, START-style)</b>\n" +
                "<color=#4CC07F>GREEN / Minor</color>: the person can walk.\n" +
                "<color=#FF6B6B>RED / Immediate</color>: not breathing, severe visible bleeding, or unresponsive.\n" +
                "<color=#F2C14E>YELLOW / Delayed</color>: cannot walk but none of the RED signs.\n" +
                "Read the observations and decide; the expected answer is only shown after your choice. You can reassess anyone.\n\n" +
                "<b>SCORING</b>\nCorrect triage +100, completed simulated aid +75, objectives +25 each. Wrong triage -15, wrong supply -10. A small time bonus (max +50) never outweighs accuracy.\n\n" +
                "<b>DISCLAIMER</b>\nThis is an educational simulation, not medical training, medical advice or a substitute for emergency services. In a real emergency, call local emergency services and follow qualified professional guidance.";
            TMP_Text body = UiFactory.Text(view.transform, instructions, 19, new Vector2(900f, 580f),
                new Vector2(0f, 0f), TextAlignmentOptions.TopLeft);
            body.enableAutoSizing = true;
            body.fontSizeMin = 13f;
            body.fontSizeMax = 19f;
            UiFactory.Button(view.transform, "BACK", new Vector2(0f, -340f),
                new Vector2(250f, 54f), ShowHome, Palette.Slate);
        }

        public void ShowSettings()
        {
            GameObject view = NewView("Settings", new Vector2(720f, 520f));
            UiFactory.Text(view.transform, "SETTINGS", 38, new Vector2(600f, 60f),
                new Vector2(0f, 190f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            UiFactory.Text(view.transform, "MASTER VOLUME", 22, new Vector2(500f, 50f),
                new Vector2(0f, 95f), TextAlignmentOptions.Center);
            UiFactory.VolumeSlider(view.transform, new Vector2(0f, 30f), new Vector2(460f, 36f));
            UiFactory.Text(view.transform, "Saved automatically. Also available from the in-game pause menu.",
                16, new Vector2(600f, 40f), new Vector2(0f, -40f), TextAlignmentOptions.Center, Palette.LightGrey);
            UiFactory.Button(view.transform, "BACK", new Vector2(0f, -175f),
                new Vector2(250f, 56f), ShowHome, Palette.Slate);
        }

        public void StartGame()
        {
            SceneManager.LoadScene(GameScene);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private GameObject NewView(string viewName, Vector2 size)
        {
            if (currentView != null)
            {
                currentView.SetActive(false);
                Destroy(currentView);
            }

            currentView = UiFactory.Panel(canvas.transform, viewName, size, Vector2.zero, PanelColor);
            return currentView;
        }
    }
}
