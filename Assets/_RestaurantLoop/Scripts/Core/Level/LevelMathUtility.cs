using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Seeded generation helpers for queue stacks and explicit customer order.
    /// Guarantees exact customer demand conservation and perfectly aligned rectangular queue grids.
    /// </summary>
    public static class LevelMathUtility
    {
        private const int StepSize = 5;

        public static List<QueueStackConfig> PartitionDemandToStacks(
            List<CustomerDemandConfig> demandConfigs,
            int minStackSize,
            int maxStackSize,
            int columnCount,
            out int calculatedRowCount,
            int seed = 0)
        {
            List<CustomerDemandConfig> validDemands = GetValidDemands(demandConfigs);
            System.Random random = new System.Random(seed);

            columnCount = Mathf.Max(1, columnCount);
            minStackSize = Mathf.Max(StepSize, Mathf.RoundToInt((float)minStackSize / StepSize) * StepSize);
            maxStackSize = Mathf.Max(minStackSize, Mathf.RoundToInt((float)maxStackSize / StepSize) * StepSize);

            // Calculate total raw demand
            int totalRawDemand = 0;
            for (int i = 0; i < validDemands.Count; i++) totalRawDemand += validDemands[i].totalCustomerCount;

            // Determine target stack count (must be a multiple of columnCount and >= food types count)
            int minRequiredStacks = Mathf.Max(columnCount, validDemands.Count);
            int targetRows = Mathf.Max(1, Mathf.RoundToInt((float)totalRawDemand / (columnCount * minStackSize)));
            int targetTotalStacks = targetRows * columnCount;

            if (targetTotalStacks < minRequiredStacks)
            {
                targetTotalStacks = Mathf.CeilToInt((float)minRequiredStacks / columnCount) * columnCount;
                targetRows = targetTotalStacks / columnCount;
            }

            // Distribute target stacks among available food types
            List<QueueStackConfig> generatedStacks = new List<QueueStackConfig>();
            int[] stacksPerFood = new int[validDemands.Count];
            int assignedStacks = 0;

            // Give at least 1 stack to each food type
            for (int i = 0; i < validDemands.Count; i++)
            {
                stacksPerFood[i] = 1;
                assignedStacks++;
            }

            // Distribute remaining stack quota based on demand weight
            while (assignedStacks < targetTotalStacks)
            {
                int maxDemandIndex = 0;
                float maxRatio = -1f;

                for (int i = 0; i < validDemands.Count; i++)
                {
                    float ratio = (float)validDemands[i].totalCustomerCount / stacksPerFood[i];
                    if (ratio > maxRatio)
                    {
                        maxRatio = ratio;
                        maxDemandIndex = i;
                    }
                }

                stacksPerFood[maxDemandIndex]++;
                assignedStacks++;
            }

            // Build exact stack sizes matching StepSize constraints
            for (int i = 0; i < validDemands.Count; i++)
            {
                CustomerDemandConfig demand = validDemands[i];
                int totalFoodDemand = demand.totalCustomerCount;
                int foodStacksCount = stacksPerFood[i];

                int baseStackSize = Mathf.Max(StepSize, (totalFoodDemand / foodStacksCount / StepSize) * StepSize);
                int currentSum = 0;

                for (int s = 0; s < foodStacksCount; s++)
                {
                    int stackSize = (s == foodStacksCount - 1)
                        ? Mathf.Max(StepSize, totalFoodDemand - currentSum)
                        : baseStackSize;

                    stackSize = Mathf.Clamp((stackSize / StepSize) * StepSize, StepSize, maxStackSize);
                    currentSum += stackSize;

                    generatedStacks.Add(new QueueStackConfig
                    {
                        itemData = demand.itemData,
                        itemCount = stackSize
                    });
                }

                // Update demand config count to match adjusted stack totals exactly
                demandConfigs[i] = new CustomerDemandConfig
                {
                    itemData = demand.itemData,
                    totalCustomerCount = currentSum
                };
            }

            Shuffle(generatedStacks, random);
            calculatedRowCount = targetRows;
            return generatedStacks;
        }

        public static List<ItemDataSO> GenerateDeterministicCustomerSequence(
            List<CustomerDemandConfig> demands,
            int seed)
        {
            List<ItemDataSO> items = new List<ItemDataSO>();
            List<int> remainingCounts = new List<int>();
            AggregateDemands(demands, items, remainingCounts);

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

        private static List<CustomerDemandConfig> GetValidDemands(List<CustomerDemandConfig> demands)
        {
            List<CustomerDemandConfig> validDemands = new List<CustomerDemandConfig>();
            if (demands == null) return validDemands;

            for (int i = 0; i < demands.Count; i++)
            {
                CustomerDemandConfig demand = demands[i];
                if (demand.itemData != null && demand.totalCustomerCount > 0) validDemands.Add(demand);
            }

            return validDemands;
        }

        private static void AggregateDemands(
            List<CustomerDemandConfig> demands,
            List<ItemDataSO> items,
            List<int> counts)
        {
            if (demands == null) return;

            for (int i = 0; i < demands.Count; i++)
            {
                CustomerDemandConfig demand = demands[i];
                if (demand.itemData == null || demand.totalCustomerCount <= 0) continue;

                int existingIndex = items.IndexOf(demand.itemData);
                if (existingIndex >= 0)
                {
                    counts[existingIndex] += demand.totalCustomerCount;
                }
                else
                {
                    items.Add(demand.itemData);
                    counts.Add(demand.totalCustomerCount);
                }
            }
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
                int selectedIndex = random.Next(0, i + 1);
                (values[i], values[selectedIndex]) = (values[selectedIndex], values[i]);
            }
        }
    }
}