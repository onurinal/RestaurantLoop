using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace RestaurantLoop.Core
{
    public class QueueColumn : MonoBehaviour
    {
        [SerializeField] private float shiftDuration = 0.3f;

        private readonly List<QueueSlot> slots = new List<QueueSlot>();
        private bool isShifting;

        public QueueSlot FrontSlot => slots.Count > 0 ? slots[0] : null;
        public bool IsTransitioning => isShifting;

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
                slot.ClearSlot();
                stackToSend.transform.SetParent(null, true);
                Destroy(stackToSend.gameObject);
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

            // Clear slot only after confirming the transfer is valid
            slot.ClearSlot();

            if (!ConveyorManager.Instance.TrySendStackToBelt(stackToSend))
            {
                slot.PlaceStack(stackToSend);
                stackToSend.transform.localPosition = Vector3.zero;
                return false;
            }

            ShiftColumnItemsUp(slotIndex);
            QueueManager.Instance?.NotifyQueueChanged();
            return true;
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
                    StackItem itemToMove = currentSlot.CurrentStack;

                    currentSlot.ClearSlot();
                    itemToMove.transform.SetParent(previousSlot.transform);
                    pendingMoves++;

                    itemToMove.transform.DOLocalMove(Vector3.zero, shiftDuration)
                        .OnComplete(() =>
                        {
                            previousSlot.PlaceStack(itemToMove);
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
    }
}
