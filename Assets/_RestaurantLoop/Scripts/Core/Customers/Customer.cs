using System;
using System.Collections;
using UnityEngine;
using DG.Tweening;
using RestaurantLoop.Infrastructure;

namespace RestaurantLoop.Core
{
    public class Customer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private OrderBalloon orderBalloon;

        [Header("Animation & Visuals")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform visualContainer;

        [Header("Data")]
        private ItemDataSO requiredData;

        public bool IsServed { get; private set; }
        public bool IsEdgeCustomer { get; private set; }
        public ItemDataSO RequiredData => requiredData;

        private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
        private static readonly int EatHash = Animator.StringToHash("Eat");
        private static readonly int JumpHash = Animator.StringToHash("Jump");

        private Vector3 authoredLocalScale;
        private Transform ModelTransform => visualContainer != null ? visualContainer : (animator != null ? animator.transform : transform);

        private void Awake()
        {
            authoredLocalScale = transform.localScale;
            if (orderBalloon == null) orderBalloon = GetComponentInChildren<OrderBalloon>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void OnDestroy()
        {
            transform.DOKill();
            ModelTransform.DOKill();
        }

        public void Initialize(ItemDataSO data)
        {
            transform.DOKill();
            ModelTransform.DOKill();

            requiredData = data;
            IsServed = false;
            IsEdgeCustomer = false;

            transform.localScale = authoredLocalScale;
            transform.rotation = Quaternion.identity;
            ModelTransform.localRotation = Quaternion.identity;

            if (orderBalloon != null && requiredData != null)
            {
                orderBalloon.SetColorAndState(requiredData.UIColor, false);
            }
        }

        public void SetModelRotation(float yAngle)
        {
            ModelTransform.localRotation = Quaternion.Euler(0f, yAngle, 0f);
        }

        public void SetEdgeStatus(bool isEdge)
        {
            IsEdgeCustomer = isEdge;
            if (orderBalloon != null && requiredData != null)
            {
                orderBalloon.SetColorAndState(requiredData.UIColor, isEdge);
            }
        }

        public void MoveAlongPath(Vector3[] waypoints, float duration, bool setAsEdge, float targetYRotation = 0f, Action onComplete = null)
        {
            SetEdgeStatus(setAsEdge);
            transform.DOKill();
            ModelTransform.DOKill();

            if (animator != null) animator.SetBool(IsWalkingHash, true);

            ModelTransform.DOLookAt(waypoints[waypoints.Length - 1], duration, AxisConstraint.Y);

            transform.DOPath(waypoints, duration, PathType.CatmullRom)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    ModelTransform.DOLocalRotate(new Vector3(0f, targetYRotation, 0f), 0.2f).OnComplete(() =>
                    {
                        if (animator != null) animator.SetBool(IsWalkingHash, false);
                        onComplete?.Invoke();
                    });
                });
        }

        public void MoveToEdgeSlot(Vector3 targetPosition, Action onComplete = null)
        {
            SetEdgeStatus(true);
            transform.DOKill();
            ModelTransform.DOKill();

            if (animator != null) animator.SetBool(IsWalkingHash, true);

            Vector3 moveDir = targetPosition - transform.position;
            moveDir.y = 0f;

            if (moveDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                ModelTransform.DORotateQuaternion(targetRotation, 0.15f);
            }

            transform.DOMove(targetPosition, 0.6f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    ModelTransform.DOLocalRotate(Vector3.zero, 0.2f).OnComplete(() =>
                    {
                        if (animator != null) animator.SetBool(IsWalkingHash, false);
                        onComplete?.Invoke();
                    });
                });
        }

        public void ReceiveItem(StackItem stack, Action onComplete)
        {
            IsServed = true;
            SetEdgeStatus(false);
            transform.DOKill();
            ModelTransform.DOKill();

            StartCoroutine(EatAndLeaveRoutine(onComplete));
        }

        private IEnumerator EatAndLeaveRoutine(Action onComplete)
        {
            yield return new WaitForSeconds(0.35f);

            if (animator != null) animator.SetTrigger(EatHash);

            yield return new WaitForSeconds(0.5f);

            if (animator != null) animator.SetTrigger(JumpHash);

            transform.DOScale(Vector3.zero, 0.4f).SetEase(Ease.InBack);
            transform.DOJump(transform.position, 0.5f, 1, 0.4f)
                .OnComplete(() =>
                {
                    onComplete?.Invoke();
                    PoolManager.Instance.Despawn(gameObject);
                });
        }
    }
}
