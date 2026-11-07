using TMPro;
using UnityEngine;

namespace GoldenHour
{
    /// <summary>
    /// The physical triage tag attached to a casualty, plus a coloured light stick visible from a distance.
    /// In colour-blind mode the tag also carries a priority number and a stripe pattern
    /// (RED = 3 stripes / "1", YELLOW = 2 / "2", GREEN = 1 / "3", BLACK = none / "0").
    /// </summary>
    public sealed class TriageTagVisual : MonoBehaviour
    {
        private GameObject card;
        private GameObject stick;
        private TextMeshPro label;

        public TriageTag Tag { get; private set; }

        public static Color ColorFor(TriageTag tag)
        {
            switch (tag)
            {
                case TriageTag.Red: return Palette.TriageRed;
                case TriageTag.Yellow: return Palette.TriageYellow;
                case TriageTag.Green: return Palette.TriageGreen;
                case TriageTag.Black: return new Color32(30, 30, 34, 255);
                default: return Palette.Slate;
            }
        }

        public static string Symbol(TriageTag tag)
        {
            switch (tag)
            {
                case TriageTag.Red: return "1";
                case TriageTag.Yellow: return "2";
                case TriageTag.Green: return "3";
                case TriageTag.Black: return "0";
                default: return "-";
            }
        }

        public static int Stripes(TriageTag tag)
        {
            switch (tag)
            {
                case TriageTag.Red: return 3;
                case TriageTag.Yellow: return 2;
                case TriageTag.Green: return 1;
                default: return 0;
            }
        }

        /// <summary>Text label used everywhere a tag is named in UI (includes the shape word in colour-blind mode).</summary>
        public static string UiLabel(TriageTag tag)
        {
            string label = StartTriage.Label(tag);
            if (!GameSettings.ColourblindTags || tag == TriageTag.None) return label;
            string shape = tag == TriageTag.Red ? "triangle" : tag == TriageTag.Yellow ? "square" : tag == TriageTag.Green ? "circle" : "cross";
            return "[" + Symbol(tag) + " " + shape + "] " + label;
        }

        public void Apply(TriageTag tag, Vector3 cardLocal, Vector3 stickLocal)
        {
            Tag = tag;
            if (card != null) Destroy(card);
            if (stick != null) Destroy(stick);
            if (tag == TriageTag.None) return;

            Color color = ColorFor(tag);
            card = AccidentEnvironment.Decal(transform, "Triage Tag", cardLocal, new Vector3(0.16f, 0.02f, 0.22f),
                MaterialFactory.Create(color));
            card.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
            Material white = MaterialFactory.Create(Color.white);
            int stripes = GameSettings.ColourblindTags ? Stripes(tag) : 0;
            for (int i = 0; i < stripes; i++)
            {
                AccidentEnvironment.Decal(card.transform, "Stripe", new Vector3(0f, 0.6f, -0.35f + i * 0.25f),
                    new Vector3(0.9f, 0.4f, 0.08f), white);
            }

            GameObject labelObject = new GameObject("Tag Label");
            labelObject.transform.SetParent(card.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.8f, 0.2f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            labelObject.transform.localScale = new Vector3(1f / 0.16f, 1f / 0.22f, 1f / 0.02f) * 0.05f;
            label = labelObject.AddComponent<TextMeshPro>();
            label.text = GameSettings.ColourblindTags ? Symbol(tag) : string.Empty;
            label.fontSize = 6f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = tag == TriageTag.Yellow ? Color.black : Color.white;

            stick = AccidentEnvironment.Decal(transform, "Tag Light Stick", stickLocal, new Vector3(0.05f, 0.18f, 0.05f),
                MaterialFactory.CreateUnlit(color, tag == TriageTag.Black ? 0.6f : 2.2f), PrimitiveType.Cylinder);
        }
    }
}
