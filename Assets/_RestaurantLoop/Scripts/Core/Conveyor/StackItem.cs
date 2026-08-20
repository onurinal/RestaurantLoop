using UnityEngine;
using DG.Tweening;

namespace RestaurantLoop.Core
{
    public class StackItem : MonoBehaviour, IInteractable
    {
        [SerializeField] private int remainingCount = 10;
        [SerializeField] private float jumpPower = 1.5f;
        [SerializeField] private float jumpDuration = 0.5f;

        private float currentDistance;
        private float traveledDistance;
        private float targetTravelDistance;

        public int RemainingItemCount => remainingCount;
        public bool IsJumping { get; private set; }

        public void OnTap()
        {
            if (IsJumping) return;

            QueueSlot queueSlot = GetComponentInParent<QueueSlot>();
            if (queueSlot != null)
            {
                QueueColumn column = queueSlot.GetComponentInParent<QueueColumn>();
                if (column != null && column.FrontSlot == queueSlot)
                {
                    column.TrySendFrontStackToBelt();
                    return;
                }
            }

            RackSlot rackSlot = GetComponentInParent<RackSlot>();
            if (rackSlot != null && RackManager.Instance != null)
            {
                RackManager.Instance.TrySendRackStackToBelt(this);
            }
        }

        public void InitializeOnBelt(SplineConveyorPath path, float startDistance, float totalDistanceToExit)
        {
            currentDistance = startDistance;
            traveledDistance = 0f;
            targetTravelDistance = totalDistanceToExit;
            IsJumping = false;
            UpdateTransform(path, true);
        }

        public void MoveAlongBelt(SplineConveyorPath path, float speed, bool isClockwise, float deltaTime)
        {
            if (IsJumping) return;

            float moveDelta = (isClockwise ? speed : -speed) * deltaTime;

            currentDistance = (currentDistance + moveDelta) % path.Length;
            if (currentDistance < 0f) currentDistance += path.Length;

            traveledDistance += Mathf.Abs(moveDelta);
            UpdateTransform(path, isClockwise);

            if (traveledDistance >= targetTravelDistance)
            {
                OnExitReached();
            }
        }

        public void JumpToConveyor(Vector3 targetPosition, System.Action onComplete)
        {
            IsJumping = true;
            transform.DOKill();
            transform.DOJump(targetPosition, jumpPower, 1, jumpDuration)
                .OnComplete(() =>
                {
                    IsJumping = false;
                    onComplete?.Invoke();
                });
        }

        public void JumpToSlot(Transform slotTransform, System.Action onComplete = null)
        {
            IsJumping = true;
            transform.DOKill();
            transform.DOJump(slotTransform.position, jumpPower, 1, jumpDuration)
                .OnComplete(() =>
                {
                    IsJumping = false;
                    transform.SetParent(slotTransform);
                    transform.localPosition = Vector3.zero;
                    onComplete?.Invoke();
                });
        }

        public void Shake()
        {
            if (IsJumping || DOTween.IsTweening(transform)) return;

            transform.DOKill();
            transform.localPosition = Vector3.zero;

            transform.DOShakePosition(0.2f, 0.12f, 10, 90f)
                .OnComplete(() => { transform.localPosition = Vector3.zero; });
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

        private void OnExitReached()
        {
            if (remainingCount > 0)
            {
                IsJumping = true;
                RackManager.Instance.TryAddStackToRack(this);
            }
        }
    }
}