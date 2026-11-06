using UnityEngine;

namespace GoldenHour
{
    [CreateAssetMenu(menuName = "Golden Hour/Supply Data")]
    public sealed class SupplyData : ScriptableObject
    {
        public string supplyId;
        public string displayName;
        [TextArea] public string description;
        [TextArea] public string educationalPurpose;
        public Color displayColor = Color.white;
    }
}
