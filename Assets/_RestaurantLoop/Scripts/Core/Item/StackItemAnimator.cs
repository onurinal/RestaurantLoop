using System;
using UnityEngine;
using DG.Tweening;
using RestaurantLoop.Infrastructure;

namespace RestaurantLoop.Core
{
    public class StackItemAnimator : MonoBehaviour
    {
        [Header("Animation Parameters")]
        [SerializeField] private float jumpPower = 1.5f;
        [SerializeField] private float jumpDuration = 0.5f;

        public bool IsJumping { get; private set; }

        private Action pendingMovementCompletion;
        private Action pendingSlotCompletion;
        private Transform pendingSlotTransform;
        private TweenCallback shakeCompletedCallback;
        private TweenCallback movementCompletedCallback;
        private TweenCallback slotCompletedCallback;

        private void Awake()
        {
            shakeCompletedCallback = HandleShakeCompleted;
            movementCompletedCallback = HandleMovementCompleted;
            slotCompletedCallback = HandleSlotCompleted;
        }

        private void OnDisable()
        {
            IsJumping = false;
            pendingMovementCompletion = null;
            pendingSlotCompletion = null;
            pendingSlotTransform = null;
        }

        public void Shake()
        {
            if (IsJumping || DOTween.IsTweening(transform)) return;

            transform.DOKill();
            transform.localPosition = Vector3.zero;
            transform.DOShakePosition(0.2f, 0.12f, 10, 90f)
                .OnComplete(shakeCompletedCallback);
        }

        public void JumpToConveyor(Vector3 targetPosition, Action onComplete)
        {
            IsJumping = true;
            pendingMovementCompletion = onComplete;
            transform.DOKill();
            transform.DOJump(targetPosition, jumpPower, 1, jumpDuration)
                .OnComplete(movementCompletedCallback);
        }

        public void MoveToConveyor(Vector3 targetPosition, float duration, Action onComplete)
        {
            IsJumping = true;
            pendingMovementCompletion = onComplete;
            transform.DOKill();
            transform.DOMove(targetPosition, duration)
                .SetEase(Ease.InQuad)
                .OnComplete(movementCompletedCallback);
        }

        public void JumpToSlot(Transform slotTransform, Action onComplete = null)
        {
            IsJumping = true;
            pendingSlotTransform = slotTransform;
            pendingSlotCompletion = onComplete;
            transform.DOKill();

            transform.DORotateQuaternion(slotTransform.rotation, jumpDuration);

            transform.DOJump(slotTransform.position, jumpPower, 1, jumpDuration)
                .OnComplete(slotCompletedCallback);
        }

        private void HandleShakeCompleted()
        {
            transform.localPosition = Vector3.zero;
        }

        private void HandleMovementCompleted()
        {
            IsJumping = false;
            Action completion = pendingMovementCompletion;
            pendingMovementCompletion = null;
            completion?.Invoke();
        }

        private void HandleSlotCompleted()
        {
            IsJumping = false;
            Transform slotTransform = pendingSlotTransform;
            pendingSlotTransform = null;

            if (slotTransform != null)
            {
                transform.SetParent(slotTransform);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }

            Action completion = pendingSlotCompletion;
            pendingSlotCompletion = null;
            completion?.Invoke();
        }

        public void AnimateItemThrowToCustomer(GameObject itemPrefab, Vector3 targetCustomerPosition, bool ignoreTimeScale = false)
        {
            if (itemPrefab == null) return;

            GameObject flyingItem = PoolManager.Instance.Spawn(itemPrefab, transform.position, Quaternion.identity);
            flyingItem.transform.localScale = transform.lossyScale;

            if (flyingItem.TryGetComponent(out StackItem stackScript)) stackScript.enabled = false;
            if (flyingItem.TryGetComponent(out Collider col)) col.enabled = false;

            if (flyingItem.TryGetComponent(out StackItemVisuals visuals))
            {
                if (visuals.SingleMeshModel != null) visuals.SingleMeshModel.SetActive(true);
                visuals.UpdateCountText(false, 0);
            }

            TrailRenderer dummyTrail = flyingItem.GetComponentInChildren<TrailRenderer>(true);
            if (dummyTrail != null)
            {
                dummyTrail.enabled = true;
                dummyTrail.Clear();
            }

            Tween throwTween = flyingItem.transform.DOJump(targetCustomerPosition, 2f, 1, 0.35f);
            if (ignoreTimeScale) throwTween.SetUpdate(true);

            throwTween.OnComplete(() =>
            {
                if (dummyTrail != null)
                {
                    dummyTrail.Clear();
                    dummyTrail.enabled = false;
                }

                if (stackScript != null) stackScript.enabled = true;
                if (col != null) col.enabled = true;

                PoolManager.Instance.Despawn(flyingItem);
            });
        }
    }
}