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

        // Small, consistent environment palette (warm late-afternoon street).
        public static readonly Color Asphalt = new Color32(54, 56, 62, 255);
        public static readonly Color Concrete = new Color32(168, 162, 150, 255);
        public static readonly Color Grass = new Color32(104, 126, 74, 255);
        public static readonly Color GrassDark = new Color32(78, 102, 58, 255);
        public static readonly Color LinePaint = new Color32(236, 230, 212, 255);
        public static readonly Color LineYellow = new Color32(232, 184, 70, 255);
        public static readonly Color WallWarm = new Color32(214, 192, 160, 255);
        public static readonly Color WallCool = new Color32(176, 170, 162, 255);
        public static readonly Color Glass = new Color32(96, 128, 150, 255);
        public static readonly Color Wood = new Color32(122, 88, 60, 255);
        public static readonly Color Foliage = new Color32(82, 118, 66, 255);
        public static readonly Color Metal = new Color32(78, 82, 88, 255);
        public static readonly Color SafetyOrange = new Color32(232, 110, 40, 255);
        public static readonly Color DarkTrim = new Color32(36, 38, 42, 255);
        public static readonly Color SkidMark = new Color32(26, 27, 30, 255);
        public static readonly Color Hazard = new Color32(255, 170, 40, 255);
        public static readonly Color LampGlow = new Color32(255, 214, 150, 255);
        public static readonly Color Beacon = new Color32(255, 236, 170, 255);
    }
}
