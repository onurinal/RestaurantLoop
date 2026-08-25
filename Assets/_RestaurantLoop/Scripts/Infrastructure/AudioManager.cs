using UnityEngine;

namespace RestaurantLoop.Audio
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource sfxSource;

        [Header("SFX Clips")]
        public AudioClip tapSound;         // Screen tap sound
        public AudioClip boardClickSound;  // "şık" - Plate boarding the conveyor
        public AudioClip throwSound;       // "fyuu" - Plate thrown in an arc
        public AudioClip rackDropSound;    // "tık" - Plate dropping to the rack
        public AudioClip nomNomSound;      // Eating (nom-nom) animation sound
        public AudioClip happyJumpSound;   // Customer happy jump sound after eating
        public AudioClip popSound;         // Customer catching the thrown plate

        private void Awake()
        {
            // Singleton pattern implementation
            if (Instance == null)
            {
                Instance = this;
                // Ensure the audio manager persists across scene transitions
                DontDestroyOnLoad(gameObject); 
            }
            else
            {
                // Destroy duplicate instances to prevent audio overlaps
                Destroy(gameObject);
            }
        }

        // Main method to be called from any script to play a sound effect
        public void PlaySFX(AudioClip clip)
        {
            if (clip == null || sfxSource == null) return;

            // Retrieve the volume set by the UI Slider (between 0.0f and 1.0f). Defaults to 1.0f.
            float currentSfxVolume = PlayerPrefs.GetFloat("SfxVolume", 1f);

            // Play the sound scaled by the current volume level
            if (currentSfxVolume > 0f)
            {
                sfxSource.PlayOneShot(clip, currentSfxVolume);
            }
        }
    }
}