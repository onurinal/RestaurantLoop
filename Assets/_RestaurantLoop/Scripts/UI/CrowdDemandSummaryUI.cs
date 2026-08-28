using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RestaurantLoop.Core;

namespace RestaurantLoop.UI
{
    public class CrowdDemandSummaryUI : MonoBehaviour
    {
        [Header("Prefab & Layout References")]
        [SerializeField] private CrowdDemandBadgeUI badgePrefab;
        [SerializeField] private RectTransform badgeContainer;

        private readonly Dictionary<ItemDataSO, CrowdDemandBadgeUI> activeBadges = new Dictionary<ItemDataSO, CrowdDemandBadgeUI>();
        private bool isSubscribed = false;

        private void OnEnable()
        {
            TrySubscribe();
            RefreshSummary();
        }

        private void Start()
        {
            TrySubscribe();
            RefreshSummary();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void TrySubscribe()
        {
            if (isSubscribed || CrowdManager.Instance == null) return;
            CrowdManager.Instance.OnDemandChanged += HandleDemandChanged;
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed || CrowdManager.Instance == null) return;
            CrowdManager.Instance.OnDemandChanged -= HandleDemandChanged;
            isSubscribed = false;
        }

        private void HandleDemandChanged(int totalDemand, Dictionary<ItemDataSO, int> demandPerType)
        {
            RefreshSummary();
        }

        public void RefreshSummary()
        {
            if (CrowdManager.Instance == null) return;

            Dictionary<ItemDataSO, int> levelDemands = CrowdManager.Instance.GetRemainingLevelDemands();

            foreach (var kvp in levelDemands)
            {
                ItemDataSO data = kvp.Key;
                int count = kvp.Value;

                if (!activeBadges.ContainsKey(data))
                {
                    if (count > 0 && badgePrefab != null && badgeContainer != null)
                    {
                        CrowdDemandBadgeUI newBadge = Instantiate(badgePrefab, badgeContainer);
                        newBadge.Setup(data, count);
                        activeBadges[data] = newBadge;
                    }
                }
                else
                {
                    activeBadges[data].UpdateCount(count);
                }
            }

            if (badgeContainer != null)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(badgeContainer);
            }
        }
    }
}