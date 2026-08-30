using UnityEngine;
using Dreamteck.Splines;

namespace RestaurantLoop.Core
{
    [RequireComponent(typeof(SplineComputer))]
    public class SplineConveyorPath : MonoBehaviour
    {
        [SerializeField] private SplineComputer splineComputer;

        public float Length { get; private set; }

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (splineComputer == null)
            {
                splineComputer = GetComponent<SplineComputer>();
            }

            if (splineComputer != null)
            {
                Length = (float)splineComputer.CalculateLength();
            }
        }

        /// <summary>
        /// Position and direction for one distance from a single Travel walk.
        /// Travel walks the cached sample array from the start, so it is by far the
        /// expensive part; calling GetPosition and GetDirection separately repeated that
        /// identical walk twice per stack per frame. Both Evaluate calls are kept exactly
        /// as they were, so the resulting transform is bit-identical.
        /// </summary>
        public bool TryGetSample(float distance, bool isClockwise, out Vector3 position, out Vector3 direction)
        {
            if (Length <= 0f)
            {
                Initialize();
            }

            if (Length <= 0f)
            {
                position = transform.position;
                direction = transform.forward;
                return false;
            }

            float clampedDistance = Mathf.Repeat(distance, Length);
            double percent = splineComputer.Travel(0, clampedDistance);

            position = splineComputer.EvaluatePosition(percent);
            Vector3 forward = splineComputer.Evaluate(percent).forward;
            direction = isClockwise ? forward : -forward;
            return true;
        }

        public Vector3 GetPosition(float distance)
        {
            if (Length <= 0f)
            {
                Initialize();
            }

            if (Length <= 0f)
            {
                return transform.position;
            }

            float clampedDistance = Mathf.Repeat(distance, Length);
            double percent = splineComputer.Travel(0, clampedDistance);
            return splineComputer.EvaluatePosition(percent);
        }

        public Vector3 GetDirection(float distance, bool isClockwise)
        {
            if (Length <= 0f)
            {
                Initialize();
            }

            if (Length <= 0f)
            {
                return transform.forward;
            }

            float clampedDistance = Mathf.Repeat(distance, Length);
            double percent = splineComputer.Travel(0, clampedDistance);
            Vector3 forward = splineComputer.Evaluate(percent).forward;

            return isClockwise ? forward : -forward;
        }
    }
}