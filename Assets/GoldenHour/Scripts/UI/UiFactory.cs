using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoldenHour
{
    internal static class UiFactory
    {
        private static TMP_FontAsset font;

        public static Action ButtonPressed;

        public static Canvas CreateCanvas(string canvasName)
        {
            GameObject canvasObject = new GameObject(canvasName,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static GameObject Panel(Transform parent, string panelName, Vector2 size,
            Vector2 position, Color color, bool stretch = false)
        {
            GameObject panel = new GameObject(panelName, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
            }

            panel.GetComponent<Image>().color = color;
            return panel;
        }

        public static TMP_Text Text(Transform parent, string text, int fontSize,
            Vector2 size, Vector2 position, TextAlignmentOptions alignment = TextAlignmentOptions.Left,
            Color? color = null)
        {
            GameObject textObject = new GameObject("Text", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            TMP_Text component = textObject.GetComponent<TMP_Text>();
            component.text = text;
            component.fontSize = fontSize;
            component.alignment = alignment;
            component.color = color ?? new Color32(246, 247, 248, 255);
            component.textWrappingMode = TextWrappingModes.Normal;
            component.overflowMode = TextOverflowModes.Ellipsis;
            component.margin = new Vector4(8f, 4f, 8f, 4f);
            if (font == null)
            {
                font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            }

            if (font != null)
            {
                component.font = font;
            }

            return component;
        }

        public static Button Button(Transform parent, string label, Vector2 position,
            Vector2 size, Action onClick, Color? color = null)
        {
            GameObject buttonObject = new GameObject(label + " Button",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = buttonObject.GetComponent<Image>();
            image.color = color ?? new Color32(194, 43, 52, 255);
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(image.color, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(image.color, Color.black, 0.18f);
            button.colors = colors;

            TMP_Text text = Text(buttonObject.transform, label, 22,
                size - new Vector2(18f, 10f), Vector2.zero,
                TextAlignmentOptions.Center);
            text.fontStyle = FontStyles.Bold;
            button.onClick.AddListener(() =>
            {
                ButtonPressed?.Invoke();
                onClick?.Invoke();
            });
            return button;
        }
    }
}
