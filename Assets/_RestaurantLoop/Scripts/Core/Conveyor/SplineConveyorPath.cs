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
                Length = splineComputer.CalculateLength();
            }
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

            float t = Mathf.Repeat(distance, Length) / Length;
            return splineComputer.EvaluatePosition(t);
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

            float t = Mathf.Repeat(distance, Length) / Length;
            Vector3 forward = splineComputer.Evaluate(t).forward;

            return isClockwise ? forward : -forward;
        }
    }
}