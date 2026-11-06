using UnityEngine;

namespace GoldenHour
{
    /// <summary>
    /// Educational scenario profile for one injured person. Observations are shown to the
    /// player; <see cref="correctTriage"/>, <see cref="reasoning"/> and the recommended supply
    /// are only revealed after the player decides.
    /// </summary>
    [CreateAssetMenu(menuName = "Golden Hour/Patient Data")]
    public sealed class PatientData : ScriptableObject
    {
        [Header("Identity")]
        public string patientId;
        public string patientName;
        [Range(1, 120)] public int age;
        public Color clothingColor = new Color32(48, 82, 120, 255);

        [Header("Observations")]
        [TextArea] public string injuryDescription;
        [TextArea] public string symptoms;
        public string responsiveness;
        public bool breathing = true;
        public bool severeBleeding;
        public bool canWalk;
        public bool followsCommands = true;

        [Header("Scenario key (revealed after the decision)")]
        public TriageCategory correctTriage;
        [TextArea] public string reasoning;
        public SupplyData recommendedSupply;
        [TextArea] public string aidAction;
    }
}
