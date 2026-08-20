using UnityEngine;
using UnityEngine.Splines;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Pure geometry handler that calculates positions and directions along the spline.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    public class SplineConveyorPath : MonoBehaviour
    {
        [SerializeField] private SplineContainer splineContainer;

        public float Length { get; private set; }

        private void Awake()
        {
            GetOrCalculateLength();
        }

        /// <summary>
        /// Calculates spline length dynamically if not calculated yet (e.g. in Edit Mode).
        /// </summary>
        public float GetOrCalculateLength()
        {
            if (splineContainer == null)
            {
                splineContainer = GetComponent<SplineContainer>();
            }

            if (splineContainer != null)
            {
                Length = splineContainer.CalculateLength();
                return Length;
            }

            return 0f;
        }

        public Vector3 GetPosition(float distance)
        {
            float currentLength = Length > 0f ? Length : GetOrCalculateLength();

            if (currentLength <= 0f)
            {
                return transform.position;
            }

            float t = (distance % currentLength) / currentLength;
            if (t < 0f)
            {
                t += 1f;
            }

            return splineContainer.EvaluatePosition(t);
        }

        public Vector3 GetDirection(float distance, bool isClockwise)
        {
            float currentLength = Length > 0f ? Length : GetOrCalculateLength();

            if (currentLength <= 0f)
            {
                return transform.forward;
            }

            float t = (distance % currentLength) / currentLength;
            if (t < 0f)
            {
                t += 1f;
            }

            Vector3 tangent = Vector3.Normalize(splineContainer.EvaluateTangent(t));

            if (!isClockwise)
            {
                return -tangent;
            }

            return tangent;
        }
    }
}