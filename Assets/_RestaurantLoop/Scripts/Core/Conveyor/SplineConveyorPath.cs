using UnityEngine;
using UnityEngine.Splines;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Calculates positions and directions along the spline.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    public class SplineConveyorPath : MonoBehaviour
    {
        [SerializeField] private SplineContainer splineContainer;

        public float Length { get; private set; }

        private void Awake()
        {
            if (splineContainer == null)
            {
                splineContainer = GetComponent<SplineContainer>();
            }

            Length = splineContainer.CalculateLength();
        }

        /// <summary>
        /// Calculates world position along the spline based on distance.
        /// </summary>
        public Vector3 GetPosition(float distance)
        {
            float t = (distance % Length) / Length;
            if (t < 0f)
            {
                t += 1f;
            }

            return splineContainer.EvaluatePosition(t);
        }

        /// <summary>
        /// Calculates movement direction vector along the spline.
        /// </summary>
        public Vector3 GetDirection(float distance, bool isClockwise)
        {
            float t = (distance % Length) / Length;
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