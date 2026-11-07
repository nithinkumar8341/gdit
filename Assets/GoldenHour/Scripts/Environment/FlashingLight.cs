using UnityEngine;

namespace GoldenHour
{
    /// <summary>
    /// Blinks an unlit lamp material and an optional point light (hazard / emergency lights).
    /// A phase offset lets several lamps alternate.
    /// </summary>
    public sealed class FlashingLight : MonoBehaviour
    {
        public Color color = Color.white;
        public float onIntensity = 6f;
        public float period = 0.8f;
        [Range(0f, 1f)] public float phase;
        public Light pointLight;
        public float lightIntensity = 2.5f;

        private Material material;

        public bool IsOn { get; private set; }

        public void Initialize(Renderer lamp, Color lampColor, float flashPeriod, float phaseOffset, Light light = null)
        {
            color = lampColor;
            period = flashPeriod;
            phase = phaseOffset;
            pointLight = light;
            material = lamp != null ? lamp.material : null;
            Apply(true);
        }

        private void Update()
        {
            bool on = Mathf.Repeat(Time.time / period + phase, 1f) < 0.5f;
            if (on != IsOn) Apply(on);
        }

        private void Apply(bool on)
        {
            IsOn = on;
            if (material != null) MaterialFactory.SetUnlitColor(material, on ? color : color * 0.18f, on ? onIntensity : 1f);
            if (pointLight != null) pointLight.intensity = on ? lightIntensity : 0f;
        }
    }
}
