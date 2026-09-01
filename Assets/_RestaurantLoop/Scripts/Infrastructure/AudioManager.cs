using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Audio
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource musicSource;

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

        [Header("Timed Customer SFX")]
        [Tooltip("Looping tick sound played when a timed customer enters the yellow/red warning zone")]
        public AudioClip timedCustomerWarningSound;
        [Range(0f, 1f)] public float timedCustomerWarningSoundVolume = 1f;

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

        [Header("Food Specific Spawn Sounds")]
        public AudioClip beefSound;
        public AudioClip burgerSound;
        public AudioClip cakeSound;
        public AudioClip drinkSound;
        public AudioClip friesSound;
        public AudioClip sushiSound;

        [Header("Audio Limiter Settings")]
        [Tooltip("Maximum number of times the SAME audio clip can play simultaneously.")]
        public int maxSimultaneousSounds = 4;

        private Dictionary<AudioClip, List<float>> activeClips = new Dictionary<AudioClip, List<float>>();
        private Dictionary<AudioClip, float> clipVolumeModifiers = new Dictionary<AudioClip, float>();

        private float cachedSfxVolume = -1f;

        private AudioSource timedWarningSource;
        private int activeWarningCount = 0;
        private int activeCriticalCount = 0;

        public float SfxVolume
        {
            get
            {
                if (cachedSfxVolume < 0f)
                {
                    cachedSfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("SfxVolume", 1f));
                }

                return cachedSfxVolume;
            }
        }

        public bool IsSfxMuted => SfxVolume <= Mathf.Epsilon;

        public void InvalidateSfxVolumeCache()
        {
            cachedSfxVolume = -1f;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeVolumeDictionary();

                timedWarningSource = gameObject.AddComponent<AudioSource>();
                timedWarningSource.loop = true;
                timedWarningSource.playOnAwake = false;

                activeWarningCount = 0;
                activeCriticalCount = 0;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (musicSource != null && backgroundMusic != null)
            {
                musicSource.clip = backgroundMusic;
                musicSource.loop = true;
                musicSource.volume = PlayerPrefs.GetFloat("MusicVolume", 1f) * backgroundMusicVolume;
                musicSource.Play();
            }
        }

        private void Update()
        {
            if (timedWarningSource != null && timedWarningSource.isPlaying)
            {
                float baseVolume = SfxVolume * timedCustomerWarningSoundVolume;

                if (activeCriticalCount > 0)
                {
                    timedWarningSource.volume = baseVolume; 
                    timedWarningSource.pitch = 1.25f; 
                }
                else
                {
                    timedWarningSource.volume = baseVolume * 0.85f; 
                    timedWarningSource.pitch = 1.0f; 
                }
            }
        }

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

        public void SetMusicVolume(float volume)
        {
            if (musicSource != null)
            {
                musicSource.volume = volume * backgroundMusicVolume;
            }
        }

        public void StartTimedWarning()
        {
            activeWarningCount++;
            EvaluateWarningAudio();
        }

        public void StopTimedWarning()
        {
            activeWarningCount = Mathf.Max(0, activeWarningCount - 1);
            EvaluateWarningAudio();
        }

        public void StartCriticalWarning()
        {
            activeCriticalCount++;
            EvaluateWarningAudio();
        }

        public void StopCriticalWarning()
        {
            activeCriticalCount = Mathf.Max(0, activeCriticalCount - 1);
            EvaluateWarningAudio();
        }

        private void EvaluateWarningAudio()
        {
            if (timedWarningSource == null || timedCustomerWarningSound == null) return;

            if (activeWarningCount > 0 || activeCriticalCount > 0)
            {
                if (!timedWarningSource.isPlaying)
                {
                    timedWarningSource.clip = timedCustomerWarningSound;
                    timedWarningSource.Play();
                }
            }
            else
            {
                if (timedWarningSource.isPlaying)
                {
                    timedWarningSource.Stop();
                }
            }
        }

        public void ResetClipLimits(AudioClip clip = null)
        {
            if (clip != null)
            {
                if (activeClips.ContainsKey(clip)) activeClips[clip].Clear();
            }
            else
            {
                activeClips.Clear();
            }
        }

        public void PlaySFX(AudioClip clip, bool ignoreLimit = false)
        {
            if (clip == null || sfxSource == null) return;

            float globalSfxVolume = SfxVolume;

            if (globalSfxVolume > 0f)
            {
                float currentTime = Time.unscaledTime;

                if (!activeClips.ContainsKey(clip))
                {
                    activeClips[clip] = new List<float>();
                }

                List<float> startTimes = activeClips[clip];
                float clipLength = clip.length;
                for (int i = startTimes.Count - 1; i >= 0; i--)
                {
                    if (currentTime - startTimes[i] >= clipLength)
                    {
                        startTimes.RemoveAt(i);
                    }
                }

                if (!ignoreLimit && startTimes.Count >= maxSimultaneousSounds)
                {
                    return;
                }

                startTimes.Add(currentTime);

                float individualVolumeMultiplier = 1f;
                if (clipVolumeModifiers.TryGetValue(clip, out float specificVolume))
                {
                    individualVolumeMultiplier = specificVolume;
                }

                float finalVolume = globalSfxVolume * individualVolumeMultiplier;

                sfxSource.pitch = Random.Range(0.92f, 1.08f);
                sfxSource.PlayOneShot(clip, finalVolume);
            }
        }

        public void SetSfxVolume(float volume)
        {
            cachedSfxVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat("SfxVolume", cachedSfxVolume);

            if (timedWarningSource != null && timedWarningSource.isPlaying)
            {
                timedWarningSource.volume = cachedSfxVolume * timedCustomerWarningSoundVolume;
            }
        }

        public void PlayFoodSpawnSound(string foodName)
        {
            if (string.IsNullOrEmpty(foodName)) return;
            
            AudioClip clipToPlay = null;
            string lowerName = foodName.ToLower();

            if (lowerName.Contains("beef")) clipToPlay = beefSound;
            else if (lowerName.Contains("burger")) clipToPlay = burgerSound;
            else if (lowerName.Contains("cake")) clipToPlay = cakeSound;
            else if (lowerName.Contains("drink")) clipToPlay = drinkSound;
            else if (lowerName.Contains("fries")) clipToPlay = friesSound;
            else if (lowerName.Contains("sushi")) clipToPlay = sushiSound;

            if (clipToPlay != null) 
            {
                PlaySFX(clipToPlay);
            }
            else if (boardClickSound != null) 
            {
                PlaySFX(boardClickSound);
            }
        }

        // ADDED: Pause looping game sounds (like the timed warning) when the game is paused
        public void PauseGameSounds()
        {
            if (timedWarningSource != null && timedWarningSource.isPlaying)
            {
                timedWarningSource.Pause();
            }
        }

        // ADDED: Resume paused looping game sounds
        public void ResumeGameSounds()
        {
            if (timedWarningSource != null)
            {
                timedWarningSource.UnPause();
            }
        }
    }
}