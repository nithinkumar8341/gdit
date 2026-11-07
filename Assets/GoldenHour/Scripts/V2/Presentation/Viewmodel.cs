using UnityEngine;

namespace GoldenHour
{
    /// <summary>First-person hands (gloved once gloves are on).</summary>
    public sealed class Viewmodel : MonoBehaviour
    {
        public static Viewmodel Instance { get; private set; }
        public bool GlovesVisible { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetGloves(bool on)
        {
            GlovesVisible = on;
        }
    }
}
