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
        public AudioClip backgroundMusic;
        [Range(0f, 1f)] public float backgroundMusicVolume = 1f;

        [Header("SFX Clips & Volumes")]
        public AudioClip tapSound;
        [Range(0f, 1f)] public float tapSoundVolume = 1f;
        
        [Space(5)]
        public AudioClip boardClickSound;
        [Range(0f, 1f)] public float boardClickSoundVolume = 1f;
        
        [Space(5)]
        public AudioClip throwSound;
        [Range(0f, 1f)] public float throwSoundVolume = 1f;
        
        [Space(5)]
        public AudioClip rackDropSound;
        [Range(0f, 1f)] public float rackDropSoundVolume = 1f;
        
        [Space(5)]
        public AudioClip nomNomSound;
        [Range(0f, 1f)] public float nomNomSoundVolume = 1f;
        
        [Space(5)]
        public AudioClip happyJumpSound;
        [Range(0f, 1f)] public float happyJumpSoundVolume = 1f;
        
        [Space(5)]
        public AudioClip popSound;
        [Range(0f, 1f)] public float popSoundVolume = 1f;
        
        [Space(5)]
        [Tooltip("Played when the gate opens and the crowd enters")]
        public AudioClip customerEntranceSound;
        [Range(0f, 1f)] public float customerEntranceSoundVolume = 1f;
        
        [Space(5)]
        [Tooltip("Played when the level is successfully completed")]
        public AudioClip levelWinSound;
        [Range(0f, 1f)] public float levelWinSoundVolume = 1f;
        
        [Space(5)]
        [Tooltip("Played when the rack overflows and the level is lost")]
        public AudioClip levelLoseSound;
        [Range(0f, 1f)] public float levelLoseSoundVolume = 1f;

        [Header("Power-Up SFX")]
        public AudioClip powerUp1Sound;
        [Range(0f, 1f)] public float powerUp1SoundVolume = 1f;
        
        [Space(5)]
        public AudioClip powerUp2Sound;
        [Range(0f, 1f)] public float powerUp2SoundVolume = 1f;
        
        [Space(5)]
        public AudioClip powerUp3Sound;
        [Range(0f, 1f)] public float powerUp3SoundVolume = 1f;
        
        [Space(5)]
        public AudioClip powerUp4Sound;
        [Range(0f, 1f)] public float powerUp4SoundVolume = 1f;

        [Header("Audio Limiter Settings")]
        [Tooltip("Maximum number of times the SAME audio clip can play simultaneously.")]
        public int maxSimultaneousSounds = 4;
        
        // Tracks the start times of currently playing clips to limit overlapping
        private Dictionary<AudioClip, List<float>> activeClips = new Dictionary<AudioClip, List<float>>();
        
        // Maps each specific clip to its individual volume modifier set in the inspector
        private Dictionary<AudioClip, float> clipVolumeModifiers = new Dictionary<AudioClip, float>();

        public float SfxVolume => Mathf.Clamp01(PlayerPrefs.GetFloat("SfxVolume", 1f));
        public bool IsSfxMuted => SfxVolume <= Mathf.Epsilon;

        private void Awake()
        {
            // Singleton pattern to ensure only one AudioManager exists
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject); 
                InitializeVolumeDictionary();
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
                musicSource.loop = true; 
                // Multiply global music volume by the specific background music volume
                musicSource.volume = PlayerPrefs.GetFloat("MusicVolume", 1f) * backgroundMusicVolume;
                musicSource.Play();
            }
        }

        // Caches the inspector volume values for quick lookup during gameplay
        private void InitializeVolumeDictionary()
        {
            clipVolumeModifiers.Clear();
            if (tapSound != null) clipVolumeModifiers[tapSound] = tapSoundVolume;
            if (boardClickSound != null) clipVolumeModifiers[boardClickSound] = boardClickSoundVolume;
            if (throwSound != null) clipVolumeModifiers[throwSound] = throwSoundVolume;
            if (rackDropSound != null) clipVolumeModifiers[rackDropSound] = rackDropSoundVolume;
            if (nomNomSound != null) clipVolumeModifiers[nomNomSound] = nomNomSoundVolume;
            if (happyJumpSound != null) clipVolumeModifiers[happyJumpSound] = happyJumpSoundVolume;
            if (popSound != null) clipVolumeModifiers[popSound] = popSoundVolume;
            if (customerEntranceSound != null) clipVolumeModifiers[customerEntranceSound] = customerEntranceSoundVolume;
            if (levelWinSound != null) clipVolumeModifiers[levelWinSound] = levelWinSoundVolume;
            if (levelLoseSound != null) clipVolumeModifiers[levelLoseSound] = levelLoseSoundVolume;
            if (powerUp1Sound != null) clipVolumeModifiers[powerUp1Sound] = powerUp1SoundVolume;
            if (powerUp2Sound != null) clipVolumeModifiers[powerUp2Sound] = powerUp2SoundVolume;
            if (powerUp3Sound != null) clipVolumeModifiers[powerUp3Sound] = powerUp3SoundVolume;
            if (powerUp4Sound != null) clipVolumeModifiers[powerUp4Sound] = powerUp4SoundVolume;
        }

        // Updates music volume in real-time when toggled or adjusted via UI
        public void SetMusicVolume(float volume)
        {
            if (musicSource != null)
            {
                musicSource.volume = volume * backgroundMusicVolume;
            }
        }

        // Plays a single sound effect respecting the current SFX volume settings, individual clip limits, and overlapping limits
        public void PlaySFX(AudioClip clip)
        {
            if (clip == null || sfxSource == null) return;

            float globalSfxVolume = SfxVolume;

            if (globalSfxVolume > 0f)
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

                // Determine specific volume modifier for this clip (defaults to 1f if not found)
                float individualVolumeMultiplier = 1f;
                if (clipVolumeModifiers.TryGetValue(clip, out float specificVolume))
                {
                    individualVolumeMultiplier = specificVolume;
                }

                float finalVolume = globalSfxVolume * individualVolumeMultiplier;

                // Add slight pitch variation to prevent phasing/robotic sound when multiple same clips play
                sfxSource.pitch = Random.Range(0.92f, 1.08f);

                sfxSource.PlayOneShot(clip, finalVolume);
            }
        }
    }
}
