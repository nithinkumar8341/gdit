using UnityEngine;

namespace GoldenHour
{
    /// <summary>
    /// Shows the aid given on the casualty's body using the Blender TreatmentProps model: a gauze dressing on the
    /// bleeding limb, head or trunk, and a foil blanket (over the body when lying, round the shoulders when sitting or
    /// standing). Parts are parented to HumanoidBody joints so they follow every pose. No wounds are depicted.
    /// </summary>
    public sealed class TreatmentVisuals : MonoBehaviour
    {
        /// <summary>Blender TreatmentProps.fbx (assigned by the director from a serialized reference).</summary>
        public static GameObject Model { get; set; }

        private HumanoidBody body;
        private GameObject dressing;
        private GameObject blanketLying;
        private GameObject blanketShawl;

        public bool DressingShown => dressing != null && dressing.activeInHierarchy;
        public bool BlanketShown => (blanketLying != null && blanketLying.activeInHierarchy) || (blanketShawl != null && blanketShawl.activeInHierarchy);
        public Transform DressingTransform => dressing != null ? dressing.transform : null;
        public string DressingJoint { get; private set; }

        public static TreatmentVisuals For(Casualty casualty)
        {
            TreatmentVisuals visuals = casualty.GetComponent<TreatmentVisuals>();
            if (visuals == null) visuals = casualty.gameObject.AddComponent<TreatmentVisuals>();
            visuals.body = casualty.Body;
            return visuals;
        }

        /// <summary>Puts a dressing on <paramref name="jointName"/> (LowerLeg/LowerArm .L/.R, Head or Spine).</summary>
        public void ShowDressing(string jointName)
        {
            Transform joint = body != null ? body.Joint(jointName) : null;
            if (joint == null || Model == null) return;
            if (dressing != null) Destroy(dressing);
            string part = jointName == "Head" ? "Dressing_Head" : jointName == "Spine" ? "Dressing_Torso" : "Dressing_Limb";
            dressing = Spawn(part, joint);
            if (dressing == null) return;
            DressingJoint = jointName;
            float size = body.Height / 1.75f;
            if (part == "Dressing_Limb")
            {
                bool arm = jointName.StartsWith("LowerArm");
                dressing.transform.localPosition = new Vector3(0f, arm ? -0.16f : -0.2f, 0f) * size;
                dressing.transform.localScale = Vector3.one * (arm ? 0.8f : 1f) * size;
            }
            else
            {
                dressing.transform.localScale = Vector3.one * size;
            }
        }

        public void ShowBlanket()
        {
            if (body == null || Model == null) return;
            Transform hips = body.Joint("Hips");
            Transform chest = body.Joint("Chest");
            if (hips == null || chest == null) return;
            float size = body.Height / 1.75f;
            if (blanketLying == null) blanketLying = Spawn("Blanket_Lying", hips);
            if (blanketShawl == null) blanketShawl = Spawn("Blanket_Shawl", chest);
            if (blanketLying != null) blanketLying.transform.localScale = Vector3.one * size;
            if (blanketShawl != null) blanketShawl.transform.localScale = Vector3.one * size;
            UpdateBlanket();
        }

        private GameObject Spawn(string partName, Transform parent)
        {
            Transform source = FindDeep(Model.transform, partName);
            if (source == null) return null;
            GameObject instance = Instantiate(source.gameObject, parent, false);
            instance.name = partName.Replace('_', ' ');
            instance.transform.localPosition = Vector3.zero;
            // Keep any axis-conversion rotation the importer folded into the part (relative to the model root).
            instance.transform.localRotation = Quaternion.Inverse(Model.transform.rotation) * source.rotation;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            foreach (Collider collider in instance.GetComponentsInChildren<Collider>()) Destroy(collider);
            return instance;
        }

        public static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }

            return null;
        }

        private void UpdateBlanket()
        {
            if (body == null) return;
            bool lying = body.State == BodyState.Lying || body.State == BodyState.Carried || body.State == BodyState.Recovery;
            if (blanketLying != null && blanketLying.activeSelf != lying) blanketLying.SetActive(lying);
            if (blanketShawl != null && blanketShawl.activeSelf == lying) blanketShawl.SetActive(!lying);
        }

        private void LateUpdate() => UpdateBlanket();
    }
}
