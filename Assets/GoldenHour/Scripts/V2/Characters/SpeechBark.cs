using System;
using UnityEngine;

namespace GoldenHour
{
    /// <summary>Short floating speech line above a character (also mirrored to subtitles).</summary>
    public sealed class SpeechBark : MonoBehaviour
    {
        public static event Action<string, Vector3> Spoken;

        private TMPro.TextMeshPro label;
        private float hideTime;

        public void Initialize(float height)
        {
            GameObject labelObject = new GameObject("Speech");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(0f, height, 0f);
            label = labelObject.AddComponent<TMPro.TextMeshPro>();
            label.fontSize = 1.5f;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.color = new Color(1f, 0.97f, 0.88f);
            label.textWrappingMode = TMPro.TextWrappingModes.Normal;
            label.rectTransform.sizeDelta = new Vector2(2.6f, 0.8f);
            label.fontStyle = TMPro.FontStyles.Italic;
            labelObject.AddComponent<PatientBillboard>();
            label.text = string.Empty;
        }

        public void Say(string text, float seconds = 3f)
        {
            if (label == null || string.IsNullOrEmpty(text)) return;
            label.text = "\"" + text + "\"";
            hideTime = Time.time + seconds;
            Spoken?.Invoke(text, transform.position);
        }

        public void SetHeight(float height)
        {
            if (label != null) label.transform.localPosition = new Vector3(0f, height, 0f);
        }

        private void Update()
        {
            if (label != null && label.text.Length > 0 && Time.time > hideTime) label.text = string.Empty;
        }
    }
}
