using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RestaurantLoop.Audio;

namespace RestaurantLoop.UI
{
    /// <summary>
    /// Plays a bounded fireworks sequence whenever the owning win panel becomes active.
    /// Features trailing audio extension to sustain sound effects for 3-4 seconds after explosions finish.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class WinFireworkFXController : MonoBehaviour
    {
        [Header("Burst Settings")]
        [SerializeField, Min(1)] private int explosionCount = 4;

        [Header("References")]
        [SerializeField] private GameObject fireworkPrefab;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Canvas winCanvas;

        [Header("Audio Settings")]
        [SerializeField] private AudioClip explosionSound;
        [SerializeField] private AudioSource audioSource;
        [SerializeField, Range(0.8f, 1.2f)] private float minPitch = 0.90f;
        [SerializeField, Range(0.8f, 1.2f)] private float maxPitch = 1.10f;

        [Header("Audio Extension (After Visual Bursts)")]
        [Tooltip("Number of trailing audio echoes/crackle sounds to play after visual explosions end.")]
        [SerializeField, Min(0)] private int trailingSoundCount = 3;
        [Tooltip("Delay in seconds between trailing sound echoes.")]
        [SerializeField, Min(0.1f)] private float trailingSoundInterval = 1.1f;

        [Header("Render Sorting")]
        [SerializeField, Min(1)] private int sortingOrderOffset = 100;

        [Header("Viewport Spawn Area")]
        [SerializeField, Range(0f, 1f)] private float minViewportX = 0.15f;
        [SerializeField, Range(0f, 1f)] private float maxViewportX = 0.85f;
        [SerializeField, Range(0f, 1f)] private float minViewportY = 0.15f;
        [SerializeField, Range(0f, 1f)] private float maxViewportY = 0.85f;
        [SerializeField, Min(0.01f)] private float spawnDistance = 10f;

        [Header("Sequence Delays")]
        [SerializeField, Min(0f)] private float minimumDelay = 0.5f;
        [SerializeField, Min(0f)] private float maximumDelay = 0.9f;
        [SerializeField, Min(0f)] private float fallbackLifetime = 10f;

        private readonly List<ParticleSystem> particleSystems = new();
        private readonly List<ParticleSystemRenderer> particleRenderers = new();
        private readonly List<GameObject> activeSpawnedFireworks = new();

        private Coroutine sequence;
        private int sortingLayerId;
        private int particleSortingOrder;
        private float instanceLifetime;

        private void Awake()
        {
            winCanvas ??= GetComponentInParent<Canvas>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();

            ConfigureAudioSource();
            RefreshReferences();
            instanceLifetime = CalculatePrefabLifetime();
        }

        private void OnEnable()
        {
            RefreshReferences();
            ClearSpawnedFireworks();
            sequence = StartCoroutine(PlaySequence());
        }

        private void OnDisable()
        {
            if (sequence != null)
            {
                StopCoroutine(sequence);
                sequence = null;
            }

            ClearSpawnedFireworks();
        }

        private void ConfigureAudioSource()
        {
            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // 2D Sound for UI
            }
        }

        private void RefreshReferences()
        {
            if (winCanvas == null) winCanvas = GetComponentInParent<Canvas>();

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

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

        private IEnumerator PlaySequence()
        {
            RefreshReferences();

            if (fireworkPrefab == null || targetCamera == null)
            {
                Debug.LogWarning($"{nameof(WinFireworkFXController)} on '{name}' requires a firework prefab and active target camera.", this);
                yield break;
            }

            // 1. Primary Visual + Audio Explosions
            for (int i = 0; i < explosionCount; i++)
            {
                SpawnFirework();

                if (i < explosionCount - 1)
                {
                    yield return new WaitForSeconds(Random.Range(
                        Mathf.Min(minimumDelay, maximumDelay),
                        Mathf.Max(minimumDelay, maximumDelay)));
                }
            }

            // 2. Trailing Audio Phase (Extends sound for an additional 3-4 seconds)
            for (int j = 0; j < trailingSoundCount; j++)
            {
                yield return new WaitForSeconds(trailingSoundInterval);
                PlayExplosionAudio(0.7f); // Slightly softer volume for trailing echoes
            }

            sequence = null;
        }

        private void SpawnFirework()
        {
            GameObject instance = Instantiate(
                fireworkPrefab,
                targetCamera.ViewportToWorldPoint(GetEdgeViewportPosition()),
                Quaternion.identity);

            activeSpawnedFireworks.Add(instance);
            ApplyParticleSorting(instance);
            Destroy(instance, instanceLifetime);

            PlayExplosionAudio(1.0f);
        }

        private void PlayExplosionAudio(float volumeScale)
        {
            if (explosionSound == null) return;

            if (audioSource != null)
            {
                audioSource.pitch = Random.Range(minPitch, maxPitch);
                audioSource.PlayOneShot(explosionSound, volumeScale);
            }
            else if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(explosionSound);
            }
        }

        private void ClearSpawnedFireworks()
        {
            for (int i = activeSpawnedFireworks.Count - 1; i >= 0; i--)
            {
                if (activeSpawnedFireworks[i] != null)
                {
                    Destroy(activeSpawnedFireworks[i]);
                }
            }

            activeSpawnedFireworks.Clear();
        }

        private Vector3 GetEdgeViewportPosition()
        {
            float minX = Mathf.Min(minViewportX, maxViewportX);
            float maxX = Mathf.Max(minViewportX, maxViewportX);
            float minY = Mathf.Min(minViewportY, maxViewportY);
            float maxY = Mathf.Max(minViewportY, maxViewportY);

            switch (Random.Range(0, 4))
            {
                case 0: // Left edge
                    return new Vector3(Random.Range(minX, Mathf.Lerp(minX, maxX, 0.25f)), Random.Range(minY, maxY), spawnDistance);
                case 1: // Right edge
                    return new Vector3(Random.Range(Mathf.Lerp(minX, maxX, 0.75f), maxX), Random.Range(minY, maxY), spawnDistance);
                case 2: // Upper area
                    return new Vector3(Random.Range(minX, maxX), Random.Range(Mathf.Lerp(minY, maxY, 0.65f), maxY), spawnDistance);
                default: // Lower area
                    return new Vector3(Random.Range(minX, maxX), Random.Range(minY, Mathf.Lerp(minY, maxY, 0.35f)), spawnDistance);
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

        private float CalculatePrefabLifetime()
        {
            if (fireworkPrefab == null) return fallbackLifetime;

            particleSystems.Clear();
            fireworkPrefab.GetComponentsInChildren(true, particleSystems);

            float lifetime = fallbackLifetime;
            for (int i = 0; i < particleSystems.Count; i++)
            {
                ParticleSystem.MainModule main = particleSystems[i].main;
                lifetime = Mathf.Max(lifetime, main.duration + main.startDelay.constantMax + main.startLifetime.constantMax);
            }

            return lifetime;
        }
    }
}