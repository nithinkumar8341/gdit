using System.Collections.Generic;
using UnityEngine;

namespace GoldenHour
{
    public enum BodyState
    {
        Standing,
        Walking,
        Sitting,
        Lying,
        Recovery,
        Carried,
        Kneeling
    }

    public enum BodyStyle
    {
        Civilian,
        Child,
        Motorcyclist,
        Elderly,
        Worker,
        Paramedic,
        Firefighter,
        Police
    }

    /// <summary>
    /// Low-poly jointed humanoid with procedural animation: breathing, walking, sitting, lying, recovery
    /// position, being carried, restless and limp states. Joints use fixed names (Hips, Spine, Chest, Head,
    /// UpperArm.L ...). When a rigged Blender model with those bone names is supplied it is used instead of the
    /// built-in primitive body. No injuries are depicted.
    /// </summary>
    public sealed class HumanoidBody : MonoBehaviour
    {
        public static readonly string[] JointNames =
        {
            "Hips", "Spine", "Chest", "Neck", "Head", "UpperArm.L", "LowerArm.L", "UpperArm.R", "LowerArm.R",
            "UpperLeg.L", "LowerLeg.L", "UpperLeg.R", "LowerLeg.R"
        };

        private readonly Dictionary<string, Transform> joints = new Dictionary<string, Transform>();
        private readonly Dictionary<string, Quaternion> rest = new Dictionary<string, Quaternion>();
        private Transform pivot;
        private Transform chest;
        private Vector3 chestScale = Vector3.one;
        private float phase;
        private float recoveryRoll;
        private float restlessSeed;

        public BodyState State { get; private set; } = BodyState.Standing;
        public float BreathsPerMinute { get; set; } = 14f;
        public bool Restless { get; set; }
        public bool Limp { get; set; }
        public float Height { get; private set; } = 1.75f;
        public Transform Pivot => pivot;
        public bool UsesRig { get; private set; }

        /// <summary>0 = on the back, 1 = fully on the side (recovery position).</summary>
        public float RecoveryRoll
        {
            get => recoveryRoll;
            set => recoveryRoll = Mathf.Clamp01(value);
        }

        public Transform Joint(string jointName) => joints.TryGetValue(jointName, out Transform joint) ? joint : null;

        public void Build(BodyStyle style, Color clothing, float scale, GameObject rigPrefab, int seed)
        {
            restlessSeed = seed * 0.37f;
            pivot = new GameObject("Body Pivot").transform;
            pivot.SetParent(transform, false);
            Height = 1.75f * scale;

            if (rigPrefab != null && TryBindRig(rigPrefab, style, clothing, scale, seed))
            {
                UsesRig = true;
            }
            else
            {
                BuildPrimitiveBody(style, clothing, scale, seed);
            }

            foreach (KeyValuePair<string, Transform> pair in joints) rest[pair.Key] = pair.Value.localRotation;
            chest = Joint("Chest");
            if (chest != null) chestScale = chest.localScale;
            SetState(BodyState.Standing);
        }

        private bool TryBindRig(GameObject rigPrefab, BodyStyle style, Color clothing, float scale, int seed)
        {
            GameObject rig = Instantiate(rigPrefab, pivot);
            rig.name = "Rig";
            rig.transform.localPosition = Vector3.zero;
            rig.transform.localRotation = Quaternion.identity;
            rig.transform.localScale = Vector3.one * scale;
            foreach (Transform child in rig.GetComponentsInChildren<Transform>(true))
            {
                if (System.Array.IndexOf(JointNames, child.name) >= 0 && !joints.ContainsKey(child.name)) joints[child.name] = child;
            }

            if (joints.Count < JointNames.Length || !NormaliseRig(rig.transform, scale))
            {
                joints.Clear();
                Destroy(rig);
                return false;
            }

            System.Random random = new System.Random(seed);
            Color[] skins =
            {
                new Color32(232, 196, 160, 255), new Color32(198, 150, 110, 255), new Color32(150, 104, 70, 255),
                new Color32(110, 74, 50, 255)
            };
            Color skin = skins[random.Next(skins.Length)];
            Color hair = style == BodyStyle.Elderly ? new Color32(200, 200, 205, 255)
                : new Color32((byte)(40 + random.Next(80)), (byte)(28 + random.Next(40)), 20, 255);
            Color trousers = style == BodyStyle.Paramedic ? new Color32(40, 70, 52, 255)
                : style == BodyStyle.Firefighter ? new Color32(60, 50, 40, 255)
                : style == BodyStyle.Police ? new Color32(28, 34, 52, 255)
                : new Color32(48, 52, 62, 255);
            foreach (Renderer part in rig.GetComponentsInChildren<Renderer>(true))
            {
                string materialName = part.sharedMaterial != null ? part.sharedMaterial.name : string.Empty;
                if (materialName.Contains("Clothing")) MaterialFactory.TintShared(part, clothing);
                else if (materialName.Contains("Skin")) MaterialFactory.TintShared(part, skin);
                else if (materialName.Contains("Hair")) MaterialFactory.TintShared(part, hair);
                else if (materialName.Contains("Trousers")) MaterialFactory.TintShared(part, trousers);
                bool accessory = part.name.Contains("Helmet") || part.name.Contains("HardHat") || part.name.Contains("Vest") ||
                                 part.name.Contains("Cap");
                if (accessory) part.gameObject.SetActive(AccessoryVisible(part.name, style));
                Collider collider = part.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                bool detail = part.name.Contains("Nose") || part.name.Contains("Ear") || part.name.Contains("Hand") || part.name.Contains("Hair") ||
                              part.name.Contains("Collar") || part.name.Contains("Stripe") || part.name.Contains("Peak") || part.name.Contains("Brim") ||
                              part.name.Contains("Visor") || part.name.Contains("Shoulder") || part.name.Contains("Neck");
                if (detail) part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            return true;
        }

        /// <summary>
        /// Makes every joint frame axis-aligned with the body pivot (procedural poses assume
        /// x = right, y = up, z = forward), keeps the meshes where they are, and turns the rig
        /// to face +Z. Returns false when the imported proportions are implausible.
        /// </summary>
        public static bool NormaliseRig(Transform rig, float scale)
        {
            Transform parent = rig.parent;
            Transform head = FindDeep(rig, "Head");
            Transform hips = FindDeep(rig, "Hips");
            Transform leftArm = FindDeep(rig, "UpperArm.L");
            Transform nose = null;
            foreach (Transform child in rig.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.EndsWith("_Nose")) nose = child;
            }

            if (head == null || hips == null || leftArm == null) return false;
            Vector3 Local(Transform t) => parent != null ? parent.InverseTransformPoint(t.position) : t.position;
            Vector3 headPoint = Local(head);
            Vector3 facing = nose != null ? Local(nose) - headPoint : Vector3.forward;
            bool facingBack = facing.z < 0f;
            bool leftOnRight = Local(leftArm).x > Local(hips).x;
            if (facingBack && leftOnRight) rig.localRotation = Quaternion.Euler(0f, 180f, 0f) * rig.localRotation;

            float hipsHeight = Local(hips).y / Mathf.Max(0.01f, scale);
            if (hipsHeight < 0.6f || hipsHeight > 1.3f) return false;

            Quaternion target = parent != null ? parent.rotation : Quaternion.identity;
            foreach (string jointName in JointNames)
            {
                Transform joint = FindDeep(rig, jointName);
                List<Transform> children = new List<Transform>();
                foreach (Transform child in joint) children.Add(child);
                foreach (Transform child in children) child.SetParent(null, true);
                joint.rotation = target;
                foreach (Transform child in children) child.SetParent(joint, true);
            }

            return true;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name) return child;
            }

            return null;
        }

        private static bool AccessoryVisible(string partName, BodyStyle style)
        {
            if (partName.Contains("Helmet")) return style == BodyStyle.Motorcyclist || style == BodyStyle.Firefighter;
            if (partName.Contains("HardHat")) return style == BodyStyle.Worker;
            if (partName.Contains("Vest")) return style == BodyStyle.Worker || style == BodyStyle.Paramedic || style == BodyStyle.Police;
            if (partName.Contains("Cap")) return style == BodyStyle.Police;
            return true;
        }

        private void BuildPrimitiveBody(BodyStyle style, Color clothing, float scale, int seed)
        {
            System.Random random = new System.Random(seed);
            Color[] skins =
            {
                new Color32(232, 196, 160, 255), new Color32(198, 150, 110, 255), new Color32(150, 104, 70, 255),
                new Color32(110, 74, 50, 255)
            };
            Material skin = MaterialFactory.Create(skins[random.Next(skins.Length)]);
            Material cloth = MaterialFactory.Create(clothing);
            Color trouserColor = style == BodyStyle.Paramedic ? new Color32(40, 70, 52, 255)
                : style == BodyStyle.Firefighter ? new Color32(60, 50, 40, 255)
                : style == BodyStyle.Police ? new Color32(28, 34, 52, 255)
                : new Color32(48, 52, 62, 255);
            Material trousers = MaterialFactory.Create(trouserColor);
            Material shoes = MaterialFactory.Create(new Color32(30, 30, 32, 255));
            Material hair = MaterialFactory.Create(style == BodyStyle.Elderly ? new Color32(200, 200, 205, 255)
                : new Color32((byte)(40 + random.Next(80)), (byte)(28 + random.Next(40)), 20, 255));

            Transform root = new GameObject("Primitive Body").transform;
            root.SetParent(pivot, false);
            root.localScale = Vector3.one * scale;
            float stoop = style == BodyStyle.Elderly ? 8f : 0f;

            Transform hips = MakeJoint("Hips", root, new Vector3(0f, 0.95f, 0f));
            Part(hips, PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(0.36f, 0.2f, 0.22f), trousers);
            Transform spine = MakeJoint("Spine", hips, new Vector3(0f, 0.1f, 0f));
            spine.localRotation = Quaternion.Euler(stoop, 0f, 0f);
            Transform chestJoint = MakeJoint("Chest", spine, new Vector3(0f, 0.18f, 0f));
            Part(chestJoint, PrimitiveType.Cube, new Vector3(0f, 0.12f, 0f), new Vector3(0.42f, 0.48f, 0.24f), cloth);
            Transform neck = MakeJoint("Neck", chestJoint, new Vector3(0f, 0.38f, 0f));
            Part(neck, PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f), new Vector3(0.1f, 0.05f, 0.1f), skin);
            Transform head = MakeJoint("Head", neck, new Vector3(0f, 0.08f, 0f));
            Part(head, PrimitiveType.Sphere, new Vector3(0f, 0.12f, 0.01f), new Vector3(0.22f, 0.26f, 0.24f), skin);
            Part(head, PrimitiveType.Sphere, new Vector3(0f, 0.18f, -0.02f), new Vector3(0.23f, 0.17f, 0.23f), hair);

            if (style == BodyStyle.Motorcyclist || style == BodyStyle.Firefighter)
            {
                Color helmet = style == BodyStyle.Motorcyclist ? new Color32(30, 32, 40, 255) : new Color32(220, 180, 40, 255);
                Part(head, PrimitiveType.Sphere, new Vector3(0f, 0.14f, 0f), new Vector3(0.3f, 0.32f, 0.31f),
                    MaterialFactory.Create(helmet, 0.2f, 0.7f)).name = "Helmet";
                if (style == BodyStyle.Motorcyclist)
                {
                    Part(head, PrimitiveType.Cube, new Vector3(0f, 0.12f, 0.14f), new Vector3(0.2f, 0.08f, 0.04f),
                        MaterialFactory.Create(new Color32(60, 80, 100, 255), 0.3f, 0.9f)).name = "Visor";
                }
            }

            if (style == BodyStyle.Worker)
            {
                Part(head, PrimitiveType.Sphere, new Vector3(0f, 0.22f, 0f), new Vector3(0.28f, 0.14f, 0.3f),
                    MaterialFactory.Create(new Color32(240, 200, 30, 255))).name = "HardHat";
            }

            if (style == BodyStyle.Worker || style == BodyStyle.Paramedic || style == BodyStyle.Police)
            {
                Color vest = style == BodyStyle.Paramedic ? new Color32(200, 230, 60, 255)
                    : style == BodyStyle.Police ? new Color32(220, 235, 60, 255) : new Color32(250, 140, 30, 255);
                Part(chestJoint, PrimitiveType.Cube, new Vector3(0f, 0.12f, 0f), new Vector3(0.44f, 0.36f, 0.26f),
                    MaterialFactory.Create(vest)).name = "Vest";
            }

            if (style == BodyStyle.Police)
            {
                Part(head, PrimitiveType.Cylinder, new Vector3(0f, 0.26f, 0f), new Vector3(0.26f, 0.04f, 0.26f),
                    MaterialFactory.Create(new Color32(25, 30, 50, 255))).name = "Cap";
            }

            for (int side = -1; side <= 1; side += 2)
            {
                string suffix = side < 0 ? ".L" : ".R";
                Transform upperArm = MakeJoint("UpperArm" + suffix, chestJoint, new Vector3(side * 0.26f, 0.32f, 0f));
                Part(upperArm, PrimitiveType.Cube, new Vector3(0f, -0.15f, 0f), new Vector3(0.12f, 0.32f, 0.13f), cloth);
                Transform lowerArm = MakeJoint("LowerArm" + suffix, upperArm, new Vector3(0f, -0.31f, 0f));
                Part(lowerArm, PrimitiveType.Cube, new Vector3(0f, -0.13f, 0f), new Vector3(0.1f, 0.28f, 0.11f), cloth);
                Part(lowerArm, PrimitiveType.Sphere, new Vector3(0f, -0.3f, 0f), new Vector3(0.09f, 0.1f, 0.09f), skin);

                Transform upperLeg = MakeJoint("UpperLeg" + suffix, hips, new Vector3(side * 0.1f, -0.08f, 0f));
                Part(upperLeg, PrimitiveType.Cube, new Vector3(0f, -0.2f, 0f), new Vector3(0.15f, 0.42f, 0.16f), trousers);
                Transform lowerLeg = MakeJoint("LowerLeg" + suffix, upperLeg, new Vector3(0f, -0.42f, 0f));
                Part(lowerLeg, PrimitiveType.Cube, new Vector3(0f, -0.2f, 0f), new Vector3(0.13f, 0.42f, 0.14f), trousers);
                Part(lowerLeg, PrimitiveType.Cube, new Vector3(0f, -0.42f, 0.04f), new Vector3(0.13f, 0.08f, 0.24f), shoes);
            }
        }

        private Transform MakeJoint(string jointName, Transform parent, Vector3 localPosition)
        {
            Transform joint = new GameObject(jointName).transform;
            joint.SetParent(parent, false);
            joint.localPosition = localPosition;
            joints[jointName] = joint;
            return joint;
        }

        private static GameObject Part(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = AccidentEnvironment.Decal(parent, "Part", position, scale, material, type);
            return part;
        }

        public void SetState(BodyState state)
        {
            State = state;
            phase = 0f;
            ApplyPose(0f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            phase += dt;
            ApplyPose(dt);
        }

        private void ApplyPose(float dt)
        {
            if (pivot == null) return;
            ResetJoints();
            switch (State)
            {
                case BodyState.Walking:
                    pivot.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(phase * 7f)) * 0.03f, 0f);
                    pivot.localRotation = Quaternion.identity;
                    float swing = Mathf.Sin(phase * 7f) * 28f;
                    Rotate("UpperLeg.L", swing, 0f, 0f);
                    Rotate("UpperLeg.R", -swing, 0f, 0f);
                    Rotate("LowerLeg.L", Mathf.Max(0f, -swing) * 0.8f, 0f, 0f);
                    Rotate("LowerLeg.R", Mathf.Max(0f, swing) * 0.8f, 0f, 0f);
                    Rotate("UpperArm.L", -swing * 0.7f, 0f, 0f);
                    Rotate("UpperArm.R", swing * 0.7f, 0f, 0f);
                    break;
                case BodyState.Sitting:
                    pivot.localPosition = new Vector3(0f, -0.78f * Height / 1.75f, 0f);
                    pivot.localRotation = Quaternion.identity;
                    Rotate("UpperLeg.L", -88f, 0f, -6f);
                    Rotate("UpperLeg.R", -88f, 0f, 6f);
                    Rotate("LowerLeg.L", 30f, 0f, 0f);
                    Rotate("LowerLeg.R", 30f, 0f, 0f);
                    Rotate("Spine", 12f, 0f, 0f);
                    Rotate("UpperArm.L", -30f, 0f, 10f);
                    Rotate("UpperArm.R", -40f, 0f, -10f);
                    Rotate("LowerArm.R", -50f, 0f, 0f);
                    break;
                case BodyState.Kneeling:
                    pivot.localPosition = new Vector3(0f, -0.42f * Height / 1.75f, 0f);
                    pivot.localRotation = Quaternion.identity;
                    Rotate("UpperLeg.L", -80f, 0f, 0f);
                    Rotate("LowerLeg.L", 80f, 0f, 0f);
                    Rotate("UpperLeg.R", 0f, 0f, 0f);
                    Rotate("LowerLeg.R", 90f, 0f, 0f);
                    Rotate("Spine", 18f, 0f, 0f);
                    Rotate("UpperArm.L", -50f, 0f, 0f);
                    Rotate("UpperArm.R", -50f, 0f, 0f);
                    break;
                case BodyState.Lying:
                case BodyState.Carried:
                case BodyState.Recovery:
                    float roll = State == BodyState.Recovery ? 1f : recoveryRoll;
                    pivot.localPosition = new Vector3(0f, 0.15f + roll * 0.12f, 0f);
                    pivot.localRotation = Quaternion.AngleAxis(roll * 90f, Vector3.forward) * Quaternion.Euler(-90f, 0f, 0f);
                    if (roll > 0.5f)
                    {
                        Rotate("UpperLeg.R", -60f * roll, 0f, 0f);
                        Rotate("LowerLeg.R", 70f * roll, 0f, 0f);
                        Rotate("UpperArm.R", -80f * roll, 0f, 0f);
                        Rotate("Head", -10f * roll, 0f, 0f);
                    }
                    else if (!Limp)
                    {
                        Rotate("UpperArm.L", 0f, 0f, -12f);
                        Rotate("UpperArm.R", 0f, 0f, 12f);
                    }
                    else
                    {
                        Rotate("UpperArm.L", 0f, 0f, -22f);
                        Rotate("UpperArm.R", 0f, 0f, 30f);
                        Rotate("Head", 0f, 25f, 0f);
                    }

                    break;
                default:
                    pivot.localPosition = Vector3.zero;
                    pivot.localRotation = Quaternion.identity;
                    Rotate("UpperArm.L", 0f, 0f, -6f);
                    Rotate("UpperArm.R", 0f, 0f, 6f);
                    break;
            }

            if (Restless && !Limp)
            {
                float t = Time.time * 2.3f + restlessSeed;
                Rotate("Head", Mathf.Sin(t * 1.7f) * 10f, Mathf.Sin(t) * 18f, 0f, true);
                Rotate("UpperArm.L", Mathf.Sin(t * 1.3f) * 12f, 0f, 0f, true);
                Rotate("LowerLeg.R", Mathf.Max(0f, Mathf.Sin(t * 0.9f)) * 15f, 0f, 0f, true);
            }

            if (chest != null)
            {
                float breath = 0f;
                if (BreathsPerMinute > 0f && !Limp)
                {
                    breath = (Mathf.Sin(Time.time * BreathsPerMinute / 60f * Mathf.PI * 2f) + 1f) * 0.5f;
                }

                float amplitude = BreathsPerMinute > 28f ? 0.08f : 0.05f;
                chest.localScale = new Vector3(chestScale.x * (1f + breath * amplitude * 0.6f), chestScale.y,
                    chestScale.z * (1f + breath * amplitude));
            }
        }

        private void ResetJoints()
        {
            foreach (KeyValuePair<string, Transform> pair in joints)
            {
                pair.Value.localRotation = rest.TryGetValue(pair.Key, out Quaternion restRotation) ? restRotation : Quaternion.identity;
            }
        }

        private void Rotate(string jointName, float x, float y, float z, bool additive = false)
        {
            Transform joint = Joint(jointName);
            if (joint == null) return;
            Quaternion rotation = Quaternion.Euler(x, y, z);
            Quaternion baseRotation = rest.TryGetValue(jointName, out Quaternion restRotation) ? restRotation : Quaternion.identity;
            joint.localRotation = additive ? joint.localRotation * rotation : baseRotation * rotation;
        }
    }
}
