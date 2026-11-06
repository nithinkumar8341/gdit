using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoldenHour
{
    /// <summary>
    /// A single full-screen modal (backdrop + panel + title + body). Tracks the current panel by
    /// reference, so replacing a modal in the same frame never attaches buttons to a panel
    /// that is pending destruction.
    /// </summary>
    public sealed class ModalWindow
    {
        private readonly Transform root;
        private GameObject backdrop;

        public ModalWindow(Transform canvasRoot)
        {
            root = canvasRoot;
        }

        public bool IsOpen => backdrop != null;
        public bool Closable { get; private set; }
        public string Title { get; private set; } = string.Empty;
        public Transform Panel { get; private set; }
        public Vector2 Size { get; private set; }

        /// <summary>Opens a modal. <paramref name="reservedBottom"/> is the height kept for buttons.</summary>
        public Transform Show(string title, string body, Vector2 size, float reservedBottom = 110f,
            bool closable = true)
        {
            Close();
            Title = title;
            Size = size;
            Closable = closable;
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

        /// <summary>Y position for a button row, counted up from the panel's bottom edge.</summary>
        public float RowY(int rowFromBottom)
        {
            return -Size.y * 0.5f + 48f + rowFromBottom * 66f;
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
            Title = string.Empty;
        }
    }
}
