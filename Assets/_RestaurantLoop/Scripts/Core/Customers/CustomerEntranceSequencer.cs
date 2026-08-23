using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RestaurantLoop.Infrastructure;

namespace RestaurantLoop.Core
{
    public class CustomerEntranceSequencer
    {
        private readonly float spawnInterval;
        private readonly float moveDuration;
        private readonly float pathJitterAmount;
        private readonly Vector3 outerSpawnOffset;

        public bool IsRunning { get; private set; }
        public Vector3 OuterSpawnOffset => outerSpawnOffset;

        public CustomerEntranceSequencer(float spawnInterval, float moveDuration, float pathJitterAmount, Vector3 outerSpawnOffset)
        {
            this.spawnInterval = spawnInterval;
            this.moveDuration = moveDuration;
            this.pathJitterAmount = pathJitterAmount;
            this.outerSpawnOffset = outerSpawnOffset;
        }

        public IEnumerator Run(
            List<StationConfig> configs,
            Customer customerPrefab,
            Transform parent,
            ConveyorManager conveyor,
            Vector3 spawnPos,
            Vector3 gapCenter,
            Vector3 roomCenter,
            EdgeSlotService edgeSlots,
            StationLayoutManager stationLayout,
            Action<Customer, int> onEdgeSlotAssigned)
        {
            IsRunning = true;

            List<ItemDataSO> incomingQueue = BuildShuffledQueue(configs);
            int edgeSlotIndex = 0;
            int activeWalkers = 0;

            for (int i = 0; i < incomingQueue.Count; i++)
            {
                ItemDataSO customerData = incomingQueue[i];
                
                GameObject customerObj = PoolManager.Instance.Spawn(customerPrefab.gameObject, spawnPos, customerPrefab.transform.rotation, parent);
                Customer newCustomer = customerObj.GetComponent<Customer>();
                
                newCustomer.Initialize(customerData);
                activeWalkers++;

                if (edgeSlotIndex < edgeSlots.SlotCount)
                {
                    int slotIdx = edgeSlotIndex++;
                    edgeSlots.Occupy(slotIdx, newCustomer);

                    Vector3 edgeTargetPos = edgeSlots.GetSlotWorldPosition(slotIdx, conveyor, roomCenter, parent.position);
                    Vector3[] waypoints = EntrancePathUtility.BuildOrganicPath(spawnPos, edgeTargetPos, gapCenter, roomCenter, pathJitterAmount);

                    newCustomer.MoveAlongPath(waypoints, moveDuration, true, () => activeWalkers--);
                    onEdgeSlotAssigned?.Invoke(newCustomer, slotIdx);
                }
                else
                {
                    CustomerStation targetStation = stationLayout.FindStationFor(customerData);
                    if (targetStation != null)
                    {
                        Vector3[] waypoints = EntrancePathUtility.BuildOrganicPath(spawnPos, targetStation.position, gapCenter, roomCenter, pathJitterAmount);

                        newCustomer.MoveAlongPath(waypoints, moveDuration, false, () =>
                        {
                            targetStation.remainingCount++;
                            targetStation.UpdateUI();
                            
                            PoolManager.Instance.Despawn(newCustomer.gameObject);
                            activeWalkers--;
                        });
                    }
                    else
                    {
                        activeWalkers--;
                    }
                }

                yield return new WaitForSeconds(spawnInterval);
            }

            yield return new WaitUntil(() => activeWalkers <= 0);

            IsRunning = false;
        }

        private static List<ItemDataSO> BuildShuffledQueue(List<StationConfig> configs)
        {
            List<ItemDataSO> queue = new List<ItemDataSO>();
            foreach (var cfg in configs)
            {
                for (int i = 0; i < cfg.remainingCount; i++)
                {
                    queue.Add(cfg.itemData);
                }
            }

            for (int i = queue.Count - 1; i > 0; i--)
            {
                int randomIndex = UnityEngine.Random.Range(0, i + 1);
                (queue[i], queue[randomIndex]) = (queue[randomIndex], queue[i]);
            }

            return queue;
        }
    }
}