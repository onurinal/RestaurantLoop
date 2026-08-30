using UnityEngine;
using System.Collections.Generic;
using RestaurantLoop.Core;

namespace RestaurantLoop.Audio
{
    public class CrowdAudioGenerator : MonoBehaviour
    {
        [Header("Crowd Settings")]
        [Tooltip("The single voice audio clip to be used")]
        public AudioClip singleVoiceClip;
        
        [Tooltip("Max number of voices to generate for a full crowd")]
        [Range(2, 10)] public int maxVoices = 5;
        
        [Tooltip("The pitch range of the voices (1 is normal pitch)")]
        public Vector2 pitchRange = new Vector2(0.8f, 1.25f);
        
        [Tooltip("The start delay range in seconds")]
        public Vector2 delayRange = new Vector2(0f, 1.5f);
        
        [Tooltip("Maximum volume level per voice when crowd is full")]
        [Range(0f, 1f)] public float maxVolumePerVoice = 0.4f;

        private List<AudioSource> activeSources = new List<AudioSource>();
        private int initialCrowdSize = 0;
        private float currentCrowdRatio = 1f;

        private void Start()
        {
            if (singleVoiceClip == null)
            {
                Debug.LogWarning("CrowdAudioGenerator: Single voice clip is not assigned!");
                return;
            }

            GenerateCrowd();

            // Subscribe to LevelManager to get the initial crowd size when a level loads
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.OnLevelLoaded += HandleLevelLoaded;
            }
            
            // Subscribe to CrowdManager to listen for customer count changes
            if (CrowdManager.Instance != null)
            {
                CrowdManager.Instance.OnDemandChanged += HandleDemandChanged;
                
                // Initialize for the first time if the level is already loaded
                initialCrowdSize = CrowdManager.Instance.TotalRemainingDemand;
                UpdateCrowdRatio(initialCrowdSize);
            }
        }

        private void GenerateCrowd()
        {
            for (int i = 0; i < maxVoices; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.clip = singleVoiceClip;
                source.pitch = Random.Range(pitchRange.x, pitchRange.y);
                source.panStereo = Random.Range(-0.6f, 0.6f);
                source.volume = 0f; // Start at 0, Update() will set the correct volume
                source.loop = true;
                
                float randomDelay = Random.Range(delayRange.x, delayRange.y);
                source.PlayDelayed(randomDelay);
                
                activeSources.Add(source);
            }
        }

        private void HandleLevelLoaded(int levelIndex)
        {
            if (CrowdManager.Instance != null)
            {
                // Reset the initial crowd size for the new level
                initialCrowdSize = CrowdManager.Instance.TotalRemainingDemand;
                UpdateCrowdRatio(initialCrowdSize);
            }
        }

        private void HandleDemandChanged(int remainingDemand, Dictionary<ItemDataSO, int> demandPerType)
        {
            // Update the ratio whenever a customer is served
            UpdateCrowdRatio(remainingDemand);
        }

        private void UpdateCrowdRatio(int currentDemand)
        {
            if (initialCrowdSize <= 0) return;

            // Calculate how much of the crowd is left (e.g. 50/100 = 0.5)
            currentCrowdRatio = Mathf.Clamp01((float)currentDemand / initialCrowdSize);
        }

        private void Update()
        {
            // Get the global SFX volume from the UI Options Toggle (1 is ON, 0 is OFF).
            // Read from AudioManager's cache rather than hitting PlayerPrefs every frame;
            // the preference is only ever written by the settings sliders, which are 0..1,
            // so the cache's Clamp01 cannot change the value.
            float globalSfxVolume = AudioManager.Instance != null
                ? AudioManager.Instance.SfxVolume
                : PlayerPrefs.GetFloat("SfxVolume", 1f);
            
            // Calculate final volume based on base volume, remaining crowd ratio, and UI settings
            float finalVolume = maxVolumePerVoice * currentCrowdRatio * globalSfxVolume;

            // Apply it to all generated audio sources smoothly
            foreach (var source in activeSources)
            {
                if (source != null && source.volume != finalVolume)
                {
                    source.volume = finalVolume;
                }
            }
        }

        private void OnDestroy()
        {
            // Clean up events to prevent memory leaks
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.OnLevelLoaded -= HandleLevelLoaded;
            }
            
            if (CrowdManager.Instance != null)
            {
                CrowdManager.Instance.OnDemandChanged -= HandleDemandChanged;
            }
        }
    }
}