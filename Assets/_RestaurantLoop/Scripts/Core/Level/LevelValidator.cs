using System;
using System.Collections.Generic;
using System.Text;
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
        public string ValidationMessage { get; }
        public int StrictStatesExplored { get; }

        public LevelValidationReport(
            bool withoutPowerUps,
            bool withPowerUps,
            ValidationPowerUpUsage usage,
            string validationMessage = null,
            int strictStatesExplored = 0)
        {
            SolvableWithoutPowerUps = withoutPowerUps;
            SolvableWithPowerUps = withPowerUps;
            PowerUpsUsedByGreedySimulation = usage;
            ValidationMessage = validationMessage;
            StrictStatesExplored = strictStatesExplored;
        }
    }

    /// <summary>
    /// Scene-independent validator. Structural checks enforce exact conservation. The strict pass
    /// uses bounded backtracking to find a concrete one-stack-at-a-time strategy, which is a valid
    /// subset of gameplay and therefore proves the level can finish without boosters.
    /// </summary>
    public static class LevelValidator
    {
        private const int BaseConveyorCapacity = 5;
        private const int MaxIterations = 10000;
        private const int MaxStrictSearchStates = 150000;

        public static bool ValidateLevel(LevelDataSO levelData)
        {
            if (!TryValidateStructure(levelData, out _)) return false;
            return RunStrictPlanner(levelData).Solved;
        }

        public static bool ValidateLevelWithPowerUps(LevelDataSO levelData)
        {
            return TryValidateStructure(levelData, out _) && SimulateWithPowerUps(levelData).Solved;
        }

        public static LevelValidationReport AnalyzeLevel(LevelDataSO levelData)
        {
            if (!TryValidateStructure(levelData, out string structuralMessage))
            {
                return new LevelValidationReport(false, false, ValidationPowerUpUsage.None, structuralMessage);
            }

            StrictPlannerResult strict = RunStrictPlanner(levelData);
            SimulationResult assisted = strict.Solved
                ? new SimulationResult(true, ValidationPowerUpUsage.None)
                : SimulateWithPowerUps(levelData);

            string message = strict.Solved
                ? $"Strict no-power-up solution certified in {strict.StatesExplored:N0} search states."
                : strict.SearchLimitReached
                    ? $"Strict search exceeded {MaxStrictSearchStates:N0} states; the level is not certified."
                    : "No rack-safe, no-power-up completion strategy exists.";

            return new LevelValidationReport(
                strict.Solved,
                assisted.Solved,
                assisted.PowerUpsUsed,
                message,
                strict.StatesExplored);
        }

        public static bool TryValidateStructure(LevelDataSO data, out string validationMessage)
        {
            if (data == null)
            {
                validationMessage = "Level data is missing.";
                return false;
            }

            if (!LevelMathUtility.IsSupportedColumnCount(data.columnCount))
            {
                validationMessage = "Queue columns must be 2, 3, or 4.";
                return false;
            }

            if (data.rackSlotCount != LevelDataSO.FixedRackSlotCount)
            {
                validationMessage = $"Rack capacity must remain fixed at {LevelDataSO.FixedRackSlotCount}.";
                return false;
            }

            if (data.activeEdgeSlotCount <= 0 || data.activeEdgeSlotCount > 20)
            {
                validationMessage = "Active edge slots must be between 1 and 20.";
                return false;
            }

            if (data.minStackSize < LevelMathUtility.StackSizeStep ||
                data.maxStackSize < data.minStackSize ||
                data.minStackSize % LevelMathUtility.StackSizeStep != 0 ||
                data.maxStackSize % LevelMathUtility.StackSizeStep != 0)
            {
                validationMessage = "Stack bounds must be positive multiples of 5.";
                return false;
            }

            if (data.customerDemands == null || data.customerDemands.Count == 0)
            {
                validationMessage = "Customer demand is empty.";
                return false;
            }

            if (data.queueStackConfigs == null || data.queueStackConfigs.Count == 0)
            {
                validationMessage = "Queue stacks are empty.";
                return false;
            }

            if (data.calculatedRowCount <= 0 ||
                data.queueStackConfigs.Count != data.columnCount * data.calculatedRowCount)
            {
                validationMessage =
                    $"Queue contains {data.queueStackConfigs.Count} stacks, but {data.columnCount} columns × " +
                    $"{data.calculatedRowCount} rows requires {data.columnCount * data.calculatedRowCount}.";
                return false;
            }

            Dictionary<ItemDataSO, int> balances = new Dictionary<ItemDataSO, int>();
            long demandTotal = 0;
            for (int i = 0; i < data.customerDemands.Count; i++)
            {
                CustomerDemandConfig demand = data.customerDemands[i];
                if (demand.itemData == null || demand.totalCustomerCount <= 0)
                {
                    validationMessage = $"Customer demand #{i + 1} requires an item and positive count.";
                    return false;
                }

                demandTotal += demand.totalCustomerCount;
                balances.TryGetValue(demand.itemData, out int current);
                balances[demand.itemData] = current + demand.totalCustomerCount;
            }

            long queueTotal = 0;
            for (int i = 0; i < data.queueStackConfigs.Count; i++)
            {
                QueueStackConfig stack = data.queueStackConfigs[i];
                if (stack.itemData == null || stack.itemCount <= 0 ||
                    stack.itemCount % LevelMathUtility.StackSizeStep != 0)
                {
                    validationMessage = $"Queue stack #{i + 1} requires an item and a positive multiple-of-5 count.";
                    return false;
                }

                if (stack.itemCount < data.minStackSize || stack.itemCount > data.maxStackSize)
                {
                    validationMessage =
                        $"Queue stack #{i + 1} contains {stack.itemCount}; expected {data.minStackSize}–{data.maxStackSize}.";
                    return false;
                }

                if (!balances.TryGetValue(stack.itemData, out int remaining))
                {
                    validationMessage = $"Queue stack #{i + 1} uses an item absent from customer demand.";
                    return false;
                }

                queueTotal += stack.itemCount;
                balances[stack.itemData] = remaining - stack.itemCount;
            }

            if (queueTotal != demandTotal)
            {
                validationMessage = $"Exact conservation failed: demand is {demandTotal}, queue items are {queueTotal}.";
                return false;
            }

            foreach (KeyValuePair<ItemDataSO, int> balance in balances)
            {
                if (balance.Value == 0) continue;
                validationMessage =
                    $"Exact conservation failed for '{GetItemName(balance.Key)}' by {Mathf.Abs(balance.Value)} items.";
                return false;
            }

            if (data.OrderedCustomerSequence.Count > 0 &&
                !data.ValidateOrderedCustomerSequence(out validationMessage))
                return false;

            if (!data.ValidateTimedCustomers(out validationMessage)) return false;

            validationMessage =
                $"Structure valid: {demandTotal} demand = {queueTotal} queue items, " +
                $"{data.columnCount}×{data.calculatedRowCount} grid, rack capacity {data.rackSlotCount}.";
            return true;
        }

        private static StrictPlannerResult RunStrictPlanner(LevelDataSO data)
        {
            PlannerModel model = PlannerModel.Create(data);
            PlannerState initial = model.CreateInitialState(data.activeEdgeSlotCount);
            HashSet<string> rejectedStates = new HashSet<string>(StringComparer.Ordinal);
            int explored = 0;
            bool limitReached = false;
            bool solved = Search(model, initial, rejectedStates, ref explored, ref limitReached);
            return new StrictPlannerResult(solved, explored, limitReached);
        }

        private static bool Search(
            PlannerModel model,
            PlannerState state,
            HashSet<string> rejectedStates,
            ref int explored,
            ref bool limitReached)
        {
            if (state.Edge.Count == 0 && state.NextCustomerIndex >= model.CustomerSequence.Length)
                return true;

            if (++explored > MaxStrictSearchStates)
            {
                limitReached = true;
                return false;
            }

            string key = BuildStateKey(state);
            if (!rejectedStates.Add(key)) return false;

            // Serving an exposed matching stack can never consume rack capacity without first
            // satisfying current demand, so evaluate those productive moves first.
            for (int rackIndex = 0; rackIndex < state.Rack.Count; rackIndex++)
            {
                if (!state.Edge.Contains(state.Rack[rackIndex].ItemId)) continue;
                PlannerState next = state.Clone();
                PlannerStack stack = next.Rack[rackIndex];
                next.Rack.RemoveAt(rackIndex);
                if (ResolveStack(model, next, stack) &&
                    Search(model, next, rejectedStates, ref explored, ref limitReached))
                    return true;
                if (limitReached) return false;
            }

            for (int column = 0; column < model.Columns.Length; column++)
            {
                int position = state.ColumnPositions[column];
                if (position >= model.Columns[column].Length) continue;
                PlannerStack stack = model.Columns[column][position];
                if (!state.Edge.Contains(stack.ItemId)) continue;

                PlannerState next = state.Clone();
                next.ColumnPositions[column]++;
                if (ResolveStack(model, next, stack) &&
                    Search(model, next, rejectedStates, ref explored, ref limitReached))
                    return true;
                if (limitReached) return false;
            }

            // A nonmatching front stack may be parked to expose a needed deeper stack. This is the
            // only move that consumes rack capacity in the strict one-stack conveyor strategy.
            if (state.Rack.Count < LevelDataSO.FixedRackSlotCount)
            {
                for (int column = 0; column < model.Columns.Length; column++)
                {
                    int position = state.ColumnPositions[column];
                    if (position >= model.Columns[column].Length) continue;
                    PlannerStack stack = model.Columns[column][position];
                    if (state.Edge.Contains(stack.ItemId)) continue;

                    PlannerState next = state.Clone();
                    next.ColumnPositions[column]++;
                    next.Rack.Add(stack);
                    if (Search(model, next, rejectedStates, ref explored, ref limitReached))
                        return true;
                    if (limitReached) return false;
                }
            }

            return false;
        }

        private static bool ResolveStack(PlannerModel model, PlannerState state, PlannerStack stack)
        {
            int remaining = stack.Count;
            bool servedOnLap;
            do
            {
                servedOnLap = false;
                for (int edgeIndex = state.Edge.Count - 1; edgeIndex >= 0 && remaining > 0; edgeIndex--)
                {
                    if (state.Edge[edgeIndex] != stack.ItemId) continue;
                    state.Edge.RemoveAt(edgeIndex);
                    remaining--;
                    servedOnLap = true;
                    if (state.NextCustomerIndex < model.CustomerSequence.Length)
                        state.Edge.Add(model.CustomerSequence[state.NextCustomerIndex++]);
                }
            } while (servedOnLap && remaining > 0);

            if (remaining <= 0) return true;
            if (state.Rack.Count >= LevelDataSO.FixedRackSlotCount) return false;
            state.Rack.Add(new PlannerStack(stack.ItemId, remaining));
            return true;
        }

        private static string BuildStateKey(PlannerState state)
        {
            StringBuilder builder = new StringBuilder(96);
            builder.Append(state.NextCustomerIndex).Append('|');
            for (int i = 0; i < state.ColumnPositions.Length; i++)
                builder.Append(state.ColumnPositions[i]).Append(',');

            builder.Append('|');
            for (int i = 0; i < state.Edge.Count; i++) builder.Append(state.Edge[i]).Append(',');

            List<PlannerStack> sortedRack = new List<PlannerStack>(state.Rack);
            sortedRack.Sort(PlannerStackComparer.Instance);
            builder.Append('|');
            for (int i = 0; i < sortedRack.Count; i++)
                builder.Append(sortedRack[i].ItemId).Append(':').Append(sortedRack[i].Count).Append(',');
            return builder.ToString();
        }

        private static SimulationResult SimulateWithPowerUps(LevelDataSO levelData)
        {
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
                if (edge.Count == 0 && unspawned.Count == 0) return new SimulationResult(true, usage);

                if (AdvanceOneBeltStack(belt, rack, levelData.rackSlotCount, edge, unspawned, out bool rackOverflow))
                {
                    if (rackOverflow) return new SimulationResult(false, usage);
                    continue;
                }

                if (TryDeployMatchingRack(rack, belt, ref conveyorCapacity, ref addStackUsed, ref usage, edge) ||
                    TryDeployMatchingFront(columns, belt, ref conveyorCapacity, ref addStackUsed, ref usage, edge))
                    continue;

                if (!handUsed && TryDeployMatchingDeeper(columns, belt, conveyorCapacity, edge))
                {
                    handUsed = true;
                    usage |= ValidationPowerUpUsage.Hand;
                    continue;
                }

                if (!shuffleUsed && TryShuffleMatchingStackToFront(columns, edge))
                {
                    shuffleUsed = true;
                    usage |= ValidationPowerUpUsage.Shuffle;
                    continue;
                }

                if (TryDeployFirstFront(columns, belt, ref conveyorCapacity, ref addStackUsed, ref usage))
                    continue;

                return new SimulationResult(false, usage);
            }

            return new SimulationResult(false, usage);
        }

        private static List<List<QueueStackConfig>> BuildQueueColumns(LevelDataSO data)
        {
            List<List<QueueStackConfig>> columns = new List<List<QueueStackConfig>>(data.columnCount);
            int stackIndex = 0;
            for (int columnIndex = 0; columnIndex < data.columnCount; columnIndex++)
            {
                List<QueueStackConfig> column = new List<QueueStackConfig>(data.calculatedRowCount);
                for (int rowIndex = 0; rowIndex < data.calculatedRowCount; rowIndex++)
                    column.Add(data.queueStackConfigs[stackIndex++]);
                columns.Add(column);
            }

            return columns;
        }

        private static List<ItemDataSO> BuildDemand(LevelDataSO data)
        {
            List<ItemDataSO> demand = new List<ItemDataSO>();
            data.CopyResolvedCustomerSequenceTo(demand);
            return demand;
        }

        private static List<ItemDataSO> TakeInitialEdgeDemand(List<ItemDataSO> unspawned, int edgeCapacity)
        {
            List<ItemDataSO> edge = new List<ItemDataSO>(edgeCapacity);
            while (edge.Count < edgeCapacity && unspawned.Count > 0)
            {
                edge.Add(unspawned[0]);
                unspawned.RemoveAt(0);
            }

            return edge;
        }

        private static bool AdvanceOneBeltStack(
            List<QueueStackConfig> belt,
            List<QueueStackConfig> rack,
            int rackCapacity,
            List<ItemDataSO> edge,
            List<ItemDataSO> unspawned,
            out bool rackOverflow)
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

        private static bool TryDeployMatchingRack(
            List<QueueStackConfig> rack,
            List<QueueStackConfig> belt,
            ref int capacity,
            ref bool addStackUsed,
            ref ValidationPowerUpUsage usage,
            List<ItemDataSO> edge)
        {
            for (int index = 0; index < rack.Count; index++)
            {
                if (!CanFulfillDemand(rack[index].itemData, edge)) continue;
                if (!TryReserveBeltSlot(belt, ref capacity, ref addStackUsed, ref usage)) return false;
                belt.Add(rack[index]);
                rack.RemoveAt(index);
                return true;
            }

            return false;
        }

        private static bool TryDeployMatchingFront(
            List<List<QueueStackConfig>> columns,
            List<QueueStackConfig> belt,
            ref int capacity,
            ref bool addStackUsed,
            ref ValidationPowerUpUsage usage,
            List<ItemDataSO> edge)
        {
            for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                List<QueueStackConfig> column = columns[columnIndex];
                if (column.Count == 0 || !CanFulfillDemand(column[0].itemData, edge)) continue;
                if (!TryReserveBeltSlot(belt, ref capacity, ref addStackUsed, ref usage)) return false;
                belt.Add(column[0]);
                column.RemoveAt(0);
                return true;
            }

            return false;
        }

        private static bool TryDeployMatchingDeeper(
            List<List<QueueStackConfig>> columns,
            List<QueueStackConfig> belt,
            int capacity,
            List<ItemDataSO> edge)
        {
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

        private static bool TryShuffleMatchingStackToFront(
            List<List<QueueStackConfig>> columns,
            List<ItemDataSO> edge)
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

        private static bool TryDeployFirstFront(
            List<List<QueueStackConfig>> columns,
            List<QueueStackConfig> belt,
            ref int capacity,
            ref bool addStackUsed,
            ref ValidationPowerUpUsage usage)
        {
            for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                if (columns[columnIndex].Count == 0) continue;
                if (!TryReserveBeltSlot(belt, ref capacity, ref addStackUsed, ref usage)) return false;
                belt.Add(columns[columnIndex][0]);
                columns[columnIndex].RemoveAt(0);
                return true;
            }

            return false;
        }

        private static bool TryReserveBeltSlot(
            List<QueueStackConfig> belt,
            ref int capacity,
            ref bool addStackUsed,
            ref ValidationPowerUpUsage usage)
        {
            if (belt.Count < capacity) return true;
            if (addStackUsed) return false;
            addStackUsed = true;
            capacity++;
            usage |= ValidationPowerUpUsage.AddStack;
            return true;
        }

        private static bool CanFulfillDemand(ItemDataSO data, List<ItemDataSO> edge)
        {
            return edge.Contains(data);
        }

        private static int ProcessItemOnBelt(
            ref QueueStackConfig stack,
            List<ItemDataSO> edge,
            List<ItemDataSO> unspawned)
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

        private static string GetItemName(ItemDataSO item)
        {
            return item != null && !string.IsNullOrEmpty(item.ItemName) ? item.ItemName : item?.name ?? "Unknown";
        }

        private readonly struct PlannerStack
        {
            public int ItemId { get; }
            public int Count { get; }

            public PlannerStack(int itemId, int count)
            {
                ItemId = itemId;
                Count = count;
            }
        }

        private sealed class PlannerStackComparer : IComparer<PlannerStack>
        {
            public static readonly PlannerStackComparer Instance = new PlannerStackComparer();

            public int Compare(PlannerStack left, PlannerStack right)
            {
                int itemComparison = left.ItemId.CompareTo(right.ItemId);
                return itemComparison != 0 ? itemComparison : left.Count.CompareTo(right.Count);
            }
        }

        private sealed class PlannerState
        {
            public int[] ColumnPositions { get; }
            public List<PlannerStack> Rack { get; }
            public List<int> Edge { get; }
            public int NextCustomerIndex { get; set; }

            public PlannerState(int columnCount, int edgeCapacity)
            {
                ColumnPositions = new int[columnCount];
                Rack = new List<PlannerStack>(LevelDataSO.FixedRackSlotCount);
                Edge = new List<int>(edgeCapacity);
            }

            private PlannerState(PlannerState source)
            {
                ColumnPositions = (int[])source.ColumnPositions.Clone();
                Rack = new List<PlannerStack>(source.Rack);
                Edge = new List<int>(source.Edge);
                NextCustomerIndex = source.NextCustomerIndex;
            }

            public PlannerState Clone() => new PlannerState(this);
        }

        private sealed class PlannerModel
        {
            public PlannerStack[][] Columns { get; private set; }
            public int[] CustomerSequence { get; private set; }

            public static PlannerModel Create(LevelDataSO data)
            {
                Dictionary<ItemDataSO, int> itemIds = new Dictionary<ItemDataSO, int>();
                int nextItemId = 0;
                for (int i = 0; i < data.customerDemands.Count; i++)
                {
                    ItemDataSO item = data.customerDemands[i].itemData;
                    if (!itemIds.ContainsKey(item)) itemIds.Add(item, nextItemId++);
                }

                PlannerStack[][] columns = new PlannerStack[data.columnCount][];
                int stackIndex = 0;
                for (int column = 0; column < data.columnCount; column++)
                {
                    columns[column] = new PlannerStack[data.calculatedRowCount];
                    for (int row = 0; row < data.calculatedRowCount; row++)
                    {
                        QueueStackConfig stack = data.queueStackConfigs[stackIndex++];
                        columns[column][row] = new PlannerStack(itemIds[stack.itemData], stack.itemCount);
                    }
                }

                List<ItemDataSO> sequence = new List<ItemDataSO>();
                data.CopyResolvedCustomerSequenceTo(sequence);
                int[] customerSequence = new int[sequence.Count];
                for (int i = 0; i < sequence.Count; i++) customerSequence[i] = itemIds[sequence[i]];

                return new PlannerModel { Columns = columns, CustomerSequence = customerSequence };
            }

            public PlannerState CreateInitialState(int edgeCapacity)
            {
                PlannerState state = new PlannerState(Columns.Length, edgeCapacity);
                int initialCount = Mathf.Min(edgeCapacity, CustomerSequence.Length);
                for (int i = 0; i < initialCount; i++) state.Edge.Add(CustomerSequence[i]);
                state.NextCustomerIndex = initialCount;
                return state;
            }
        }

        private readonly struct StrictPlannerResult
        {
            public bool Solved { get; }
            public int StatesExplored { get; }
            public bool SearchLimitReached { get; }

            public StrictPlannerResult(bool solved, int statesExplored, bool searchLimitReached)
            {
                Solved = solved;
                StatesExplored = statesExplored;
                SearchLimitReached = searchLimitReached;
            }
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
