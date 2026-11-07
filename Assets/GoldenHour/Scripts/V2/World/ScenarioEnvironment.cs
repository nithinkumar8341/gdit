using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace GoldenHour
{
    /// <summary>Prefabs and materials the environment builders may use (assigned by the editor builder).</summary>
    [System.Serializable]
    public sealed class EnvironmentAssets
    {
        public GameObject carPrefab;
        public GameObject barrierPrefab;
        public GameObject conePrefab;
        public GameObject busPrefab;
        public GameObject policeCarPrefab;
        public GameObject fireTruckPrefab;
        public GameObject motorbikePrefab;
        public Material smokeMaterial;
        public Material rainMaterial;
    }

    /// <summary>
    /// Builds each scenario's low-poly location procedurally (training yard, highway, night bus rollover,
    /// collapsed building site), applies its lighting/weather and bakes the NavMesh for NPCs at runtime.
    /// </summary>
    public static class ScenarioEnvironment
    {
        public static Transform Build(ScenarioData scenario, AccidentEnvironment highway, EnvironmentAssets assets, Camera playerCamera)
        {
            Transform root;
            switch (scenario.environment)
            {
                case EnvironmentKind.Highway:
                    root = highway.Build();
                    BuildMotorbike(root, assets, new Vector3(-3.6f, 0f, -6.2f));
                    break;
                case EnvironmentKind.BusRollover:
                    root = BuildBusRollover(assets);
                    break;
                case EnvironmentKind.BuildingSite:
                    root = BuildBuildingSite(assets);
                    break;
                default:
                    root = BuildTrainingYard(highway, assets);
                    break;
            }

            if (scenario.environment != EnvironmentKind.Highway) AccidentEnvironment.ApplyLighting();
            if (scenario.night) ApplyNight(playerCamera);
            if (scenario.rain && playerCamera != null) AddRain(playerCamera.transform, assets.rainMaterial != null ? assets.rainMaterial : assets.smokeMaterial);
            BuildGreenZone(root, scenario.greenZone);
            BakeNavMesh(root);
            return root;
        }

        // ------------------------------------------------------------------ shared

        public static void BakeNavMesh(Transform root)
        {
            NavMeshSurface surface = root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~(1 << Walker.CharacterLayer);
            surface.BuildNavMesh();
        }

        public static bool NavMeshReady => NavMesh.SamplePosition(Vector3.zero, out _, 50f, NavMesh.AllAreas);

        private static void BuildGreenZone(Transform root, Vector3 centre)
        {
            Material green = MaterialFactory.Create(new Color32(60, 150, 80, 255), 0f, 0.1f);
            AccidentEnvironment.Decal(root, "Green Zone Sheet", centre + new Vector3(0f, 0.03f, 0f), new Vector3(5f, 0.02f, 4f), green);
            Material pole = MaterialFactory.Create(new Color32(220, 220, 220, 255));
            AccidentEnvironment.Decal(root, "Green Zone Flag Pole", centre + new Vector3(2.4f, 1.1f, 1.9f), new Vector3(0.05f, 1.1f, 0.05f), pole,
                PrimitiveType.Cylinder);
            AccidentEnvironment.Decal(root, "Green Zone Flag", centre + new Vector3(2.75f, 2f, 1.9f), new Vector3(0.7f, 0.45f, 0.02f), green);
            GameObject label = new GameObject("Green Zone Label");
            label.transform.SetParent(root, false);
            label.transform.position = centre + new Vector3(0f, 2.6f, 0f);
            TMPro.TextMeshPro text = label.AddComponent<TMPro.TextMeshPro>();
            text.text = "GREEN ZONE\n<size=60%>walking wounded</size>";
            text.fontSize = 3f;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.color = new Color(0.6f, 1f, 0.7f);
            text.rectTransform.sizeDelta = new Vector2(5f, 2f);
            label.AddComponent<PatientBillboard>();
        }

        private static void Ground(Transform root, Vector3 centre, Vector3 size, Color color)
        {
            AccidentEnvironment.Box(root, "Ground", centre + new Vector3(0f, -0.2f, 0f), new Vector3(size.x, 0.4f, size.z), MaterialFactory.Create(color, 0f, 0.05f));
        }

        private static void Boundary(Transform root, Vector3 min, Vector3 max)
        {
            Vector3 centre = (min + max) * 0.5f;
            Vector3 size = max - min;
            Wall(root, new Vector3(min.x, 2f, centre.z), new Vector3(0.5f, 6f, size.z));
            Wall(root, new Vector3(max.x, 2f, centre.z), new Vector3(0.5f, 6f, size.z));
            Wall(root, new Vector3(centre.x, 2f, min.z), new Vector3(size.x, 6f, 0.5f));
            Wall(root, new Vector3(centre.x, 2f, max.z), new Vector3(size.x, 6f, 0.5f));
        }

        private static void Wall(Transform root, Vector3 position, Vector3 size)
        {
            GameObject wall = new GameObject("Invisible Boundary");
            wall.transform.SetParent(root, false);
            wall.transform.position = position;
            wall.AddComponent<BoxCollider>().size = size;
            wall.layer = Walker.CharacterLayer;
        }

        private static GameObject Place(Transform root, GameObject prefab, Vector3 position, float yaw, string name)
        {
            if (prefab == null) return null;
            GameObject instance = Object.Instantiate(prefab, position, Quaternion.Euler(0f, yaw, 0f), root);
            instance.name = name;
            AccidentEnvironment.AddBoundsCollider(instance);
            return instance;
        }

        private static void Lamp(Transform root, Vector3 position, bool withLight, Material metal, Material glow)
        {
            AccidentEnvironment.Primitive(root, "Street Lamp Post", PrimitiveType.Cylinder, position + new Vector3(0f, 2.4f, 0f),
                new Vector3(0.14f, 2.4f, 0.14f), metal);
            AccidentEnvironment.Decal(root, "Street Lamp Head", position + new Vector3(0f, 4.8f, 0.4f), new Vector3(0.35f, 0.12f, 1f), metal);
            AccidentEnvironment.Decal(root, "Street Lamp Bulb", position + new Vector3(0f, 4.72f, 0.7f), new Vector3(0.25f, 0.05f, 0.4f), glow);
            if (!withLight) return;
            Light light = new GameObject("Street Light").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.position = position + new Vector3(0f, 4.4f, 0.8f);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.5f);
            light.range = 16f;
            light.intensity = 2.2f;
            light.shadows = LightShadows.None;
        }

        private static void Tree(Transform root, Vector3 position, Material trunk, Material leaves, float scale = 1f)
        {
            AccidentEnvironment.Primitive(root, "Tree Trunk", PrimitiveType.Cylinder, position + new Vector3(0f, 1.3f * scale, 0f),
                new Vector3(0.3f, 1.3f, 0.3f) * scale, trunk);
            AccidentEnvironment.Decal(root, "Tree Crown", position + new Vector3(0f, 3.2f * scale, 0f), new Vector3(2.4f, 2.6f, 2.4f) * scale, leaves,
                PrimitiveType.Sphere);
        }

        private static void BuildMotorbike(Transform root, EnvironmentAssets assets, Vector3 position)
        {
            if (Place(root, assets.motorbikePrefab, position, 75f, "Fallen Motorbike") != null) return;
            Transform bike = new GameObject("Fallen Motorbike").transform;
            bike.SetParent(root, false);
            bike.SetPositionAndRotation(position, Quaternion.Euler(0f, 75f, 88f));
            Material frame = MaterialFactory.Create(new Color32(30, 60, 140, 255), 0.4f, 0.6f);
            Material tyre = MaterialFactory.Create(new Color32(20, 20, 22, 255));
            AccidentEnvironment.Box(bike, "Frame", new Vector3(0f, 0.55f, 0f), new Vector3(0.25f, 0.4f, 1.3f), frame);
            AccidentEnvironment.Decal(bike, "Tank", new Vector3(0f, 0.8f, 0.15f), new Vector3(0.3f, 0.22f, 0.5f), frame);
            foreach (float z in new[] { -0.7f, 0.7f })
            {
                GameObject wheel = AccidentEnvironment.Decal(bike, "Wheel", new Vector3(0f, 0.32f, z), new Vector3(0.64f, 0.08f, 0.64f), tyre,
                    PrimitiveType.Cylinder);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        // --------------------------------------------------------- training yard

        private static Transform BuildTrainingYard(AccidentEnvironment highway, EnvironmentAssets assets)
        {
            Transform root = new GameObject("Training Yard").transform;
            Ground(root, new Vector3(0f, 0f, -8f), new Vector3(60f, 0f, 70f), Palette.Grass);
            AccidentEnvironment.Box(root, "Yard Asphalt", new Vector3(0f, -0.02f, 0f), new Vector3(26f, 0.1f, 26f),
                MaterialFactory.Create(Palette.Asphalt, 0f, 0.12f));
            AccidentEnvironment.Box(root, "Access Road", new Vector3(6f, -0.03f, -26f), new Vector3(6f, 0.1f, 28f),
                MaterialFactory.Create(Palette.Asphalt, 0f, 0.12f));
            Material paint = MaterialFactory.Create(Palette.LinePaint);
            for (int i = -4; i <= 4; i++)
            {
                AccidentEnvironment.Decal(root, "Bay Line", new Vector3(i * 2.6f, 0.035f, -9f), new Vector3(0.1f, 0.02f, 4f), paint);
            }

            Material wall = MaterialFactory.Create(Palette.WallWarm);
            AccidentEnvironment.Box(root, "Training Centre", new Vector3(0f, 3f, 16f), new Vector3(30f, 6f, 6f), wall);
            AccidentEnvironment.Decal(root, "Training Centre Roof", new Vector3(0f, 6.15f, 16f), new Vector3(30.4f, 0.3f, 6.4f),
                MaterialFactory.Create(Palette.Concrete));
            Material glass = MaterialFactory.Create(Palette.Glass, 0.2f, 0.7f);
            for (int i = -3; i <= 3; i++)
            {
                AccidentEnvironment.Decal(root, "Window", new Vector3(i * 4f, 2.2f, 12.95f), new Vector3(2.2f, 1.4f, 0.05f), glass);
            }

            GameObject sign = new GameObject("Centre Sign");
            sign.transform.SetParent(root, false);
            sign.transform.position = new Vector3(0f, 4.6f, 12.9f);
            TMPro.TextMeshPro text = sign.AddComponent<TMPro.TextMeshPro>();
            text.text = "FIRST RESPONDER TRAINING CENTRE";
            text.fontSize = 6f;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.color = new Color(0.15f, 0.15f, 0.18f);
            text.rectTransform.sizeDelta = new Vector2(20f, 1.5f);
            sign.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            Material fence = MaterialFactory.Create(Palette.Metal, 0.5f, 0.4f);
            for (float x = -13f; x <= 13f; x += 2f)
            {
                if (x > 2f && x < 10f) continue;
                AccidentEnvironment.Decal(root, "Fence Post", new Vector3(x, 0.9f, -13f), new Vector3(0.08f, 1.8f, 0.08f), fence);
            }

            AccidentEnvironment.Box(root, "Fence Mesh West", new Vector3(-5.5f, 0.9f, -13f), new Vector3(15f, 1.8f, 0.05f),
                MaterialFactory.Create(new Color32(120, 125, 130, 255)));
            AccidentEnvironment.Box(root, "Fence Mesh East", new Vector3(11.5f, 0.9f, -13f), new Vector3(3f, 1.8f, 0.05f),
                MaterialFactory.Create(new Color32(120, 125, 130, 255)));

            Place(root, assets.carPrefab, new Vector3(2.4f, 0f, 4.2f), 70f, "Training Car");
            Material bollard = MaterialFactory.Create(new Color32(240, 200, 40, 255));
            AccidentEnvironment.Primitive(root, "Bollard", PrimitiveType.Cylinder, new Vector3(4.6f, 0.5f, 5.4f), new Vector3(0.3f, 0.5f, 0.3f), bollard);
            Material trunk = MaterialFactory.Create(Palette.Wood);
            Material leaves = MaterialFactory.Create(Palette.Foliage);
            Tree(root, new Vector3(-11f, 0f, 9f), trunk, leaves);
            Tree(root, new Vector3(11f, 0f, 9f), trunk, leaves);
            Tree(root, new Vector3(-16f, 0f, -4f), trunk, leaves, 1.2f);
            Boundary(root, new Vector3(-14f, 0f, -38f), new Vector3(14f, 0f, 13f));
            return root;
        }

        // ------------------------------------------------------- bus rollover

        private static Transform BuildBusRollover(EnvironmentAssets assets)
        {
            Transform root = new GameObject("Rural Road").transform;
            Ground(root, new Vector3(0f, 0f, 8f), new Vector3(90f, 0f, 120f), new Color32(70, 88, 56, 255));
            Material asphalt = MaterialFactory.Create(new Color32(40, 42, 46, 255), 0f, 0.35f);
            AccidentEnvironment.Box(root, "Road", new Vector3(0f, -0.02f, 8f), new Vector3(10f, 0.1f, 120f), asphalt);
            Material paint = MaterialFactory.Create(Palette.LinePaint);
            AccidentEnvironment.Decal(root, "Edge Line W", new Vector3(-4.7f, 0.035f, 8f), new Vector3(0.12f, 0.02f, 120f), paint);
            AccidentEnvironment.Decal(root, "Edge Line E", new Vector3(4.7f, 0.035f, 8f), new Vector3(0.12f, 0.02f, 120f), paint);
            for (float z = -50f; z <= 66f; z += 6f)
            {
                AccidentEnvironment.Decal(root, "Centre Dash", new Vector3(0f, 0.035f, z), new Vector3(0.14f, 0.02f, 3f), paint);
            }

            Material rail = MaterialFactory.Create(new Color32(170, 172, 175, 255), 0.6f, 0.5f);
            for (int side = -1; side <= 1; side += 2)
            {
                AccidentEnvironment.Box(root, "Guard Rail", new Vector3(side * 6.2f, 0.55f, -10f), new Vector3(0.1f, 0.3f, 30f), rail);
                for (float z = -24f; z <= 4f; z += 3f)
                {
                    AccidentEnvironment.Decal(root, "Rail Post", new Vector3(side * 6.2f, 0.35f, z), new Vector3(0.1f, 0.7f, 0.1f), rail);
                }
            }

            // Bus on its side across the verge, with broken windows and scattered luggage.
            if (Place(root, assets.busPrefab, new Vector3(3f, 0f, 12f), 12f, "Overturned Bus") == null)
            {
                Transform bus = new GameObject("Overturned Bus").transform;
                bus.SetParent(root, false);
                bus.SetPositionAndRotation(new Vector3(3f, 0f, 12f), Quaternion.Euler(0f, 12f, 0f));
                Material body = MaterialFactory.Create(new Color32(220, 170, 40, 255), 0.2f, 0.5f);
                Material dark = MaterialFactory.Create(new Color32(30, 34, 40, 255), 0.2f, 0.8f);
                AccidentEnvironment.Box(bus, "Bus Body", new Vector3(0f, 1.25f, 0f), new Vector3(3f, 2.5f, 11.5f), body);
                for (int i = 0; i < 6; i++)
                {
                    AccidentEnvironment.Decal(bus, "Bus Window", new Vector3(0f, 2.51f, -4.5f + i * 1.8f), new Vector3(2f, 0.04f, 1.3f), dark);
                }

                AccidentEnvironment.Decal(bus, "Bus Roof Edge", new Vector3(-1.52f, 1.25f, 0f), new Vector3(0.04f, 2.2f, 11.3f), dark);
                Material tyre = MaterialFactory.Create(new Color32(20, 20, 22, 255));
                foreach (float z in new[] { -3.8f, 3.6f })
                {
                    GameObject wheel = AccidentEnvironment.Decal(bus, "Bus Wheel", new Vector3(1.6f, 2.0f, z), new Vector3(1f, 0.25f, 1f), tyre,
                        PrimitiveType.Cylinder);
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }

            Material luggage = MaterialFactory.Create(new Color32(90, 60, 50, 255));
            System.Random random = new System.Random(11);
            for (int i = 0; i < 8; i++)
            {
                Vector3 position = new Vector3(-3f + (float)random.NextDouble() * 6f, 0.2f, 3f + (float)random.NextDouble() * 14f);
                AccidentEnvironment.Decal(root, "Luggage", position, new Vector3(0.6f, 0.4f, 0.35f), luggage)
                    .transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 180f, 0f);
            }

            Material glassBits = MaterialFactory.Create(new Color32(170, 200, 214, 255), 0.1f, 0.85f);
            for (int i = 0; i < 14; i++)
            {
                Vector3 position = new Vector3(-1f + (float)random.NextDouble() * 4f, 0.03f, 6f + (float)random.NextDouble() * 12f);
                AccidentEnvironment.Decal(root, "Glass Shard", position, new Vector3(0.14f, 0.02f, 0.1f), glassBits);
            }

            Place(root, assets.policeCarPrefab, new Vector3(-2.5f, 0f, -14f), 20f, "Police Car");
            Material trunk = MaterialFactory.Create(new Color32(70, 55, 40, 255));
            Material leaves = MaterialFactory.Create(new Color32(40, 70, 45, 255));
            for (int i = 0; i < 10; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Tree(root, new Vector3(side * (11f + (i % 3) * 3f), 0f, -20f + i * 7f), trunk, leaves, 1.1f);
            }

            Material metal = MaterialFactory.Create(Palette.Metal, 0.5f, 0.4f);
            Material glow = MaterialFactory.CreateUnlit(Palette.LampGlow, 3f);
            Lamp(root, new Vector3(-6.8f, 0f, -6f), true, metal, glow);
            Lamp(root, new Vector3(6.8f, 0f, 18f), true, metal, glow);
            Lamp(root, new Vector3(-6.8f, 0f, 30f), false, metal, glow);
            Boundary(root, new Vector3(-12f, 0f, -26f), new Vector3(12f, 0f, 44f));
            return root;
        }

        // ------------------------------------------------------- building site

        private static Transform BuildBuildingSite(EnvironmentAssets assets)
        {
            Transform root = new GameObject("Building Site").transform;
            Ground(root, new Vector3(0f, 0f, 0f), new Vector3(90f, 0f, 90f), new Color32(140, 120, 92, 255));
            Material asphalt = MaterialFactory.Create(Palette.Asphalt, 0f, 0.12f);
            AccidentEnvironment.Box(root, "Street", new Vector3(0f, -0.02f, -20f), new Vector3(90f, 0.1f, 9f), asphalt);
            Material concrete = MaterialFactory.Create(new Color32(160, 156, 148, 255));
            Material brick = MaterialFactory.Create(new Color32(150, 92, 70, 255));
            Material steel = MaterialFactory.Create(new Color32(90, 96, 104, 255), 0.6f, 0.4f);

            // Standing remains of the community hall.
            AccidentEnvironment.Box(root, "Hall Back Wall", new Vector3(0f, 3f, 20f), new Vector3(20f, 6f, 0.5f), brick);
            AccidentEnvironment.Box(root, "Hall Side Wall W", new Vector3(-10f, 2.4f, 14f), new Vector3(0.5f, 4.8f, 12f), brick);
            AccidentEnvironment.Box(root, "Hall Side Wall E Stub", new Vector3(10f, 1.4f, 17f), new Vector3(0.5f, 2.8f, 6f), brick);
            // Collapsed roof slabs and rubble.
            System.Random random = new System.Random(23);
            for (int i = 0; i < 5; i++)
            {
                GameObject slab = AccidentEnvironment.Box(root, "Collapsed Slab", new Vector3(-6f + i * 3f, 0.8f + (i % 2) * 0.4f, 12f + (i % 3)),
                    new Vector3(3.6f, 0.3f, 5f), concrete);
                slab.transform.rotation = Quaternion.Euler(10f + i * 7f, i * 18f, (i % 2 == 0 ? 1 : -1) * 14f);
            }

            for (int i = 0; i < 26; i++)
            {
                Vector3 position = new Vector3(-9f + (float)random.NextDouble() * 18f, 0.2f, 7f + (float)random.NextDouble() * 11f);
                float s = 0.3f + (float)random.NextDouble() * 0.7f;
                GameObject rubble = (i % 3 == 0 ? AccidentEnvironment.Box(root, "Rubble", position, new Vector3(s, s * 0.6f, s), i % 2 == 0 ? concrete : brick)
                    : AccidentEnvironment.Decal(root, "Rubble Chip", position, new Vector3(s * 0.5f, s * 0.3f, s * 0.5f), i % 2 == 0 ? concrete : brick));
                rubble.transform.rotation = Quaternion.Euler((float)random.NextDouble() * 40f, (float)random.NextDouble() * 180f, 0f);
            }

            // Scaffolding, site cabin and fence.
            for (int x = 0; x < 4; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    AccidentEnvironment.Decal(root, "Scaffold Pole", new Vector3(11.5f + x * 1.8f, 2f, 8f + y * 1.5f), new Vector3(0.06f, 4f, 0.06f), steel);
                }
            }

            AccidentEnvironment.Decal(root, "Scaffold Board", new Vector3(14.2f, 2f, 8.75f), new Vector3(5.6f, 0.06f, 1.6f), MaterialFactory.Create(Palette.Wood));
            AccidentEnvironment.Box(root, "Site Cabin", new Vector3(-16f, 1.3f, 4f), new Vector3(6f, 2.6f, 2.6f), MaterialFactory.Create(new Color32(60, 120, 170, 255)));
            Material fence = MaterialFactory.Create(new Color32(130, 135, 140, 255));
            AccidentEnvironment.Box(root, "Site Fence W", new Vector3(-19f, 1f, 5f), new Vector3(0.05f, 2f, 30f), fence);
            AccidentEnvironment.Box(root, "Site Fence E", new Vector3(19f, 1f, 5f), new Vector3(0.05f, 2f, 30f), fence);
            AccidentEnvironment.Box(root, "Site Fence N", new Vector3(0f, 1f, 22f), new Vector3(38f, 2f, 0.05f), fence);

            // School next door (green zone side): low building and playground markings.
            AccidentEnvironment.Box(root, "School Building", new Vector3(-30f, 2.5f, -4f), new Vector3(12f, 5f, 16f), MaterialFactory.Create(Palette.WallCool));
            Material paint = MaterialFactory.Create(Palette.LinePaint);
            AccidentEnvironment.Decal(root, "Playground", new Vector3(-17f, 0.02f, -6f), new Vector3(8f, 0.02f, 8f), MaterialFactory.Create(new Color32(170, 80, 70, 255)));
            AccidentEnvironment.Decal(root, "Playground Circle", new Vector3(-17f, 0.03f, -6f), new Vector3(3f, 0.01f, 3f), paint, PrimitiveType.Cylinder);

            Place(root, assets.fireTruckPrefab, new Vector3(12f, 0f, -20f), -90f, "Fire Engine");
            Material metal = MaterialFactory.Create(Palette.Metal, 0.5f, 0.4f);
            Material glow = MaterialFactory.CreateUnlit(Palette.LampGlow, 1.6f);
            Lamp(root, new Vector3(-8f, 0f, -15.5f), false, metal, glow);
            Lamp(root, new Vector3(8f, 0f, -15.5f), false, metal, glow);
            Boundary(root, new Vector3(-24f, 0f, -26f), new Vector3(20f, 0f, 22f));
            return root;
        }

        // -------------------------------------------------------- night and rain

        public static void ApplyNight(Camera camera)
        {
            Light sun = RenderSettings.sun;
            if (sun != null)
            {
                sun.color = new Color(0.55f, 0.65f, 0.9f);
                sun.intensity = 0.18f;
                sun.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
                sun.shadows = LightShadows.None;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.14f, 0.17f, 0.26f);
            RenderSettings.ambientEquatorColor = new Color(0.1f, 0.11f, 0.15f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.06f);
            RenderSettings.fogColor = new Color(0.08f, 0.09f, 0.12f);
            RenderSettings.fogStartDistance = 8f;
            RenderSettings.fogEndDistance = 60f;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
            }
        }

        public static ParticleSystem AddRain(Transform follow, Material material)
        {
            GameObject rainObject = new GameObject("Rain");
            rainObject.transform.SetParent(follow, false);
            rainObject.transform.localPosition = new Vector3(0f, 8f, 4f);
            rainObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ParticleSystem rain = rainObject.AddComponent<ParticleSystem>();
            rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = rain.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = 1.1f;
            main.startSpeed = 14f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.04f);
            main.startColor = new Color(0.7f, 0.78f, 0.9f, 0.45f);
            main.maxParticles = GameSettings.Graphics == GraphicsPreset.Low ? 300 : 900;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = rain.emission;
            emission.rateOverTime = GameSettings.Graphics == GraphicsPreset.Low ? 250f : 750f;
            ParticleSystem.ShapeModule shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(24f, 24f, 1f);
            ParticleSystemRenderer renderer = rainObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 3f;
            renderer.velocityScale = 0.05f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (material != null) renderer.sharedMaterial = material;
            rain.Play();
            return rain;
        }
    }
}
