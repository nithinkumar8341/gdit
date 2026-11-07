using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoldenHour
{
    /// <summary>
    /// v2 gameplay HUD on a Screen Space - Overlay canvas: objectives, golden-hour clock and score, compass,
    /// supplies and gloves, radio messages, subtitles, prompt/status, action progress bar, score toasts,
    /// danger warning, tutorial tips and crosshair. Every text sits on a dark backing for legibility.
    /// </summary>
    public sealed class IncidentHud
    {
        private static readonly Color Backing = new Color32(14, 19, 26, 215);

        private readonly GameObject root;
        private readonly TMP_Text objectives;
        private readonly TMP_Text clock;
        private readonly TMP_Text supplies;
        private readonly TMP_Text prompt;
        private readonly TMP_Text status;
        private readonly TMP_Text radio;
        private readonly TMP_Text subtitle;
        private readonly TMP_Text danger;
        private readonly TMP_Text tip;
        private readonly TMP_Text compassText;
        private readonly TMP_Text progressLabel;
        private readonly GameObject promptBacking;
        private readonly GameObject statusBacking;
        private readonly GameObject radioPanel;
        private readonly GameObject subtitleBacking;
        private readonly GameObject dangerBacking;
        private readonly GameObject tipPanel;
        private readonly GameObject compass;
        private readonly RectTransform compassArrow;
        private readonly GameObject progressRoot;
        private readonly RectTransform progressFill;
        private readonly Transform toastRoot;
        private readonly List<(TMP_Text text, float end)> toasts = new List<(TMP_Text, float)>();
        private float statusEnd;
        private float radioEnd;
        private float subtitleEnd;
        private float tipEnd = -1f;

        public bool CompassVisible => compass.activeSelf;
        public string CompassLabel => compassText.text;
        public float CompassAngle { get; private set; }
        public string RadioText => radioPanel.activeSelf ? radio.text : string.Empty;
        public string StatusText => status.text;
        public bool TipVisible => tipPanel.activeSelf;
        public string TipText => tip.text;
        public bool DangerVisible => dangerBacking.activeSelf;
        public GameObject Root => root;
        public GameObject OverlayRoot { get; private set; }

        public IncidentHud(Transform canvas)
        {
            root = UiFactory.Panel(canvas, "Incident HUD", Vector2.zero, Vector2.zero, Color.clear, true);
            root.GetComponent<Image>().raycastTarget = false;

            objectives = PanelText(Corner(new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(480f, 196f)), 18,
                new Vector2(460f, 186f), TextAlignmentOptions.TopLeft);
            clock = PanelText(Corner(new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(300f, 100f)), 20,
                new Vector2(284f, 94f), TextAlignmentOptions.TopRight);
            clock.fontStyle = FontStyles.Bold;
            supplies = PanelText(Corner(new Vector2(1f, 1f), new Vector2(-24f, -134f), new Vector2(300f, 118f)), 17,
                new Vector2(284f, 112f), TextAlignmentOptions.TopLeft);

            compass = Corner(new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(360f, 60f));
            GameObject arrowRoot = new GameObject("Compass Arrow", typeof(RectTransform));
            arrowRoot.transform.SetParent(compass.transform, false);
            compassArrow = arrowRoot.GetComponent<RectTransform>();
            compassArrow.anchoredPosition = new Vector2(-148f, 0f);
            compassArrow.sizeDelta = new Vector2(40f, 40f);
            Bar(compassArrow, new Vector2(-6f, 10f), new Vector2(5f, 18f), -40f);
            Bar(compassArrow, new Vector2(6f, 10f), new Vector2(5f, 18f), 40f);
            Bar(compassArrow, new Vector2(0f, -6f), new Vector2(5f, 22f), 0f);
            compassText = UiFactory.Text(compass.transform, string.Empty, 17, new Vector2(290f, 56f), new Vector2(26f, 0f),
                TextAlignmentOptions.MidlineLeft);
            compass.SetActive(false);

            radioPanel = Corner(new Vector2(0f, 0f), new Vector2(24f, 110f), new Vector2(520f, 104f));
            radioPanel.GetComponent<Image>().color = new Color32(20, 34, 30, 225);
            radio = UiFactory.Text(radioPanel.transform, string.Empty, 17, new Vector2(500f, 98f), Vector2.zero, TextAlignmentOptions.TopLeft,
                new Color32(170, 240, 190, 255));
            radioPanel.SetActive(false);

            subtitle = Bottom(176f, 19, new Vector2(800f, 40f), new Color32(255, 246, 220, 255), out subtitleBacking);
            prompt = Bottom(126f, 23, new Vector2(820f, 44f), Palette.White, out promptBacking);
            prompt.fontStyle = FontStyles.Bold;
            status = Bottom(80f, 18, new Vector2(1040f, 40f), Palette.Warning, out statusBacking);
            Bottom(30f, 15, new Vector2(1180f, 32f), Palette.LightGrey, out _).text = ControlsLine();

            danger = PanelText(Corner(new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(620f, 52f)), 22,
                new Vector2(600f, 48f), TextAlignmentOptions.Center, new Color32(255, 120, 110, 255));
            danger.fontStyle = FontStyles.Bold;
            dangerBacking = danger.transform.parent.gameObject;
            dangerBacking.GetComponent<Image>().color = new Color32(80, 10, 14, 220);
            dangerBacking.SetActive(false);

            tipPanel = UiFactory.Panel(root.transform, "Tip", new Vector2(700f, 96f), new Vector2(0f, -150f), new Color32(120, 26, 32, 228));
            tipPanel.GetComponent<Image>().raycastTarget = false;
            tip = UiFactory.Text(tipPanel.transform, string.Empty, 18, new Vector2(670f, 90f), Vector2.zero, TextAlignmentOptions.Center);
            tipPanel.SetActive(false);

            Canvas overlay = UiFactory.CreateCanvas("Incident Overlay");
            overlay.sortingOrder = 40;
            OverlayRoot = overlay.gameObject;
            progressRoot = UiFactory.Panel(overlay.transform, "Action Progress", new Vector2(420f, 58f), new Vector2(0f, -110f), Backing);
            progressRoot.GetComponent<Image>().raycastTarget = false;
            progressLabel = UiFactory.Text(progressRoot.transform, string.Empty, 16, new Vector2(400f, 26f), new Vector2(0f, 12f),
                TextAlignmentOptions.Center);
            GameObject track = UiFactory.Panel(progressRoot.transform, "Track", new Vector2(380f, 12f), new Vector2(0f, -12f),
                new Color32(60, 66, 74, 255));
            GameObject fill = UiFactory.Panel(track.transform, "Fill", Vector2.zero, Vector2.zero, Palette.EmergencyRed, true);
            progressFill = fill.GetComponent<RectTransform>();
            progressFill.anchorMax = new Vector2(0f, 1f);
            progressRoot.SetActive(false);

            toastRoot = new GameObject("Toasts", typeof(RectTransform)).transform;
            toastRoot.SetParent(overlay.transform, false);

            UiFactory.Panel(root.transform, "Crosshair", new Vector2(6f, 6f), Vector2.zero, new Color32(255, 255, 255, 230))
                .GetComponent<Image>().raycastTarget = false;
            promptBacking.SetActive(false);
            statusBacking.SetActive(false);
            subtitleBacking.SetActive(false);
        }

        public static string ControlsLine()
        {
            return "WASD move  |  " + GameInput.KeyName(GameInput.Interact) + " interact  |  " + GameInput.KeyName(GameInput.Radio) +
                   " radio  |  " + GameInput.KeyName(GameInput.Shout) + " shout  |  " + GameInput.KeyName(GameInput.Gloves) +
                   " gloves  |  " + GameInput.KeyName(GameInput.Tablet) + " tablet  |  " + GameInput.KeyName(GameInput.Crouch) +
                   " kneel  |  Esc pause";
        }

        public void SetVisible(bool visible) => root.SetActive(visible);

        /// <summary>Removes pending score toasts (used when the debrief opens).</summary>
        public void ClearToasts()
        {
            foreach ((TMP_Text text, float end) toast in toasts)
            {
                if (toast.text != null) Object.Destroy(toast.text.gameObject);
            }

            toasts.Clear();
            pendingToasts.Clear();
        }
        public void SetObjectives(string text) => objectives.text = text;
        public string ObjectivesText => objectives.text;

        /// <summary>Canvas-space rectangles of the tip and radio panels (tests: overlap check).</summary>
        public Rect TipRect => CanvasRect(tipPanel);
        public Rect RadioRect => CanvasRect(radioPanel);

        private Rect CanvasRect(GameObject panel)
        {
            RectTransform rect = (RectTransform)panel.transform;
            RectTransform parent = (RectTransform)root.transform;
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = parent.InverseTransformPoint(corners[0]);
            Vector2 max = parent.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        public void SetClock(string text) => clock.text = text;
        public void SetSupplies(string text) => supplies.text = text;

        public void SetPrompt(string text)
        {
            prompt.text = text;
            promptBacking.SetActive(!string.IsNullOrEmpty(text));
        }

        public void SetStatus(string text, float seconds = 5f)
        {
            status.text = text;
            statusBacking.SetActive(!string.IsNullOrEmpty(text));
            statusEnd = Time.time + seconds;
        }

        public void ShowRadio(string text, float seconds = 7f)
        {
            radio.text = "<b>RADIO</b>  " + text;
            radioPanel.SetActive(true);
            radioEnd = Time.time + seconds;
        }

        public void ShowSubtitle(string text, float seconds = 3f)
        {
            if (!GameSettings.Subtitles) return;
            subtitle.text = text;
            subtitleBacking.SetActive(true);
            subtitleEnd = Time.time + seconds;
        }

        public void ShowTip(string text, float seconds)
        {
            tip.text = text;
            tipPanel.SetActive(!string.IsNullOrEmpty(text));
            tipEnd = seconds > 0f ? Time.time + seconds : -1f;
        }

        public void SetDanger(string text)
        {
            bool show = !string.IsNullOrEmpty(text);
            if (dangerBacking.activeSelf != show) dangerBacking.SetActive(show);
            if (show) danger.text = text;
        }

        public void SetProgress(string label, float progress01, bool visible)
        {
            if (progressRoot.activeSelf != visible) progressRoot.SetActive(visible);
            if (!visible) return;
            progressRoot.transform.SetAsLastSibling();
            progressLabel.text = label;
            progressFill.anchorMax = new Vector2(Mathf.Clamp01(progress01), 1f);
        }

        public const float ToastInterval = 0.3f;
        public const float ToastSeconds = 2.6f;
        private readonly Queue<(int points, string reason)> pendingToasts = new Queue<(int, string)>();
        private float nextToastTime;

        /// <summary>Set by the director each frame: while a modal is open, toasts move to the right-hand column.</summary>
        public bool ModalOpen { get; set; }

        /// <summary>Toasts on screen plus queued ones (tests).</summary>
        public int ToastCount => toasts.Count + pendingToasts.Count;

        /// <summary>Screen rectangles (canvas units) of the visible toasts (tests: overlap and modal checks).</summary>
        public List<Rect> ToastRects()
        {
            List<Rect> rects = new List<Rect>();
            foreach ((TMP_Text text, float end) toast in toasts)
            {
                if (toast.text == null) continue;
                RectTransform rect = toast.text.rectTransform;
                Vector2 size = rect.sizeDelta;
                rects.Add(new Rect(rect.anchoredPosition - size * 0.5f, size));
            }

            return rects;
        }

        /// <summary>Queues a score message; one appears every <see cref="ToastInterval"/> seconds so they never stack.</summary>
        public void Toast(int points, string reason)
        {
            pendingToasts.Enqueue((points, reason));
        }

        private void SpawnToast(int points, string reason)
        {
            Color color = points >= 0 ? new Color32(127, 224, 166, 255) : new Color32(255, 138, 138, 255);
            TMP_Text text = UiFactory.Text(toastRoot, (points >= 0 ? "+" : "") + points + "  " + reason, 20, new Vector2(700f, 34f),
                ToastSlot(0), TextAlignmentOptions.Center, color);
            text.fontStyle = FontStyles.Bold;
            text.outlineWidth = 0.18f;
            text.outlineColor = new Color32(10, 14, 18, 255);
            text.enableAutoSizing = true;
            text.fontSizeMin = 13f;
            text.fontSizeMax = 20f;
            if (toasts.Count >= 5)
            {
                if (toasts[0].text != null) Object.Destroy(toasts[0].text.gameObject);
                toasts.RemoveAt(0);
            }

            toasts.Add((text, Time.time + ToastSeconds));
        }

        /// <summary>Normal play: centred above the prompt. Modal open: a right-hand column beside the panel.</summary>
        private Vector2 ToastSlot(int slot) => ModalOpen ? new Vector2(711f, 150f - slot * 72f) : new Vector2(0f, 60f + slot * 34f);

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
            if (status.text.Length > 0 && Time.time > statusEnd) SetStatus(string.Empty);
            if (radioPanel.activeSelf && Time.time > radioEnd) radioPanel.SetActive(false);
            if (subtitleBacking.activeSelf && Time.time > subtitleEnd) subtitleBacking.SetActive(false);
            if (tipEnd > 0f && Time.time > tipEnd)
            {
                tipEnd = -1f;
                tipPanel.SetActive(false);
            }

            if (pendingToasts.Count > 0 && Time.unscaledTime >= nextToastTime)
            {
                (int points, string reason) next = pendingToasts.Dequeue();
                SpawnToast(next.points, next.reason);
                nextToastTime = Time.unscaledTime + ToastInterval;
            }

            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                if (toasts[i].text == null) continue;
                RectTransform rect = toasts[i].text.rectTransform;
                rect.sizeDelta = ModalOpen ? new Vector2(170f, 66f) : new Vector2(700f, 34f);
                toasts[i].text.fontSizeMax = ModalOpen ? 15f : 20f;
                toasts[i].text.alignment = ModalOpen ? TextAlignmentOptions.Right : TextAlignmentOptions.Center;
                rect.anchoredPosition = ToastSlot(toasts.Count - 1 - i);
                if (Time.time > toasts[i].end)
                {
                    Object.Destroy(toasts[i].text.gameObject);
                    toasts.RemoveAt(i);
                }
            }
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

        private static TMP_Text PanelText(GameObject panel, int size, Vector2 box, TextAlignmentOptions alignment, Color? color = null)
        {
            TMP_Text text = UiFactory.Text(panel.transform, string.Empty, size, box, Vector2.zero, alignment, color);
            text.enableAutoSizing = true;
            text.fontSizeMin = 11f;
            text.fontSizeMax = size;
            return text;
        }

        private TMP_Text Bottom(float y, int size, Vector2 box, Color color, out GameObject backing)
        {
            backing = UiFactory.Panel(root.transform, "HUD Backing", box, Vector2.zero, Backing);
            backing.GetComponent<Image>().raycastTarget = false;
            RectTransform rect = backing.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, y);
            TMP_Text text = UiFactory.Text(backing.transform, string.Empty, size, box, Vector2.zero, TextAlignmentOptions.Center, color);
            text.enableAutoSizing = true;
            text.fontSizeMin = 11f;
            text.fontSizeMax = size;
            return text;
        }

        private static void Bar(Transform parent, Vector2 position, Vector2 size, float rotation)
        {
            GameObject bar = UiFactory.Panel(parent, "Arrow", size, position, Palette.Warning);
            bar.GetComponent<Image>().raycastTarget = false;
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }
    }
}
