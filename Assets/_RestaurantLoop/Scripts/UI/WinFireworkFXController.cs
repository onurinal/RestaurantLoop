using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RestaurantLoop.Audio;

namespace RestaurantLoop.UI
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class WinFireworkFXController : MonoBehaviour
    {
        [Header("Burst Settings")]
        [SerializeField, Min(1)] private int explosionCount = 2;

        [Header("References")]
        [SerializeField] private GameObject fireworkPrefab;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Canvas winCanvas;

        [Header("Audio Settings")]
        [SerializeField] private AudioClip explosionSound;
        [SerializeField] private AudioSource audioSource;
        [SerializeField, Range(0f, 1f)] private float explosionVolume = 0.3f;
        [SerializeField, Range(0.8f, 1.2f)] private float minPitch = 0.95f;
        [SerializeField, Range(0.8f, 1.2f)] private float maxPitch = 1.05f;

        [Header("Audio Continuous Playback / Repeat")]
        [SerializeField] private bool repeatAudioDuringParticles = true;
        [SerializeField, Min(0.1f)] private float audioRepeatInterval = 0.35f;
        [SerializeField, Range(0f, 1f)] private float repeatAudioVolumeScale = 0.6f;

        [Header("Automated Audio Lifetime Sync")]
        [SerializeField] private bool autoDetectParticleLifetime = true;
        [SerializeField, Min(0.1f)] private float fallbackParticleLifetime = 3.5f;
        [SerializeField, Min(0f)] private float extraSustainPadding = 1.0f;
        [SerializeField, Min(0f)] private float audioFadeOutDuration = 0.5f;
        [SerializeField] private bool stopAudioOnDisable = true;

        [Header("Render Sorting")]
        [SerializeField, Min(1)] private int sortingOrderOffset = 100;

        [Header("Viewport Spawn Area")]
        [SerializeField, Range(0f, 1f)] private float minViewportX = 0.15f;
        [SerializeField, Range(0f, 1f)] private float maxViewportX = 0.85f;
        [SerializeField, Range(0f, 1f)] private float minViewportY = 0.15f;
        [SerializeField, Range(0f, 1f)] private float maxViewportY = 0.85f;
        [SerializeField, Min(0.01f)] private float spawnDistance = 10f;

        [Header("Sequence Delays (Unscaled Realtime)")]
        [SerializeField, Min(0f)] private float initialDelay = 0.2f;
        [SerializeField, Min(0.05f)] private float minimumDelay = 0.25f;
        [SerializeField, Min(0.05f)] private float maximumDelay = 0.45f;

        private readonly List<ParticleSystem> particleSystems = new();
        private readonly List<ParticleSystemRenderer> particleRenderers = new();
        private readonly List<GameObject> activeSpawnedFireworks = new();

        private Coroutine sequenceCoroutine;
        private Coroutine fadeOutCoroutine;
        private int sortingLayerId;
        private int particleSortingOrder;
        private float detectedParticleLifetime;

        private void Awake()
        {
            winCanvas ??= GetComponentInParent<Canvas>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();

            ConfigureAudioSource();
            RefreshReferences();
            UpdateParticleLifetime();
        }

        private void OnEnable()
        {
            RefreshReferences();
            ClearSpawnedFireworks();
            UpdateParticleLifetime();

            if (audioSource != null)
            {
                audioSource.volume = 1f;
            }
        }

        private void OnDisable()
        {
            if (sequenceCoroutine != null) StopCoroutine(sequenceCoroutine);
            if (fadeOutCoroutine != null) StopCoroutine(fadeOutCoroutine);

            sequenceCoroutine = null;
            fadeOutCoroutine = null;

            ClearSpawnedFireworks();

            if (stopAudioOnDisable && audioSource != null)
            {
                audioSource.Stop();
            }
        }

        public void PlayFireworks(Action onComplete)
        {
            if (sequenceCoroutine != null)
            {
                StopCoroutine(sequenceCoroutine);
            }

            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
            }

            sequenceCoroutine = StartCoroutine(PlaySequence(onComplete));
        }

        private void ConfigureAudioSource()
        {
            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
        }

        private void RefreshReferences()
        {
            if (winCanvas == null) winCanvas = GetComponentInParent<Canvas>();
            if (targetCamera == null) targetCamera = Camera.main;

            if (winCanvas != null)
            {
                sortingLayerId = winCanvas.sortingLayerID;
                particleSortingOrder = winCanvas.sortingOrder + sortingOrderOffset;
            }
            else
            {
                particleSortingOrder = sortingOrderOffset;
            }
        }

        private void UpdateParticleLifetime()
        {
            if (!autoDetectParticleLifetime || fireworkPrefab == null)
            {
                detectedParticleLifetime = fallbackParticleLifetime;
                return;
            }

            particleSystems.Clear();
            fireworkPrefab.GetComponentsInChildren(true, particleSystems);

            if (particleSystems.Count == 0)
            {
                detectedParticleLifetime = fallbackParticleLifetime;
                return;
            }

            float maxLifetime = 0f;
            for (int i = 0; i < particleSystems.Count; i++)
            {
                ParticleSystem.MainModule main = particleSystems[i].main;
                float systemTotal = main.startDelay.constantMax + main.duration + main.startLifetime.constantMax;
                if (systemTotal > maxLifetime) maxLifetime = systemTotal;
            }

            detectedParticleLifetime = maxLifetime > 0f ? maxLifetime : fallbackParticleLifetime;
        }

        private IEnumerator PlaySequence(Action onComplete)
        {
            RefreshReferences();

            if (fireworkPrefab == null || targetCamera == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            if (initialDelay > 0f) yield return new WaitForSecondsRealtime(initialDelay);

            for (int i = 0; i < explosionCount; i++)
            {
                SpawnFirework();

                if (i < explosionCount - 1)
                {
                    float delay = UnityEngine.Random.Range(Mathf.Min(minimumDelay, maximumDelay), Mathf.Max(minimumDelay, maximumDelay));
                    yield return new WaitForSecondsRealtime(delay);
                }
            }

            float totalVisualLifetime = detectedParticleLifetime + extraSustainPadding;
            float elapsedSustain = 0f;

            if (repeatAudioDuringParticles && audioRepeatInterval > 0f)
            {
                while (elapsedSustain < (totalVisualLifetime - audioFadeOutDuration))
                {
                    yield return new WaitForSecondsRealtime(audioRepeatInterval);
                    elapsedSustain += audioRepeatInterval;

                    if (elapsedSustain < (totalVisualLifetime - audioFadeOutDuration))
                    {
                        PlayExplosionAudio(repeatAudioVolumeScale);
                    }
                }
            }
            else
            {
                float remainingTime = totalVisualLifetime - audioFadeOutDuration;
                if (remainingTime > 0f) yield return new WaitForSecondsRealtime(remainingTime);
            }

            if (audioFadeOutDuration > 0f)
            {
                yield return StartCoroutine(FadeOutAudioRoutine(audioFadeOutDuration));
            }
            else
            {
                if (audioSource != null && audioSource.gameObject.activeInHierarchy) audioSource.Stop();
            }

            sequenceCoroutine = null;

            onComplete?.Invoke();
        }

        private IEnumerator FadeOutAudioRoutine(float duration)
        {
            if (audioSource == null || !audioSource.gameObject.activeInHierarchy) yield break;

            float startVolume = audioSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (audioSource != null && audioSource.gameObject.activeInHierarchy)
                {
                    audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                }

                yield return null;
            }

            if (audioSource != null && audioSource.gameObject.activeInHierarchy)
            {
                audioSource.Stop();
                audioSource.volume = startVolume;
            }
        }

        private void SpawnFirework()
        {
            GameObject instance = Instantiate(
                fireworkPrefab,
                targetCamera.ViewportToWorldPoint(GetEdgeViewportPosition()),
                Quaternion.identity);

            activeSpawnedFireworks.Add(instance);
            ApplyParticleSorting(instance);
            Destroy(instance, detectedParticleLifetime + extraSustainPadding);

            PlayExplosionAudio(1.0f);
        }

        private void PlayExplosionAudio(float volumeScale)
        {
            if (explosionSound == null) return;

            AudioManager audioManager = AudioManager.Instance;
            float globalSfxVolume = audioManager != null ? audioManager.SfxVolume : Mathf.Clamp01(PlayerPrefs.GetFloat("SfxVolume", 1f));

            if ((audioManager != null && audioManager.IsSfxMuted) || globalSfxVolume <= Mathf.Epsilon) return;

            float finalVolume = volumeScale * explosionVolume * globalSfxVolume;

            if (audioSource != null && audioSource.gameObject.activeInHierarchy)
            {
                audioSource.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
                audioSource.PlayOneShot(explosionSound, finalVolume);
            }
            else if (audioManager != null)
            {
                audioManager.PlaySFX(explosionSound);
            }
        }

        private void ClearSpawnedFireworks()
        {
            for (int i = activeSpawnedFireworks.Count - 1; i >= 0; i--)
            {
                if (activeSpawnedFireworks[i] != null) Destroy(activeSpawnedFireworks[i]);
            }

            activeSpawnedFireworks.Clear();
        }

        private Vector3 GetEdgeViewportPosition()
        {
            float minX = Mathf.Min(minViewportX, maxViewportX);
            float maxX = Mathf.Max(minViewportX, maxViewportX);
            float minY = Mathf.Min(minViewportY, maxViewportY);
            float maxY = Mathf.Max(minViewportY, maxViewportY);

            switch (UnityEngine.Random.Range(0, 4))
            {
                case 0: return new Vector3(UnityEngine.Random.Range(minX, Mathf.Lerp(minX, maxX, 0.25f)), UnityEngine.Random.Range(minY, maxY), spawnDistance);
                case 1: return new Vector3(UnityEngine.Random.Range(Mathf.Lerp(minX, maxX, 0.75f), maxX), UnityEngine.Random.Range(minY, maxY), spawnDistance);
                case 2: return new Vector3(UnityEngine.Random.Range(minX, maxX), UnityEngine.Random.Range(Mathf.Lerp(minY, maxY, 0.65f), maxY), spawnDistance);
                default: return new Vector3(UnityEngine.Random.Range(minX, maxX), UnityEngine.Random.Range(minY, Mathf.Lerp(minY, maxY, 0.35f)), spawnDistance);
            }
        }

        private void ApplyParticleSorting(GameObject instance)
        {
            particleRenderers.Clear();
            instance.GetComponentsInChildren(true, particleRenderers);

            for (int i = 0; i < particleRenderers.Count; i++)
            {
                ParticleSystemRenderer renderer = particleRenderers[i];
                renderer.sortingLayerID = sortingLayerId;
                renderer.sortingOrder = particleSortingOrder;
            }
        }
    }
}