using System;
using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Controls customer order visualization, movement from central stations to edge slots, and service animations.
    /// </summary>
    public class Customer : MonoBehaviour
    {
        [Header("Data & References")]
        [SerializeField] private ItemDataSO requiredData;
        [SerializeField] private OrderBalloon balloon;

        public ItemDataSO RequiredData => requiredData;
        public bool IsServed { get; private set; }
        public bool IsEdgeCustomer { get; private set; }

        private void Awake()
        {
            balloon = GetComponentInChildren<OrderBalloon>(true);
        }

        public void Initialize(ItemDataSO data)
        {
            requiredData = data;
            UpdateBalloonVisual();
        }

        public void MoveToEdgeSlot(Vector3 targetPosition, Action onArrived)
        {
            SetEdgeStatus(false);
            transform.DOMove(targetPosition, 0.5f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                SetEdgeStatus(true);
                onArrived?.Invoke();
            });
        }

        public void SetEdgeStatus(bool onEdge)
        {
            IsEdgeCustomer = onEdge;
            UpdateBalloonVisual();
        }

        private void UpdateBalloonVisual()
        {
            if (balloon == null)
            {
                balloon = GetComponentInChildren<OrderBalloon>();
            }

            if (balloon != null && requiredData != null)
            {
                balloon.SetColorAndState(requiredData.UIColor, IsEdgeCustomer);
            }
        }

        public void ReceiveItem(StackItem stack, Action onComplete)
        {
            IsServed = true;
            StartCoroutine(EatAndLeaveRoutine(onComplete));
        }

        private IEnumerator EatAndLeaveRoutine(Action onComplete)
        {
            yield return new WaitForSeconds(0.35f);
            yield return new WaitForSeconds(0.4f);

            transform.DOJump(transform.position, 0.6f, 1, 0.3f).OnComplete(() =>
            {
                onComplete?.Invoke();
                Destroy(gameObject);
            });
        }
    }
}