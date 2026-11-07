using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoldenHour
{
    /// <summary>
    /// Always-visible gameplay HUD on a Screen Space - Overlay canvas (always drawn over the 3D
    /// world): objectives, score/timer, a compass arrow to the next target, prompt, status,
    /// controls hint, a timed tutorial tip and the crosshair. Every text sits on its own dark
    /// backing so road markings or bright scenery never reduce legibility.
    /// </summary>
    public sealed class GameplayHud
    {
        private static readonly Color Backing = new Color32(14, 19, 26, 210);

        private readonly TMP_Text objectivesText;
        private readonly TMP_Text scoreText;
        private readonly TMP_Text promptText;
        private readonly TMP_Text statusText;
        private readonly TMP_Text compassText;
        private readonly TMP_Text tutorialText;
        private readonly GameObject promptBacking;
        private readonly GameObject statusBacking;
        private readonly GameObject compass;
        private readonly RectTransform compassArrow;
        private readonly CanvasGroup tutorialGroup;
        private readonly GameObject root;
        private float statusClearTime;
        private float tutorialEndTime = -1f;

        public const float TutorialSeconds = 15f;

        public bool TutorialVisible => tutorialGroup.gameObject.activeSelf && tutorialGroup.alpha > 0.01f;
        public bool CompassVisible => compass.activeSelf;
        public string CompassLabel => compassText.text;
        public float CompassAngle { get; private set; }

        public GameplayHud(Transform canvas)
        {
            root = UiFactory.Panel(canvas, "HUD", Vector2.zero, Vector2.zero, Color.clear, true);
            root.GetComponent<Image>().raycastTarget = false;

            GameObject objectivesPanel = Corner(new Vector2(0f, 1f), new Vector2(24f, -24f),
                new Vector2(460f, 168f));
            objectivesText = UiFactory.Text(objectivesPanel.transform, string.Empty, 19,
                new Vector2(440f, 158f), Vector2.zero, TextAlignmentOptions.TopLeft);

            GameObject scorePanel = Corner(new Vector2(1f, 1f), new Vector2(-24f, -24f),
                new Vector2(250f, 84f));
            scoreText = UiFactory.Text(scorePanel.transform, string.Empty, 22,
                new Vector2(230f, 76f), Vector2.zero, TextAlignmentOptions.MidlineRight);
            scoreText.fontStyle = FontStyles.Bold;

            // Compass: an arrow pointing to the nearest person who still needs examining (or the kit).
            compass = Corner(new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(340f, 64f));
            GameObject arrowRoot = new GameObject("Compass Arrow", typeof(RectTransform));
            arrowRoot.transform.SetParent(compass.transform, false);
            compassArrow = arrowRoot.GetComponent<RectTransform>();
            compassArrow.anchoredPosition = new Vector2(-136f, 0f);
            compassArrow.sizeDelta = new Vector2(40f, 40f);
            ArrowBar(compassArrow, new Vector2(-6f, 4f), -40f);
            ArrowBar(compassArrow, new Vector2(6f, 4f), 40f);
            UiFactory.Panel(compassArrow, "Arrow Stem", new Vector2(5f, 22f), new Vector2(0f, -6f), Palette.Warning)
                .GetComponent<Image>().raycastTarget = false;
            compassText = UiFactory.Text(compass.transform, string.Empty, 18, new Vector2(270f, 58f),
                new Vector2(28f, 0f), TextAlignmentOptions.MidlineLeft);

            promptText = BottomText(126f, 24, new Vector2(800f, 46f), Palette.White, out promptBacking);
            promptText.fontStyle = FontStyles.Bold;
            statusText = BottomText(80f, 19, new Vector2(1000f, 40f), Palette.Warning, out statusBacking);
            BottomText(30f, 16, new Vector2(760f, 34f), Palette.LightGrey, out _).text =
                "WASD move   |   Mouse look   |   Shift faster   |   E interact   |   Esc pause";

            GameObject tutorial = UiFactory.Panel(root.transform, "Tutorial Tip", new Vector2(760f, 92f),
                new Vector2(0f, -230f), new Color32(120, 26, 32, 225));
            tutorial.GetComponent<Image>().raycastTarget = false;
            tutorialGroup = tutorial.AddComponent<CanvasGroup>();
            tutorialGroup.blocksRaycasts = false;
            tutorialText = UiFactory.Text(tutorial.transform, string.Empty, 19, new Vector2(740f, 86f),
                Vector2.zero, TextAlignmentOptions.Center);
            tutorial.SetActive(false);

            GameObject crosshair = UiFactory.Panel(root.transform, "Crosshair",
                new Vector2(6f, 6f), Vector2.zero, new Color32(255, 255, 255, 230));
            crosshair.GetComponent<Image>().raycastTarget = false;
            compass.SetActive(false);
            promptBacking.SetActive(false);
            statusBacking.SetActive(false);
        }

        public void SetObjectives(string text) => objectivesText.text = text;

        public void SetScore(int score, string time)
        {
            scoreText.text = "SCORE  " + score + "\nTIME  " + time;
        }

        public void SetPrompt(string text)
        {
            promptText.text = text;
            promptBacking.SetActive(!string.IsNullOrEmpty(text));
        }

        public void SetStatus(string text, float seconds = 6f)
        {
            statusText.text = text;
            statusBacking.SetActive(!string.IsNullOrEmpty(text));
            statusClearTime = Time.time + seconds;
        }

        /// <summary>Shows the first-time tutorial tip; it fades out after <see cref="TutorialSeconds"/>.</summary>
        public void ShowTutorial(string text)
        {
            tutorialText.text = text;
            tutorialGroup.gameObject.SetActive(true);
            tutorialGroup.alpha = 1f;
            tutorialEndTime = Time.time + TutorialSeconds;
        }

        /// <summary>Points the compass. <paramref name="angle"/> is degrees clockwise from straight ahead.</summary>
        public void SetCompass(bool visible, float angle, string label)
        {
            if (compass.activeSelf != visible) compass.SetActive(visible);
            if (!visible) return;
            CompassAngle = angle;
            compassArrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
            compassText.text = label;
        }

        public void Tick()
        {
            if (statusText.text.Length > 0 && Time.time > statusClearTime)
            {
                SetStatus(string.Empty);
            }

            if (tutorialEndTime > 0f)
            {
                float remaining = tutorialEndTime - Time.time;
                tutorialGroup.alpha = Mathf.Clamp01(remaining / 1.2f);
                if (remaining <= 0f)
                {
                    tutorialEndTime = -1f;
                    tutorialGroup.gameObject.SetActive(false);
                }
            }
        }

        public void SetVisible(bool visible) => root.SetActive(visible);

        private void ArrowBar(Transform parent, Vector2 position, float rotation)
        {
            GameObject bar = UiFactory.Panel(parent, "Arrow Head", new Vector2(5f, 18f), position, Palette.Warning);
            bar.GetComponent<Image>().raycastTarget = false;
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            bar.GetComponent<RectTransform>().anchoredPosition = position + new Vector2(0f, 6f);
        }

        private GameObject Corner(Vector2 anchor, Vector2 position, Vector2 size)
        {
            GameObject panel = UiFactory.Panel(root.transform, "HUD Panel", size, Vector2.zero, Backing);
            panel.GetComponent<Image>().raycastTarget = false;
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            return panel;
        }

        private TMP_Text BottomText(float y, int size, Vector2 box, Color color, out GameObject backing)
        {
            backing = UiFactory.Panel(root.transform, "HUD Backing", box, Vector2.zero, Backing);
            backing.GetComponent<Image>().raycastTarget = false;
            RectTransform backingRect = backing.GetComponent<RectTransform>();
            backingRect.anchorMin = new Vector2(0.5f, 0f);
            backingRect.anchorMax = new Vector2(0.5f, 0f);
            backingRect.anchoredPosition = new Vector2(0f, y);

            TMP_Text text = UiFactory.Text(backing.transform, string.Empty, size, box, Vector2.zero,
                TextAlignmentOptions.Center, color);
            return text;
        }
    }
}
