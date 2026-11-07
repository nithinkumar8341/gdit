using System.Collections.Generic;
using UnityEngine;

namespace GoldenHour
{
    /// <summary>
    /// All game audio, procedurally generated clips: ambience beds (road, rain, wind), UI clicks/hover,
    /// footsteps, kit, objectives, radio static, 3D sirens and casualty voices, stress heartbeat/breathing,
    /// CPR metronome and a three-layer music track whose layers follow scene tension.
    /// Music and SFX volumes are separate settings on top of the master volume.
    /// </summary>
    public sealed class GoldenHourAudio : MonoBehaviour
    {
        public AudioClip ambience;
        public AudioClip buttonClick;
        public AudioClip kitOpen;
        public AudioClip objectiveComplete;
        public AudioClip patientInteraction;
        public AudioClip footstep;
        public AudioClip buttonHover;

        [Header("v2")]
        public AudioClip siren;
        public AudioClip radioStatic;
        public AudioClip voiceMurmur;
        public AudioClip heartbeat;
        public AudioClip breathing;
        public AudioClip rainLoop;
        public AudioClip windLoop;
        public AudioClip musicPad;
        public AudioClip musicPulse;
        public AudioClip musicDrums;
        public AudioClip metronome;
        public AudioClip compression;

        private readonly List<AudioSource> musicLayers = new List<AudioSource>();
        private readonly List<AudioSource> voices = new List<AudioSource>();
        private AudioSource ambienceSource;
        private AudioSource effectsSource;
        private AudioSource footstepSource;
        private AudioSource weatherSource;
        private AudioSource windSource;
        private AudioSource heartbeatSource;
        private AudioSource breathingSource;
        private float tension;
        private float stress;
        private int nextVoice;

        public bool AmbiencePlaying => ambienceSource != null && ambienceSource.isPlaying;
        public int FootstepsPlayed { get; private set; }
        public int SirensPlaying { get; private set; }
        public int RadioPlays { get; private set; }
        public float Tension => tension;
        public bool MusicPlaying => musicLayers.Count > 0 && musicLayers[0].isPlaying;
        public float MusicLayerVolume(int layer) => layer < musicLayers.Count ? musicLayers[layer].volume : 0f;
        public float HeartbeatVolume => heartbeatSource != null ? heartbeatSource.volume : 0f;

        private void Awake()
        {
            ambienceSource = Source(true, 0f);
            effectsSource = Source(false, 0f);
            effectsSource.ignoreListenerPause = true;
            footstepSource = Source(false, 0f);
            weatherSource = Source(true, 0f);
            windSource = Source(true, 0f);
            heartbeatSource = Source(true, 0f);
            breathingSource = Source(true, 0f);
            AudioListener.volume = GameSettings.Volume;

            if (ambience != null)
            {
                ambienceSource.clip = ambience;
                ambienceSource.volume = 0.12f * GameSettings.SfxVolume;
                ambienceSource.Play();
            }

            foreach (AudioClip clip in new[] { musicPad, musicPulse, musicDrums })
            {
                if (clip == null) continue;
                AudioSource layer = Source(true, 0f);
                layer.clip = clip;
                layer.volume = 0f;
                musicLayers.Add(layer);
            }

            for (int i = 0; i < 3; i++)
            {
                AudioSource voice = new GameObject("Voice Source").AddComponent<AudioSource>();
                voice.transform.SetParent(transform, false);
                voice.spatialBlend = 1f;
                voice.minDistance = 1.5f;
                voice.maxDistance = 18f;
                voice.rolloffMode = AudioRolloffMode.Linear;
                voice.playOnAwake = false;
                voices.Add(voice);
            }
        }

        private AudioSource Source(bool loop, float spatial)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = spatial;
            return source;
        }

        /// <summary>Starts the layered music (pad always; pulse and drums follow tension).</summary>
        public void StartMusic()
        {
            double start = AudioSettings.dspTime + 0.1;
            foreach (AudioSource layer in musicLayers)
            {
                if (!layer.isPlaying) layer.PlayScheduled(start);
            }
        }

        public void StartWeather(bool rain, bool wind)
        {
            if (rain && rainLoop != null)
            {
                weatherSource.clip = rainLoop;
                weatherSource.volume = 0.35f * GameSettings.SfxVolume;
                weatherSource.Play();
            }

            if (wind && windLoop != null)
            {
                windSource.clip = windLoop;
                windSource.volume = 0.18f * GameSettings.SfxVolume;
                windSource.Play();
            }
        }

        /// <summary>0 = calm, 1 = maximum pressure. Drives music layers.</summary>
        public void SetTension(float value)
        {
            tension = Mathf.Clamp01(value);
        }

        /// <summary>0..1 stress drives heartbeat and breathing (disabled by the accessibility toggle).</summary>
        public void SetStress(float value)
        {
            stress = GameSettings.StressEffects ? Mathf.Clamp01(value) : 0f;
        }

        private void Update()
        {
            float music = GameSettings.MusicVolume;
            float[] targets = { 0.35f, Mathf.InverseLerp(0.25f, 0.6f, tension) * 0.3f, Mathf.InverseLerp(0.6f, 0.95f, tension) * 0.28f };
            for (int i = 0; i < musicLayers.Count; i++)
            {
                musicLayers[i].volume = Mathf.MoveTowards(musicLayers[i].volume, targets[Mathf.Min(i, 2)] * music, Time.unscaledDeltaTime * 0.25f);
            }

            float sfx = GameSettings.SfxVolume;
            if (ambienceSource.clip != null) ambienceSource.volume = 0.12f * sfx;
            UpdateLoop(heartbeatSource, heartbeat, Mathf.InverseLerp(0.4f, 1f, stress) * 0.55f * sfx, 0.85f + stress * 0.5f);
            UpdateLoop(breathingSource, breathing, Mathf.InverseLerp(0.6f, 1f, stress) * 0.3f * sfx, 0.9f + stress * 0.4f);
        }

        private static void UpdateLoop(AudioSource source, AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return;
            if (volume <= 0.001f)
            {
                if (source.isPlaying) source.Stop();
                source.volume = 0f;
                return;
            }

            if (source.clip != clip) source.clip = clip;
            if (!source.isPlaying) source.Play();
            source.volume = Mathf.MoveTowards(source.volume, volume, Time.deltaTime);
            source.pitch = pitch;
        }

        public void PlayButton() => Play(buttonClick, 0.75f);
        public void PlayKitOpen() => Play(kitOpen, 0.75f);
        public void PlayObjective() => Play(objectiveComplete, 0.75f);
        public void PlayPatient() => Play(patientInteraction, 0.75f);
        public void PlayMetronome() => Play(metronome, 0.8f);
        public void PlayCompression() => Play(compression, 0.9f);

        public void PlayHover()
        {
            if (buttonHover != null && effectsSource != null) effectsSource.PlayOneShot(buttonHover, 0.5f * GameSettings.SfxVolume);
        }

        public void PlayRadio()
        {
            RadioPlays++;
            Play(radioStatic, 0.6f);
        }

        /// <summary>Footstep with slight pitch/volume variation so repeated steps do not sound identical.</summary>
        public void PlayFootstep(bool sprinting)
        {
            if (footstep == null || footstepSource == null) return;
            footstepSource.pitch = Random.Range(0.86f, 1.1f) * (sprinting ? 1.08f : 1f);
            footstepSource.PlayOneShot(footstep, Random.Range(0.45f, 0.6f) * GameSettings.SfxVolume);
            FootstepsPlayed++;
        }

        /// <summary>3D looping siren with Doppler, attached to an approaching vehicle.</summary>
        public void PlaySiren(Transform vehicle)
        {
            if (siren == null || vehicle == null) return;
            AudioSource source = vehicle.gameObject.AddComponent<AudioSource>();
            source.clip = siren;
            source.loop = true;
            source.spatialBlend = 1f;
            source.dopplerLevel = 1.2f;
            source.minDistance = 6f;
            source.maxDistance = 120f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.volume = 0.8f * GameSettings.SfxVolume;
            source.Play();
            SirensPlaying++;
        }

        /// <summary>Short spatial murmur at a casualty's position (speech barks).</summary>
        public void PlayVoice(Vector3 position)
        {
            if (voiceMurmur == null || voices.Count == 0) return;
            AudioSource voice = voices[nextVoice++ % voices.Count];
            voice.transform.position = position;
            voice.pitch = Random.Range(0.85f, 1.15f);
            voice.PlayOneShot(voiceMurmur, 0.8f * GameSettings.SfxVolume);
        }

        private void Play(AudioClip clip, float volume)
        {
            if (clip != null && effectsSource != null) effectsSource.PlayOneShot(clip, volume * GameSettings.SfxVolume);
        }
    }
}
