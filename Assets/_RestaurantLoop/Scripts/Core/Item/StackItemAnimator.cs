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

        private void OnDisable()
        {
            IsJumping = false;
        }

        public void Shake()
        {
            if (IsJumping || DOTween.IsTweening(transform)) return;

            transform.DOKill();
            transform.localPosition = Vector3.zero;
            transform.DOShakePosition(0.2f, 0.12f, 10, 90f)
                .OnComplete(() => { transform.localPosition = Vector3.zero; });
        }

        public void JumpToConveyor(Vector3 targetPosition, Action onComplete)
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

        public void MoveToConveyor(Vector3 targetPosition, float duration, Action onComplete)
        {
            IsJumping = true;
            transform.DOKill();
            transform.DOMove(targetPosition, duration)
                .SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    IsJumping = false;
                    onComplete?.Invoke();
                });
        }

        public void JumpToSlot(Transform slotTransform, Action onComplete = null)
        {
            IsJumping = true;
            transform.DOKill();

            transform.DORotateQuaternion(slotTransform.rotation, jumpDuration);

            transform.DOJump(slotTransform.position, jumpPower, 1, jumpDuration)
                .OnComplete(() =>
                {
                    IsJumping = false;
                    transform.SetParent(slotTransform);
                    transform.localPosition = Vector3.zero;
                    transform.localRotation = Quaternion.identity;
                    onComplete?.Invoke();
                });
        }

        public void AnimateItemThrowToCustomer(GameObject singleMeshModel, Vector3 targetCustomerPosition, bool ignoreTimeScale = false)
        {
            if (singleMeshModel == null) return;

            // Rack stacks inherit a smaller world scale from their slot hierarchy. Preserve the
            // source mesh's rendered size when its flight visual is spawned at the scene root.
            Vector3 sourceWorldScale = singleMeshModel.transform.lossyScale;
            GameObject flyingItem = PoolManager.Instance.Spawn(singleMeshModel, transform.position, Quaternion.identity);
            flyingItem.transform.localScale = sourceWorldScale;

            Tween throwTween = flyingItem.transform.DOJump(targetCustomerPosition, 2f, 1, 0.35f);
            if (ignoreTimeScale) throwTween.SetUpdate(true);

            throwTween
                .OnComplete(() => { PoolManager.Instance.Despawn(flyingItem); });
        }
    }
}