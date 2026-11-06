using UnityEngine;

namespace GoldenHour
{
    /// <summary>Shared accessible colours: white, emergency red, charcoal and light grey.</summary>
    public static class Palette
    {
        public static readonly Color EmergencyRed = new Color32(194, 43, 52, 255);
        public static readonly Color TriageRed = new Color32(214, 48, 56, 255);
        public static readonly Color TriageYellow = new Color32(232, 176, 44, 255);
        public static readonly Color TriageGreen = new Color32(52, 150, 90, 255);
        public static readonly Color Charcoal = new Color32(24, 32, 41, 250);
        public static readonly Color Slate = new Color32(62, 76, 88, 255);
        public static readonly Color LightGrey = new Color32(205, 211, 216, 255);
        public static readonly Color White = new Color32(246, 247, 248, 255);
        public static readonly Color Warning = new Color32(246, 209, 113, 255);

        public static Color ForCategory(TriageCategory category)
        {
            switch (category)
            {
                case TriageCategory.Red: return TriageRed;
                case TriageCategory.Yellow: return TriageYellow;
                case TriageCategory.Green: return TriageGreen;
                default: return Slate;
            }
        }

        public static string Label(TriageCategory category)
        {
            switch (category)
            {
                case TriageCategory.Red: return "RED | IMMEDIATE";
                case TriageCategory.Yellow: return "YELLOW | DELAYED";
                case TriageCategory.Green: return "GREEN | MINOR";
                default: return category.ToString().ToUpperInvariant();
            }
        }
    }
}
