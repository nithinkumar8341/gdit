using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoldenHour
{
    /// <summary>Always-visible gameplay HUD: objectives, score/timer, prompt, status and crosshair.</summary>
    public sealed class GameplayHud
    {
        private readonly TMP_Text objectivesText;
        private readonly TMP_Text scoreText;
        private readonly TMP_Text promptText;
        private readonly TMP_Text statusText;
        private readonly GameObject root;
        private float statusClearTime;

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

            promptText = BottomText(122f, 24, new Vector2(800f, 46f), Palette.White);
            promptText.fontStyle = FontStyles.Bold;
            statusText = BottomText(80f, 19, new Vector2(1000f, 40f), Palette.Warning);
            BottomText(30f, 16, new Vector2(800f, 34f), Palette.LightGrey).text =
                "WASD move   |   Mouse look   |   E interact   |   Esc pause";

            GameObject crosshair = UiFactory.Panel(root.transform, "Crosshair",
                new Vector2(6f, 6f), Vector2.zero, new Color32(255, 255, 255, 230));
            crosshair.GetComponent<Image>().raycastTarget = false;
        }

        public void SetObjectives(string text) => objectivesText.text = text;

        public void SetScore(int score, string time)
        {
            scoreText.text = "SCORE  " + score + "\nTIME  " + time;
        }

        public void SetPrompt(string text) => promptText.text = text;

        public void SetStatus(string text, float seconds = 6f)
        {
            statusText.text = text;
            statusClearTime = Time.time + seconds;
        }

        public void Tick()
        {
            if (statusText.text.Length > 0 && Time.time > statusClearTime)
            {
                statusText.text = string.Empty;
            }
        }

        public void SetVisible(bool visible) => root.SetActive(visible);

        private GameObject Corner(Vector2 anchor, Vector2 position, Vector2 size)
        {
            GameObject panel = UiFactory.Panel(root.transform, "HUD Panel", size, Vector2.zero,
                new Color32(18, 24, 31, 200));
            panel.GetComponent<Image>().raycastTarget = false;
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            return panel;
        }

        private TMP_Text BottomText(float y, int size, Vector2 box, Color color)
        {
            TMP_Text text = UiFactory.Text(root.transform, string.Empty, size, box, Vector2.zero,
                TextAlignmentOptions.Center, color);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, y);
            text.outlineWidth = 0.15f;
            text.outlineColor = new Color32(10, 12, 16, 255);
            return text;
        }
    }
}
