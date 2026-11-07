using UnityEngine;

namespace GoldenHour
{
    /// <summary>
    /// A glowing, pulsing marker and light beam above a patient so they can be found from a
    /// distance. Colour follows the patient's state: amber (not assessed), light grey (examined),
    /// triage colour (triaged) and a small steady green marker once aid is complete.
    /// </summary>
    public sealed class PatientBeacon : MonoBehaviour
    {
        private const float BaseScale = 0.42f;
        private Transform marker;
        private Material markerMaterial;
        private Material beamMaterial;
        private Transform beam;
        private Color color = Palette.Beacon;
        private bool settled;

        public Color CurrentColor => color;
        public bool Pulsing => !settled;

        public void Initialize(float height)
        {
            marker = AccidentEnvironment.Decal(transform, "Beacon Marker", new Vector3(0f, height, 0f),
                Vector3.one * BaseScale, MaterialFactory.CreateUnlit(color, 2.4f)).transform;
            marker.localRotation = Quaternion.Euler(45f, 0f, 45f);
            markerMaterial = marker.GetComponent<Renderer>().sharedMaterial;
            marker.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            beam = AccidentEnvironment.Decal(transform, "Beacon Beam", new Vector3(0f, height + 1.6f, 0f),
                new Vector3(0.07f, 1.4f, 0.07f), MaterialFactory.CreateUnlit(color, 1.6f),
                PrimitiveType.Cylinder).transform;
            beamMaterial = beam.GetComponent<Renderer>().sharedMaterial;
            beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void SetState(Color stateColor, bool complete)
        {
            color = stateColor;
            settled = complete;
            if (beam != null) beam.gameObject.SetActive(!complete);
            if (beamMaterial != null) MaterialFactory.SetUnlitColor(beamMaterial, color, 1.6f);
            if (complete && marker != null)
            {
                marker.localScale = Vector3.one * (BaseScale * 0.6f);
                MaterialFactory.SetUnlitColor(markerMaterial, color, 1.4f);
            }
        }

        private void Update()
        {
            if (marker == null) return;
            marker.Rotate(0f, 70f * Time.deltaTime, 0f, Space.World);
            if (settled) return;
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * 4.2f);
            marker.localScale = Vector3.one * (BaseScale * (1f + 0.28f * wave));
            MaterialFactory.SetUnlitColor(markerMaterial, color, 1.6f + 2.4f * wave);
        }
    }
}
