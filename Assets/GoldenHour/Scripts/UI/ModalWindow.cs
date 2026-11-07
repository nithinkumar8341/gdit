using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoldenHour
{
    /// <summary>
    /// A single modal: either full-screen (dimmed backdrop + centred panel) or docked (a compact panel on the
    /// right-hand side with no backdrop, so the 3D scene stays visible). Tracks the current panel by
    /// reference, so replacing a modal in the same frame never attaches buttons to a panel that is pending
    /// destruction.
    /// </summary>
    public sealed class ModalWindow
    {
        /// <summary>Docked panel width and the space kept free above it (clock and supplies) and below it (status line).</summary>
        public const float DockWidth = 540f;
        public const float DockTop = 262f;
        public const float DockBottom = 110f;
        public const float DockMargin = 24f;

        private readonly Transform root;
        private GameObject backdrop;

        public ModalWindow(Transform canvasRoot)
        {
            root = canvasRoot;
        }

        public bool IsOpen => backdrop != null;
        public bool Closable { get; private set; }
        public bool Docked { get; private set; }
        public bool Hidden => backdrop != null && !backdrop.activeSelf;
        public string Title { get; private set; } = string.Empty;
        public Transform Panel { get; private set; }
        public Vector2 Size { get; private set; }

        /// <summary>Opens a full-screen modal. <paramref name="reservedBottom"/> is the height kept for buttons.</summary>
        public Transform Show(string title, string body, Vector2 size, float reservedBottom = 110f,
            bool closable = true)
        {
            Close();
            Title = title;
            Size = size;
            Closable = closable;
            Docked = false;
            backdrop = UiFactory.Panel(root, "Modal Backdrop", Vector2.zero, Vector2.zero,
                new Color32(5, 9, 14, 175), true);
            backdrop.transform.SetAsLastSibling();
            GameObject panel = UiFactory.Panel(backdrop.transform, "Modal Panel", size,
                Vector2.zero, Palette.Charcoal);
            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = Palette.EmergencyRed;
            outline.effectDistance = new Vector2(3f, -3f);
            Panel = panel.transform;

            TMP_Text titleText = UiFactory.Text(Panel, title, 30, new Vector2(size.x - 60f, 56f),
                new Vector2(0f, size.y * 0.5f - 44f), TextAlignmentOptions.Center, Palette.White);
            titleText.fontStyle = FontStyles.Bold;

            float top = size.y * 0.5f - 84f;
            float bottom = -size.y * 0.5f + reservedBottom;
            TMP_Text bodyText = UiFactory.Text(Panel, body, 20,
                new Vector2(size.x - 80f, Mathf.Max(40f, top - bottom)),
                new Vector2(0f, (top + bottom) * 0.5f), TextAlignmentOptions.TopLeft);
            bodyText.enableAutoSizing = true;
            bodyText.fontSizeMin = 14f;
            bodyText.fontSizeMax = 20f;
            bodyText.overflowMode = TextOverflowModes.Truncate;
            return Panel;
        }

        /// <summary>
        /// Opens a compact panel docked to the right edge, between the supplies panel and the status line. There is
        /// no backdrop: the world (and the casualty) stays visible on the left. <paramref name="buttonRows"/> rows of
        /// <see cref="RowY"/> are kept free at the bottom.
        /// </summary>
        public Transform ShowDocked(string title, string body, int buttonRows, bool closable = true)
        {
            Close();
            Title = title;
            Closable = closable;
            Docked = true;
            float canvasHeight = root is RectTransform canvasRect && canvasRect.rect.height > 1f ? canvasRect.rect.height : 900f;
            Size = new Vector2(DockWidth, Mathf.Clamp(canvasHeight - DockTop - DockBottom, 400f, 620f));

            backdrop = UiFactory.Panel(root, "Docked Panel", Size, Vector2.zero, new Color32(17, 21, 27, 238));
            backdrop.transform.SetAsLastSibling();
            RectTransform rect = backdrop.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-DockMargin, -DockTop);
            Outline outline = backdrop.AddComponent<Outline>();
            outline.effectColor = Palette.EmergencyRed;
            outline.effectDistance = new Vector2(2f, -2f);
            Panel = backdrop.transform;

            GameObject band = UiFactory.Panel(Panel, "Title Band", new Vector2(Size.x, 40f), new Vector2(0f, Size.y * 0.5f - 20f),
                Palette.EmergencyRed);
            band.GetComponent<Image>().raycastTarget = false;
            TMP_Text titleText = UiFactory.Text(band.transform, title, 21, new Vector2(Size.x - 30f, 38f), Vector2.zero,
                TextAlignmentOptions.MidlineLeft, Palette.White);
            titleText.fontStyle = FontStyles.Bold;

            float top = Size.y * 0.5f - 46f;
            float bottom = RowY(buttonRows) - 26f;
            TMP_Text bodyText = UiFactory.Text(Panel, body, 17,
                new Vector2(Size.x - 24f, Mathf.Max(40f, top - bottom)),
                new Vector2(0f, (top + bottom) * 0.5f), TextAlignmentOptions.TopLeft);
            bodyText.enableAutoSizing = true;
            bodyText.fontSizeMin = 12f;
            bodyText.fontSizeMax = 17f;
            bodyText.overflowMode = TextOverflowModes.Truncate;
            return Panel;
        }

        /// <summary>Y position for a button row, counted up from the panel's bottom edge.</summary>
        public float RowY(int rowFromBottom)
        {
            return Docked ? -Size.y * 0.5f + 30f + rowFromBottom * 46f : -Size.y * 0.5f + 48f + rowFromBottom * 66f;
        }

        /// <summary>The open panel's rectangle in canvas units, origin at the canvas centre (tests: layout checks).</summary>
        public Rect PanelCanvasRect()
        {
            if (Panel == null || !(root is RectTransform parent)) return Rect.zero;
            Vector3[] corners = new Vector3[4];
            ((RectTransform)Panel).GetWorldCorners(corners);
            Vector2 min = parent.InverseTransformPoint(corners[0]);
            Vector2 max = parent.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>Temporarily hides the modal (it stays open) so the scene behind is visible.</summary>
        public void SetHidden(bool hidden)
        {
            if (backdrop != null) backdrop.SetActive(!hidden);
        }

        public void Close()
        {
            if (backdrop != null)
            {
                backdrop.SetActive(false);
                Object.Destroy(backdrop);
            }

            backdrop = null;
            Panel = null;
            Docked = false;
            Title = string.Empty;
        }
    }
}
