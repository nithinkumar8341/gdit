using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GoldenHour
{
    public sealed class MainMenuController : MonoBehaviour
    {
        private const string GameScene = "Samplespace";
        private Canvas canvas;
        private GoldenHourAudio audioManager;

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Application.targetFrameRate = 60;
            audioManager = GetComponent<GoldenHourAudio>();
            UiFactory.ButtonPressed = audioManager != null ? audioManager.PlayButton : null;
            canvas = UiFactory.CreateCanvas("Golden Hour Main Menu");
            ShowHome();
        }

        private void ShowHome()
        {
            ClearView();
            GameObject view = UiFactory.Panel(canvas.transform, "Main Menu",
                new Vector2(620f, 700f), Vector2.zero, new Color32(22, 30, 39, 248));
            UiFactory.Text(view.transform, "GOLDEN HOUR", 48, new Vector2(560f, 80f),
                new Vector2(0f, 265f), TextAlignmentOptions.Center,
                new Color32(255, 255, 255, 255)).fontStyle = FontStyles.Bold;
            UiFactory.Text(view.transform, "ROAD-ACCIDENT RESCUE SIMULATION", 18,
                new Vector2(540f, 42f), new Vector2(0f, 220f),
                TextAlignmentOptions.Center, new Color32(229, 174, 76, 255));
            UiFactory.Button(view.transform, "START SCENARIO", new Vector2(0f, 135f),
                new Vector2(380f, 64f), () => SceneManager.LoadScene(GameScene));
            UiFactory.Button(view.transform, "HOW TO PLAY", new Vector2(0f, 50f),
                new Vector2(380f, 58f), ShowHowToPlay, new Color32(62, 76, 88, 255));
            UiFactory.Button(view.transform, "SETTINGS", new Vector2(0f, -28f),
                new Vector2(380f, 58f), ShowSettings, new Color32(62, 76, 88, 255));
            UiFactory.Button(view.transform, "EXIT", new Vector2(0f, -106f),
                new Vector2(380f, 58f), Application.Quit, new Color32(93, 49, 53, 255));
            UiFactory.Text(view.transform, "An educational simulation — not medical advice or training.",
                16, new Vector2(560f, 50f), new Vector2(0f, -260f),
                TextAlignmentOptions.Center, new Color32(200, 207, 213, 255));
        }

        private void ShowHowToPlay()
        {
            ClearView();
            GameObject view = UiFactory.Panel(canvas.transform, "How To Play",
                new Vector2(880f, 720f), Vector2.zero, new Color32(22, 30, 39, 248));
            UiFactory.Text(view.transform, "HOW TO PLAY", 38, new Vector2(740f, 60f),
                new Vector2(0f, 300f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            string instructions =
                "MISSION\nAssess five people involved in a road accident. Examine each person, assign a triage priority, then use an appropriate item from the first-aid kit.\n\n" +
                "CONTROLS\nWASD — move     Mouse — look around\nE — interact / examine     Esc — pause or resume\n\n" +
                "LEARNING TRIAGE MODEL\nThis simplified scenario uses a basic START-style educational model: walking patients are assigned GREEN; not breathing, severe visible bleeding, or unresponsive presentations are RED; other non-walking patients are YELLOW. Read the observations and decide before choosing. This simplified game is not a clinical protocol.\n\n" +
                "OBJECTIVES\nReach the scene, examine all five people, triage everyone, select a suitable supply, and complete the simulated aid steps.\n\n" +
                "DISCLAIMER\nThis is an educational simulation, not medical training, medical advice, diagnosis, or a substitute for emergency services. In a real emergency, contact local emergency services and follow qualified professional guidance.";
            UiFactory.Text(view.transform, instructions, 20, new Vector2(760f, 520f),
                new Vector2(0f, 10f), TextAlignmentOptions.TopLeft);
            UiFactory.Button(view.transform, "BACK", new Vector2(0f, -300f),
                new Vector2(250f, 54f), ShowHome, new Color32(62, 76, 88, 255));
        }

        private void ShowSettings()
        {
            ClearView();
            GameObject view = UiFactory.Panel(canvas.transform, "Settings",
                new Vector2(720f, 520f), Vector2.zero, new Color32(22, 30, 39, 248));
            UiFactory.Text(view.transform, "SETTINGS", 38, new Vector2(600f, 60f),
                new Vector2(0f, 190f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            UiFactory.Text(view.transform, "MASTER VOLUME", 22, new Vector2(500f, 50f),
                new Vector2(0f, 95f), TextAlignmentOptions.Center);

            GameObject sliderObject = new GameObject("Master Volume",
                typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(view.transform, false);
            RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
            sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRect.anchoredPosition = new Vector2(0f, 25f);
            sliderRect.sizeDelta = new Vector2(460f, 36f);
            Slider slider = sliderObject.GetComponent<Slider>();
            GameObject track = UiFactory.Panel(sliderObject.transform, "Track",
                new Vector2(440f, 12f), Vector2.zero, new Color32(73, 82, 91, 255));
            RectTransform trackRect = track.GetComponent<RectTransform>();
            trackRect.anchorMin = Vector2.zero;
            trackRect.anchorMax = Vector2.one;
            trackRect.offsetMin = Vector2.zero;
            trackRect.offsetMax = Vector2.zero;
            GameObject fill = UiFactory.Panel(sliderObject.transform, "Fill",
                new Vector2(440f, 12f), Vector2.zero, new Color32(194, 43, 52, 255));
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            GameObject handle = UiFactory.Panel(sliderObject.transform, "Handle",
                new Vector2(26f, 30f), Vector2.zero, Color.white);
            slider.fillRect = fillRect;
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = PlayerPrefs.GetFloat("GoldenHour.Volume", 0.8f);
            slider.onValueChanged.AddListener(value =>
            {
                AudioListener.volume = value;
                PlayerPrefs.SetFloat("GoldenHour.Volume", value);
            });
            UiFactory.Button(view.transform, "BACK", new Vector2(0f, -175f),
                new Vector2(250f, 56f), ShowHome, new Color32(62, 76, 88, 255));
        }

        private void ClearView()
        {
            for (int i = canvas.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(canvas.transform.GetChild(i).gameObject);
            }
        }
    }
}
