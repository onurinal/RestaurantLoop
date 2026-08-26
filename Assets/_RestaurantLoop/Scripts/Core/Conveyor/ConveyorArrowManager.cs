using System.Collections.Generic;
using UnityEngine;
using Dreamteck.Splines;

namespace RestaurantLoop.Core
{
    public class ConveyorArrowManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SplineComputer splineComputer;
        [SerializeField] private GameObject arrowPrefab;

        [Header("Modular Direction & Flow")]
        [Tooltip("True: Moves backwards (1 -> 0). False: Moves forwards (0 -> 1).")]
        [SerializeField] private bool isReversed = false;

        [Header("Arrow Density & Speed")]
        [Tooltip("How many arrows should loop on the belt.")]
        [SerializeField] private int arrowCount = 6;
        [Tooltip("Base movement speed in meters per second.")]
        [SerializeField] private float speed = 3f;

        [Header("Transform Adjustments")]
        [Tooltip("Local height offset relative to belt orientation (Y = float height).")]
        [SerializeField] private Vector3 localPositionOffset = new Vector3(0f, 0.08f, 0f);
        [Tooltip("Camera angle tilt alignment.")]
        [SerializeField] private Vector3 rotationOffset = new Vector3(30f, 0f, 0f);

        private readonly List<Transform> spawnedArrows = new List<Transform>();
        private float[] arrowProgresses;
        private float splineLength;

        private void Start()
        {
            if (splineComputer == null) splineComputer = GetComponent<SplineComputer>();

            RecalculateSplineLength();
            SpawnArrows();
        }

        public void RecalculateSplineLength()
        {
            if (splineComputer != null)
            {
                splineLength = splineComputer.CalculateLength();
            }
        }

        public void SetDirection(bool reversed)
        {
            isReversed = reversed;
        }

        public void SpawnArrows()
        {
            if (arrowPrefab == null || splineComputer == null) return;

            foreach (var t in spawnedArrows)
            {
                if (t != null) Destroy(t.gameObject);
            }

            spawnedArrows.Clear();

            arrowProgresses = new float[arrowCount];
            float step = 1f / arrowCount;

            for (int i = 0; i < arrowCount; i++)
            {
                GameObject arrow = Instantiate(arrowPrefab, transform);
                spawnedArrows.Add(arrow.transform);
                arrowProgresses[i] = i * step;
            }
        }

        private void Update()
        {
            if (splineComputer == null || spawnedArrows.Count == 0 || splineLength <= 0f) return;

            float currentSpeed = speed;

            // Direction multiplier
            float directionMultiplier = isReversed ? -1f : 1f;
            float deltaProgress = (currentSpeed * directionMultiplier * Time.deltaTime) / splineLength;

            for (int i = 0; i < spawnedArrows.Count; i++)
            {
                arrowProgresses[i] += deltaProgress;

                if (arrowProgresses[i] >= 1f) arrowProgresses[i] -= 1f;
                if (arrowProgresses[i] < 0f) arrowProgresses[i] += 1f;

                SplineSample sample = splineComputer.Evaluate((double)arrowProgresses[i]);

                Quaternion flowRotation = sample.rotation * Quaternion.Euler(0f, isReversed ? 180f : 0f, 0f);
                Quaternion finalRotation = flowRotation * Quaternion.Euler(rotationOffset);

                spawnedArrows[i].position = sample.position + (sample.rotation * localPositionOffset);
                spawnedArrows[i].rotation = finalRotation;
            }
        }
    }
}