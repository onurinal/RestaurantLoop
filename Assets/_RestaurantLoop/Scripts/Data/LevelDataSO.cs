using System;
using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Core
{
    [Serializable]
    public struct CustomerDemandConfig
    {
        public ItemDataSO itemData;
        public int totalCustomerCount;
    }

    [Serializable]
    public struct QueueStackConfig
    {
        public ItemDataSO itemData;
        public int itemCount;
    }

    [CreateAssetMenu(fileName = "Level_01", menuName = "RestaurantLoop/Level Data")]
    public class LevelDataSO : ScriptableObject
    {
        [Header("Active Edge Setup")]
        [Tooltip("Active customer slots around the conveyor belt.")]
        [Range(3, 20)] public int activeEdgeSlotCount = 6;

        [Header("Rack Setup")]
        [Range(1, 10)] public int rackSlotCount = 5;

        [Header("Queue Layout Setup")]
        [Range(1, 8)] public int columnCount = 3;
        public int calculatedRowCount = 3;

        [Header("Queue Stack Size Constraints")]
        public int minStackSize = 10;
        public int maxStackSize = 40;

        [Header("Conveyor Settings")]
        public float conveyorSpeedMultiplier = 1.0f;

        [Header("Customer Demand Setup")]
        public List<CustomerDemandConfig> customerDemands = new List<CustomerDemandConfig>();

        [Header("Deterministic Customer Order")]
        [Tooltip("Exact runtime customer order. Leave empty to use customerDemands in Inspector order as a deterministic legacy fallback.")]
        [SerializeField] private List<ItemDataSO> orderedCustomerSequence = new List<ItemDataSO>();

        [Header("Queue Initial Stack Setup")]
        public List<QueueStackConfig> queueStackConfigs = new List<QueueStackConfig>();

        public IReadOnlyList<ItemDataSO> OrderedCustomerSequence =>
            orderedCustomerSequence ??= new List<ItemDataSO>();

        public int TotalCustomerDemand
        {
            get
            {
                int total = 0;
                if (customerDemands == null) return total;
                foreach (var c in customerDemands) total += Mathf.Max(0, c.totalCustomerCount);
                return total;
            }
        }

        public int TotalQueueItems
        {
            get
            {
                int total = 0;
                if (queueStackConfigs == null) return total;
                foreach (var q in queueStackConfigs) total += Mathf.Max(0, q.itemCount);
                return total;
            }
        }

        /// <summary>Replaces the explicit customer order without exposing the mutable backing list.</summary>
        public void SetOrderedCustomerSequence(IEnumerable<ItemDataSO> sequence)
        {
            orderedCustomerSequence ??= new List<ItemDataSO>();
            orderedCustomerSequence.Clear();
            if (sequence != null) orderedCustomerSequence.AddRange(sequence);
        }

        /// <summary>
        /// Rebuilds the explicit order by expanding customerDemands in their current Inspector order.
        /// This is deterministic and useful when migrating existing level assets.
        /// </summary>
        public void SyncOrderedCustomerSequenceWithDemands()
        {
            orderedCustomerSequence ??= new List<ItemDataSO>();
            orderedCustomerSequence.Clear();
            AppendDemandOrder(orderedCustomerSequence);
        }

        /// <summary>Validates count, null entries, and per-item totals against customerDemands.</summary>
        public bool ValidateOrderedCustomerSequence(out string validationMessage)
        {
            int expectedCount = TotalCustomerDemand;
            int actualCount = orderedCustomerSequence != null ? orderedCustomerSequence.Count : 0;
            if (actualCount != expectedCount)
            {
                validationMessage = $"Customer sequence contains {actualCount} entries; expected {expectedCount}.";
                return false;
            }

            Dictionary<ItemDataSO, int> expectedCounts = new Dictionary<ItemDataSO, int>();
            if (customerDemands != null)
            {
                for (int i = 0; i < customerDemands.Count; i++)
                {
                    CustomerDemandConfig demand = customerDemands[i];
                    if (demand.itemData == null || demand.totalCustomerCount < 0)
                    {
                        validationMessage = $"Customer demand #{i + 1} has an invalid item or count.";
                        return false;
                    }

                    expectedCounts.TryGetValue(demand.itemData, out int currentCount);
                    expectedCounts[demand.itemData] = currentCount + demand.totalCustomerCount;
                }
            }

            Dictionary<ItemDataSO, int> actualCounts = new Dictionary<ItemDataSO, int>();
            for (int i = 0; i < actualCount; i++)
            {
                ItemDataSO item = orderedCustomerSequence[i];
                if (item == null)
                {
                    validationMessage = $"Customer sequence entry #{i + 1} is empty.";
                    return false;
                }

                actualCounts.TryGetValue(item, out int currentCount);
                actualCounts[item] = currentCount + 1;
            }

            foreach (KeyValuePair<ItemDataSO, int> expected in expectedCounts)
            {
                if (!actualCounts.TryGetValue(expected.Key, out int actual) || actual != expected.Value)
                {
                    validationMessage = $"Customer sequence count for '{expected.Key.ItemName}' is {actual}; expected {expected.Value}.";
                    return false;
                }
            }

            if (actualCounts.Count != expectedCounts.Count)
            {
                validationMessage = "Customer sequence contains an item that is not present in customerDemands.";
                return false;
            }

            validationMessage = "Customer sequence matches all configured demand.";
            return true;
        }

        /// <summary>
        /// Copies the runtime order into a caller-owned list. Existing assets with no explicit
        /// sequence fall back to expanding customerDemands in deterministic Inspector order.
        /// </summary>
        public void CopyResolvedCustomerSequenceTo(List<ItemDataSO> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            destination.Clear();
            if (orderedCustomerSequence != null && orderedCustomerSequence.Count > 0)
            {
                destination.AddRange(orderedCustomerSequence);
                return;
            }

            AppendDemandOrder(destination);
        }

        private void AppendDemandOrder(List<ItemDataSO> destination)
        {
            if (customerDemands == null) return;

            for (int demandIndex = 0; demandIndex < customerDemands.Count; demandIndex++)
            {
                CustomerDemandConfig demand = customerDemands[demandIndex];
                for (int count = 0; count < Mathf.Max(0, demand.totalCustomerCount); count++)
                {
                    destination.Add(demand.itemData);
                }
            }
        }
    }
}
