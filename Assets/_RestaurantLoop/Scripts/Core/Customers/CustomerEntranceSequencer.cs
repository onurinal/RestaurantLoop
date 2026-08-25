using System;
using System.Collections;
using UnityEngine;
using RestaurantLoop.Infrastructure;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Spawns edge customers and visible crowd customers, placing remaining overflow customers directly as hidden instances.
    /// </summary>
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
            CentralCrowdService centralCrowd,
            int maxVisibleCrowdCount,
            Func<int, Vector3> getEdgeSlotPosFunc,
            Action<Customer, int> onEdgeSlotAssigned)
        {
            IsRunning = true;

            // 1. Populate active Edge Slots FIRST
            for (int i = 0; i < edgeSlots.SlotCount; i++)
            {
                ItemDataSO customerData = popDemandFunc?.Invoke();
                if (customerData == null) break;

                Customer customerPrefab = customerPrefabResolver?.Invoke(customerData);
                Customer customer = SpawnCustomer(customerPrefab, customerData, spawnPos, parent);
                if (customer == null) continue;
                edgeSlots.Occupy(i, customer);

                Vector3 targetPos = getEdgeSlotPosFunc(i);
                Vector3[] waypoints = EntrancePathUtility.BuildOrganicPath(spawnPos, targetPos, gapCenter, roomCenter, pathJitterAmount);

                customer.MoveAlongPath(waypoints, moveDuration, true);
                onEdgeSlotAssigned?.Invoke(customer, i);

                yield return new WaitForSeconds(spawnInterval);
            }

            // 2. Spawn and walk VISIBLE Central Crowd customers (up to maxVisibleCrowdCount)
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

            // 3. Spawn remaining HIDDEN Central Crowd customers instantly at their assigned positions
            for (int i = visibleCount; i < centralCrowd.Count; i++)
            {
                ItemDataSO customerData = popDemandFunc?.Invoke();
                if (customerData == null) break;

                CentralCrowdSlot slot = centralCrowd.Slots[i];
                Customer customerPrefab = customerPrefabResolver?.Invoke(customerData);
                Customer customer = SpawnCustomer(customerPrefab, customerData, slot.Position, parent);
                if (customer == null) continue;

                customer.SetModelRotation(slot.YRotation);
                customer.gameObject.SetActive(false); // Hide overflow customers
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
