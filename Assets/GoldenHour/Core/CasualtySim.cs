using System;
using System.Collections.Generic;

namespace GoldenHour
{
    public enum TreatmentResult
    {
        Effective,
        NotIndicated,
        AlreadyApplied,
        NotPossible
    }

    public enum SimEventKind
    {
        Worsened,
        Died,
        Stabilised,
        Treated,
        Transported
    }

    [Serializable]
    public struct SimEvent
    {
        public float minute;
        public SimEventKind kind;
        public string text;
    }

    /// <summary>
    /// Runtime physiology of one casualty (pure C#, deterministic for a given seed).
    /// Untreated conditions deteriorate over simulated minutes; indicated treatments stabilise.
    /// </summary>
    public sealed class CasualtySim
    {
        private readonly Random random;
        private readonly float deteriorationScale;
        private readonly float deathJitter;
        private readonly List<SimEvent> events = new List<SimEvent>();
        private bool worsened;

        public CasualtySim(CasualtyProfile profile, int seed, float deteriorationScale = 1f)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            random = new Random(seed);
            this.deteriorationScale = Math.Max(0.25f, deteriorationScale);
            deathJitter = 0.95f + (float)random.NextDouble() * 0.1f;
            State = profile.initial.Clone();
            InitiallyExpectant = StartTriage.Evaluate(State) == TriageTag.Black;
        }

        public CasualtyProfile Profile { get; }
        public PhysiologyState State { get; }
        public float Minute { get; private set; }
        public Treatment Applied { get; private set; }
        public bool Transported { get; private set; }
        public bool InitiallyExpectant { get; }
        public bool Worsened => worsened;
        public IReadOnlyList<SimEvent> Events => events;

        public bool Stabilised => Profile.stabilisedBy != Treatment.None && (Applied & Profile.stabilisedBy) != 0;

        /// <summary>A death that triage, treatment or transport could have prevented in this model.</summary>
        public bool PreventableDeath => State.deceased && !InitiallyExpectant;

        public TriageTag TrueTag => StartTriage.Evaluate(State);

        public TriageTag Evaluate(out StartStep step) => StartTriage.Evaluate(State, out step);

        /// <summary>Simulated minute of death if nothing changes (0 = no scheduled death).</summary>
        public float EffectiveDeathMinute
        {
            get
            {
                if (Profile.deathMinute <= 0f) return 0f;
                float minute = Profile.deathMinute * deathJitter / deteriorationScale;
                if (Profile.condition == CriticalCondition.Airway && State.airwayOpened)
                {
                    minute += Profile.airwayOpeningDelayMinutes;
                }

                if ((Applied & Treatment.Blanket) != 0 && Profile.condition == CriticalCondition.Shock) minute += 4f;
                return minute;
            }
        }

        public float EffectiveWorsenMinute => Profile.worsenMinute <= 0f ? 0f : Profile.worsenMinute / deteriorationScale;

        public void Advance(float minutes)
        {
            if (minutes <= 0f) return;
            Minute += minutes;
            if (State.deceased || Transported || Stabilised) return;

            if (State.bleeding == BleedingLevel.Severe && !State.bleedingControlled &&
                Profile.capillaryRefillRisePerMinute > 0f)
            {
                State.capillaryRefill = Math.Min(6f, State.capillaryRefill +
                                                     Profile.capillaryRefillRisePerMinute * minutes * deteriorationScale);
                if (State.capillaryRefill > 3.5f) State.radialPulse = false;
                if (State.capillaryRefill > 4.5f) State.followsCommands = false;
            }

            if (!worsened && EffectiveWorsenMinute > 0f && Minute >= EffectiveWorsenMinute)
            {
                worsened = true;
                State.canWalk = false;
                if (Profile.worsenedRespiratoryRate > 0) State.respiratoryRate = Profile.worsenedRespiratoryRate;
                if (Profile.worsenedCapillaryRefill > 0f)
                {
                    State.capillaryRefill = Profile.worsenedCapillaryRefill;
                    if (State.capillaryRefill > 3.5f) State.radialPulse = false;
                }

                if (Profile.worsenedLosesCommands) State.followsCommands = false;
                Log(SimEventKind.Worsened, "Condition worsened: now " + StartTriage.Label(TrueTag) + ".");
            }

            float death = EffectiveDeathMinute;
            if (death > 0f && Minute >= death) Die();
        }

        public TreatmentResult Treat(Treatment treatment)
        {
            if (State.deceased || Transported) return TreatmentResult.NotPossible;
            if ((Applied & treatment) != 0) return TreatmentResult.AlreadyApplied;
            bool indicated = (Profile.indicatedTreatments & treatment) != 0;
            bool wasStable = Stabilised;

            switch (treatment)
            {
                case Treatment.PressureDressing:
                    if (State.bleeding != BleedingLevel.Severe || State.bleedingControlled) return TreatmentResult.NotIndicated;
                    State.bleedingControlled = true;
                    State.bleeding = BleedingLevel.Minor;
                    break;
                case Treatment.AirwayOpening:
                    if (!State.airwayObstructed || State.airwayOpened) return TreatmentResult.NotIndicated;
                    State.airwayOpened = true;
                    break;
                case Treatment.RecoveryPosition:
                    if (!indicated || State.canWalk) return TreatmentResult.NotIndicated;
                    if (State.airwayObstructed) State.airwayOpened = true;
                    break;
                case Treatment.Cpr:
                    // In mass-casualty triage CPR is deprioritised; it never changes the outcome in this model.
                    return TreatmentResult.NotIndicated;
                default:
                    if (!indicated) return TreatmentResult.NotIndicated;
                    break;
            }

            Applied |= treatment;
            Log(SimEventKind.Treated, treatment + " applied.");
            if (!wasStable && Stabilised) Log(SimEventKind.Stabilised, "Stabilised - deterioration stopped.");
            return TreatmentResult.Effective;
        }

        public void Transport()
        {
            if (State.deceased || Transported) return;
            Transported = true;
            Log(SimEventKind.Transported, "Handed over to paramedics and transported.");
        }

        /// <summary>Breathing rate as a rescuer would count it (never crosses the 30/min threshold).</summary>
        public int MeasureRespiratoryRate()
        {
            if (!State.Breathing) return 0;
            int rate = State.respiratoryRate;
            int measured = rate + random.Next(-1, 2);
            return rate > StartTriage.RespiratoryRateLimit
                ? Math.Max(StartTriage.RespiratoryRateLimit + 1, measured)
                : Math.Min(StartTriage.RespiratoryRateLimit, Math.Max(1, measured));
        }

        private void Die()
        {
            State.deceased = true;
            State.respiratoryRate = 0;
            State.radialPulse = false;
            State.followsCommands = false;
            State.canWalk = false;
            Log(SimEventKind.Died, "Died at minute " + Minute.ToString("0.0") + " (" + Profile.condition + " untreated).");
        }

        private void Log(SimEventKind kind, string text)
        {
            events.Add(new SimEvent { minute = Minute, kind = kind, text = text });
        }
    }
}
