using UnityEngine;

namespace GoldenHour
{
    /// <summary>
    /// First-person forearms and hands under the player camera. Bare skin until gloves are put on
    /// (blue nitrile). Hands sway with movement, tremble slightly with stress and reach forward
    /// while an action or minigame is running. Reduce-motion disables sway and tremor.
    /// </summary>
    public sealed class Viewmodel : MonoBehaviour
    {
        public static readonly Color SkinColor = new Color32(214, 170, 132, 255);
        public static readonly Color GloveColor = new Color32(70, 120, 215, 255);

        public static Viewmodel Instance { get; private set; }
        public bool GlovesVisible { get; private set; }
        public float Stress { get; set; }
        public bool Reaching { get; set; }
        public bool HandsShown => root != null && root.gameObject.activeSelf;

        private Transform root;
        private Transform left;
        private Transform right;
        private Material handMaterial;
        private Vector3 leftRest = new Vector3(-0.23f, -0.25f, 0.42f);
        private Vector3 rightRest = new Vector3(0.23f, -0.25f, 0.42f);
        private float reach;
        private Vector3 lastPosition;
        private float swayPhase;

        private void Awake()
        {
            Instance = this;
            Build();
            lastPosition = transform.position;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Build()
        {
            root = new GameObject("Viewmodel Hands").transform;
            root.SetParent(transform, false);
            handMaterial = MaterialFactory.Create(SkinColor, 0f, 0.55f);
            Material sleeve = MaterialFactory.Create(new Color32(40, 60, 80, 255), 0f, 0.8f);
            left = Arm("Left", -1f, sleeve);
            right = Arm("Right", 1f, sleeve);
            left.localPosition = leftRest;
            right.localPosition = rightRest;
            foreach (Renderer part in root.GetComponentsInChildren<Renderer>())
            {
                part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                part.receiveShadows = false;
            }
        }

        private Transform Arm(string side, float sign, Material sleeve)
        {
            Transform arm = new GameObject(side + " Arm").transform;
            arm.SetParent(root, false);
            arm.localRotation = Quaternion.Euler(-12f, -sign * 14f, sign * 8f);
            Part(arm, "Sleeve", new Vector3(0f, 0f, -0.16f), new Vector3(0.085f, 0.085f, 0.26f), sleeve, PrimitiveType.Cube);
            Part(arm, "Wrist", new Vector3(0f, 0f, -0.02f), new Vector3(0.062f, 0.055f, 0.06f), handMaterial, PrimitiveType.Cube);
            Part(arm, "Palm", new Vector3(0f, 0f, 0.05f), new Vector3(0.085f, 0.035f, 0.1f), handMaterial, PrimitiveType.Cube);
            Part(arm, "Fingers", new Vector3(0f, -0.006f, 0.125f), new Vector3(0.08f, 0.026f, 0.07f), handMaterial, PrimitiveType.Cube);
            Part(arm, "Thumb", new Vector3(-sign * 0.05f, 0.004f, 0.06f), new Vector3(0.022f, 0.022f, 0.06f), handMaterial, PrimitiveType.Cube)
                .localRotation = Quaternion.Euler(0f, sign * 35f, 0f);
            return arm;
        }

        private static Transform Part(Transform parent, string name, Vector3 position, Vector3 scale, Material material, PrimitiveType type)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            part.layer = parent.gameObject.layer;
            return part.transform;
        }

        public string HeldItem { get; private set; }
        private GameObject heldObject;

        /// <summary>Which item the hands hold for an action label (dressing, blanket, tag, gloves) or null.</summary>
        public static string ItemFor(string actionLabel)
        {
            string label = (actionLabel ?? string.Empty).ToLowerInvariant();
            if (label.Contains("dressing")) return "dressing";
            if (label.Contains("blanket")) return "blanket";
            if (label.Contains("tag")) return "tag";
            if (label.Contains("gloves")) return "gloves";
            return null;
        }

        /// <summary>Shows a simple item between the hands (null hides it).</summary>
        public void Hold(string item)
        {
            HeldItem = item;
            if (heldObject != null) Destroy(heldObject);
            heldObject = null;
            if (string.IsNullOrEmpty(item) || root == null) return;
            Color color;
            Vector3 size;
            switch (item)
            {
                case "dressing":
                    color = new Color32(240, 240, 232, 255);
                    size = new Vector3(0.11f, 0.025f, 0.13f);
                    break;
                case "blanket":
                    color = new Color32(214, 178, 74, 255);
                    size = new Vector3(0.24f, 0.04f, 0.16f);
                    break;
                case "tag":
                    color = new Color32(200, 40, 40, 255);
                    size = new Vector3(0.075f, 0.006f, 0.12f);
                    break;
                default:
                    color = GloveColor;
                    size = new Vector3(0.09f, 0.03f, 0.12f);
                    break;
            }

            heldObject = Part(root, "Held " + item, new Vector3(0f, -0.2f, 0.56f), size, MaterialFactory.Create(color, 0.1f, 0.6f), PrimitiveType.Cube).gameObject;
            heldObject.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            Renderer held = heldObject.GetComponent<Renderer>();
            held.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void SetGloves(bool on)
        {
            GlovesVisible = on;
            Color color = on ? GloveColor : SkinColor;
            handMaterial.color = color;
            if (handMaterial.HasProperty("_BaseColor")) handMaterial.SetColor("_BaseColor", color);
            if (handMaterial.HasProperty("_Smoothness")) handMaterial.SetFloat("_Smoothness", on ? 0.75f : 0.45f);
        }

        public void SetVisible(bool visible)
        {
            if (root != null) root.gameObject.SetActive(visible);
        }

        private void LateUpdate()
        {
            if (root == null) return;
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            Vector3 velocity = (transform.position - lastPosition) / dt;
            lastPosition = transform.position;
            bool calm = GameSettings.ReduceMotion;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            swayPhase += dt * (1.5f + speed * 2.2f);
            reach = Mathf.MoveTowards(reach, Reaching ? 1f : 0f, dt * 3f);
            Vector3 sway = calm ? Vector3.zero : new Vector3(Mathf.Sin(swayPhase) * 0.006f * (0.4f + speed * 0.25f),
                Mathf.Abs(Mathf.Cos(swayPhase)) * 0.008f * Mathf.Clamp01(speed / 3f), 0f);
            Vector3 tremor = calm || Stress < 0.35f ? Vector3.zero
                : new Vector3(Mathf.PerlinNoise(Time.time * 9f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, Time.time * 9f) - 0.5f, 0f) * 0.006f * Stress;
            Vector3 forward = new Vector3(0f, 0.07f, 0.12f) * reach;
            left.localPosition = leftRest + sway + tremor + forward + new Vector3(0.05f, 0f, 0f) * reach;
            right.localPosition = rightRest + new Vector3(-sway.x, sway.y, 0f) - tremor + forward - new Vector3(0.05f, 0f, 0f) * reach;
        }
    }
}
