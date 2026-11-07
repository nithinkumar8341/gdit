using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GoldenHour
{
    public enum AmbulanceState
    {
        Approaching,
        Waiting,
        Departing,
        Gone
    }

    /// <summary>
    /// An ambulance that drives in with sirens, sends two paramedics to the casualty collection point,
    /// takes casualties in the order the player hands them over, carries them on a stretcher and departs.
    /// </summary>
    public sealed class Ambulance : MonoBehaviour
    {
        private readonly Queue<Casualty> queue = new Queue<Casualty>();
        private readonly List<Casualty> loaded = new List<Casualty>();
        private Vector3 spawn;
        private Vector3 bay;
        private Vector3 collectionPoint;
        private Paramedic lead;
        private Paramedic partner;
        private Transform stretcher;

        /// <summary>Optional Blender stretcher model (wheeled trolley); a primitive frame is used otherwise.</summary>
        public static GameObject StretcherModel { get; set; }
        private bool fetching;
        private bool released;
        private float speed;

        public int Number { get; private set; }
        public int Capacity { get; private set; }
        public AmbulanceState State { get; private set; }
        public int Assigned { get; private set; }
        public int FreeSpaces => Capacity - Assigned;
        public bool Busy => fetching || queue.Count > 0;
        public IReadOnlyList<Casualty> Loaded => loaded;
        public Paramedic Lead => lead;
        public Paramedic Partner => partner;
        public Transform StretcherFrame => stretcher;

        /// <summary>Distance behind the lead at which the second paramedic holds the foot end of the stretcher.</summary>
        public const float PartnerDistance = 2.55f;
        public FlashingLight[] Sirens { get; private set; } = new FlashingLight[0];

        public void Initialize(int number, int capacity, Vector3 spawnPoint, Vector3 bayPoint, Vector3 sceneCentre,
            GameObject vehiclePrefab, GameObject rigPrefab)
        {
            Number = number;
            Capacity = capacity;
            spawn = spawnPoint;
            bay = bayPoint;
            Vector3 toScene = sceneCentre - bay;
            toScene.y = 0f;
            collectionPoint = bay + (toScene.sqrMagnitude > 0.01f ? toScene.normalized * 4f : Vector3.forward * 4f);
            gameObject.name = "Ambulance " + number;
            Vector3 direction = bay - spawn;
            direction.y = 0f;
            transform.SetPositionAndRotation(spawn, Quaternion.LookRotation(direction.sqrMagnitude > 0.01f ? direction : Vector3.forward));
            BuildVehicle(vehiclePrefab);
            State = AmbulanceState.Approaching;
            speed = 14f;

            lead = SpawnParamedic("Paramedic A", true, rigPrefab, number * 10 + 1);
            partner = SpawnParamedic("Paramedic B", false, rigPrefab, number * 10 + 2);
            stretcher = BuildStretcher();
            lead.Body.Grip = CarryGrip.Behind;
            partner.Body.Grip = CarryGrip.Front;
        }

        private void BuildVehicle(GameObject prefab)
        {
            if (prefab != null)
            {
                GameObject model = Instantiate(prefab, transform);
                model.name = "Ambulance Model";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                AccidentEnvironment.AddBoundsCollider(model);
            }
            else
            {
                Material white = MaterialFactory.Create(new Color32(236, 236, 230, 255));
                Material green = MaterialFactory.Create(new Color32(60, 160, 70, 255));
                Material red = MaterialFactory.Create(new Color32(200, 40, 40, 255));
                Material glass = MaterialFactory.Create(Palette.Glass, 0.2f, 0.8f);
                Material tyre = MaterialFactory.Create(new Color32(20, 20, 22, 255));
                AccidentEnvironment.Box(transform, "Box Body", new Vector3(0f, 1.55f, -0.6f), new Vector3(2.3f, 2.3f, 4.2f), white);
                AccidentEnvironment.Box(transform, "Cab", new Vector3(0f, 1.15f, 2.3f), new Vector3(2.2f, 1.5f, 1.8f), white);
                AccidentEnvironment.Decal(transform, "Windscreen", new Vector3(0f, 1.55f, 3.21f), new Vector3(1.9f, 0.7f, 0.04f), glass);
                AccidentEnvironment.Decal(transform, "Stripe L", new Vector3(-1.16f, 1.25f, 0.3f), new Vector3(0.02f, 0.3f, 6f), green);
                AccidentEnvironment.Decal(transform, "Stripe R", new Vector3(1.16f, 1.25f, 0.3f), new Vector3(0.02f, 0.3f, 6f), green);
                AccidentEnvironment.Decal(transform, "Cross L", new Vector3(-1.17f, 2f, -0.8f), new Vector3(0.02f, 0.6f, 0.2f), red);
                AccidentEnvironment.Decal(transform, "Cross L2", new Vector3(-1.17f, 2f, -0.8f), new Vector3(0.02f, 0.2f, 0.6f), red);
                AccidentEnvironment.Decal(transform, "Cross R", new Vector3(1.17f, 2f, -0.8f), new Vector3(0.02f, 0.6f, 0.2f), red);
                AccidentEnvironment.Decal(transform, "Cross R2", new Vector3(1.17f, 2f, -0.8f), new Vector3(0.02f, 0.2f, 0.6f), red);
                foreach (float z in new[] { 2.1f, -1.9f })
                {
                    foreach (float x in new[] { -1.05f, 1.05f })
                    {
                        GameObject wheel = AccidentEnvironment.Decal(transform, "Wheel", new Vector3(x, 0.42f, z), new Vector3(0.84f, 0.15f, 0.84f), tyre,
                            PrimitiveType.Cylinder);
                        wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    }
                }
            }

            List<FlashingLight> sirens = new List<FlashingLight>();
            for (int i = 0; i < 4; i++)
            {
                Color color = i % 2 == 0 ? new Color(0.2f, 0.45f, 1f) : new Color(1f, 0.15f, 0.15f);
                Vector3 local = new Vector3(-0.75f + i * 0.5f, 2.78f, 1.3f);
                GameObject lamp = AccidentEnvironment.Decal(transform, "Siren Lamp", local, new Vector3(0.4f, 0.14f, 0.3f),
                    MaterialFactory.CreateUnlit(color));
                FlashingLight flasher = lamp.AddComponent<FlashingLight>();
                Light light = null;
                if (i == 0 || i == 3)
                {
                    light = new GameObject("Siren Light").AddComponent<Light>();
                    light.transform.SetParent(lamp.transform, false);
                    light.type = LightType.Point;
                    light.color = color;
                    light.range = 9f;
                    light.shadows = LightShadows.None;
                }

                flasher.Initialize(lamp.GetComponent<Renderer>(), color, 0.5f, i % 2 == 0 ? 0f : 0.5f, light);
                flasher.lightIntensity = 3f;
                sirens.Add(flasher);
            }

            Sirens = sirens.ToArray();
            Walker.SetCharacterLayer(gameObject);
        }

        private Paramedic SpawnParamedic(string name, bool isLead, GameObject rigPrefab, int seed)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(transform.parent, false);
            HumanoidBody body = new GameObject("Body").AddComponent<HumanoidBody>();
            body.transform.SetParent(root.transform, false);
            body.Build(BodyStyle.Paramedic, new Color32(40, 90, 60, 255), 1f, rigPrefab, seed);
            Paramedic medic = root.AddComponent<Paramedic>();
            medic.Ambulance = this;
            medic.Body = body;
            medic.Lead = isLead;
            medic.Walker = root.AddComponent<Walker>();
            medic.Walker.Initialize(body, 2.2f);
            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            capsule.radius = 0.3f;
            Walker.SetCharacterLayer(root);
            root.SetActive(false);
            return medic;
        }

        private Transform BuildStretcher()
        {
            Transform frame = new GameObject("Stretcher").transform;
            frame.SetParent(lead.transform, false);
            frame.localPosition = new Vector3(0f, 0.75f, -1.15f);
            if (StretcherModel != null)
            {
                GameObject model = Instantiate(StretcherModel, frame);
                model.name = "Stretcher Model";
                model.transform.localPosition = new Vector3(0f, -0.69f, 0f);
                model.transform.localRotation = Quaternion.identity;
                foreach (Collider collider in model.GetComponentsInChildren<Collider>()) Destroy(collider);
                Walker.SetCharacterLayer(model);
                return frame;
            }

            Material metal = MaterialFactory.Create(new Color32(170, 175, 180, 255), 0.6f, 0.5f);
            Material fabric = MaterialFactory.Create(new Color32(200, 60, 50, 255));
            AccidentEnvironment.Decal(frame, "Bed", Vector3.zero, new Vector3(0.6f, 0.06f, 2f), fabric);
            AccidentEnvironment.Decal(frame, "Rail L", new Vector3(-0.32f, 0.02f, 0f), new Vector3(0.04f, 0.04f, 2.3f), metal);
            AccidentEnvironment.Decal(frame, "Rail R", new Vector3(0.32f, 0.02f, 0f), new Vector3(0.04f, 0.04f, 2.3f), metal);
            return frame;
        }

        private void Update()
        {
            switch (State)
            {
                case AmbulanceState.Approaching:
                    Vector3 to = bay - transform.position;
                    to.y = 0f;
                    float distance = to.magnitude;
                    float step = Mathf.Min(distance, Mathf.Max(2f, Mathf.Min(speed, distance * 1.2f)) * Time.deltaTime);
                    if (distance > 0.05f) transform.position += to.normalized * step;
                    if (distance <= 0.1f) Arrive();
                    break;
                case AmbulanceState.Waiting:
                    if (released && !Busy) Depart();
                    break;
                case AmbulanceState.Departing:
                    transform.position += transform.forward * (speed * Time.deltaTime);
                    if (Vector3.Distance(transform.position, bay) > 70f)
                    {
                        State = AmbulanceState.Gone;
                        gameObject.SetActive(false);
                    }

                    break;
            }

            if (partner != null && partner.gameObject.activeSelf && lead.Walker.Moving)
            {
                Vector3 behind = lead.transform.position - lead.transform.forward * PartnerDistance;
                partner.transform.position = Vector3.Lerp(partner.transform.position, behind, Time.deltaTime * 8f);
                partner.transform.rotation = lead.transform.rotation;
                if (partner.Body.State != BodyState.Walking) partner.Body.SetState(BodyState.Walking);
            }
            else if (partner != null && partner.Body.State == BodyState.Walking && !lead.Walker.Moving)
            {
                partner.Body.SetState(BodyState.Standing);
            }
        }

        private Vector3 Rear => transform.position - transform.forward * 3.4f;

        private void Arrive()
        {
            transform.position = bay;
            State = AmbulanceState.Waiting;
            Vector3 rear = Rear;
            lead.transform.position = rear + transform.right * 0.6f;
            partner.transform.position = rear - transform.right * 0.6f;
            lead.gameObject.SetActive(true);
            partner.gameObject.SetActive(true);
            lead.Walker.GoTo(collectionPoint, () => FaceScene(lead));
            IncidentDirector.Instance?.OnAmbulanceArrived(this);
        }

        private void FaceScene(Paramedic medic)
        {
            Vector3 look = collectionPoint - bay;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f) medic.transform.rotation = Quaternion.LookRotation(look);
        }

        public void Assign(Casualty casualty)
        {
            if (FreeSpaces <= 0) return;
            Assigned++;
            casualty.AssignedToAmbulance = true;
            queue.Enqueue(casualty);
            if (!fetching) StartCoroutine(FetchRoutine());
        }

        public void Release()
        {
            released = true;
        }

        private IEnumerator FetchRoutine()
        {
            fetching = true;
            while (queue.Count > 0)
            {
                Casualty casualty = queue.Peek();
                if (casualty == null || casualty.Sim.State.deceased)
                {
                    queue.Dequeue();
                    if (casualty != null) IncidentDirector.Instance?.Announce("Ambulance " + Number + ": " + casualty.Id + " died before we reached them.");
                    continue;
                }

                bool arrived = false;
                Vector3 target = casualty.FocusPoint;
                target.y = 0f;
                Vector3 approach = target + (lead.transform.position - target).normalized * 1.3f;
                lead.Walker.GoTo(approach, () => arrived = true);
                float timeout = Time.time + 40f;
                while (!arrived && Time.time < timeout) yield return null;
                if (!arrived)
                {
                    lead.Walker.Stop();
                    lead.transform.position = approach;
                }

                lead.Body.SetState(BodyState.Kneeling);
                yield return new WaitForSeconds(1.5f);
                lead.Body.SetState(BodyState.Standing);
                if (casualty.Sim.State.deceased)
                {
                    queue.Dequeue();
                    IncidentDirector.Instance?.Announce("Ambulance " + Number + ": " + casualty.Id + " has died.");
                    continue;
                }

                casualty.OnStretcher = true;
                casualty.Walker.Stop(BodyState.Carried);
                casualty.transform.SetParent(stretcher, false);
                casualty.transform.localPosition = new Vector3(0f, 0.05f, 0.9f);
                casualty.transform.localRotation = Quaternion.identity;
                casualty.Body.SetState(BodyState.Carried);

                arrived = false;
                lead.Walker.GoTo(Rear - transform.forward * 1.2f, () => arrived = true);
                timeout = Time.time + 40f;
                while (!arrived && Time.time < timeout) yield return null;
                if (!arrived) lead.Walker.Stop();
                yield return new WaitForSeconds(1f);

                queue.Dequeue();
                loaded.Add(casualty);
                casualty.transform.SetParent(transform, false);
                casualty.HideFromScene();
                IncidentDirector.Instance?.OnCasualtyLoaded(this, casualty);
            }

            lead.Walker.GoTo(collectionPoint, () => FaceScene(lead));
            fetching = false;
            if (FreeSpaces <= 0) released = true;
        }

        private void Depart()
        {
            State = AmbulanceState.Departing;
            lead.gameObject.SetActive(false);
            partner.gameObject.SetActive(false);
            IncidentDirector.Instance?.OnAmbulanceDeparted(this);
        }

        /// <summary>Test/diagnostic: completes all pending loads immediately.</summary>
        public void CompleteLoadingNow()
        {
            StopAllCoroutines();
            if (lead != null)
            {
                lead.Walker.Stop();
                lead.Body.SetState(BodyState.Standing);
            }

            while (queue.Count > 0)
            {
                Casualty casualty = queue.Dequeue();
                if (casualty == null || casualty.Sim.State.deceased) continue;
                loaded.Add(casualty);
                casualty.OnStretcher = true;
                casualty.transform.SetParent(transform, false);
                casualty.HideFromScene();
                IncidentDirector.Instance?.OnCasualtyLoaded(this, casualty);
            }

            fetching = false;
            if (FreeSpaces <= 0) released = true;
        }
    }
}
