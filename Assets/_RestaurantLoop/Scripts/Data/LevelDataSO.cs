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

        [Header("Queue Initial Stack Setup")]
        public List<QueueStackConfig> queueStackConfigs = new List<QueueStackConfig>();

        public int TotalCustomerDemand
        {
            get
            {
                int total = 0;
                foreach (var c in customerDemands) total += c.totalCustomerCount;
                return total;
            }
        }

        public int TotalQueueItems
        {
            get
            {
                int total = 0;
                foreach (var q in queueStackConfigs) total += q.itemCount;
                return total;
            }
        }
    }
}