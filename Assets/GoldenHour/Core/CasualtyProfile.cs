using UnityEngine;

namespace GoldenHour
{
    /// <summary>
    /// Data for one casualty: identity, appearance, initial physiology and how the condition changes over
    /// simulated minutes. Injuries are described in non-graphic terms. Educational model only.
    /// </summary>
    [CreateAssetMenu(menuName = "Golden Hour/Casualty Profile")]
    public sealed class CasualtyProfile : ScriptableObject
    {
        [Header("Identity")]
        public string casualtyId = "C01";
        public string displayName = "Casualty";
        public int age = 30;
        public BodyVariant variant = BodyVariant.Adult;
        public BodyPose pose = BodyPose.Lying;
        public Color clothingColor = new Color(0.3f, 0.4f, 0.55f);
        [TextArea] public string appearance = "Lying still on the road.";
        [TextArea] public string injurySummary = "No obvious injury.";

        [Header("Initial physiology")]
        public PhysiologyState initial = new PhysiologyState();

        [Header("Deterioration (simulated minutes, 0 = never)")]
        public CriticalCondition condition = CriticalCondition.None;
        [Tooltip("Dies at this minute unless stabilised.")] public float deathMinute;
        [Tooltip("Condition worsens at this minute unless stabilised (e.g. YELLOW becomes RED).")] public float worsenMinute;
        public int worsenedRespiratoryRate;
        public float worsenedCapillaryRefill;
        public bool worsenedLosesCommands;
        [Tooltip("Capillary refill rise per minute while bleeding is uncontrolled.")] public float capillaryRefillRisePerMinute;
        [Tooltip("Treatments that stop the deterioration.")] public Treatment stabilisedBy = Treatment.None;
        [Tooltip("Extra minutes gained by opening the airway when recovery position is also needed.")] public float airwayOpeningDelayMinutes = 6f;

        [Header("Aid")]
        [Tooltip("Treatments that are indicated and earn credit for this casualty.")] public Treatment indicatedTreatments = Treatment.Blanket;
        [TextArea] public string aidNote = "Keep the person warm and reassured.";

        [Header("Behaviour")]
        public string[] barks = { "Help..." };
    }
}
