using UnityEngine;
using DG.Tweening;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Handles individual stack movement, loop checks, and jump animations.
    /// </summary>
    public class StackItem : MonoBehaviour
    {
        [SerializeField] private int remainingCount = 10;
        [SerializeField] private float jumpPower = 1.5f;
        [SerializeField] private float jumpDuration = 0.5f;

        private float currentDistance;
        private float distanceInLoop;

        public int RemainingItemCount => remainingCount;

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
                OnLoopCompleted();
            }
        }

        /// <summary>
        /// Animates  jump to the rack slot position.
        /// </summary>
        public void JumpToSlot(Transform slotTransform)
        {
            transform.DOJump(slotTransform.position, jumpPower, 1, jumpDuration)
                .OnComplete(() => { transform.SetParent(slotTransform); });
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

        private void OnLoopCompleted()
        {
            if (remainingCount > 0)
            {
                ConveyorController.Instance.RemoveStack(this);
                RackManager.Instance.TryAddStackToRack(this);
            }
        }
    }
}