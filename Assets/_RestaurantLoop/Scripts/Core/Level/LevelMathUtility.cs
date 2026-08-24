using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Mathematical utility for partitioning customer demand into solvable 5-step stack sizes and layout bounds.
    /// Ensures perfectly balanced rectangular queue grids.
    /// </summary>
    public static class LevelMathUtility
    {
        private const int StepSize = 5;

        public static List<QueueStackConfig> PartitionDemandToStacks(
            List<CustomerDemandConfig> demandConfigs,
            int minStackSize,
            int maxStackSize,
            int columnCount,
            out int calculatedRowCount)
        {
            List<QueueStackConfig> generatedStacks = new List<QueueStackConfig>();

            minStackSize = Mathf.Max(StepSize, Mathf.RoundToInt((float)minStackSize / StepSize) * StepSize);
            maxStackSize = Mathf.Max(minStackSize, Mathf.RoundToInt((float)maxStackSize / StepSize) * StepSize);

            foreach (var demand in demandConfigs)
            {
                int remainingDemand = demand.totalCustomerCount;

                while (remainingDemand > 0)
                {
                    if (remainingDemand <= minStackSize)
                    {
                        generatedStacks.Add(new QueueStackConfig
                        {
                            itemData = demand.itemData,
                            itemCount = remainingDemand
                        });
                        remainingDemand = 0;
                        break;
                    }

                    int maxPossible = Mathf.Min(remainingDemand, maxStackSize);
                    int minPossible = minStackSize;

                    int minSteps = minPossible / StepSize;
                    int maxSteps = maxPossible / StepSize;

                    int chosenSteps = Random.Range(minSteps, maxSteps + 1);
                    int stackSize = chosenSteps * StepSize;

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
                        itemData = demand.itemData,
                        itemCount = stackSize
                    });
                }
            }

            int remainder = generatedStacks.Count % columnCount;
            if (remainder != 0)
            {
                int extraStacksNeeded = columnCount - remainder;

                for (int i = 0; i < extraStacksNeeded; i++)
                {
                    var randomDemandType = demandConfigs[Random.Range(0, demandConfigs.Count)];

                    int minSteps = minStackSize / StepSize;
                    int maxSteps = maxStackSize / StepSize;
                    int chosenSteps = Random.Range(minSteps, maxSteps + 1);
                    int paddingStackSize = chosenSteps * StepSize;

                    generatedStacks.Add(new QueueStackConfig
                    {
                        itemData = randomDemandType.itemData,
                        itemCount = paddingStackSize
                    });
                }
            }

            for (int i = generatedStacks.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);
                var temp = generatedStacks[i];
                generatedStacks[i] = generatedStacks[randomIndex];
                generatedStacks[randomIndex] = temp;
            }

            calculatedRowCount = generatedStacks.Count / columnCount;
            return generatedStacks;
        }
    }
}