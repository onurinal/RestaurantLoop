using System;
using System.Collections;
using UnityEngine;
using DG.Tweening;
using RestaurantLoop.Infrastructure; // Added for PoolManager

namespace RestaurantLoop.Core
{
    public class Customer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private OrderBalloon orderBalloon;

        [Header("Animation")]
        [SerializeField] private Animator animator;

        [Header("Data")]
        [SerializeField] private ItemDataSO requiredData;

        public bool IsServed { get; private set; }
        public bool IsEdgeCustomer { get; private set; }
        public ItemDataSO RequiredData => requiredData;

        private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
        private static readonly int EatHash = Animator.StringToHash("Eat");
        private static readonly int JumpHash = Animator.StringToHash("Jump");

        private void Awake()
        {
            if (orderBalloon == null)
            {
                orderBalloon = GetComponentInChildren<OrderBalloon>();
            }
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }

        public void Initialize(ItemDataSO data)
        {
            requiredData = data;
            IsServed = false;
            IsEdgeCustomer = false;
            
            // Reset scale for object pooling since it scales down to zero on exit
            transform.localScale = Vector3.one;

            if (orderBalloon != null && data != null)
            {
                orderBalloon.SetColorAndState(data.UIColor, false);
            }
        }

        public void SetEdgeStatus(bool isEdge)
        {
            IsEdgeCustomer = isEdge;
            if (orderBalloon != null && requiredData != null)
            {
                orderBalloon.SetColorAndState(requiredData.UIColor, isEdge);
            }
        }

        public void MoveAlongPath(Vector3[] waypoints, float duration, bool setAsEdge, Action onComplete = null)
        {
            SetEdgeStatus(setAsEdge);
            transform.DOKill();

            if (animator != null) animator.SetBool(IsWalkingHash, true);

            transform.DOPath(waypoints, duration, PathType.CatmullRom)
                .SetLookAt(0.01f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    if (this != null && transform != null)
                    {
                        if (animator != null) animator.SetBool(IsWalkingHash, false);
                        onComplete?.Invoke();
                    }
                });
        }

        public void MoveToEdgeSlot(Vector3 targetPosition, Action onComplete = null)
        {
            SetEdgeStatus(true);
            transform.DOKill();

            if (animator != null) animator.SetBool(IsWalkingHash, true);

            Vector3 lookPos = targetPosition;
            lookPos.y = transform.position.y;
            transform.DOLookAt(lookPos, 0.2f);

            transform.DOMove(targetPosition, 0.6f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => 
                {
                    if (animator != null) animator.SetBool(IsWalkingHash, false);
                    onComplete?.Invoke();
                });
        }

        public void ReceiveItem(StackItem stack, Action onComplete)
        {
            IsServed = true;
            SetEdgeStatus(false);
            transform.DOKill();

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