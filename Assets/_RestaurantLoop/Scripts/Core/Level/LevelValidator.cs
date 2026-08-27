using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Greedy simulator utility that plays through a LevelDataSO asset step-by-step
    /// to guarantee that the level is 100% mathematically solvable without rack overflow.
    /// </summary>
    public static class LevelValidator
    {
        public static bool ValidateLevel(LevelDataSO levelData)
        {
            if (levelData == null || levelData.customerDemands == null || levelData.queueStackConfigs == null)
                return false;

            // 1. Demand & Queue Item Conservation Check
            if (levelData.TotalCustomerDemand != levelData.TotalQueueItems)
                return false;

            // 2. Setup simulation state
            List<QueueStackConfig> queue = new List<QueueStackConfig>(levelData.queueStackConfigs);
            List<ItemDataSO> unspawnedDemand = new List<ItemDataSO>();

            foreach (var demand in levelData.customerDemands)
            {
                for (int i = 0; i < demand.totalCustomerCount; i++)
                    unspawnedDemand.Add(demand.itemData);
            }

            List<ItemDataSO> activeEdgeSlots = new List<ItemDataSO>();
            int maxEdgeSlots = levelData.activeEdgeSlotCount;

            // Populate initial active edge slots
            while (activeEdgeSlots.Count < maxEdgeSlots && unspawnedDemand.Count > 0)
            {
                activeEdgeSlots.Add(unspawnedDemand[0]);
                unspawnedDemand.RemoveAt(0);
            }

            List<QueueStackConfig> rackSlots = new List<QueueStackConfig>();
            int maxRackSlots = levelData.rackSlotCount;

            int maxIterations = 1000;
            int currentIteration = 0;

            // 3. Run greedy playthrough simulation
            while ((queue.Count > 0 || rackSlots.Count > 0) && currentIteration < maxIterations)
            {
                currentIteration++;
                bool actionTaken = false;

                // Priority A: Try sending matching stacks from the Rack to the belt
                for (int r = 0; r < rackSlots.Count; r++)
                {
                    if (CanFulfillDemand(rackSlots[r].itemData, activeEdgeSlots))
                    {
                        QueueStackConfig item = rackSlots[r];
                        rackSlots.RemoveAt(r);
                        ProcessItemOnBelt(ref item, activeEdgeSlots, unspawnedDemand);

                        if (item.itemCount > 0)
                        {
                            if (rackSlots.Count >= maxRackSlots) return false;
                            rackSlots.Add(item);
                        }

                        actionTaken = true;
                        break;
                    }
                }

                if (actionTaken) continue;

                // Priority B: Try sending matching front queue items to the belt
                int columnCount = levelData.columnCount;
                int frontRowLimit = Mathf.Min(columnCount, queue.Count);

                for (int q = 0; q < frontRowLimit; q++)
                {
                    if (CanFulfillDemand(queue[q].itemData, activeEdgeSlots))
                    {
                        QueueStackConfig item = queue[q];
                        queue.RemoveAt(q);
                        ProcessItemOnBelt(ref item, activeEdgeSlots, unspawnedDemand);

                        if (item.itemCount > 0)
                        {
                            if (rackSlots.Count >= maxRackSlots) return false;
                            rackSlots.Add(item);
                        }

                        actionTaken = true;
                        break;
                    }
                }

                // Priority C: If no direct demand match exists, send the first front item to cycle the belt
                if (!actionTaken && queue.Count > 0)
                {
                    if (rackSlots.Count >= maxRackSlots) return false;

                    QueueStackConfig item = queue[0];
                    queue.RemoveAt(0);
                    ProcessItemOnBelt(ref item, activeEdgeSlots, unspawnedDemand);

                    if (item.itemCount > 0)
                    {
                        rackSlots.Add(item);
                    }

                    actionTaken = true;
                }

                if (!actionTaken) break;
            }

            return activeEdgeSlots.Count == 0 && unspawnedDemand.Count == 0 && queue.Count == 0;
        }

        private static bool CanFulfillDemand(ItemDataSO data, List<ItemDataSO> edgeSlots)
        {
            return edgeSlots.Contains(data);
        }

        private static void ProcessItemOnBelt(ref QueueStackConfig stack, List<ItemDataSO> edgeSlots, List<ItemDataSO> unspawnedDemand)
        {
            for (int i = edgeSlots.Count - 1; i >= 0; i--)
            {
                if (stack.itemCount <= 0) break;

                if (edgeSlots[i] == stack.itemData)
                {
                    edgeSlots.RemoveAt(i);
                    stack.itemCount--;

                    if (unspawnedDemand.Count > 0)
                    {
                        edgeSlots.Add(unspawnedDemand[0]);
                        unspawnedDemand.RemoveAt(0);
                    }
                }
            }
        }
    }
}