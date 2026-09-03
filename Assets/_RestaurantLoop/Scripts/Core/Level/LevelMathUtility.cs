using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Seeded generation helpers for exact queue grids and explicit customer order.
    /// Demand is immutable: generation either conserves every item exactly or fails.
    /// </summary>
    public static class LevelMathUtility
    {
        public const int StackSizeStep = 5;
        public const int MinimumColumnCount = 2;
        public const int MaximumColumnCount = 4;

        public static bool IsSupportedColumnCount(int columnCount)
        {
            return columnCount >= MinimumColumnCount && columnCount <= MaximumColumnCount;
        }

        /// <summary>
        /// Finds a feasible row count and exact five-step partition. Rows are derived from demand,
        /// stack bounds, and the supported column count instead of being independently authored.
        /// </summary>
        public static bool TryCalculateRowsAndPartition(
            IReadOnlyList<CustomerDemandConfig> demandConfigs,
            int minStackSize,
            int maxStackSize,
            int columnCount,
            int seed,
            out int calculatedRowCount,
            out List<QueueStackConfig> generatedStacks,
            out string error)
        {
            calculatedRowCount = 0;
            generatedStacks = new List<QueueStackConfig>();

            if (!IsSupportedColumnCount(columnCount))
            {
                error = $"Queue columns must be between {MinimumColumnCount} and {MaximumColumnCount}.";
                return false;
            }

            if (!TryAggregateDemands(demandConfigs, out List<ItemDataSO> items,
                    out List<int> demandCounts, out error) ||
                !AreValidStackBounds(minStackSize, maxStackSize, out error))
                return false;

            int totalDemand = 0;
            for (int i = 0; i < demandCounts.Count; i++) totalDemand += demandCounts[i];

            int minimumRows = Mathf.Max(1, Mathf.CeilToInt(items.Count / (float)columnCount));
            int maximumRows = totalDemand / (columnCount * minStackSize);
            if (maximumRows < minimumRows)
            {
                error = "Demand is too small to populate a queue with the selected columns and stack bounds.";
                return false;
            }

            float averageStackSize = (minStackSize + maxStackSize) * 0.5f;
            int preferredRows = Mathf.Clamp(
                Mathf.RoundToInt(totalDemand / (columnCount * averageStackSize)),
                minimumRows,
                maximumRows);

            for (int offset = 0; offset <= maximumRows - minimumRows; offset++)
            {
                int lower = preferredRows - offset;
                if (lower >= minimumRows && TryPartitionDemandToGrid(demandConfigs, minStackSize,
                        maxStackSize, columnCount, lower, seed, out generatedStacks, out _))
                {
                    calculatedRowCount = lower;
                    error = null;
                    return true;
                }

                int upper = preferredRows + offset;
                if (upper != lower && upper <= maximumRows && TryPartitionDemandToGrid(demandConfigs,
                        minStackSize, maxStackSize, columnCount, upper, seed, out generatedStacks, out _))
                {
                    calculatedRowCount = upper;
                    error = null;
                    return true;
                }
            }

            error = "No exact rectangular queue can conserve this demand with the selected stack bounds.";
            return false;
        }

        /// <summary>
        /// Fills an exact columns-by-rows grid with five-step stacks without adding or losing demand.
        /// </summary>
        public static bool TryPartitionDemandToGrid(
            IReadOnlyList<CustomerDemandConfig> demandConfigs,
            int minStackSize,
            int maxStackSize,
            int columnCount,
            int rowCount,
            int seed,
            out List<QueueStackConfig> generatedStacks,
            out string error)
        {
            generatedStacks = new List<QueueStackConfig>();
            if (!TryAggregateDemands(demandConfigs, out List<ItemDataSO> items, out List<int> demandCounts, out error))
                return false;

            if (!AreValidStackBounds(minStackSize, maxStackSize, out error)) return false;
            if (!IsSupportedColumnCount(columnCount) || rowCount <= 0)
            {
                error = $"Queue columns must be {MinimumColumnCount}, 3, or {MaximumColumnCount}; rows must be positive.";
                return false;
            }

            int requiredSlotCount = columnCount * rowCount;
            if (items.Count > requiredSlotCount)
            {
                error = $"The {columnCount}×{rowCount} grid has {requiredSlotCount} slots, but demand contains " +
                        $"{items.Count} food types. Every food type requires at least one stack.";
                return false;
            }

            int[] slotsPerItem = new int[items.Count];
            int[] maximumSlotsPerItem = new int[items.Count];
            int assignedSlotCount = 0;
            int maximumAssignableSlots = 0;

            for (int i = 0; i < items.Count; i++)
            {
                int demand = demandCounts[i];
                if (demand % StackSizeStep != 0)
                {
                    error = $"Demand for '{GetItemName(items[i])}' is {demand}. Exact five-step stacks require " +
                            "each food demand to be divisible by 5.";
                    return false;
                }

                int minimumSlots = Mathf.CeilToInt(demand / (float)maxStackSize);
                int maximumSlots = demand / minStackSize;
                if (minimumSlots > maximumSlots)
                {
                    error = $"Demand for '{GetItemName(items[i])}' ({demand}) cannot be split into stacks " +
                            $"between {minStackSize} and {maxStackSize}.";
                    return false;
                }

                slotsPerItem[i] = minimumSlots;
                maximumSlotsPerItem[i] = maximumSlots;
                assignedSlotCount += minimumSlots;
                maximumAssignableSlots += maximumSlots;
            }

            if (requiredSlotCount < assignedSlotCount || requiredSlotCount > maximumAssignableSlots)
            {
                error = $"The exact {columnCount}×{rowCount} grid requires {requiredSlotCount} stacks, but the fixed " +
                        $"demand supports {assignedSlotCount}–{maximumAssignableSlots} stacks at sizes " +
                        $"{minStackSize}–{maxStackSize}. Adjust rows, columns, or stack bounds.";
                return false;
            }

            System.Random random = new System.Random(seed);
            AllocateRemainingSlots(demandCounts, slotsPerItem, maximumSlotsPerItem, requiredSlotCount, random);

            generatedStacks = new List<QueueStackConfig>(requiredSlotCount);
            for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                AppendExactItemStacks(items[itemIndex], demandCounts[itemIndex], slotsPerItem[itemIndex],
                    minStackSize, maxStackSize, random, generatedStacks);
            }

            Shuffle(generatedStacks, random);
            error = null;
            return true;
        }

        /// <summary>
        /// Legacy auto-row overload retained for integrations. It never mutates customer demand.
        /// </summary>
        public static List<QueueStackConfig> PartitionDemandToStacks(
            List<CustomerDemandConfig> demandConfigs,
            int minStackSize,
            int maxStackSize,
            int columnCount,
            out int calculatedRowCount,
            int seed = 0)
        {
            calculatedRowCount = 0;
            if (!TryGetTotalDemand(demandConfigs, out int totalDemand) ||
                !AreValidStackBounds(minStackSize, maxStackSize, out _))
                return new List<QueueStackConfig>();

            if (!IsSupportedColumnCount(columnCount)) return new List<QueueStackConfig>();
            float averageStackSize = (minStackSize + maxStackSize) * 0.5f;
            int preferredRows = Mathf.Max(1, Mathf.RoundToInt(totalDemand / (columnCount * averageStackSize)));
            int maximumRows = Mathf.Max(1, totalDemand / (columnCount * minStackSize));

            for (int offset = 0; offset <= maximumRows; offset++)
            {
                int lowerRows = preferredRows - offset;
                if (lowerRows > 0 && TryPartitionDemandToGrid(demandConfigs, minStackSize, maxStackSize,
                        columnCount, lowerRows, seed, out List<QueueStackConfig> lowerStacks, out _))
                {
                    calculatedRowCount = lowerRows;
                    return lowerStacks;
                }

                int upperRows = preferredRows + offset;
                if (upperRows != lowerRows && upperRows <= maximumRows && TryPartitionDemandToGrid(
                        demandConfigs, minStackSize, maxStackSize, columnCount, upperRows, seed,
                        out List<QueueStackConfig> upperStacks, out _))
                {
                    calculatedRowCount = upperRows;
                    return upperStacks;
                }
            }

            return new List<QueueStackConfig>();
        }

        public static List<ItemDataSO> GenerateDeterministicCustomerSequence(
            List<CustomerDemandConfig> demands,
            int seed)
        {
            List<ItemDataSO> items = new List<ItemDataSO>();
            List<int> remainingCounts = new List<int>();
            AggregateValidDemands(demands, items, remainingCounts);

            int totalCount = 0;
            for (int i = 0; i < remainingCounts.Count; i++) totalCount += remainingCounts[i];

            List<ItemDataSO> sequence = new List<ItemDataSO>(totalCount);
            List<int> activeIndices = new List<int>(items.Count);
            System.Random random = new System.Random(seed);
            ItemDataSO lastItem = null;

            while (sequence.Count < totalCount)
            {
                activeIndices.Clear();
                for (int i = 0; i < remainingCounts.Count; i++)
                {
                    if (remainingCounts[i] > 0) activeIndices.Add(i);
                }

                Shuffle(activeIndices, random);
                AvoidImmediateRepeat(activeIndices, items, lastItem);

                for (int i = 0; i < activeIndices.Count; i++)
                {
                    int itemIndex = activeIndices[i];
                    if (remainingCounts[itemIndex] <= 0) continue;

                    ItemDataSO item = items[itemIndex];
                    sequence.Add(item);
                    remainingCounts[itemIndex]--;
                    lastItem = item;
                }
            }

            return sequence;
        }

        /// <summary>Checks the first accessible rows in the column-major queue layout.</summary>
        public static bool IsItemAccessibleInFirstRows(
            ItemDataSO item,
            IReadOnlyList<QueueStackConfig> stacks,
            int columnCount,
            int rowCount,
            int accessibleRowCount = 2)
        {
            if (item == null || stacks == null || columnCount <= 0 || rowCount <= 0) return false;

            int rowsToInspect = Mathf.Min(rowCount, Mathf.Max(1, accessibleRowCount));
            for (int column = 0; column < columnCount; column++)
            {
                int columnStart = column * rowCount;
                for (int row = 0; row < rowsToInspect; row++)
                {
                    int stackIndex = columnStart + row;
                    if (stackIndex < stacks.Count && stacks[stackIndex].itemData == item) return true;
                }
            }

            return false;
        }

        private static void AllocateRemainingSlots(
            IReadOnlyList<int> demandCounts,
            int[] slotsPerItem,
            IReadOnlyList<int> maximumSlotsPerItem,
            int targetSlotCount,
            System.Random random)
        {
            int assigned = 0;
            for (int i = 0; i < slotsPerItem.Length; i++) assigned += slotsPerItem[i];

            List<int> candidates = new List<int>(slotsPerItem.Length);
            while (assigned < targetSlotCount)
            {
                candidates.Clear();
                float highestAverage = float.MinValue;
                for (int i = 0; i < slotsPerItem.Length; i++)
                {
                    if (slotsPerItem[i] >= maximumSlotsPerItem[i]) continue;
                    float average = demandCounts[i] / (float)slotsPerItem[i];
                    if (average > highestAverage + 0.001f)
                    {
                        candidates.Clear();
                        highestAverage = average;
                    }

                    if (Mathf.Abs(average - highestAverage) <= 0.001f) candidates.Add(i);
                }

                int selected = candidates[random.Next(candidates.Count)];
                slotsPerItem[selected]++;
                assigned++;
            }
        }

        private static void AppendExactItemStacks(
            ItemDataSO item,
            int totalDemand,
            int stackCount,
            int minStackSize,
            int maxStackSize,
            System.Random random,
            List<QueueStackConfig> destination)
        {
            int[] sizes = new int[stackCount];
            for (int i = 0; i < stackCount; i++) sizes[i] = minStackSize;

            int remaining = totalDemand - stackCount * minStackSize;
            List<int> candidates = new List<int>(stackCount);
            while (remaining > 0)
            {
                candidates.Clear();
                for (int i = 0; i < sizes.Length; i++)
                {
                    if (sizes[i] < maxStackSize) candidates.Add(i);
                }

                Shuffle(candidates, random);
                for (int i = 0; i < candidates.Count && remaining > 0; i++)
                {
                    sizes[candidates[i]] += StackSizeStep;
                    remaining -= StackSizeStep;
                }
            }

            for (int i = 0; i < sizes.Length; i++)
            {
                destination.Add(new QueueStackConfig { itemData = item, itemCount = sizes[i] });
            }
        }

        private static bool TryAggregateDemands(
            IReadOnlyList<CustomerDemandConfig> demands,
            out List<ItemDataSO> items,
            out List<int> counts,
            out string error)
        {
            items = new List<ItemDataSO>();
            counts = new List<int>();
            if (demands == null || demands.Count == 0)
            {
                error = "Add at least one customer demand configuration.";
                return false;
            }

            for (int i = 0; i < demands.Count; i++)
            {
                CustomerDemandConfig demand = demands[i];
                if (demand.itemData == null || demand.totalCustomerCount <= 0)
                {
                    error = $"Customer demand #{i + 1} requires an item and a positive count.";
                    return false;
                }

                int existingIndex = items.IndexOf(demand.itemData);
                if (existingIndex >= 0) counts[existingIndex] += demand.totalCustomerCount;
                else
                {
                    items.Add(demand.itemData);
                    counts.Add(demand.totalCustomerCount);
                }
            }

            error = null;
            return true;
        }

        private static void AggregateValidDemands(
            IReadOnlyList<CustomerDemandConfig> demands,
            List<ItemDataSO> items,
            List<int> counts)
        {
            if (demands == null) return;
            for (int i = 0; i < demands.Count; i++)
            {
                CustomerDemandConfig demand = demands[i];
                if (demand.itemData == null || demand.totalCustomerCount <= 0) continue;

                int existingIndex = items.IndexOf(demand.itemData);
                if (existingIndex >= 0) counts[existingIndex] += demand.totalCustomerCount;
                else
                {
                    items.Add(demand.itemData);
                    counts.Add(demand.totalCustomerCount);
                }
            }
        }

        private static bool TryGetTotalDemand(IReadOnlyList<CustomerDemandConfig> demands, out int totalDemand)
        {
            totalDemand = 0;
            if (demands == null || demands.Count == 0) return false;
            for (int i = 0; i < demands.Count; i++)
            {
                if (demands[i].itemData == null || demands[i].totalCustomerCount <= 0) return false;
                totalDemand += demands[i].totalCustomerCount;
            }

            return totalDemand > 0;
        }

        private static bool AreValidStackBounds(int minStackSize, int maxStackSize, out string error)
        {
            if (minStackSize < StackSizeStep || maxStackSize < minStackSize ||
                minStackSize % StackSizeStep != 0 || maxStackSize % StackSizeStep != 0)
            {
                error = $"Stack bounds must be multiples of {StackSizeStep}, with minimum at least " +
                        $"{StackSizeStep} and maximum greater than or equal to minimum.";
                return false;
            }

            error = null;
            return true;
        }

        private static string GetItemName(ItemDataSO item)
        {
            return item != null && !string.IsNullOrEmpty(item.ItemName)
                ? item.ItemName
                : item != null ? item.name : "Unknown";
        }

        private static void AvoidImmediateRepeat(List<int> indices, List<ItemDataSO> items, ItemDataSO lastItem)
        {
            if (lastItem == null || indices.Count < 2 || items[indices[0]] != lastItem) return;
            for (int i = 1; i < indices.Count; i++)
            {
                if (items[indices[i]] == lastItem) continue;
                (indices[0], indices[i]) = (indices[i], indices[0]);
                return;
            }
        }

        private static void Shuffle<T>(List<T> values, System.Random random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int selectedIndex = random.Next(i + 1);
                (values[i], values[selectedIndex]) = (values[selectedIndex], values[i]);
            }
        }
    }
}
