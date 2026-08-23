using System;
using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Core
{
    [Serializable]
    public struct StationLevelConfig
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
        [Range(3, 16)] public int activeEdgeSlotCount = 6;

        [Header("Rack Setup")]
        [Range(1, 10)] public int rackSlotCount = 4;

        [Header("Queue Layout Setup")]
        [Tooltip("Fixed column count defined for the queue layout.")]
        [Range(1, 8)] public int columnCount = 3;

        [Tooltip("Calculated row depth automatically computed by LevelMathUtility based on total stacks.")]
        public int calculatedRowCount = 3;

        [Header("Queue Stack Size Constraints")]
        public int minStackSize = 10;
        public int maxStackSize = 40;

        [Header("Conveyor Settings")]
        public float conveyorSpeedMultiplier = 1.0f;

        [Header("Station Demand Setup")]
        public List<StationLevelConfig> stationConfigs = new List<StationLevelConfig>();

        [Header("Queue Initial Stack Setup")]
        public List<QueueStackConfig> queueStackConfigs = new List<QueueStackConfig>();

        // Inspector Validation Properties
        public int TotalCustomerDemand
        {
            get
            {
                int total = 0;
                foreach (var c in stationConfigs) total += c.totalCustomerCount;
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