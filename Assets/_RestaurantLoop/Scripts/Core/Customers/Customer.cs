using System;
using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Controls customer order visualization, edge states, and service animations with auto-binding.
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

            // DİKKAT: Ana kuleyi (stack) zıplatıp silme kodlarını kaldırdık!
            // Uçma işlemini artık StackItem.cs kendi içindeki kopan 'thrownItem' ile yapıyor.
            
            // Sadece bekleme ve yeme animasyonunu başlat
            StartCoroutine(EatAndLeaveRoutine(onComplete));
        }

        private IEnumerator EatAndLeaveRoutine(Action onComplete)
        {
            // 1. Tabağın havada kavis çizip gelme süresini (0.35 saniye) bekle
            yield return new WaitForSeconds(0.35f);

            // 2. Müşterinin tabağı yeme (nom-nom) süresi (0.4 saniye)
            yield return new WaitForSeconds(0.4f);

            // 3. Yeme işi bitti, zıplayarak masadan ayrıl
            transform.DOJump(transform.position, 0.6f, 1, 0.3f).OnComplete(() =>
            {
                onComplete?.Invoke();
                Destroy(gameObject);
            });
        }
    }
}