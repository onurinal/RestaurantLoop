using System;
using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Core
{
    [Flags]
    public enum ValidationPowerUpUsage
    {
        None = 0,
        AddStack = 1 << 0,
        Shuffle = 1 << 1,
        Hand = 1 << 2
    }

    public readonly struct LevelValidationReport
    {
        public bool SolvableWithoutPowerUps { get; }
        public bool SolvableWithPowerUps { get; }
        public ValidationPowerUpUsage PowerUpsUsedByGreedySimulation { get; }

        public LevelValidationReport(bool withoutPowerUps, bool withPowerUps, ValidationPowerUpUsage usage)
        {
            SolvableWithoutPowerUps = withoutPowerUps;
            SolvableWithPowerUps = withPowerUps;
            PowerUpsUsedByGreedySimulation = usage;
        }
    }

    /// <summary>
    /// Deterministic, scene-independent level simulator. It mirrors the queue's
    /// column-major layout and can run both strict and one-use-per-power-up passes.
    /// </summary>
    public static class LevelValidator
    {
        private const int BaseConveyorCapacity = 5;
        private const int MaxIterations = 10000;

        /// <summary>Strict validation: generated levels must not require a booster.</summary>
        public static bool ValidateLevel(LevelDataSO levelData) => Simulate(levelData, false).Solved;

        public static bool ValidateLevelWithPowerUps(LevelDataSO levelData) => Simulate(levelData, true).Solved;

        public static LevelValidationReport AnalyzeLevel(LevelDataSO levelData)
        {
            SimulationResult withoutPowerUps = Simulate(levelData, false);
            SimulationResult withPowerUps = Simulate(levelData, true);
            return new LevelValidationReport(withoutPowerUps.Solved, withPowerUps.Solved, withPowerUps.PowerUpsUsed);
        }

        private static SimulationResult Simulate(LevelDataSO levelData, bool allowPowerUps)
        {
            if (!HasConservedContent(levelData)) return default;

            List<List<QueueStackConfig>> columns = BuildQueueColumns(levelData);
            List<QueueStackConfig> rack = new List<QueueStackConfig>();
            List<QueueStackConfig> belt = new List<QueueStackConfig>();
            List<ItemDataSO> unspawned = BuildDemand(levelData);
            List<ItemDataSO> edge = TakeInitialEdgeDemand(unspawned, levelData.activeEdgeSlotCount);
            int conveyorCapacity = BaseConveyorCapacity;
            bool addStackUsed = false;
            bool shuffleUsed = false;
            bool handUsed = false;
            ValidationPowerUpUsage usage = ValidationPowerUpUsage.None;

            for (int iteration = 0; iteration < MaxIterations; iteration++)
            {
                if (IsComplete(columns, rack, belt, edge, unspawned)) return new SimulationResult(true, usage);

                if (AdvanceOneBeltStack(belt, rack, levelData.rackSlotCount, edge, unspawned, out bool rackOverflow))
                {
                    if (rackOverflow) return new SimulationResult(false, usage);
                    continue;
                }

                if (TryDeployMatchingRack(rack, belt, ref conveyorCapacity, allowPowerUps, ref addStackUsed, ref usage, edge) ||
                    TryDeployMatchingFront(columns, belt, ref conveyorCapacity, allowPowerUps, ref addStackUsed, ref usage, edge))
                    continue;

                if (allowPowerUps && !handUsed && TryDeployMatchingDeeper(columns, belt, ref conveyorCapacity,
                        edge))
                {
                    handUsed = true;
                    usage |= ValidationPowerUpUsage.Hand;
                    continue;
                }

                if (allowPowerUps && !shuffleUsed && TryShuffleMatchingStackToFront(columns, edge))
                {
                    shuffleUsed = true;
                    usage |= ValidationPowerUpUsage.Shuffle;
                    continue;
                }

                if (TryDeployFirstFront(columns, belt, ref conveyorCapacity, allowPowerUps, ref addStackUsed, ref usage))
                    continue;

                return new SimulationResult(false, usage);
            }

            return new SimulationResult(false, usage);
        }

        private static bool HasConservedContent(LevelDataSO data)
        {
            return data != null && data.customerDemands != null && data.queueStackConfigs != null &&
                data.TotalCustomerDemand == data.TotalQueueItems;
        }

        private static List<List<QueueStackConfig>> BuildQueueColumns(LevelDataSO data)
        {
            List<List<QueueStackConfig>> columns = new List<List<QueueStackConfig>>();
            int stackIndex = 0;
            int columnCount = Mathf.Max(1, data.columnCount);
            int rowCount = Mathf.Max(0, data.calculatedRowCount);

            for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                List<QueueStackConfig> column = new List<QueueStackConfig>();
                for (int rowIndex = 0; rowIndex < rowCount && stackIndex < data.queueStackConfigs.Count; rowIndex++)
                {
                    column.Add(data.queueStackConfigs[stackIndex++]);
                }
                columns.Add(column);
            }

            return columns;
        }

        private static List<ItemDataSO> BuildDemand(LevelDataSO data)
        {
            List<ItemDataSO> demand = new List<ItemDataSO>();
            foreach (CustomerDemandConfig config in data.customerDemands)
            {
                for (int count = 0; count < config.totalCustomerCount; count++) demand.Add(config.itemData);
            }
            return demand;
        }

        private static List<ItemDataSO> TakeInitialEdgeDemand(List<ItemDataSO> unspawned, int edgeCapacity)
        {
            List<ItemDataSO> edge = new List<ItemDataSO>();
            while (edge.Count < edgeCapacity && unspawned.Count > 0)
            {
                edge.Add(unspawned[0]);
                unspawned.RemoveAt(0);
            }
            return edge;
        }

        private static bool AdvanceOneBeltStack(List<QueueStackConfig> belt, List<QueueStackConfig> rack, int rackCapacity,
            List<ItemDataSO> edge, List<ItemDataSO> unspawned, out bool rackOverflow)
        {
            rackOverflow = false;
            if (belt.Count == 0) return false;

            QueueStackConfig stack = belt[0];
            int served = ProcessItemOnBelt(ref stack, edge, unspawned);
            belt.RemoveAt(0);

            if (stack.itemCount <= 0) return true;
            if (served > 0)
            {
                belt.Add(stack);
                return true;
            }

            if (rack.Count >= rackCapacity)
            {
                rackOverflow = true;
                return true;
            }

            rack.Add(stack);
            return true;
        }

        private static bool TryDeployMatchingRack(List<QueueStackConfig> rack, List<QueueStackConfig> belt,
            ref int capacity, bool allowPowerUps, ref bool addStackUsed, ref ValidationPowerUpUsage usage, List<ItemDataSO> edge)
        {
            for (int index = 0; index < rack.Count; index++)
            {
                if (!CanFulfillDemand(rack[index].itemData, edge)) continue;
                if (!TryReserveBeltSlot(belt, ref capacity, allowPowerUps, ref addStackUsed, ref usage)) return false;
                belt.Add(rack[index]);
                rack.RemoveAt(index);
                return true;
            }
            return false;
        }

        private static bool TryDeployMatchingFront(List<List<QueueStackConfig>> columns, List<QueueStackConfig> belt,
            ref int capacity, bool allowPowerUps, ref bool addStackUsed, ref ValidationPowerUpUsage usage, List<ItemDataSO> edge)
        {
            for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                List<QueueStackConfig> column = columns[columnIndex];
                if (column.Count == 0 || !CanFulfillDemand(column[0].itemData, edge)) continue;
                if (!TryReserveBeltSlot(belt, ref capacity, allowPowerUps, ref addStackUsed, ref usage)) return false;
                belt.Add(column[0]);
                column.RemoveAt(0);
                return true;
            }
            return false;
        }

        private static bool TryDeployMatchingDeeper(List<List<QueueStackConfig>> columns, List<QueueStackConfig> belt,
            ref int capacity, List<ItemDataSO> edge)
        {
            // Hand does not supply capacity: as in gameplay, it is unavailable
            // until a normal conveyor slot has opened.
            if (belt.Count >= capacity) return false;

            for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                List<QueueStackConfig> column = columns[columnIndex];
                for (int rowIndex = 1; rowIndex < column.Count; rowIndex++)
                {
                    if (!CanFulfillDemand(column[rowIndex].itemData, edge)) continue;
                    belt.Add(column[rowIndex]);
                    column.RemoveAt(rowIndex);
                    return true;
                }
            }
            return false;
        }

        private static bool TryShuffleMatchingStackToFront(List<List<QueueStackConfig>> columns, List<ItemDataSO> edge)
        {
            for (int sourceColumnIndex = 0; sourceColumnIndex < columns.Count; sourceColumnIndex++)
            {
                List<QueueStackConfig> source = columns[sourceColumnIndex];
                for (int sourceRowIndex = 0; sourceRowIndex < source.Count; sourceRowIndex++)
                {
                    if (!CanFulfillDemand(source[sourceRowIndex].itemData, edge)) continue;
                    for (int targetColumnIndex = 0; targetColumnIndex < columns.Count; targetColumnIndex++)
                    {
                        List<QueueStackConfig> target = columns[targetColumnIndex];
                        if (target.Count == 0) continue;
                        QueueStackConfig temporary = target[0];
                        target[0] = source[sourceRowIndex];
                        source[sourceRowIndex] = temporary;
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool TryDeployFirstFront(List<List<QueueStackConfig>> columns, List<QueueStackConfig> belt,
            ref int capacity, bool allowPowerUps, ref bool addStackUsed, ref ValidationPowerUpUsage usage)
        {
            for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                List<QueueStackConfig> column = columns[columnIndex];
                if (column.Count == 0) continue;
                if (!TryReserveBeltSlot(belt, ref capacity, allowPowerUps, ref addStackUsed, ref usage)) return false;
                belt.Add(column[0]);
                column.RemoveAt(0);
                return true;
            }
            return false;
        }

        private static bool TryReserveBeltSlot(List<QueueStackConfig> belt, ref int capacity, bool allowPowerUps,
            ref bool addStackUsed, ref ValidationPowerUpUsage usage)
        {
            if (belt.Count < capacity) return true;
            if (!allowPowerUps || addStackUsed) return false;

            addStackUsed = true;
            capacity++;
            usage |= ValidationPowerUpUsage.AddStack;
            return true;
        }

        private static bool IsComplete(List<List<QueueStackConfig>> columns, List<QueueStackConfig> rack,
            List<QueueStackConfig> belt, List<ItemDataSO> edge, List<ItemDataSO> unspawned)
        {
            if (rack.Count > 0 || belt.Count > 0 || edge.Count > 0 || unspawned.Count > 0) return false;
            for (int index = 0; index < columns.Count; index++) if (columns[index].Count > 0) return false;
            return true;
        }

        private static bool CanFulfillDemand(ItemDataSO data, List<ItemDataSO> edge) => edge.Contains(data);

        private static int ProcessItemOnBelt(ref QueueStackConfig stack, List<ItemDataSO> edge, List<ItemDataSO> unspawned)
        {
            int served = 0;
            for (int index = edge.Count - 1; index >= 0; index--)
            {
                if (stack.itemCount <= 0) break;
                if (edge[index] != stack.itemData) continue;

                edge.RemoveAt(index);
                stack.itemCount--;
                served++;
                if (unspawned.Count > 0)
                {
                    edge.Add(unspawned[0]);
                    unspawned.RemoveAt(0);
                }
            }
            return served;
        }

        private readonly struct SimulationResult
        {
            public bool Solved { get; }
            public ValidationPowerUpUsage PowerUpsUsed { get; }

            public SimulationResult(bool solved, ValidationPowerUpUsage powerUpsUsed)
            {
                Solved = solved;
                PowerUpsUsed = powerUpsUsed;
            }
        }
    }
}
