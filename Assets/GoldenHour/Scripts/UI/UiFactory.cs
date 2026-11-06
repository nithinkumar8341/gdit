using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoldenHour
{
    /// <summary>Builds the simple TMP/uGUI elements used by every Golden Hour screen.</summary>
    internal static class UiFactory
    {
        public const string VolumePrefKey = "GoldenHour.Volume";
        private static TMP_FontAsset font;

        public static Action ButtonPressed;

        public static Canvas CreateCanvas(string canvasName)
        {
            GameObject canvasObject = new GameObject(canvasName,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
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
            if (font == null)
            {
                font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            }

            if (font != null) component.font = font;
            component.text = text;
            component.fontSize = fontSize;
            component.alignment = alignment;
            component.color = color ?? Palette.White;
            component.textWrappingMode = TextWrappingModes.Normal;
            component.overflowMode = TextOverflowModes.Ellipsis;
            component.margin = new Vector4(8f, 4f, 8f, 4f);
            component.raycastTarget = false;
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
            image.color = color ?? Palette.EmergencyRed;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            // Tints multiply the base colour: normal is slightly dimmed so hover can brighten.
            colors.normalColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            colors.pressedColor = new Color(0.68f, 0.68f, 0.68f, 1f);
            button.colors = colors;

            TMP_Text text = Text(buttonObject.transform, label, 22,
                size - new Vector2(18f, 10f), Vector2.zero, TextAlignmentOptions.Center,
                IsLight(image.color) ? new Color32(20, 24, 28, 255) : Palette.White);
            text.fontStyle = FontStyles.Bold;
            text.enableAutoSizing = true;
            text.fontSizeMin = 13f;
            text.fontSizeMax = 22f;
            button.onClick.AddListener(() =>
            {
                ButtonPressed?.Invoke();
                onClick?.Invoke();
            });
            return button;
        }

        /// <summary>Master-volume slider bound to AudioListener.volume and PlayerPrefs.</summary>
        public static Slider VolumeSlider(Transform parent, Vector2 position, Vector2 size)
        {
            GameObject sliderObject = new GameObject("Master Volume",
                typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(parent, false);
            RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
            sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRect.anchoredPosition = position;
            sliderRect.sizeDelta = size;

            GameObject track = Panel(sliderObject.transform, "Track", Vector2.zero, Vector2.zero,
                new Color32(73, 82, 91, 255), true);
            RectTransform trackRect = track.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0f, 0.35f);
            trackRect.anchorMax = new Vector2(1f, 0.65f);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObject.transform, false);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.35f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.65f);
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;
            GameObject fill = Panel(fillArea.transform, "Fill", Vector2.zero, Vector2.zero,
                Palette.EmergencyRed, true);

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderObject.transform, false);
            RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(12f, 0f);
            handleAreaRect.offsetMax = new Vector2(-12f, 0f);
            GameObject handle = Panel(handleArea.transform, "Handle", new Vector2(24f, 0f),
                Vector2.zero, Color.white);
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0f, 0f);
            handleRect.anchorMax = new Vector2(0f, 1f);
            handleRect.sizeDelta = new Vector2(24f, 0f);

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(PlayerPrefs.GetFloat(VolumePrefKey, 0.8f));
            slider.onValueChanged.AddListener(value =>
            {
                AudioListener.volume = value;
                PlayerPrefs.SetFloat(VolumePrefKey, value);
                PlayerPrefs.Save();
            });
            return slider;
        }

        private static bool IsLight(Color color)
        {
            return color.r * 0.299f + color.g * 0.587f + color.b * 0.114f > 0.62f;
        }
    }
}
