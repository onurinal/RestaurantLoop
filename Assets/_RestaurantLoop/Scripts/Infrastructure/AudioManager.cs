using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Audio
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource musicSource; // Handles background music

        [Header("Music Clips")]
        public AudioClip backgroundMusic; // Looping background music or ambiance

        [Header("SFX Clips")]
        public AudioClip tapSound;         
        public AudioClip boardClickSound;  
        public AudioClip throwSound;       
        public AudioClip rackDropSound;    
        public AudioClip nomNomSound;      
        public AudioClip happyJumpSound;   
        public AudioClip popSound;         
        public AudioClip customerEntranceSound; // Played when the gate opens and the crowd enters
        
        [Tooltip("Played when the level is successfully completed")]
        public AudioClip levelWinSound; 
        
        [Tooltip("Played when the rack overflows and the level is lost")]
        public AudioClip levelLoseSound;

        [Header("Power-Up SFX")]
        public AudioClip powerUp1Sound;
        public AudioClip powerUp2Sound;
        public AudioClip powerUp3Sound;
        public AudioClip powerUp4Sound;

        [Header("Audio Limiter Settings")]
        [Tooltip("Maximum number of times the SAME audio clip can play simultaneously.")]
        public int maxSimultaneousSounds = 4;
        
        // Tracks the start times of currently playing clips to limit overlapping
        private Dictionary<AudioClip, List<float>> activeClips = new Dictionary<AudioClip, List<float>>();

        private void Awake()
        {
            // Singleton pattern to ensure only one AudioManager exists
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
            // Initialize and play background music automatically based on saved volume
            if (musicSource != null && backgroundMusic != null)
            {
                musicSource.clip = backgroundMusic;
                musicSource.loop = true; // Ensure the music loops continuously
                musicSource.volume = PlayerPrefs.GetFloat("MusicVolume", 1f);
                musicSource.Play();
            }
        }

        // Updates music volume in real-time when toggled or adjusted via UI
        public void SetMusicVolume(float volume)
        {
            if (musicSource != null)
            {
                musicSource.volume = volume;
            }
        }

        // Plays a single sound effect respecting the current SFX volume settings and overlapping limits
        public void PlaySFX(AudioClip clip)
        {
            if (clip == null || sfxSource == null) return;

            float currentSfxVolume = PlayerPrefs.GetFloat("SfxVolume", 1f);

            if (currentSfxVolume > 0f)
            {
                float currentTime = Time.time;

                // Ensure the dictionary has a list for this specific clip
                if (!activeClips.ContainsKey(clip))
                {
                    activeClips[clip] = new List<float>();
                }

                // Clean up finished clips from the list (if current time - start time >= clip length)
                activeClips[clip].RemoveAll(startTime => currentTime - startTime >= clip.length);

                // If the number of currently playing instances of this clip is at the limit, ignore the new request
                if (activeClips[clip].Count >= maxSimultaneousSounds)
                {
                    return; 
                }
                
                // Add the new play request to the active list
                activeClips[clip].Add(currentTime);

                // Add slight pitch variation to prevent phasing/robotic sound when multiple same clips play
                sfxSource.pitch = Random.Range(0.92f, 1.08f);

                sfxSource.PlayOneShot(clip, currentSfxVolume);
            }
        }
    }
}