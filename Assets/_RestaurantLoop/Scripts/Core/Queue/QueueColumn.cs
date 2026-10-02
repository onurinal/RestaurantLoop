using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using RestaurantLoop.Infrastructure;

namespace RestaurantLoop.Core
{
    public class QueueColumn : MonoBehaviour
    {
        [SerializeField] private float shiftDuration = 0.3f;

        private readonly List<QueueSlot> slots = new List<QueueSlot>();
        private readonly List<QueueUnit> survivorBuffer = new List<QueueUnit>();
        private bool isShifting;

        private readonly struct QueueUnit
        {
            public readonly StackItem Stack;
            public readonly FoodCell Cell;
            public readonly int SourceSlotIndex;

            public QueueUnit(StackItem stack, FoodCell cell, int sourceSlotIndex)
            {
                Stack = stack;
                Cell = cell;
                SourceSlotIndex = sourceSlotIndex;
            }
        }

        public QueueSlot FrontSlot => slots.Count > 0 ? slots[0] : null;
        public bool IsTransitioning => isShifting;

        /// <summary>
        /// The column's slots, cached by InitializeChildSlots. Callers used to rebuild this
        /// with GetComponentsInChildren, which allocates a new array on every call — including
        /// from per-frame UI availability checks. Slots are direct children only, so this list
        /// holds the same slots in the same order.
        /// </summary>
        public IReadOnlyList<QueueSlot> Slots => slots;

        public int OccupiedSlotCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i] != null && slots[i].IsOccupied) count++;
                }

                return count;
            }
        }

        public void InitializeChildSlots()
        {
            slots.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                QueueSlot slot = transform.GetChild(i).GetComponent<QueueSlot>();
                if (slot != null) slots.Add(slot);
            }
        }

        public void TrySendFrontStackToBelt()
        {
            QueueSlot front = FrontSlot;
            TrySendStackToBelt(front, requireDeeperStack: false);
        }

        public bool ContainsSlot(QueueSlot slot) => slot != null && slots.Contains(slot);

        public bool IsDeeperSlot(QueueSlot slot) => slots.IndexOf(slot) > 0;

        public bool TrySendStackToBelt(QueueSlot slot, bool requireDeeperStack)
        {
            if (isShifting || slot == null || !slot.IsOccupied) return false;

            int slotIndex = slots.IndexOf(slot);
            if (slotIndex < 0 || (requireDeeperStack && slotIndex == 0)) return false;

            StackItem stackToSend = slot.CurrentStack;
            if (stackToSend == null || stackToSend.IsJumping) return false;

            if (stackToSend.RemainingItemCount <= 0)
            {
                FoodCell consumedCell = slot.DetachConsumedStack(stackToSend);
                DespawnFoodCell(consumedCell);
                StackItem.ReleaseToPool(stackToSend);
                ShiftColumnItemsUp(slotIndex);
                QueueManager.Instance?.NotifyQueueChanged();
                return true;
            }

            // Check conveyor availability BEFORE clearing the slot
            if (!ConveyorManager.Instance.CanAcceptStack)
            {
                stackToSend.Shake();
                ConveyorManager.Instance.NotifyCapacityRejected();
                return false;
            }

            if (!ConveyorManager.Instance.TrySendStackToBelt(stackToSend))
            {
                return false;
            }

            FoodCell cellToDespawn = slot.DetachConsumedStack(stackToSend);
            DespawnFoodCell(cellToDespawn);

            ShiftColumnItemsUp(slotIndex);
            QueueManager.Instance?.NotifyQueueChanged();
            return true;
        }

        /// <summary>
        /// Removes every matching queue unit, including its cell, then compacts survivor
        /// stack/cell pairs without repainting or leaving cells in empty slots.
        /// </summary>
        public int RemoveStacksByData(ItemDataSO data)
        {
            if (data == null || isShifting) return 0;

            int matchingCount = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                StackItem stack = slots[i] != null ? slots[i].CurrentStack : null;
                if (stack != null && stack.Data == data) matchingCount++;
            }

            if (matchingCount == 0) return 0;

            survivorBuffer.Clear();
            for (int i = 0; i < slots.Count; i++)
            {
                QueueSlot slot = slots[i];
                if (slot == null) continue;

                if (!slot.IsOccupied || slot.CurrentStack == null)
                {
                    DespawnFoodCell(slot.DetachOrphanedCell());
                    continue;
                }

                StackItem stack = slot.DetachStackForShift(out FoodCell cell);
                if (stack.Data == data)
                {
                    DespawnFoodCell(cell);
                    StackItem.ReleaseToPool(stack);
                }
                else
                {
                    stack.transform.SetParent(transform, true);
                    if (cell != null) cell.transform.SetParent(transform, true);
                    survivorBuffer.Add(new QueueUnit(stack, cell, i));
                }
            }

            CompactSurvivors();
            return matchingCount;
        }

        private void CompactSurvivors()
        {
            isShifting = true;
            int pendingMoves = 0;

            for (int targetIndex = 0; targetIndex < survivorBuffer.Count; targetIndex++)
            {
                QueueUnit unit = survivorBuffer[targetIndex];
                QueueSlot targetSlot = slots[targetIndex];
                if (unit.SourceSlotIndex == targetIndex)
                {
                    targetSlot.PlaceShiftedStack(unit.Stack, unit.Cell);
                    continue;
                }

                pendingMoves++;
                Sequence sequence = DOTween.Sequence().SetUpdate(true);
                sequence.Join(unit.Stack.transform
                    .DOMove(targetSlot.transform.position, shiftDuration)
                    .SetEase(Ease.OutQuad));
                if (unit.Cell != null)
                {
                    sequence.Join(unit.Cell.transform
                        .DOMove(targetSlot.transform.position, shiftDuration)
                        .SetEase(Ease.OutQuad));
                }

                QueueSlot destination = targetSlot;
                sequence.OnComplete(() =>
                {
                    if (destination != null) destination.PlaceShiftedStack(unit.Stack, unit.Cell);
                    pendingMoves--;
                    if (pendingMoves == 0) CompleteShift();
                });
            }

            survivorBuffer.Clear();
            if (pendingMoves == 0) CompleteShift();
        }

        private void CompleteShift()
        {
            isShifting = false;
            QueueManager.Instance?.NotifyQueueChanged();
        }

        private void ShiftColumnItemsUp(int emptySlotIndex)
        {
            isShifting = true;
            int pendingMoves = 0;

            for (int i = emptySlotIndex + 1; i < slots.Count; i++)
            {
                QueueSlot currentSlot = slots[i];
                QueueSlot previousSlot = slots[i - 1];

                if (currentSlot.IsOccupied)
                {
                    StackItem itemToMove = currentSlot.DetachStackForShift(out FoodCell cellToMove);
                    if (itemToMove == null) continue;

                    itemToMove.transform.SetParent(transform, true);
                    if (cellToMove != null) cellToMove.transform.SetParent(transform, true);
                    pendingMoves++;

                    Sequence shiftSequence = DOTween.Sequence();
                    shiftSequence.Join(itemToMove.transform
                        .DOMove(previousSlot.transform.position, shiftDuration)
                        .SetEase(Ease.OutQuad));
                    if (cellToMove != null)
                    {
                        shiftSequence.Join(cellToMove.transform
                            .DOMove(previousSlot.transform.position, shiftDuration)
                            .SetEase(Ease.OutQuad));
                    }

                    shiftSequence
                        .OnComplete(() =>
                        {
                            if (previousSlot != null)
                            {
                                previousSlot.PlaceShiftedStack(itemToMove, cellToMove);
                            }

                            pendingMoves--;
                            if (pendingMoves == 0)
                            {
                                isShifting = false;
                                QueueManager.Instance?.NotifyQueueChanged();
                            }
                        });
                }
            }

            if (pendingMoves == 0)
            {
                isShifting = false;
                QueueManager.Instance?.NotifyQueueChanged();
            }
        }

        private static void DespawnFoodCell(FoodCell cell)
        {
            if (cell == null) return;

            GameObject cellObject = cell.gameObject;
            cell.transform.DOKill();
            cellObject.SetActive(false);

            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Despawn(cellObject);
            }
            else
            {
                Destroy(cellObject);
            }
        }
    }
}
