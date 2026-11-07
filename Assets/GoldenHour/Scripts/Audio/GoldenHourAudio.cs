using UnityEngine;

namespace GoldenHour
{
    /// <summary>Looping roadside ambience plus one-shot UI, hover, footstep, kit, objective and patient sounds.</summary>
    public sealed class GoldenHourAudio : MonoBehaviour
    {
        public AudioClip ambience;
        public AudioClip buttonClick;
        public AudioClip kitOpen;
        public AudioClip objectiveComplete;
        public AudioClip patientInteraction;
        public AudioClip footstep;
        public AudioClip buttonHover;

        private AudioSource musicSource;
        private AudioSource effectsSource;
        private AudioSource footstepSource;

        public bool AmbiencePlaying => musicSource != null && musicSource.isPlaying;
        public int FootstepsPlayed { get; private set; }

        private void Awake()
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            effectsSource = gameObject.AddComponent<AudioSource>();
            effectsSource.playOnAwake = false;
            effectsSource.ignoreListenerPause = true;
            footstepSource = gameObject.AddComponent<AudioSource>();
            footstepSource.playOnAwake = false;
            AudioListener.volume = GameSettings.Volume;

            if (ambience != null)
            {
                musicSource.clip = ambience;
                musicSource.volume = 0.12f;
                musicSource.Play();
            }
        }

        public void PlayButton()
        {
            Play(buttonClick);
        }

        public void PlayKitOpen()
        {
            Play(kitOpen);
        }

        public void PlayObjective()
        {
            Play(objectiveComplete);
        }

        public void PlayHover()
        {
            if (buttonHover != null && effectsSource != null) effectsSource.PlayOneShot(buttonHover, 0.5f);
        }

        /// <summary>Footstep with slight pitch/volume variation so repeated steps do not sound identical.</summary>
        public void PlayFootstep(bool sprinting)
        {
            if (footstep == null || footstepSource == null) return;
            footstepSource.pitch = Random.Range(0.86f, 1.1f) * (sprinting ? 1.08f : 1f);
            footstepSource.PlayOneShot(footstep, Random.Range(0.45f, 0.6f));
            FootstepsPlayed++;
        }

        public void PlayPatient()
        {
            Play(patientInteraction);
        }

        private void Play(AudioClip clip)
        {
            if (clip != null && effectsSource != null)
            {
                effectsSource.PlayOneShot(clip, 0.75f);
            }
        }
    }
}
