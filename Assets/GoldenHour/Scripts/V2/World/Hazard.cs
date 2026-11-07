using UnityEngine;
using UnityEngine.Rendering;

namespace GoldenHour
{
    /// <summary>
    /// A scene-safety hazard. The player spots it and marks it (Interact), which places a warning marker.
    /// Fire and sparks use small particle systems; danger zones warn the player when too close.
    /// </summary>
    public sealed class Hazard : MonoBehaviour, IInteractable
    {
        private Vector3 focus;
        private Light flicker;
        private float flickerSeed;

        public HazardType Type { get; private set; }
        public float Radius { get; private set; }
        public bool Marked { get; private set; }
        public bool ExposurePenaltyApplied { get; set; }
        public string DisplayName => ScenarioData.HazardName(Type);
        public Vector3 FocusPoint => transform.TransformPoint(focus);
        public string Prompt => Marked ? DisplayName + " (marked)" : "Mark hazard: " + DisplayName;
        public bool CanInteract => !Marked;

        /// <summary>Fire, live wires and unstable structures are dangerous to stand next to.</summary>
        public bool Dangerous => Type == HazardType.Fire || Type == HazardType.LiveWire || Type == HazardType.UnstableStructure;

        public void Interact()
        {
            if (IncidentDirector.Instance != null) IncidentDirector.Instance.MarkHazard(this);
        }

        public void Initialize(HazardPlacement placement, Material smoke, Material unlitTemplate)
        {
            Type = placement.type;
            Radius = placement.radius;
            gameObject.name = "Hazard - " + DisplayName;
            transform.SetPositionAndRotation(placement.position, Quaternion.Euler(0f, placement.yaw, 0f));
            flickerSeed = placement.position.x * 3.1f;
            BuildVisual(smoke);
            BoxCollider trigger = gameObject.AddComponent<BoxCollider>();
            trigger.center = focus;
            trigger.size = new Vector3(1.4f, 1.4f, 1.4f);
            trigger.isTrigger = true;
            gameObject.layer = Walker.CharacterLayer;
        }

        private void BuildVisual(Material smoke)
        {
            switch (Type)
            {
                case HazardType.FuelLeak:
                    Material fuel = MaterialFactory.Create(new Color32(30, 26, 34, 255), 0.4f, 0.95f);
                    AccidentEnvironment.Decal(transform, "Fuel Pool", new Vector3(0f, 0.025f, 0f), new Vector3(2.2f, 0.01f, 1.5f), fuel,
                        PrimitiveType.Cylinder);
                    AccidentEnvironment.Decal(transform, "Fuel Sheen", new Vector3(0.3f, 0.03f, 0.1f), new Vector3(1.1f, 0.01f, 0.7f),
                        MaterialFactory.Create(new Color32(70, 60, 110, 255), 0.6f, 1f), PrimitiveType.Cylinder);
                    focus = new Vector3(0f, 0.1f, 0f);
                    break;
                case HazardType.Fire:
                    AccidentEnvironment.Decal(transform, "Scorch", new Vector3(0f, 0.02f, 0f), new Vector3(1.6f, 0.01f, 1.6f),
                        MaterialFactory.Create(new Color32(20, 18, 16, 255)), PrimitiveType.Cylinder);
                    BuildFire(smoke);
                    focus = new Vector3(0f, 0.6f, 0f);
                    break;
                case HazardType.LiveWire:
                    Material pole = MaterialFactory.Create(new Color32(110, 90, 70, 255));
                    GameObject post = AccidentEnvironment.Decal(transform, "Fallen Pole", new Vector3(0f, 0.25f, 0f),
                        new Vector3(0.25f, 3.2f, 0.25f), pole, PrimitiveType.Cylinder);
                    post.transform.localRotation = Quaternion.Euler(0f, 0f, 78f);
                    Material cable = MaterialFactory.Create(new Color32(20, 20, 22, 255));
                    for (int i = 0; i < 5; i++)
                    {
                        GameObject segment = AccidentEnvironment.Decal(transform, "Cable", new Vector3(-2.6f + i * 0.55f, 0.04f, 0.4f + Mathf.Sin(i) * 0.3f),
                            new Vector3(0.6f, 0.03f, 0.03f), cable);
                        segment.transform.localRotation = Quaternion.Euler(0f, i * 17f, 0f);
                    }

                    BuildSparks(new Vector3(-0.2f, 0.08f, 1f));
                    focus = new Vector3(-0.2f, 0.2f, 1f);
                    break;
                case HazardType.UnstableStructure:
                    Material slab = MaterialFactory.Create(new Color32(150, 146, 138, 255));
                    GameObject wall = AccidentEnvironment.Box(transform, "Leaning Wall", new Vector3(0f, 1.4f, 0f), new Vector3(3.2f, 2.8f, 0.3f), slab);
                    wall.transform.localRotation = Quaternion.Euler(18f, 0f, 4f);
                    AccidentEnvironment.Decal(transform, "Crack Dust", new Vector3(0f, 0.02f, 0.8f), new Vector3(2.4f, 0.01f, 1f),
                        MaterialFactory.Create(new Color32(180, 175, 165, 255)));
                    focus = new Vector3(0f, 1.2f, 0.3f);
                    break;
                case HazardType.GasCylinder:
                    Material gas = MaterialFactory.Create(new Color32(200, 70, 40, 255), 0.4f, 0.5f);
                    GameObject bottle = AccidentEnvironment.Box(transform, "Gas Cylinder", new Vector3(0f, 0.18f, 0f), new Vector3(0.36f, 0.36f, 1.1f), gas);
                    bottle.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
                    AccidentEnvironment.Decal(transform, "Gas Cylinder Upright", new Vector3(0.6f, 0.55f, 0.3f), new Vector3(0.36f, 0.55f, 0.36f), gas,
                        PrimitiveType.Cylinder);
                    BuildVapour(smoke, new Vector3(0.45f, 0.25f, 0.4f));
                    focus = new Vector3(0.3f, 0.4f, 0.2f);
                    break;
                default:
                    // Traffic: an open approach lane where traffic could come through.
                    Material lane = MaterialFactory.Create(new Color32(236, 230, 212, 255));
                    for (int i = 0; i < 3; i++)
                    {
                        AccidentEnvironment.Decal(transform, "Approach Arrow", new Vector3(0f, 0.04f, -i * 2.2f), new Vector3(0.35f, 0.01f, 1.2f), lane);
                    }

                    focus = new Vector3(0f, 0.4f, 0f);
                    break;
            }
        }

        private void BuildFire(Material smoke)
        {
            GameObject fireObject = new GameObject("Fire");
            fireObject.transform.SetParent(transform, false);
            fireObject.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            fireObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ParticleSystem fire = fireObject.AddComponent<ParticleSystem>();
            fire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = fire.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f, 0.9f), new Color(1f, 0.85f, 0.3f, 0.8f));
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = fire.emission;
            emission.rateOverTime = 30f;
            ParticleSystem.ShapeModule shape = fire.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 10f;
            shape.radius = 0.35f;
            ParticleSystem.SizeOverLifetimeModule size = fire.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
            ParticleSystem.ColorOverLifetimeModule colour = fire.colorOverLifetime;
            colour.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0f), new GradientColorKey(new Color(0.9f, 0.25f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
            ParticleSystemRenderer renderer = fireObject.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (smoke != null) renderer.sharedMaterial = smoke;
            fire.Play();

            flicker = new GameObject("Fire Light").AddComponent<Light>();
            flicker.transform.SetParent(transform, false);
            flicker.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            flicker.type = LightType.Point;
            flicker.color = new Color(1f, 0.55f, 0.2f);
            flicker.range = 8f;
            flicker.shadows = LightShadows.None;
            BuildVapour(smoke, new Vector3(0f, 1.2f, 0f));
        }

        private void BuildSparks(Vector3 position)
        {
            GameObject sparksObject = new GameObject("Sparks");
            sparksObject.transform.SetParent(transform, false);
            sparksObject.transform.localPosition = position;
            ParticleSystem sparks = sparksObject.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = sparks.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.5f), new Color(0.6f, 0.8f, 1f));
            main.gravityModifier = 1f;
            main.maxParticles = 40;
            ParticleSystem.EmissionModule emission = sparks.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 6, 14, 1, 0.7f) });
            ParticleSystem.ShapeModule shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            ParticleSystemRenderer renderer = sparksObject.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.sharedMaterial = MaterialFactory.CreateUnlit(new Color(1f, 0.85f, 0.5f), 4f);
            sparks.Play();
            flicker = new GameObject("Spark Light").AddComponent<Light>();
            flicker.transform.SetParent(sparksObject.transform, false);
            flicker.type = LightType.Point;
            flicker.color = new Color(0.7f, 0.85f, 1f);
            flicker.range = 4f;
            flicker.shadows = LightShadows.None;
        }

        private void BuildVapour(Material smoke, Vector3 position)
        {
            if (smoke == null) return;
            GameObject vapourObject = new GameObject("Vapour");
            vapourObject.transform.SetParent(transform, false);
            vapourObject.transform.localPosition = position;
            vapourObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ParticleSystem vapour = vapourObject.AddComponent<ParticleSystem>();
            vapour.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = vapour.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = 3f;
            main.startSpeed = 0.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startColor = new Color(0.35f, 0.33f, 0.32f, 0.35f);
            main.maxParticles = 30;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = vapour.emission;
            emission.rateOverTime = 6f;
            ParticleSystem.SizeOverLifetimeModule size = vapour.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2f));
            ParticleSystemRenderer renderer = vapourObject.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.sharedMaterial = smoke;
            vapour.Play();
        }

        private void Update()
        {
            if (flicker == null) return;
            float noise = Mathf.PerlinNoise(Time.time * 8f, flickerSeed);
            flicker.intensity = Type == HazardType.LiveWire ? (noise > 0.7f ? 3f : 0f) : 1.6f + noise * 1.6f;
        }

        /// <summary>Places a yellow-and-black warning marker (and cones for traffic).</summary>
        public void Mark()
        {
            if (Marked) return;
            Marked = true;
            Material yellow = MaterialFactory.Create(new Color32(240, 200, 30, 255));
            Material black = MaterialFactory.Create(new Color32(25, 25, 28, 255));
            Vector3 basePosition = focus + new Vector3(0f, 0f, -Radius * 0.5f);
            basePosition.y = 0f;
            GameObject post = AccidentEnvironment.Decal(transform, "Hazard Marker Post", basePosition + new Vector3(0f, 0.6f, 0f),
                new Vector3(0.08f, 0.6f, 0.08f), yellow, PrimitiveType.Cylinder);
            for (int i = 0; i < 3; i++)
            {
                AccidentEnvironment.Decal(post.transform, "Band", new Vector3(0f, -0.6f + i * 0.5f, 0f), new Vector3(1.05f, 0.15f, 1.05f), black,
                    PrimitiveType.Cylinder);
            }

            GameObject sign = AccidentEnvironment.Decal(transform, "Hazard Marker Sign", basePosition + new Vector3(0f, 1.35f, 0f),
                new Vector3(0.55f, 0.55f, 0.04f), yellow);
            sign.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            AccidentEnvironment.Decal(sign.transform, "Mark", new Vector3(0f, 0f, -0.6f), new Vector3(0.15f, 0.55f, 0.5f), black);

            if (Type == HazardType.Traffic)
            {
                for (int i = -1; i <= 1; i++)
                {
                    AccidentEnvironment.Decal(transform, "Placed Cone", new Vector3(i * 1.4f, 0.3f, 1.5f), new Vector3(0.3f, 0.3f, 0.3f),
                        MaterialFactory.Create(Palette.SafetyOrange), PrimitiveType.Cylinder);
                }
            }
        }

        public float DistanceTo(Vector3 point)
        {
            Vector3 centre = FocusPoint;
            centre.y = point.y;
            return Vector3.Distance(centre, point);
        }
    }
}
