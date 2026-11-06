using UnityEngine;

namespace GoldenHour
{
    [CreateAssetMenu(menuName = "Golden Hour/Patient Data")]
    public sealed class PatientData : ScriptableObject
    {
        public string patientId;
        public string patientName;
        [Range(1, 120)] public int age;
        [TextArea] public string injuryDescription;
        [TextArea] public string symptoms;
        public string responsiveness;
        public bool breathing = true;
        public bool severeBleeding;
        public bool canWalk;
        public bool followsCommands = true;
        public TriageCategory correctTriage;
        [TextArea] public string reasoning;
        public SupplyData recommendedSupply;
        [TextArea] public string aidAction;
    }
}
