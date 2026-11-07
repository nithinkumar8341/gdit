using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace GoldenHour
{
    /// <summary>
    /// World-space prompt on a casualty's body during a timed check or treatment: a pulsing red ring on the body
    /// part (chest, head, forearm, leg) with a short label, always drawn on top and facing the camera.
    /// </summary>
    public sealed class BodyMarker
    {
        private GameObject root;
        private TextMeshPro label;
        private MarkerFollower follower;

        public bool Visible => root != null && root.activeSelf;
        public Vector3 Position => root != null ? root.transform.position : Vector3.zero;
        public Vector3 Target => follower != null && follower.Point != null ? follower.Point() : Vector3.zero;
        public string Label => label != null ? label.text : string.Empty;

        public void Show(Func<Vector3> point, string text)
        {
            Ensure();
            follower.Point = point;
            label.text = text;
            root.SetActive(true);
            follower.Follow();
        }

        public void Hide()
        {
            if (root != null && root.activeSelf) root.SetActive(false);
        }

        private void Ensure()
        {
            if (root != null) return;
            root = new GameObject("Body Marker");
            follower = root.AddComponent<MarkerFollower>();

            GameObject ringObject = new GameObject("Ring");
            ringObject.transform.SetParent(root.transform, false);
            follower.Ring = ringObject.transform;
            Material ringMaterial = MaterialFactory.CreateUnlit(Palette.EmergencyRed, 2.2f);
            OnTop(ringMaterial);
            AddRing(ringObject, 0.085f, 0.014f, ringMaterial);
            Material innerMaterial = MaterialFactory.CreateUnlit(Color.white, 1.4f);
            OnTop(innerMaterial);
            AddRing(ringObject, 0.03f, 0.01f, innerMaterial);

            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.13f, 0f);
            label = labelObject.AddComponent<TextMeshPro>();
            label.fontSize = 0.34f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.outlineWidth = 0.22f;
            label.outlineColor = new Color32(90, 8, 14, 255);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(0.9f, 0.12f);
            if (label.fontMaterial.HasProperty("unity_GUIZTestMode"))
            {
                label.fontMaterial.SetFloat("unity_GUIZTestMode", (float)CompareFunction.Always);
            }

            root.SetActive(false);
        }

        private static void OnTop(Material material)
        {
            if (material.HasProperty("_ZTest")) material.SetFloat("_ZTest", (float)CompareFunction.Always);
            material.renderQueue = (int)RenderQueue.Overlay;
        }

        private static void AddRing(GameObject parent, float radius, float width, Material material)
        {
            GameObject ringObject = new GameObject("Circle");
            ringObject.transform.SetParent(parent.transform, false);
            LineRenderer line = ringObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = width;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = material;
            const int segments = 40;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
        }

        /// <summary>Follows the body point, faces the camera and pulses (static with reduced motion).</summary>
        private sealed class MarkerFollower : MonoBehaviour
        {
            public Func<Vector3> Point;
            public Transform Ring;

            public void Follow()
            {
                if (Point == null) return;
                Vector3 point = Point();
                Camera view = Camera.main;
                if (view != null)
                {
                    // Nudge towards the camera so the ring sits on the body surface, not inside it.
                    Vector3 toCamera = view.transform.position - point;
                    point += toCamera.normalized * Mathf.Min(0.08f, toCamera.magnitude * 0.1f);
                    transform.rotation = Quaternion.LookRotation(point - view.transform.position, view.transform.up);
                }

                transform.position = point;
                float pulse = GameSettings.ReduceMotion ? 1f : 1f + 0.18f * Mathf.Sin(Time.unscaledTime * 6f);
                if (Ring != null) Ring.localScale = Vector3.one * pulse;
            }

            private void LateUpdate() => Follow();
        }
    }
}
