using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GoldenHour
{
    /// <summary>Anything the player can look at and press Interact on.</summary>
    public interface IInteractable
    {
        string Prompt { get; }
        bool CanInteract { get; }
        Vector3 FocusPoint { get; }
        void Interact();
    }

    public enum ExamAction
    {
        AskToWalk,
        CheckBreathing,
        OpenAirway,
        CheckCirculation,
        GiveCommand,
        LookForBleeding
    }

    public sealed class Finding
    {
        public string text;
        public float minute;
    }

    /// <summary>
    /// One casualty in the world: physiology simulation, jointed body with breathing/restless/limp
    /// behaviour, physical triage tag, speech barks and what the player has found out by examining.
    /// </summary>
    public sealed class Casualty : MonoBehaviour, IInteractable
    {
        private readonly Dictionary<ExamAction, Finding> findings = new Dictionary<ExamAction, Finding>();
        private TriageTagVisual tagVisual;
        private SpeechBark bark;
        private TextMeshPro idLabel;

        /// <summary>Hides the floating ID while the player kneels beside the casualty (it would fill the view).</summary>
        public void SetIdLabelVisible(bool visible)
        {
            if (idLabel != null) idLabel.enabled = visible;
        }
        private BoxCollider bodyCollider;
        private float nextBark;
        private bool collapsedAnnounced;
        private System.Random random;

        public CasualtySim Sim { get; private set; }
        public CasualtyProfile Profile => Sim.Profile;
        public TriageRecord Record { get; } = new TriageRecord();
        public HumanoidBody Body { get; private set; }
        public Walker Walker { get; private set; }
        public string Id => Profile.casualtyId;
        public bool GlovesPenaltyApplied { get; set; }
        public bool InGreenZone { get; set; }
        public bool HeadingToGreenZone { get; set; }
        public bool AssignedToAmbulance { get; set; }
        public bool OnStretcher { get; set; }
        public bool AutoTaggedGreen { get; set; }
        public float RecoveryRoll => Body != null ? Body.RecoveryRoll : 0f;
        public IReadOnlyDictionary<ExamAction, Finding> Findings => findings;
        public TriageTag Tag => Record.CurrentTag;
        public bool Lying => Body != null && (Body.State == BodyState.Lying || Body.State == BodyState.Recovery ||
                                              Body.State == BodyState.Carried);

        public Vector3 FocusPoint
        {
            get
            {
                if (Body == null) return transform.position + Vector3.up;
                switch (Body.State)
                {
                    case BodyState.Lying:
                    case BodyState.Recovery:
                    case BodyState.Carried:
                        return transform.TransformPoint(new Vector3(0f, 0.25f, -0.85f * Body.Height / 1.75f));
                    case BodyState.Sitting:
                        return transform.TransformPoint(new Vector3(0f, 0.6f, 0f));
                    default:
                        return transform.TransformPoint(new Vector3(0f, Body.Height * 0.7f, 0f));
                }
            }
        }

        public string Prompt
        {
            get
            {
                if (Sim.State.deceased && Record.HasTag) return Id + " - deceased (tagged BLACK)";
                return "Examine " + Id + (Record.HasTag ? "  [" + TriageTagVisual.UiLabel(Record.CurrentTag) + "]" : string.Empty);
            }
        }

        public bool CanInteract => !Sim.Transported && !OnStretcher && !HeadingToGreenZone;

        public void Interact()
        {
            if (IncidentDirector.Instance != null) IncidentDirector.Instance.OpenExamination(this);
        }

        /// <summary>Where the casualty was found (used by the debrief map after transport).</summary>
        public Vector3 SpawnPosition { get; private set; }

        public void Initialize(CasualtyPlacement placement, int seed, float deteriorationScale, GameObject rigPrefab)
        {
            random = new System.Random(seed);
            Sim = new CasualtySim(placement.profile, seed, deteriorationScale);
            gameObject.name = Profile.casualtyId + " - " + Profile.displayName;
            transform.SetPositionAndRotation(placement.position, Quaternion.Euler(0f, placement.yaw, 0f));
            SpawnPosition = placement.position;

            BodyStyle style = Profile.variant == BodyVariant.Child ? BodyStyle.Child
                : Profile.variant == BodyVariant.Motorcyclist ? BodyStyle.Motorcyclist
                : Profile.variant == BodyVariant.Elderly ? BodyStyle.Elderly
                : Profile.variant == BodyVariant.Worker ? BodyStyle.Worker
                : BodyStyle.Civilian;
            float scale = Profile.variant == BodyVariant.Child ? 0.66f : 1f;
            Body = new GameObject("Body").AddComponent<HumanoidBody>();
            Body.transform.SetParent(transform, false);
            Body.Build(style, Profile.clothingColor, scale, rigPrefab, seed);

            bodyCollider = gameObject.AddComponent<BoxCollider>();
            tagVisual = gameObject.AddComponent<TriageTagVisual>();
            bark = gameObject.AddComponent<SpeechBark>();
            bark.Initialize(2f);

            GameObject labelObject = new GameObject("ID Label");
            labelObject.transform.SetParent(transform, false);
            idLabel = labelObject.AddComponent<TextMeshPro>();
            idLabel.fontSize = 1.4f;
            idLabel.alignment = TextAlignmentOptions.Center;
            idLabel.color = new Color(1f, 1f, 1f, 0.8f);
            idLabel.rectTransform.sizeDelta = new Vector2(1.2f, 0.4f);
            idLabel.text = Profile.casualtyId;
            labelObject.AddComponent<PatientBillboard>();

            Walker = gameObject.AddComponent<Walker>();
            Walker.Initialize(Body, Profile.variant == BodyVariant.Child ? 1.3f : 1.5f);
            Walker.SetCharacterLayer(gameObject);
            nextBark = Time.time + 3f + (float)random.NextDouble() * 8f;
            ApplyRestPose();
        }

        private BodyState RestState()
        {
            if (Sim.State.deceased) return BodyState.Lying;
            if ((Sim.Applied & Treatment.RecoveryPosition) != 0) return BodyState.Recovery;
            if (OnStretcher) return BodyState.Carried;
            if (!Sim.State.canWalk && Profile.pose == BodyPose.Standing) return BodyState.Lying;
            switch (Profile.pose)
            {
                case BodyPose.Standing: return BodyState.Standing;
                case BodyPose.Sitting: return BodyState.Sitting;
                default: return BodyState.Lying;
            }
        }

        public void ApplyRestPose()
        {
            if (Walker != null && Walker.Moving) return;
            BodyState state = RestState();
            if (Body.State != state) Body.SetState(state);
            UpdateColliderAndLabels();
        }

        private void UpdateColliderAndLabels()
        {
            float h = Body.Height / 1.75f;
            bool lying = Lying;
            if (lying)
            {
                bodyCollider.center = new Vector3(0f, 0.2f, -0.85f * h);
                bodyCollider.size = new Vector3(0.8f, 0.45f, 1.85f * h);
            }
            else if (Body.State == BodyState.Sitting)
            {
                bodyCollider.center = new Vector3(0f, 0.45f * h, 0.15f);
                bodyCollider.size = new Vector3(0.7f, 0.9f * h, 0.9f);
            }
            else
            {
                bodyCollider.center = new Vector3(0f, 0.88f * h, 0f);
                bodyCollider.size = new Vector3(0.7f, 1.76f * h, 0.5f);
            }

            float labelHeight = lying ? 0.9f : Body.State == BodyState.Sitting ? 1.3f * h : 2.05f * h;
            float labelZ = lying ? -0.85f * h : 0f;
            idLabel.transform.localPosition = new Vector3(0f, labelHeight, labelZ);
            bark.SetHeight(labelHeight + 0.35f);
            ApplyTagVisual();
        }

        private void ApplyTagVisual()
        {
            float h = Body.Height / 1.75f;
            bool lying = Lying;
            Vector3 card = lying ? new Vector3(0.18f, 0.36f, -1.15f * h) : new Vector3(0.12f, 1.3f * h, 0.16f);
            Vector3 stick = lying ? new Vector3(-0.3f, 0.12f, -1.0f * h) : new Vector3(-0.25f, 1.0f * h, 0.12f);
            if (Body.State == BodyState.Sitting)
            {
                card = new Vector3(0.12f, 0.75f * h, 0.2f);
                stick = new Vector3(-0.25f, 0.6f * h, 0.2f);
            }

            tagVisual.Apply(Record.CurrentTag, card, stick);
        }

        public void SetTag(TriageTag tag)
        {
            ApplyTagVisual();
        }

        public void AddFinding(ExamAction action, string text, float minute)
        {
            findings[action] = new Finding { text = text, minute = minute };
        }

        public void Say(string text)
        {
            bark.Say(text);
        }

        /// <summary>Called every frame by the director after the simulation advanced.</summary>
        public void Tick()
        {
            PhysiologyState state = Sim.State;
            Body.BreathsPerMinute = state.Breathing ? state.respiratoryRate : 0f;
            Body.Limp = state.deceased || !state.Breathing || (!state.followsCommands && state.capillaryRefill > 4f);
            Body.Restless = !Body.Limp && state.followsCommands && (state.PoorCirculation || state.respiratoryRate > 30);

            if (!Walker.Moving && !OnStretcher)
            {
                BodyState rest = RestState();
                if (Body.State != rest)
                {
                    if (Body.State == BodyState.Standing && rest == BodyState.Lying && !collapsedAnnounced && !state.deceased)
                    {
                        collapsedAnnounced = true;
                        IncidentDirector.Instance?.Announce(Id + " has collapsed.");
                    }

                    Body.SetState(rest);
                    UpdateColliderAndLabels();
                }
            }

            if (Time.time >= nextBark)
            {
                nextBark = Time.time + 10f + (float)random.NextDouble() * 12f;
                string line = BarkLine();
                if (!string.IsNullOrEmpty(line)) bark.Say(line);
            }
        }

        private string BarkLine()
        {
            PhysiologyState state = Sim.State;
            if (state.deceased || !state.Breathing || !state.followsCommands || Sim.Transported) return null;
            if (state.respiratoryRate > 30) return random.Next(2) == 0 ? "Can't... catch my breath..." : "My chest...";
            if (state.PoorCirculation) return random.Next(2) == 0 ? "I feel cold... and dizzy." : "Everything's going grey...";
            string[] lines = Profile.barks;
            return lines != null && lines.Length > 0 ? lines[random.Next(lines.Length)] : null;
        }

        public void SetRecoveryRoll(float roll)
        {
            if (Body == null) return;
            if (Body.State != BodyState.Lying && Body.State != BodyState.Recovery) Body.SetState(BodyState.Lying);
            Body.RecoveryRoll = roll;
        }

        public void HideFromScene()
        {
            gameObject.SetActive(false);
        }
    }
}
