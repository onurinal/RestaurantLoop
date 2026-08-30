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

        private Dictionary<AudioClip, List<float>> activeClips = new Dictionary<AudioClip, List<float>>();
        private Dictionary<AudioClip, float> clipVolumeModifiers = new Dictionary<AudioClip, float>();

        // PlayerPrefs.GetFloat is a native call with string-key marshalling. It was previously
        // hit on every PlaySFX (hundreds per level) and once per frame by CrowdAudioGenerator.
        // The value only changes through the settings sliders, which invalidate this cache.
        private float cachedSfxVolume = -1f;

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

        /// <summary>Call after writing the SfxVolume preference so the cache re-reads it.</summary>
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

        /// <summary>
        /// Resets active clip timers for a specific sound effect or all sound effects.
        /// Useful when clear-color or power-up sequences trigger sound bursts in rapid succession.
        /// </summary>
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
                // Use unscaledTime so game pause/timeScale does not freeze audio clip cleanup
                float currentTime = Time.unscaledTime;

                if (!activeClips.ContainsKey(clip))
                {
                    activeClips[clip] = new List<float>();
                }

                // A RemoveAll lambda capturing currentTime and clip allocated a closure plus a
                // delegate on every sound. Same filtering, done in place.
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
    }
}