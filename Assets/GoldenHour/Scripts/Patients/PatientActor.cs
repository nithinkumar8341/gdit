using TMPro;
using UnityEngine;

namespace GoldenHour
{
    public sealed class PatientActor : MonoBehaviour
    {
        public PatientData Data { get; private set; }
        public bool HasBeenExamined { get; private set; }
        public bool HasBeenTriaged { get; private set; }
        public bool HasReceivedAid { get; private set; }
        public TriageCategory AssignedCategory { get; private set; }
        public bool LastTriageWasCorrect { get; private set; }

        private GameObject triageMarker;
        private Renderer markerRenderer;

        public void Initialize(PatientData data, GameObject visualPrefab)
        {
            Data = data;
            gameObject.name = data.patientId + " - " + data.patientName;

            if (visualPrefab != null)
            {
                GameObject visual = Instantiate(visualPrefab, transform);
                visual.name = "Patient Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
            }
            else
            {
                CreateFallbackVisual();
            }

            CapsuleCollider collider = gameObject.AddComponent<CapsuleCollider>();
            collider.height = 1.65f;
            collider.radius = 0.38f;
            collider.center = new Vector3(0f, 0.83f, 0f);

            triageMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            triageMarker.name = "Triage Marker";
            triageMarker.transform.SetParent(transform, false);
            triageMarker.transform.localPosition = new Vector3(0f, 2.05f, 0f);
            triageMarker.transform.localScale = Vector3.one * 0.22f;
            Collider markerCollider = triageMarker.GetComponent<Collider>();
            Destroy(markerCollider);
            markerRenderer = triageMarker.GetComponent<Renderer>();
            triageMarker.SetActive(false);

            GameObject labelObject = new GameObject("Patient Number");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 1.76f, 0f);
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = data.patientId;
            label.fontSize = 3.2f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.outlineWidth = 0.2f;
            label.outlineColor = new Color32(20, 25, 30, 255);
            label.rectTransform.sizeDelta = new Vector2(2f, 0.4f);
            labelObject.AddComponent<PatientBillboard>();
        }

        public bool MarkExamined()
        {
            if (HasBeenExamined)
            {
                return false;
            }

            HasBeenExamined = true;
            return true;
        }

        public bool SetTriage(TriageCategory category)
        {
            bool wasTriaged = HasBeenTriaged;
            bool previouslyCorrect = LastTriageWasCorrect;
            HasBeenTriaged = true;
            AssignedCategory = category;
            LastTriageWasCorrect = category == TriageEvaluator.Evaluate(Data);

            if (triageMarker != null)
            {
                triageMarker.SetActive(true);
                markerRenderer.material.color = CategoryColor(category);
            }

            return !wasTriaged || previouslyCorrect != LastTriageWasCorrect;
        }

        public void MarkAidCompleted()
        {
            HasReceivedAid = true;
        }

        private void CreateFallbackVisual()
        {
            CreatePart("Body", PrimitiveType.Capsule, new Vector3(0f, 0.9f, 0f),
                new Vector3(0.52f, 0.68f, 0.34f), new Color32(72, 101, 128, 255));
            CreatePart("Head", PrimitiveType.Sphere, new Vector3(0f, 1.68f, 0f),
                new Vector3(0.3f, 0.34f, 0.3f), new Color32(205, 166, 133, 255));
            CreatePart("Left Arm", PrimitiveType.Capsule, new Vector3(-0.39f, 0.94f, 0f),
                new Vector3(0.18f, 0.55f, 0.18f), new Color32(72, 101, 128, 255));
            CreatePart("Right Arm", PrimitiveType.Capsule, new Vector3(0.39f, 0.94f, 0f),
                new Vector3(0.18f, 0.55f, 0.18f), new Color32(72, 101, 128, 255));
            CreatePart("Left Leg", PrimitiveType.Capsule, new Vector3(-0.16f, 0.28f, 0f),
                new Vector3(0.2f, 0.55f, 0.2f), new Color32(45, 50, 58, 255));
            CreatePart("Right Leg", PrimitiveType.Capsule, new Vector3(0.16f, 0.28f, 0f),
                new Vector3(0.2f, 0.55f, 0.2f), new Color32(45, 50, 58, 255));
        }

        private void CreatePart(string partName, PrimitiveType type, Vector3 localPosition,
            Vector3 localScale, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = partName;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            Collider collider = part.GetComponent<Collider>();
            Destroy(collider);
            part.GetComponent<Renderer>().material.color = color;
        }

        private static Color CategoryColor(TriageCategory category)
        {
            switch (category)
            {
                case TriageCategory.Red: return new Color32(218, 48, 55, 255);
                case TriageCategory.Yellow: return new Color32(243, 185, 49, 255);
                case TriageCategory.Green: return new Color32(58, 166, 98, 255);
                default: return new Color32(55, 60, 67, 255);
            }
        }
    }

    public sealed class PatientBillboard : MonoBehaviour
    {
        private Camera mainCamera;

        private void LateUpdate()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            if (mainCamera != null)
            {
                transform.rotation = Quaternion.LookRotation(
                    transform.position - mainCamera.transform.position);
            }
        }
    }
}
