using UnityEngine;

namespace RestaurantLoop.Audio
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource musicSource; // Added for background music

        [Header("Music Clips")]
        public AudioClip backgroundMusic; // The looping background music/ambiance

        [Header("SFX Clips")]
        public AudioClip tapSound;         
        public AudioClip boardClickSound;  
        public AudioClip throwSound;       
        public AudioClip rackDropSound;    
        public AudioClip nomNomSound;      
        public AudioClip happyJumpSound;   
        public AudioClip popSound;         

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject); 
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Initialize and play background music automatically
            if (musicSource != null && backgroundMusic != null)
            {
                musicSource.clip = backgroundMusic;
                musicSource.loop = true; // Ensure it loops forever
                musicSource.volume = PlayerPrefs.GetFloat("MusicVolume", 1f);
                musicSource.Play();
            }
        }

        // Method to update music volume in real-time from the UI slider
        public void SetMusicVolume(float volume)
        {
            if (musicSource != null)
            {
                musicSource.volume = volume;
            }
        }

        public void PlaySFX(AudioClip clip)
        {
            if (clip == null || sfxSource == null) return;

            float currentSfxVolume = PlayerPrefs.GetFloat("SfxVolume", 1f);

            if (currentSfxVolume > 0f)
            {
                sfxSource.PlayOneShot(clip, currentSfxVolume);
            }
        }
    }
}