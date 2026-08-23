using System;
using UnityEngine;
using DG.Tweening;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Manages customer state, visual data binding via OrderBalloon, clean position movement, and safe exit callbacks.
    /// </summary>
    public class Customer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private OrderBalloon orderBalloon;

        [Header("Data")]
        [SerializeField] private ItemDataSO requiredData;

        public bool IsServed { get; private set; }
        public bool IsEdgeCustomer { get; private set; }
        public ItemDataSO RequiredData => requiredData;

        private void Awake()
        {
            if (orderBalloon == null)
            {
                orderBalloon = GetComponentInChildren<OrderBalloon>();
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

        /// <summary>
        /// Moves customer along entrance waypoints via CatmullRom spline position tweening.
        /// </summary>
        public void MoveAlongPath(Vector3[] waypoints, float duration, bool setAsEdge, Action onComplete = null)
        {
            SetEdgeStatus(setAsEdge);
            transform.DOKill();

            transform.DOPath(waypoints, duration, PathType.CatmullRom)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    if (this != null && transform != null)
                    {
                        onComplete?.Invoke();
                    }
                });
        }

        /// <summary>
        /// Moves customer from table to target edge slot position.
        /// </summary>
        public void MoveToEdgeSlot(Vector3 targetPosition, Action onComplete = null)
        {
            SetEdgeStatus(true);
            transform.DOKill();

            transform.DOMove(targetPosition, 0.6f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => onComplete?.Invoke());
        }

        /// <summary>
        /// Handles item reception, performs exit scale-down animation, and defers slot release until customer fully vanishes.
        /// </summary>
        public void ReceiveItem(StackItem stack, Action onComplete)
        {
            IsServed = true;
            SetEdgeStatus(false);
            transform.DOKill();

            transform.DOScale(Vector3.zero, 0.4f)
                .SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    onComplete?.Invoke();
                    Destroy(gameObject);
                });
        }
    }
}