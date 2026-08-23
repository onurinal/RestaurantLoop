using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Mathematical utility for partitioning customer demand into solvable 5-step stack sizes and calculating layout bounds.
    /// </summary>
    public static class LevelMathUtility
    {
        private const int StepSize = 5;

        /// <summary>
        /// Partitions total customer demand into 5-step quantized stack sizes (e.g., 10, 15, 20), shuffles them,
        /// and calculates required layout row depth.
        /// </summary>
        public static List<QueueStackConfig> PartitionDemandToStacks(
            List<StationLevelConfig> stationConfigs,
            int minStackSize,
            int maxStackSize,
            int columnCount,
            out int calculatedRowCount)
        {
            List<QueueStackConfig> generatedStacks = new List<QueueStackConfig>();

            // Snap min and max boundaries strictly to multiples of 5
            minStackSize = Mathf.Max(StepSize, Mathf.RoundToInt((float)minStackSize / StepSize) * StepSize);
            maxStackSize = Mathf.Max(minStackSize, Mathf.RoundToInt((float)maxStackSize / StepSize) * StepSize);

            foreach (var station in stationConfigs)
            {
                int remainingDemand = station.totalCustomerCount;

                while (remainingDemand > 0)
                {
                    if (remainingDemand <= minStackSize)
                    {
                        generatedStacks.Add(new QueueStackConfig
                        {
                            itemData = station.itemData,
                            itemCount = remainingDemand
                        });
                        remainingDemand = 0;
                        break;
                    }

                    int maxPossible = Mathf.Min(remainingDemand, maxStackSize);
                    int minPossible = minStackSize;

                    int minSteps = minPossible / StepSize;
                    int maxSteps = maxPossible / StepSize;

                    // Pick random step multiplier of 5
                    int chosenSteps = Random.Range(minSteps, maxSteps + 1);
                    int stackSize = chosenSteps * StepSize;

                    // Guard against leaving remainders smaller than minStackSize
                    int leftover = remainingDemand - stackSize;
                    if (leftover > 0 && leftover < minStackSize)
                    {
                        if (stackSize + leftover <= maxStackSize)
                        {
                            stackSize += leftover;
                        }
                        else
                        {
                            stackSize = remainingDemand - minStackSize;
                        }
                    }

                    remainingDemand -= stackSize;

                    generatedStacks.Add(new QueueStackConfig
                    {
                        itemData = station.itemData,
                        itemCount = stackSize
                    });
                }
            }

            // Fisher-Yates Shuffle
            for (int i = generatedStacks.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);
                var temp = generatedStacks[i];
                generatedStacks[i] = generatedStacks[randomIndex];
                generatedStacks[randomIndex] = temp;
            }

            calculatedRowCount = Mathf.CeilToInt((float)generatedStacks.Count / columnCount);
            return generatedStacks;
        }
    }
}