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
        [Tooltip("Enables timed-customer generation for this demand in the Single Level Generator.")]
        public bool includeTimedCustomers;
        [Min(0)] public int timedCustomerCount;
        [Tooltip("The Single Generator rounds this to a whole second and applies the first enabled value level-wide.")]
        [Min(1f)] public float timedCustomerDuration;
    }

    [Serializable]
    public struct QueueStackConfig
    {
        public ItemDataSO itemData;
        public int itemCount;
    }

    [Serializable]
    public struct TimedCustomerConfig
    {
        [Tooltip("Zero-based index into Ordered Customer Sequence.")]
        public int customerIndex;
        [Min(0.1f)] public float timeLimitDuration;
    }

    [CreateAssetMenu(fileName = "Level_01", menuName = "RestaurantLoop/Level Data")]
    public class LevelDataSO : ScriptableObject
    {
        public const int FixedRackSlotCount = 5;

        [Header("Active Edge Setup")]
        [Tooltip("Active customer slots around the conveyor belt.")]
        [Range(1, 20)] public int activeEdgeSlotCount = 6;

        /// <summary>Rack capacity is a global gameplay rule and is not level-authored.</summary>
        public int rackSlotCount => FixedRackSlotCount;

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

        [Header("Timed Customer Setup")]
        [Tooltip("Sequence-indexed timers restricted to foods visible in queue rows 1 or 2.")]
        [SerializeField] private List<TimedCustomerConfig> timedCustomers = new List<TimedCustomerConfig>();

        [Header("Queue Initial Stack Setup")]
        public List<QueueStackConfig> queueStackConfigs = new List<QueueStackConfig>();

        public IReadOnlyList<ItemDataSO> OrderedCustomerSequence =>
            orderedCustomerSequence ??= new List<ItemDataSO>();
        public IReadOnlyList<TimedCustomerConfig> TimedCustomers =>
            timedCustomers ??= new List<TimedCustomerConfig>();

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

        public void SetTimedCustomers(IEnumerable<TimedCustomerConfig> configurations)
        {
            timedCustomers ??= new List<TimedCustomerConfig>();
            timedCustomers.Clear();
            if (configurations != null) timedCustomers.AddRange(configurations);
        }

        public bool TryGetTimedCustomer(int customerIndex, out float duration)
        {
            if (timedCustomers != null)
            {
                for (int i = 0; i < timedCustomers.Count; i++)
                {
                    if (timedCustomers[i].customerIndex != customerIndex) continue;
                    duration = timedCustomers[i].timeLimitDuration;
                    return duration > 0f;
                }
            }

            duration = 0f;
            return false;
        }

        public bool ValidateTimedCustomers(out string validationMessage)
        {
            if (timedCustomers == null || timedCustomers.Count == 0)
            {
                validationMessage = "No timed customers configured.";
                return true;
            }

            List<ItemDataSO> resolvedSequence = new List<ItemDataSO>();
            CopyResolvedCustomerSequenceTo(resolvedSequence);
            HashSet<int> configuredIndices = new HashSet<int>();

            for (int i = 0; i < timedCustomers.Count; i++)
            {
                TimedCustomerConfig timed = timedCustomers[i];
                if (timed.customerIndex < 0 || timed.customerIndex >= resolvedSequence.Count)
                {
                    validationMessage = $"Timed customer #{i + 1} references index {timed.customerIndex}, " +
                                        $"but the sequence contains {resolvedSequence.Count} customers.";
                    return false;
                }

                if (timed.timeLimitDuration <= 0f)
                {
                    validationMessage = $"Timed customer at index {timed.customerIndex} requires a positive duration.";
                    return false;
                }

                if (!configuredIndices.Add(timed.customerIndex))
                {
                    validationMessage = $"Sequence index {timed.customerIndex} has more than one timer definition.";
                    return false;
                }

                ItemDataSO requiredItem = resolvedSequence[timed.customerIndex];
                if (!LevelMathUtility.IsItemAccessibleInFirstRows(
                        requiredItem, queueStackConfigs, columnCount, calculatedRowCount, 2))
                {
                    validationMessage = $"Timed customer #{timed.customerIndex + 1} requires " +
                                        $"'{requiredItem?.ItemName}', which does not appear in queue row 1 or 2.";
                    return false;
                }
            }

            validationMessage = $"Validated {timedCustomers.Count} timed customers.";
            return true;
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
