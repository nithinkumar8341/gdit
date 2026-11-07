using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GoldenHour
{
    /// <summary>
    /// Player stress from the situation: unstable RED casualties still on scene, the golden-hour clock
    /// running down and standing in a danger zone. Drives the heartbeat/music tension, a pulsing
    /// vignette and a slight hand tremor. The "Stress effects" setting turns the effects off;
    /// reduce-motion keeps the vignette steady.
    /// </summary>
    public sealed class StressSystem : MonoBehaviour
    {
        public static StressSystem Instance { get; private set; }

        public float Level { get; private set; }
        public float Target { get; private set; }
        public float VignetteIntensity => vignette != null ? vignette.intensity.value : 0f;

        private IncidentDirector director;
        private GoldenHourAudio audioManager;
        private Vignette vignette;
        private float baseVignette;
        private float pulse;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            director = GetComponent<IncidentDirector>();
            audioManager = GetComponent<GoldenHourAudio>();
            Volume volume = FindAnyObjectByType<Volume>();
            if (volume != null && volume.profile != null && volume.profile.TryGet(out vignette))
            {
                baseVignette = vignette.intensity.value;
                vignette.intensity.overrideState = true;
            }
        }

        /// <summary>Situation stress in [0,1] before smoothing (also used by tests).</summary>
        public static float Evaluate(IncidentDirector d)
        {
            if (d == null || d.Scenario == null || (d.Phase != IncidentPhase.Safety && d.Phase != IncidentPhase.Response)) return 0f;
            float unstable = 0f;
            foreach (Casualty casualty in d.Casualties)
            {
                if (casualty.Sim.State.deceased || casualty.Sim.Transported || casualty.AssignedToAmbulance) continue;
                if (casualty.Sim.TrueTag == TriageTag.Red && !casualty.Sim.Stabilised) unstable += 0.14f;
            }

            float time = Mathf.Clamp01(d.Minute / Mathf.Max(1f, d.Scenario.goldenHourMinutes));
            float danger = d.InDanger ? 0.35f : 0f;
            return Mathf.Clamp01(Mathf.Min(0.5f, unstable) + time * 0.35f + danger);
        }

        private void Update()
        {
            if (director == null) return;
            Target = Evaluate(director);
            Level = Mathf.MoveTowards(Level, Target, Time.unscaledDeltaTime * 0.25f);
            bool effects = GameSettings.StressEffects;
            float applied = effects ? Level : 0f;
            if (audioManager != null)
            {
                audioManager.SetStress(applied);
                bool active = director.Phase == IncidentPhase.Safety || director.Phase == IncidentPhase.Response;
                audioManager.SetTension(active ? Mathf.Clamp01(0.25f + Level * 0.75f) : 0.1f);
            }

            if (Viewmodel.Instance != null)
            {
                Viewmodel.Instance.Stress = applied;
                Viewmodel.Instance.Reaching = director.ActionRunning;
            }

            if (vignette == null) return;
            float beatsPerSecond = Mathf.Lerp(1.1f, 2.1f, Level);
            pulse += Time.unscaledDeltaTime * beatsPerSecond * Mathf.PI * 2f;
            float beat = GameSettings.ReduceMotion ? 0.5f : 0.5f + 0.5f * Mathf.Sin(pulse);
            vignette.intensity.value = baseVignette + applied * (0.12f + 0.08f * beat);
        }
    }
}
