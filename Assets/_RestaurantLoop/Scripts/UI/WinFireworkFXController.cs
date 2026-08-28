using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.UI
{
    /// <summary>
    /// Plays a bounded fireworks sequence whenever the owning win panel becomes active.
    /// </summary>
    public sealed class WinFireworkFXController : MonoBehaviour
    {
        [SerializeField] private int explosionCount = 8;

        [Header("References")]
        [SerializeField] private GameObject fireworkPrefab;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Canvas winCanvas;

        [Header("Render Sorting")]
        [SerializeField, Min(1)] private int sortingOrderOffset = 100;

        [Header("Viewport Spawn Area")]
        [SerializeField, Range(0f, 1f)] private float minViewportX = 0.15f;
        [SerializeField, Range(0f, 1f)] private float maxViewportX = 0.85f;
        [SerializeField, Range(0f, 1f)] private float minViewportY = 0.15f;
        [SerializeField, Range(0f, 1f)] private float maxViewportY = 0.85f;
        [SerializeField, Min(0.01f)] private float spawnDistance = 10f;

        [Header("Sequence")]
        [SerializeField, Min(0f)] private float minimumDelay = 0.2f;
        [SerializeField, Min(0f)] private float maximumDelay = 0.4f;
        [SerializeField, Min(0f)] private float fallbackLifetime = 10f;

        private readonly List<ParticleSystem> particleSystems = new();
        private readonly List<ParticleSystemRenderer> particleRenderers = new();

        private Coroutine sequence;
        private int sortingLayerId;
        private int particleSortingOrder;
        private float instanceLifetime;

        private void Awake()
        {
            winCanvas ??= GetComponentInParent<Canvas>();
            targetCamera ??= Camera.main;

            if (winCanvas != null)
            {
                sortingLayerId = winCanvas.sortingLayerID;
                particleSortingOrder = winCanvas.sortingOrder + sortingOrderOffset;
            }
            else
            {
                particleSortingOrder = sortingOrderOffset;
            }

            instanceLifetime = CalculatePrefabLifetime();
        }

        private void OnEnable()
        {
            sequence = StartCoroutine(PlaySequence());
        }

        private void OnDisable()
        {
            if (sequence == null)
            {
                return;
            }

            StopCoroutine(sequence);
            sequence = null;
        }

        private IEnumerator PlaySequence()
        {
            if (fireworkPrefab == null || targetCamera == null)
            {
                Debug.LogWarning($"{nameof(WinFireworkFXController)} on '{name}' requires a firework prefab and camera.", this);
                yield break;
            }

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

            sequence = null;
        }

        private void SpawnFirework()
        {
            GameObject instance = Instantiate(
                fireworkPrefab,
                targetCamera.ViewportToWorldPoint(GetEdgeViewportPosition()),
                Quaternion.identity);

            ApplyParticleSorting(instance);
            Destroy(instance, instanceLifetime);
        }

        private Vector3 GetEdgeViewportPosition()
        {
            float minX = Mathf.Min(minViewportX, maxViewportX);
            float maxX = Mathf.Max(minViewportX, maxViewportX);
            float minY = Mathf.Min(minViewportY, maxViewportY);
            float maxY = Mathf.Max(minViewportY, maxViewportY);

            switch (Random.Range(0, 4))
            {
                case 0:
                    return new Vector3(Random.Range(minX, Mathf.Lerp(minX, maxX, 0.25f)), Random.Range(minY, maxY), spawnDistance);
                case 1:
                    return new Vector3(Random.Range(Mathf.Lerp(minX, maxX, 0.75f), maxX), Random.Range(minY, maxY), spawnDistance);
                case 2:
                    return new Vector3(Random.Range(minX, maxX), Random.Range(Mathf.Lerp(minY, maxY, 0.65f), maxY), spawnDistance);
                default:
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
            if (fireworkPrefab == null)
            {
                return fallbackLifetime;
            }

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