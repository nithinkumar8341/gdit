using UnityEngine;

namespace GoldenHour
{
    /// <summary>
    /// Builds the low-poly roadside accident scene: road, markings, sidewalks, the damaged
    /// vehicle, debris, barriers, cones, buildings, trees, lamps and invisible boundaries.
    /// Prefab references are assigned by the editor builder; primitives are used as fallbacks.
    /// </summary>
    public sealed class AccidentEnvironment : MonoBehaviour
    {
        public GameObject carPrefab;
        public GameObject barrierPrefab;
        public GameObject conePrefab;

        public static readonly Vector3 PlayerStart = new Vector3(0f, 0.1f, -17f);
        public const float SceneEntryZ = -9.5f;

        public Transform Build()
        {
            Transform root = new GameObject("Accident Scene").transform;
            Material grass = MaterialFactory.Create(new Color32(92, 118, 84, 255));
            Material asphalt = MaterialFactory.Create(new Color32(50, 54, 60, 255), 0f, 0.1f);
            Material concrete = MaterialFactory.Create(new Color32(160, 163, 161, 255));
            Material paint = MaterialFactory.Create(new Color32(240, 236, 220, 255));
            Material yellowPaint = MaterialFactory.Create(new Color32(236, 190, 64, 255));
            Material dark = MaterialFactory.Create(new Color32(38, 40, 44, 255));
            Material orange = MaterialFactory.Create(new Color32(230, 106, 36, 255));

            Box(root, "Ground", new Vector3(0f, -0.25f, 4f), new Vector3(80f, 0.4f, 84f), grass);
            Box(root, "Road", new Vector3(0f, -0.02f, 4f), new Vector3(11f, 0.1f, 84f), asphalt);
            Box(root, "West Sidewalk", new Vector3(-6.3f, 0.04f, 4f), new Vector3(1.6f, 0.2f, 84f), concrete);
            Box(root, "East Sidewalk", new Vector3(6.3f, 0.04f, 4f), new Vector3(1.6f, 0.2f, 84f), concrete);
            Box(root, "West Edge Line", new Vector3(-5.25f, 0.035f, 4f), new Vector3(0.12f, 0.02f, 84f), paint);
            Box(root, "East Edge Line", new Vector3(5.25f, 0.035f, 4f), new Vector3(0.12f, 0.02f, 84f), paint);
            for (float z = -36f; z <= 44f; z += 5f)
            {
                Box(root, "Centre Dash", new Vector3(0f, 0.035f, z), new Vector3(0.14f, 0.02f, 2.4f), yellowPaint);
            }

            BuildVehicle(root, dark, orange);
            BuildTrafficControl(root, orange, paint);
            BuildRoadside(root, concrete, dark);
            BuildBoundaries(root);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color32(150, 160, 172, 255);
            return root;
        }

        private void BuildVehicle(Transform root, Material dark, Material orange)
        {
            Vector3 position = new Vector3(0.2f, 0f, 5.2f);
            Quaternion rotation = Quaternion.Euler(0f, -32f, 0f);
            if (carPrefab != null)
            {
                GameObject car = Instantiate(carPrefab, position, rotation, root);
                car.name = "Damaged Vehicle";
                AddBoundsCollider(car);
            }
            else
            {
                Transform car = new GameObject("Damaged Vehicle").transform;
                car.SetParent(root, false);
                car.SetPositionAndRotation(position, rotation);
                Box(car, "Body", new Vector3(0f, 0.6f, 0f), new Vector3(4f, 0.7f, 1.75f),
                    MaterialFactory.Create(new Color32(110, 42, 32, 255)));
                Box(car, "Cabin", new Vector3(-0.35f, 1.15f, 0f), new Vector3(2f, 0.55f, 1.55f),
                    MaterialFactory.Create(new Color32(90, 120, 140, 255)));
            }

            // Scattered debris and a small fluid stain near the vehicle.
            Box(root, "Debris Panel", new Vector3(2.6f, 0.08f, 2.4f), new Vector3(0.9f, 0.06f, 0.5f), dark)
                .transform.rotation = Quaternion.Euler(4f, 38f, 8f);
            Box(root, "Debris Bumper Piece", new Vector3(-1.8f, 0.1f, 9.4f), new Vector3(1.1f, 0.14f, 0.24f), orange)
                .transform.rotation = Quaternion.Euler(0f, -64f, 6f);
            Box(root, "Debris Fragment", new Vector3(1.4f, 0.06f, 1.2f), new Vector3(0.3f, 0.06f, 0.24f), dark)
                .transform.rotation = Quaternion.Euler(0f, 12f, 0f);
            GameObject stain = Box(root, "Fluid Stain", new Vector3(1.2f, 0.032f, 6.4f),
                new Vector3(1.8f, 0.01f, 1.2f), MaterialFactory.Create(new Color32(28, 30, 33, 255)));
            Destroy(stain.GetComponent<Collider>());
        }

        private void BuildTrafficControl(Transform root, Material orange, Material white)
        {
            Vector3[] barriers =
            {
                new Vector3(-3.6f, 0f, -11f), new Vector3(3.6f, 0f, -11f),
                new Vector3(-3.6f, 0f, 19f), new Vector3(3.6f, 0f, 19f)
            };
            foreach (Vector3 position in barriers)
            {
                if (barrierPrefab != null)
                {
                    GameObject barrier = Instantiate(barrierPrefab, position, Quaternion.identity, root);
                    barrier.name = "Road Barrier";
                    AddBoundsCollider(barrier);
                }
                else
                {
                    Box(root, "Road Barrier", position + Vector3.up * 0.6f, new Vector3(2.4f, 0.3f, 0.12f), orange);
                }
            }

            Vector3[] cones =
            {
                new Vector3(-4.6f, 0f, -7.5f), new Vector3(-3.2f, 0f, -6.2f), new Vector3(4.4f, 0f, -7.2f),
                new Vector3(-4.8f, 0f, 14.5f), new Vector3(4.5f, 0f, 15f), new Vector3(3.3f, 0f, 16.3f)
            };
            foreach (Vector3 position in cones)
            {
                if (conePrefab != null)
                {
                    GameObject cone = Instantiate(conePrefab, position, Quaternion.identity, root);
                    cone.name = "Traffic Cone";
                    AddBoundsCollider(cone);
                }
                else
                {
                    Primitive(root, "Traffic Cone", PrimitiveType.Cylinder, position + Vector3.up * 0.35f,
                        new Vector3(0.35f, 0.35f, 0.35f), orange);
                }
            }

            // A warning sign facing the approaching rescuer.
            Primitive(root, "Warning Sign Post", PrimitiveType.Cylinder, new Vector3(-5.9f, 1f, -12.5f),
                new Vector3(0.08f, 1f, 0.08f), MaterialFactory.Create(new Color32(120, 126, 130, 255)));
            GameObject sign = Box(root, "Warning Sign", new Vector3(-5.9f, 2.05f, -12.6f),
                new Vector3(0.9f, 0.9f, 0.05f), orange);
            sign.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            Box(root, "Warning Sign Mark", new Vector3(-5.9f, 2.05f, -12.64f),
                new Vector3(0.1f, 0.42f, 0.02f), white);
        }

        private void BuildRoadside(Transform root, Material concrete, Material dark)
        {
            Material wallA = MaterialFactory.Create(new Color32(198, 196, 184, 255));
            Material wallB = MaterialFactory.Create(new Color32(170, 182, 190, 255));
            Material glass = MaterialFactory.Create(new Color32(110, 150, 175, 255), 0.2f, 0.7f);
            Material trunk = MaterialFactory.Create(new Color32(100, 74, 54, 255));
            Material leaves = MaterialFactory.Create(new Color32(64, 116, 70, 255));
            Material lampMetal = MaterialFactory.Create(new Color32(80, 86, 92, 255), 0.5f, 0.4f);

            for (int i = 0; i < 6; i++)
            {
                float z = -22f + i * 11f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float height = 5f + ((i + side + 2) % 3) * 2f;
                    float x = side * 14f;
                    Material wall = (i + side) % 2 == 0 ? wallA : wallB;
                    Box(root, "Building", new Vector3(x, height * 0.5f, z), new Vector3(9f, height, 9f), wall);
                    Box(root, "Building Roof", new Vector3(x, height + 0.15f, z), new Vector3(9.4f, 0.3f, 9.4f), concrete);
                    for (int floor = 0; floor < height / 2.6f - 0.5f; floor++)
                    {
                        Box(root, "Window", new Vector3(x - side * 4.52f, 1.6f + floor * 2.6f, z - 2f),
                            new Vector3(0.06f, 1.3f, 1.6f), glass);
                        Box(root, "Window", new Vector3(x - side * 4.52f, 1.6f + floor * 2.6f, z + 2f),
                            new Vector3(0.06f, 1.3f, 1.6f), glass);
                    }
                }
            }

            for (int i = 0; i < 7; i++)
            {
                float z = -20f + i * 9f;
                float x = i % 2 == 0 ? -8.4f : 8.4f;
                Primitive(root, "Tree Trunk", PrimitiveType.Cylinder, new Vector3(x, 1.3f, z),
                    new Vector3(0.3f, 1.3f, 0.3f), trunk);
                GameObject crown = Primitive(root, "Tree Crown", PrimitiveType.Sphere,
                    new Vector3(x, 3.3f, z), new Vector3(2.2f, 2.4f, 2.2f), leaves);
                Destroy(crown.GetComponent<Collider>());

                float lampX = i % 2 == 0 ? 7.2f : -7.2f;
                Primitive(root, "Street Lamp Post", PrimitiveType.Cylinder, new Vector3(lampX, 2.2f, z + 4f),
                    new Vector3(0.12f, 2.2f, 0.12f), lampMetal);
                Box(root, "Street Lamp Head", new Vector3(lampX - Mathf.Sign(lampX) * 0.5f, 4.4f, z + 4f),
                    new Vector3(1f, 0.12f, 0.3f), lampMetal);
            }
        }

        private static void BuildBoundaries(Transform root)
        {
            Wall(root, new Vector3(-9.4f, 2f, 4f), new Vector3(0.5f, 6f, 84f));
            Wall(root, new Vector3(9.4f, 2f, 4f), new Vector3(0.5f, 6f, 84f));
            Wall(root, new Vector3(0f, 2f, -24f), new Vector3(20f, 6f, 0.5f));
            Wall(root, new Vector3(0f, 2f, 28f), new Vector3(20f, 6f, 0.5f));
        }

        private static void Wall(Transform root, Vector3 position, Vector3 size)
        {
            GameObject wall = new GameObject("Invisible Boundary");
            wall.transform.SetParent(root, false);
            wall.transform.position = position;
            wall.AddComponent<BoxCollider>().size = size;
        }

        /// <summary>Adds a box collider that encloses all child renderers (imported FBXs have none).</summary>
        public static void AddBoundsCollider(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Quaternion rotation = target.transform.rotation;
            target.transform.rotation = Quaternion.identity;
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer item in renderers) bounds.Encapsulate(item.bounds);
            BoxCollider collider = target.AddComponent<BoxCollider>();
            Vector3 scale = target.transform.lossyScale;
            collider.center = Divide(bounds.center - target.transform.position, scale);
            collider.size = Divide(bounds.size, scale);
            target.transform.rotation = rotation;
        }

        private static Vector3 Divide(Vector3 value, Vector3 scale)
        {
            return new Vector3(value.x / scale.x, value.y / scale.y, value.z / scale.z);
        }

        public static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale,
            Material material)
        {
            return Primitive(parent, name, PrimitiveType.Cube, position, scale, material);
        }

        public static GameObject Primitive(Transform parent, string name, PrimitiveType type,
            Vector3 position, Vector3 scale, Material material)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }
    }
}
