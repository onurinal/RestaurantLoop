using System;
using UnityEngine;

namespace RestaurantLoop.Core
{
    public class StackItemMovement : MonoBehaviour
    {
        private float currentDistance;
        private float traveledDistance;
        private float targetTravelDistance;
        private bool isWaitingForRack;

        public float CurrentDistance => currentDistance;

        public bool IsWaitingForRack
        {
            get => isWaitingForRack;
            set => isWaitingForRack = value;
        }

        public void InitializeOnBelt(SplineConveyorPath path, float startDistance, float totalDistanceToExit)
        {
            currentDistance = startDistance;
            traveledDistance = 0f;
            targetTravelDistance = totalDistanceToExit;
            isWaitingForRack = false;

            UpdateTransform(path, true);
        }

        public void MoveAlongBelt(SplineConveyorPath path, float speed, bool isClockwise, float deltaTime, Action onExitReached)
        {
            if (isWaitingForRack)
            {
                onExitReached?.Invoke();
                return;
            }

            float remainingTravelDistance = Mathf.Max(0f, targetTravelDistance - traveledDistance);
            float stepDistance = Mathf.Min(Mathf.Abs(speed * deltaTime), remainingTravelDistance);
            float moveDelta = isClockwise ? stepDistance : -stepDistance;

            currentDistance = (currentDistance + moveDelta) % path.Length;
            if (currentDistance < 0f) currentDistance += path.Length;

            traveledDistance += stepDistance;
            UpdateTransform(path, isClockwise);

            if (traveledDistance >= targetTravelDistance)
            {
                onExitReached?.Invoke();
            }
        }

        public void UpdateTransform(SplineConveyorPath path, bool isClockwise)
        {
            if (path == null) return;

            transform.position = path.GetPosition(currentDistance);
            Vector3 direction = path.GetDirection(currentDistance, isClockwise);
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }
}