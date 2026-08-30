using System;
using UnityEngine;
using DG.Tweening;

namespace RestaurantLoop.Core
{
    public class StackItemMovement : MonoBehaviour
    {
        [Header("Conveyor Entry Animation")]
        [SerializeField, Min(0f)] private float conveyorEntryRotationDuration = 0.16f;

        private float currentDistance;
        private float traveledDistance;
        private float targetTravelDistance;
        private bool isWaitingForRack;
        private Tween conveyorRotationTween;

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

            UpdateTransform(path, true, true);
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

            float pathLength = path != null ? path.Length : 0f;
            if (pathLength > 0f)
            {
                currentDistance = (currentDistance + moveDelta) % pathLength;
                if (currentDistance < 0f) currentDistance += pathLength;
            }

            traveledDistance += stepDistance;
            UpdateTransform(path, isClockwise);

            if (traveledDistance >= targetTravelDistance)
            {
                onExitReached?.Invoke();
            }
        }

        public void UpdateTransform(SplineConveyorPath path, bool isClockwise, bool animateRotation = false)
        {
            if (path == null || path.Length <= 0f) return;

            if (!path.TryGetSample(currentDistance, isClockwise, out Vector3 position, out Vector3 direction))
            {
                return;
            }

            transform.position = position;

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                if (animateRotation && conveyorEntryRotationDuration > 0f)
                {
                    conveyorRotationTween?.Kill();
                    conveyorRotationTween = transform
                        .DORotateQuaternion(targetRotation, conveyorEntryRotationDuration)
                        .SetEase(Ease.OutQuad);
                }
                else if (conveyorRotationTween == null || !conveyorRotationTween.IsActive())
                {
                    transform.rotation = targetRotation;
                }
            }
        }

        private void OnDisable()
        {
            conveyorRotationTween?.Kill();
            conveyorRotationTween = null;
        }
    }
}