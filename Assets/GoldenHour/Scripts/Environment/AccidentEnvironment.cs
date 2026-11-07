using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace GoldenHour
{
    /// <summary>
    /// Builds the low-poly roadside accident scene: road, markings, skid marks, sidewalks, the
    /// damaged vehicle with hazard lights and smoke, debris, barriers, cones, signs, fences,
    /// grass, buildings, trees, street lights, invisible boundaries and the late-afternoon lighting.
    /// Prefab references are assigned by the editor builder; primitives are used as fallbacks.
    /// </summary>
    public sealed class AccidentEnvironment : MonoBehaviour
    {
        public GameObject carPrefab;
        public GameObject barrierPrefab;
        public GameObject conePrefab;
        public Material smokeMaterial;

        /// <summary>Start just behind the barriers so the crash and people are in view immediately.</summary>
        public static readonly Vector3 PlayerStart = new Vector3(0f, 0.1f, -13.5f);
        public const float PlayerStartPitch = 6f;
        public const float SceneEntryZ = -9.5f;

        private static readonly Vector3 CarPosition = new Vector3(0.2f, 0f, 5.2f);
        private static readonly Quaternion CarRotation = Quaternion.Euler(0f, -32f, 0f);

        public FlashingLight[] HazardLights { get; private set; } = new FlashingLight[0];
        public ParticleSystem Smoke { get; private set; }

        public Transform Build()
        {
            Transform root = new GameObject("Accident Scene").transform;
            Material grass = MaterialFactory.Create(Palette.Grass, 0f, 0.05f);
            Material asphalt = MaterialFactory.Create(Palette.Asphalt, 0f, 0.12f);
            Material concrete = MaterialFactory.Create(Palette.Concrete, 0f, 0.1f);
            Material paint = MaterialFactory.Create(Palette.LinePaint);
            Material yellowPaint = MaterialFactory.Create(Palette.LineYellow);
            Material dark = MaterialFactory.Create(Palette.DarkTrim);
            Material orange = MaterialFactory.Create(Palette.SafetyOrange);

            Box(root, "Ground", new Vector3(0f, -0.25f, 4f), new Vector3(80f, 0.4f, 84f), grass);
            Box(root, "Road", new Vector3(0f, -0.02f, 4f), new Vector3(11f, 0.1f, 84f), asphalt);
            Box(root, "West Sidewalk", new Vector3(-6.3f, 0.04f, 4f), new Vector3(1.6f, 0.2f, 84f), concrete);
            Box(root, "East Sidewalk", new Vector3(6.3f, 0.04f, 4f), new Vector3(1.6f, 0.2f, 84f), concrete);
            Decal(root, "West Kerb", new Vector3(-5.52f, 0.05f, 4f), new Vector3(0.1f, 0.22f, 84f), dark);
            Decal(root, "East Kerb", new Vector3(5.52f, 0.05f, 4f), new Vector3(0.1f, 0.22f, 84f), dark);
            Decal(root, "West Edge Line", new Vector3(-5.25f, 0.035f, 4f), new Vector3(0.12f, 0.02f, 84f), paint);
            Decal(root, "East Edge Line", new Vector3(5.25f, 0.035f, 4f), new Vector3(0.12f, 0.02f, 84f), paint);
            for (float z = -36f; z <= 44f; z += 5f)
            {
                Decal(root, "Centre Dash", new Vector3(0f, 0.035f, z), new Vector3(0.14f, 0.02f, 2.4f), yellowPaint);
            }

            BuildVehicle(root, dark, orange);
            BuildSkidMarks(root);
            BuildTrafficControl(root, orange, paint);
            BuildRoadside(root, concrete, dark);
            BuildBoundaries(root);
            ApplyLighting();
            return root;
        }

        /// <summary>Warm late-afternoon sun, soft shadows, gradient ambient light and light haze.</summary>
        public static void ApplyLighting()
        {
            Light sun = null;
            foreach (Light light in FindObjectsByType<Light>())
            {
                if (light.type == LightType.Directional) { sun = light; break; }
            }

            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(24f, -38f, 0f);
                sun.color = new Color32(255, 204, 150, 255);
                sun.intensity = 1.55f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.82f;
                RenderSettings.sun = sun;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color32(150, 160, 186, 255);
            RenderSettings.ambientEquatorColor = new Color32(176, 150, 128, 255);
            RenderSettings.ambientGroundColor = new Color32(74, 66, 58, 255);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color32(222, 190, 160, 255);
            RenderSettings.fogStartDistance = 18f;
            RenderSettings.fogEndDistance = 95f;
        }

        // ------------------------------------------------------------- vehicle

        private void BuildVehicle(Transform root, Material dark, Material orange)
        {
            Transform car;
            if (carPrefab != null)
            {
                GameObject model = Instantiate(carPrefab, CarPosition, CarRotation, root);
                model.name = "Damaged Vehicle";
                AddBoundsCollider(model);
                car = model.transform;
            }
            else
            {
                car = new GameObject("Damaged Vehicle").transform;
                car.SetParent(root, false);
                car.SetPositionAndRotation(CarPosition, CarRotation);
                Box(car, "Body", new Vector3(0f, 0.6f, 0f), new Vector3(4f, 0.7f, 1.75f),
                    MaterialFactory.Create(new Color32(110, 42, 32, 255)));
                Box(car, "Cabin", new Vector3(-0.35f, 1.15f, 0f), new Vector3(2f, 0.55f, 1.55f),
                    MaterialFactory.Create(Palette.Glass));
            }

            // Hazard lights on all four corners (front of the model faces local +X), plus one shared
            // amber point light so the flashes also light the road surface.
            // Placed in world space from the car pose so imported model scale/axes do not matter.
            Transform fx = new GameObject("Vehicle Effects").transform;
            fx.SetParent(root, false);
            fx.SetPositionAndRotation(CarPosition, CarRotation);
            Light glow = new GameObject("Hazard Glow").AddComponent<Light>();
            glow.transform.SetParent(fx, false);
            glow.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            glow.type = LightType.Point;
            glow.color = Palette.Hazard;
            glow.range = 7f;
            glow.shadows = LightShadows.None;

            Vector3[] corners =
            {
                new Vector3(2.02f, 0.62f, 0.72f), new Vector3(2.02f, 0.62f, -0.72f),
                new Vector3(-2.02f, 0.66f, 0.72f), new Vector3(-2.02f, 0.66f, -0.72f)
            };
            HazardLights = new FlashingLight[corners.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                GameObject lamp = Decal(fx, "Hazard Lamp", corners[i], new Vector3(0.12f, 0.12f, 0.24f),
                    MaterialFactory.CreateUnlit(Palette.Hazard));
                FlashingLight flasher = lamp.AddComponent<FlashingLight>();
                flasher.Initialize(lamp.GetComponent<Renderer>(), Palette.Hazard, 0.9f, 0f, i == 0 ? glow : null);
                HazardLights[i] = flasher;
            }

            Smoke = BuildSmoke(fx, new Vector3(1.6f, 1.0f, 0.1f));

            // Crash details: debris, glass, a detached hubcap and a fluid stain.
            Material glass = MaterialFactory.Create(new Color32(170, 200, 214, 255), 0.1f, 0.85f);
            Decal(root, "Debris Panel", new Vector3(2.6f, 0.08f, 2.4f), new Vector3(0.9f, 0.06f, 0.5f), dark)
                .transform.rotation = Quaternion.Euler(4f, 38f, 8f);
            Decal(root, "Debris Bumper Piece", new Vector3(-1.8f, 0.1f, 9.4f), new Vector3(1.1f, 0.14f, 0.24f), orange)
                .transform.rotation = Quaternion.Euler(0f, -64f, 6f);
            Decal(root, "Debris Fragment", new Vector3(1.4f, 0.06f, 1.2f), new Vector3(0.3f, 0.06f, 0.24f), dark)
                .transform.rotation = Quaternion.Euler(0f, 12f, 0f);
            System.Random random = new System.Random(7);
            for (int i = 0; i < 9; i++)
            {
                Vector3 position = new Vector3(2.2f + (float)random.NextDouble() * 1.6f, 0.04f,
                    6.2f + (float)random.NextDouble() * 1.8f);
                Decal(root, "Glass Shard", position, new Vector3(0.14f, 0.02f, 0.1f), glass)
                    .transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 180f, 0f);
            }

            Decal(root, "Detached Hubcap", new Vector3(-2.3f, 0.03f, 6.8f), new Vector3(0.42f, 0.02f, 0.42f),
                MaterialFactory.Create(new Color32(186, 188, 190, 255), 0.7f, 0.6f), PrimitiveType.Cylinder);
            Decal(root, "Fluid Stain", new Vector3(1.2f, 0.032f, 6.4f), new Vector3(1.8f, 0.01f, 1.2f),
                MaterialFactory.Create(new Color32(30, 30, 34, 255), 0f, 0.85f));
        }

        private ParticleSystem BuildSmoke(Transform parent, Vector3 localPosition)
        {
            GameObject smokeObject = new GameObject("Engine Smoke");
            smokeObject.transform.SetParent(parent, false);
            smokeObject.transform.localPosition = localPosition;
            smokeObject.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            ParticleSystem smoke = smokeObject.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = smoke.main;
            main.duration = 5f;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.2f, 4.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.42f, 0.42f, 0.44f, 0.5f),
                new Color(0.62f, 0.6f, 0.58f, 0.38f));
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.02f;

            ParticleSystem.EmissionModule emission = smoke.emission;
            emission.rateOverTime = 9f;
            ParticleSystem.ShapeModule shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = 0.3f;

            ParticleSystem.SizeOverLifetimeModule size = smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.6f));
            ParticleSystem.ColorOverLifetimeModule colour = smoke.colorOverLifetime;
            colour.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;
            ParticleSystem.VelocityOverLifetimeModule drift = smoke.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.World;
            drift.x = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            drift.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            drift.z = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            ParticleSystem.RotationOverLifetimeModule spin = smoke.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

            ParticleSystemRenderer renderer = smokeObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (smokeMaterial != null) renderer.sharedMaterial = smokeMaterial;
            smoke.Play();
            return smoke;
        }

        /// <summary>Two curved tyre tracks leading to the car's rear wheels.</summary>
        private static void BuildSkidMarks(Transform root)
        {
            Material rubber = MaterialFactory.Create(Palette.SkidMark, 0f, 0.05f);
            Vector3[] wheelEnds =
            {
                CarPosition + CarRotation * new Vector3(-1.25f, 0f, 0.82f),
                CarPosition + CarRotation * new Vector3(-1.25f, 0f, -0.82f)
            };
            foreach (Vector3 end in wheelEnds)
            {
                Vector3 start = new Vector3(end.x - 1.4f, 0f, -7f);
                Vector3 control = new Vector3(end.x - 1.6f, 0f, end.z - 5f);
                Vector3 previous = start;
                const int segments = 12;
                for (int i = 1; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    Vector3 point = (1 - t) * (1 - t) * start + 2 * (1 - t) * t * control + t * t * end;
                    Vector3 middle = (previous + point) * 0.5f;
                    Vector3 direction = point - previous;
                    GameObject mark = Decal(root, "Skid Mark", new Vector3(middle.x, 0.034f, middle.z),
                        new Vector3(0.2f, 0.01f, direction.magnitude + 0.04f), rubber);
                    mark.transform.rotation = Quaternion.LookRotation(direction);
                    previous = point;
                }
            }
        }

        // ------------------------------------------------------ traffic control

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
                new Vector3(-4.6f, 0f, -7.5f), new Vector3(-3.2f, 0f, -6.2f), new Vector3(4.4f, 0f, -8.6f),
                new Vector3(-4.8f, 0f, 14.5f), new Vector3(4.5f, 0f, 15f), new Vector3(3.3f, 0f, 16.3f),
                new Vector3(-2.2f, 0f, -4.6f), new Vector3(4.6f, 0f, 3.2f)
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

            Material post = MaterialFactory.Create(Palette.Metal, 0.5f, 0.4f);
            // Diamond warning sign facing the approaching rescuer.
            Primitive(root, "Warning Sign Post", PrimitiveType.Cylinder, new Vector3(-5.9f, 1f, -12.5f),
                new Vector3(0.08f, 1f, 0.08f), post);
            GameObject sign = Box(root, "Warning Sign", new Vector3(-5.9f, 2.05f, -12.6f),
                new Vector3(0.9f, 0.9f, 0.05f), orange);
            sign.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            Decal(root, "Warning Sign Mark", new Vector3(-5.9f, 2.12f, -12.64f), new Vector3(0.1f, 0.36f, 0.02f), white);
            Decal(root, "Warning Sign Dot", new Vector3(-5.9f, 1.84f, -12.64f), new Vector3(0.1f, 0.1f, 0.02f), white);

            // "ACCIDENT AHEAD" board and a speed-limit sign on the east side.
            TextSign(root, "Accident Board", new Vector3(5.9f, 0f, -12.2f), new Vector2(1.7f, 0.9f),
                "ACCIDENT\nAHEAD", Palette.SafetyOrange, new Color32(20, 20, 22, 255), post);
            Primitive(root, "Speed Sign Post", PrimitiveType.Cylinder, new Vector3(-6.4f, 1.1f, 22f),
                new Vector3(0.08f, 1.1f, 0.08f), post);
            GameObject rim = Primitive(root, "Speed Sign Rim", PrimitiveType.Cylinder, new Vector3(-6.4f, 2.4f, 21.95f),
                new Vector3(0.8f, 0.02f, 0.8f), MaterialFactory.Create(Palette.EmergencyRed));
            rim.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            GameObject face = Primitive(root, "Speed Sign Face", PrimitiveType.Cylinder, new Vector3(-6.4f, 2.4f, 21.92f),
                new Vector3(0.62f, 0.02f, 0.62f), white);
            face.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            WorldText(root, "30", new Vector3(-6.4f, 2.4f, 21.9f), 3.2f, new Color32(20, 20, 22, 255));
        }

        private static void TextSign(Transform root, string name, Vector3 basePosition, Vector2 size, string text,
            Color board, Color ink, Material post)
        {
            Primitive(root, name + " Post", PrimitiveType.Cylinder, basePosition + new Vector3(0f, 0.8f, 0.04f),
                new Vector3(0.07f, 0.8f, 0.07f), post);
            Box(root, name, basePosition + new Vector3(0f, 1.6f + size.y * 0.5f, 0f),
                new Vector3(size.x, size.y, 0.06f), MaterialFactory.Create(board));
            WorldText(root, text, basePosition + new Vector3(0f, 1.6f + size.y * 0.5f, -0.04f), 2.6f, ink);
        }

        private static void WorldText(Transform root, string text, Vector3 position, float fontSize, Color color)
        {
            GameObject label = new GameObject("Sign Text");
            label.transform.SetParent(root, false);
            label.transform.position = position;
            TextMeshPro tmp = label.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(2f, 1f);
        }

        // ------------------------------------------------------------ roadside

        private void BuildRoadside(Transform root, Material concrete, Material dark)
        {
            Material wallA = MaterialFactory.Create(Palette.WallWarm);
            Material wallB = MaterialFactory.Create(Palette.WallCool);
            Material glass = MaterialFactory.Create(Palette.Glass, 0.2f, 0.7f);
            Material trunk = MaterialFactory.Create(Palette.Wood);
            Material leaves = MaterialFactory.Create(Palette.Foliage);
            Material lampMetal = MaterialFactory.Create(Palette.Metal, 0.5f, 0.4f);
            Material lampGlow = MaterialFactory.CreateUnlit(Palette.LampGlow, 1.6f);
            Material fenceWood = MaterialFactory.Create(new Color32(150, 112, 78, 255));
            Material tuft = MaterialFactory.Create(Palette.GrassDark, 0f, 0.05f);

            for (int i = 0; i < 6; i++)
            {
                float z = -22f + i * 11f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float height = 5f + ((i + side + 2) % 3) * 2f;
                    float x = side * 14f;
                    Material wall = (i + side) % 2 == 0 ? wallA : wallB;
                    Box(root, "Building", new Vector3(x, height * 0.5f, z), new Vector3(9f, height, 9f), wall);
                    Decal(root, "Building Roof", new Vector3(x, height + 0.15f, z), new Vector3(9.4f, 0.3f, 9.4f), concrete);
                    for (int floor = 0; floor < height / 2.6f - 0.5f; floor++)
                    {
                        Decal(root, "Window", new Vector3(x - side * 4.52f, 1.6f + floor * 2.6f, z - 2f),
                            new Vector3(0.06f, 1.3f, 1.6f), glass);
                        Decal(root, "Window", new Vector3(x - side * 4.52f, 1.6f + floor * 2.6f, z + 2f),
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
                Decal(root, "Tree Crown", new Vector3(x, 3.3f, z), new Vector3(2.2f, 2.4f, 2.2f), leaves,
                    PrimitiveType.Sphere);
                Decal(root, "Tree Crown Top", new Vector3(x + 0.3f, 4.3f, z - 0.2f), new Vector3(1.4f, 1.5f, 1.4f),
                    leaves, PrimitiveType.Sphere);

                float lampX = i % 2 == 0 ? 7.2f : -7.2f;
                Primitive(root, "Street Lamp Post", PrimitiveType.Cylinder, new Vector3(lampX, 2.2f, z + 4f),
                    new Vector3(0.12f, 2.2f, 0.12f), lampMetal);
                float headX = lampX - Mathf.Sign(lampX) * 0.5f;
                Decal(root, "Street Lamp Head", new Vector3(headX, 4.4f, z + 4f), new Vector3(1f, 0.12f, 0.3f), lampMetal);
                Decal(root, "Street Lamp Bulb", new Vector3(headX - Mathf.Sign(lampX) * 0.25f, 4.32f, z + 4f),
                    new Vector3(0.4f, 0.05f, 0.22f), lampGlow);
            }

            // Wooden fences along the back of each verge.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 9.05f;
                for (float z = -22f; z <= 26f; z += 2f)
                {
                    Decal(root, "Fence Post", new Vector3(x, 0.55f, z), new Vector3(0.1f, 1.1f, 0.1f), fenceWood);
                }

                Decal(root, "Fence Rail", new Vector3(x, 0.85f, 2f), new Vector3(0.05f, 0.1f, 48f), fenceWood);
                Decal(root, "Fence Rail", new Vector3(x, 0.45f, 2f), new Vector3(0.05f, 0.1f, 48f), fenceWood);
            }

            // Grass tufts and low bushes on the verges (fixed seed so every run looks the same).
            System.Random random = new System.Random(19);
            for (int i = 0; i < 44; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float x = side * (7.4f + (float)random.NextDouble() * 1.4f);
                float z = -22f + (float)random.NextDouble() * 47f;
                float scale = 0.25f + (float)random.NextDouble() * 0.35f;
                bool bush = i % 7 == 0;
                Decal(root, bush ? "Bush" : "Grass Tuft", new Vector3(x, scale * 0.3f, z),
                    bush ? new Vector3(1.1f, 0.8f, 1.1f) : new Vector3(scale, scale * 0.9f, scale),
                    bush ? leaves : tuft, PrimitiveType.Sphere);
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

        /// <summary>Decorative primitive without a collider (markings, foliage, small props).</summary>
        public static GameObject Decal(Transform parent, string name, Vector3 position, Vector3 scale,
            Material material, PrimitiveType type = PrimitiveType.Cube)
        {
            GameObject item = Primitive(parent, name, type, position, scale, material);
            Collider collider = item.GetComponent<Collider>();
            if (collider != null) DestroyImmediate(collider);
            return item;
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
