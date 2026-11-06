using UnityEngine;

namespace GoldenHour
{
    /// <summary>Looping roadside ambience plus one-shot UI, kit, objective and patient sounds.</summary>
    public sealed class GoldenHourAudio : MonoBehaviour
    {
        public AudioClip ambience;
        public AudioClip buttonClick;
        public AudioClip kitOpen;
        public AudioClip objectiveComplete;
        public AudioClip patientInteraction;

        private AudioSource musicSource;
        private AudioSource effectsSource;

        public bool AmbiencePlaying => musicSource != null && musicSource.isPlaying;

        private void Awake()
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            effectsSource = gameObject.AddComponent<AudioSource>();
            effectsSource.playOnAwake = false;
            AudioListener.volume = PlayerPrefs.GetFloat(UiFactory.VolumePrefKey, 0.8f);

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
