using TMPro;
using UnityEngine;

namespace GoldenHour
{
    /// <summary>
    /// One injured person in the scene: visual pose (standing if able to walk, otherwise lying),
    /// interaction collider, a pulsing beacon (visible from a distance) and a live status label.
    /// </summary>
    public sealed class PatientActor : MonoBehaviour
    {
        public PatientData Data { get; private set; }
        public bool HasBeenExamined { get; private set; }
        public bool HasBeenTriaged { get; private set; }
        public bool HasReceivedAid { get; private set; }
        public TriageCategory AssignedCategory { get; private set; }
        public bool LastTriageWasCorrect { get; private set; }

        /// <summary>World point a rescuer should look at to examine this person.</summary>
        public Vector3 FocusPoint => transform.TransformPoint(focusLocal);
        public PatientBeacon Beacon => beacon;

        private Vector3 focusLocal;
        private PatientBeacon beacon;
        private TextMeshPro statusLabel;

        public void Initialize(PatientData data, GameObject visualPrefab)
        {
            Data = data;
            gameObject.name = data.patientId + " - " + data.patientName;
            bool lying = !data.canWalk;

            Transform visual = visualPrefab != null
                ? Instantiate(visualPrefab, transform).transform
                : CreateFallbackVisual();
            visual.name = "Patient Visual";
            visual.localScale = Vector3.one;
            if (lying)
            {
                // Lie on the back, head toward local -Z, raised so the torso rests on the ground.
                visual.localPosition = new Vector3(0f, 0.14f, 0f);
                visual.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            }
            else
            {
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.identity;
            }

            foreach (Renderer part in visual.GetComponentsInChildren<Renderer>())
            {
                if (part.sharedMaterial != null && part.sharedMaterial.name.Contains("Clothing"))
                {
                    MaterialFactory.Tint(part, data.clothingColor);
                }

                Collider partCollider = part.GetComponent<Collider>();
                if (partCollider != null) Destroy(partCollider);
            }

            BoxCollider body = gameObject.AddComponent<BoxCollider>();
            if (lying)
            {
                body.center = new Vector3(0f, 0.2f, -0.85f);
                body.size = new Vector3(0.8f, 0.4f, 1.85f);
                focusLocal = new Vector3(0f, 0.25f, -1.0f);
            }
            else
            {
                body.center = new Vector3(0f, 0.88f, 0f);
                body.size = new Vector3(0.75f, 1.76f, 0.5f);
                focusLocal = new Vector3(0f, 1.2f, 0f);
            }

            if (data.severeBleeding) CreateBleedingCue(lying);

            float labelHeight = lying ? 1.3f : 2.2f;
            Vector3 labelPosition = new Vector3(0f, labelHeight, lying ? -0.9f : 0f);
            GameObject beaconObject = new GameObject("Patient Beacon");
            beaconObject.transform.SetParent(transform, false);
            beaconObject.transform.localPosition = new Vector3(0f, 0f, labelPosition.z);
            beacon = beaconObject.AddComponent<PatientBeacon>();
            beacon.Initialize(Mathf.Max(labelHeight + 0.75f, 2.6f));
            GameObject labelObject = new GameObject("Status Label");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = labelPosition;
            statusLabel = labelObject.AddComponent<TextMeshPro>();
            statusLabel.fontSize = 2f;
            statusLabel.alignment = TextAlignmentOptions.Center;
            statusLabel.color = Color.white;
            statusLabel.outlineWidth = 0.25f;
            statusLabel.outlineColor = new Color32(15, 18, 22, 255);
            statusLabel.rectTransform.sizeDelta = new Vector2(3f, 0.7f);
            statusLabel.textWrappingMode = TextWrappingModes.NoWrap;
            labelObject.AddComponent<PatientBillboard>();
            RefreshStatus();
        }

        public bool MarkExamined()
        {
            if (HasBeenExamined) return false;
            HasBeenExamined = true;
            RefreshStatus();
            return true;
        }

        public void SetTriage(TriageCategory category)
        {
            HasBeenTriaged = true;
            AssignedCategory = category;
            LastTriageWasCorrect = category == TriageEvaluator.Evaluate(Data);
            RefreshStatus();
        }

        public void MarkAidCompleted()
        {
            HasReceivedAid = true;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (beacon != null)
            {
                Color beaconColor = HasBeenTriaged ? Palette.ForCategory(AssignedCategory)
                    : HasBeenExamined ? Palette.LightGrey : Palette.Beacon;
                beacon.SetState(HasReceivedAid ? Palette.TriageGreen : beaconColor, HasReceivedAid);
            }

            if (statusLabel == null) return;
            string state;
            if (HasReceivedAid) state = "<color=#7FE0A6>AID GIVEN</color>";
            else if (HasBeenTriaged) state = "<color=#F6D171>AWAITING AID</color>";
            else if (HasBeenExamined) state = "<color=#CDD3D8>EXAMINED</color>";
            else state = "<color=#FF8A8A>NOT ASSESSED</color>";

            string tag = HasBeenTriaged
                ? "<color=#" + ColorUtility.ToHtmlStringRGB(Palette.ForCategory(AssignedCategory)) + ">" +
                  AssignedCategory.ToString().ToUpperInvariant() + "</color>  "
                : string.Empty;
            statusLabel.text = "<b>" + Data.patientId + "</b>  " + tag + "\n<size=75%>" + state + "</size>";
        }

        private void CreateBleedingCue(bool lying)
        {
            GameObject cue = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cue.name = "Visible Bleeding Cue";
            cue.transform.SetParent(transform, false);
            cue.transform.localPosition = lying ? new Vector3(0.45f, 0.015f, -0.95f) : new Vector3(0.3f, 0.015f, 0.1f);
            cue.transform.localScale = new Vector3(0.55f, 0.005f, 0.4f);
            Destroy(cue.GetComponent<Collider>());
            cue.GetComponent<Renderer>().sharedMaterial = MaterialFactory.Create(new Color32(110, 14, 18, 255), 0f, 0.8f);
        }

        private Transform CreateFallbackVisual()
        {
            Transform root = new GameObject("Fallback Visual").transform;
            root.SetParent(transform, false);
            Material clothing = MaterialFactory.Create(new Color32(72, 101, 128, 255));
            clothing.name = "Fallback Clothing";
            Material skin = MaterialFactory.Create(new Color32(205, 166, 133, 255));
            Material trousers = MaterialFactory.Create(new Color32(45, 50, 58, 255));
            AccidentEnvironment.Primitive(root, "Body", PrimitiveType.Capsule, new Vector3(0f, 1.05f, 0f),
                new Vector3(0.48f, 0.4f, 0.3f), clothing);
            AccidentEnvironment.Primitive(root, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.62f, 0f),
                new Vector3(0.26f, 0.3f, 0.26f), skin);
            AccidentEnvironment.Primitive(root, "Legs", PrimitiveType.Capsule, new Vector3(0f, 0.4f, 0f),
                new Vector3(0.36f, 0.42f, 0.26f), trousers);
            return root;
        }
    }
}
