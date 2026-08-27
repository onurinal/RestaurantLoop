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
            Func<ItemDataSO> popDemandFunc,
            Func<ItemDataSO, Customer> customerPrefabResolver,
            Transform parent,
            Vector3 spawnPos,
            Vector3 gapCenter,
            Vector3 roomCenter,
            EdgeSlotService edgeSlots,
            List<int> initialEdgeSlotIndices,
            CentralCrowdService centralCrowd,
            int maxVisibleCrowdCount,
            Func<int, Vector3> getEdgeSlotPosFunc,
            Func<int, float> getEdgeSlotRotFunc,
            Action<Customer, int> onEdgeSlotAssigned)
        {
            IsRunning = true;

            for (int k = 0; k < initialEdgeSlotIndices.Count; k++)
            {
                int slotIndex = initialEdgeSlotIndices[k];

                ItemDataSO customerData = popDemandFunc?.Invoke();
                if (customerData == null) break;

                Customer customerPrefab = customerPrefabResolver?.Invoke(customerData);
                Customer customer = SpawnCustomer(customerPrefab, customerData, spawnPos, parent);
                if (customer == null) continue;
                edgeSlots.Occupy(slotIndex, customer);

                Vector3 targetPos = getEdgeSlotPosFunc(slotIndex);
                float targetRotation = getEdgeSlotRotFunc != null ? getEdgeSlotRotFunc(slotIndex) : 0f;

                Vector3[] waypoints = EntrancePathUtility.BuildOrganicPath(spawnPos, targetPos, gapCenter, roomCenter, pathJitterAmount);

                int assignedIndex = slotIndex;
                customer.MoveAlongPath(waypoints, moveDuration, true, targetYRotation: targetRotation, roomCenter: roomCenter,
                    onComplete: () => { onEdgeSlotAssigned?.Invoke(customer, assignedIndex); });

                yield return new WaitForSeconds(spawnInterval);
            }

            // 2. Spawn and walk VISIBLE Central Crowd customers
            int visibleCount = Mathf.Min(maxVisibleCrowdCount, centralCrowd.Count);
            for (int i = 0; i < visibleCount; i++)
            {
                ItemDataSO customerData = popDemandFunc?.Invoke();
                if (customerData == null) break;

                Customer customerPrefab = customerPrefabResolver?.Invoke(customerData);
                Customer customer = SpawnCustomer(customerPrefab, customerData, spawnPos, parent);
                if (customer == null) continue;
                CentralCrowdSlot slot = centralCrowd.Slots[i];
                slot.OccupyingCustomer = customer;

                Vector3[] waypoints = EntrancePathUtility.BuildOrganicPath(spawnPos, slot.Position, gapCenter, roomCenter, pathJitterAmount);
                customer.MoveAlongPath(waypoints, moveDuration, false, targetYRotation: slot.YRotation);

                yield return new WaitForSeconds(spawnInterval);
            }

            for (int i = visibleCount; i < centralCrowd.Count; i++)
            {
                ItemDataSO customerData = popDemandFunc?.Invoke();
                if (customerData == null) break;

                CentralCrowdSlot slot = centralCrowd.Slots[i];
                Customer customerPrefab = customerPrefabResolver?.Invoke(customerData);
                Customer customer = SpawnCustomer(customerPrefab, customerData, slot.Position, parent);
                if (customer == null) continue;

                customer.SetModelRotation(slot.YRotation);
                customer.gameObject.SetActive(false);
                slot.OccupyingCustomer = customer;
            }

            IsRunning = false;
        }

        private Customer SpawnCustomer(Customer prefab, ItemDataSO data, Vector3 position, Transform parent)
        {
            if (prefab == null)
            {
                Debug.LogError($"No customer prefab is configured for {data?.ItemName ?? "an empty item"}.");
                return null;
            }

            GameObject obj = PoolManager.Instance.Spawn(prefab.gameObject, position, Quaternion.identity, parent);
            Customer customer = obj.GetComponent<Customer>();
            customer.Initialize(data);
            return customer;
        }
    }
}