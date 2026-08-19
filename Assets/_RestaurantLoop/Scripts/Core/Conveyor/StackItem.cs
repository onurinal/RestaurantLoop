using UnityEngine;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Handles individual stack movement state along the conveyor belt.
    /// </summary>
    public class StackItem : MonoBehaviour
    {
        [SerializeField] private int remainingCount = 10;

        private float currentDistance;
        private float distanceInLoop;

        public int RemainingItemCount => remainingCount;

        /// <summary>
        /// Places the stack at the designated entrance distance provided by the manager.
        /// </summary>
        public void InitializeOnBelt(SplineConveyorPath path, float startDistance)
        {
            currentDistance = startDistance;
            distanceInLoop = 0f;
            UpdateTransform(path, true);
        }

        public void MoveAlongBelt(SplineConveyorPath path, float speed, bool isClockwise, float deltaTime)
        {
            float moveDelta = (isClockwise ? speed : -speed) * deltaTime;

            currentDistance = (currentDistance + moveDelta) % path.Length;
            if (currentDistance < 0f)
            {
                currentDistance += path.Length;
            }

            distanceInLoop += Mathf.Abs(moveDelta);

            UpdateTransform(path, isClockwise);

            if (distanceInLoop >= path.Length)
            {
                distanceInLoop -= path.Length;
                Debug.Log($"Full loop completed by {gameObject.name}");
            }
        }

        private void UpdateTransform(SplineConveyorPath path, bool isClockwise)
        {
            transform.position = path.GetPosition(currentDistance);

            Vector3 direction = path.GetDirection(currentDistance, isClockwise);
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }
}