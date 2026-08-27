using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace RestaurantLoop.Core
{
    public class QueueColumn : MonoBehaviour
    {
        [SerializeField] private float shiftDuration = 0.3f;

        private readonly List<QueueSlot> slots = new List<QueueSlot>();

        public QueueSlot FrontSlot => slots.Count > 0 ? slots[0] : null;

        /// <summary>
        /// Returns the number of occupied slots currently inside this column.
        /// </summary>
        public int OccupiedSlotCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i] != null && slots[i].IsOccupied)
                    {
                        count++;
                    }
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
                if (slot != null)
                {
                    slots.Add(slot);
                }
            }
        }

        public void TrySendFrontStackToBelt()
        {
            QueueSlot front = FrontSlot;

            if (front == null || !front.IsOccupied)
            {
                return;
            }

            if (!ConveyorManager.Instance.CanAcceptStack)
            {
                front.CurrentStack.Shake();
                return;
            }

            StackItem stackToSend = front.CurrentStack;

            if (stackToSend.RemainingItemCount <= 0)
            {
                front.ClearSlot();
                stackToSend.transform.SetParent(null, true);
                Destroy(stackToSend.gameObject);
                ShiftColumnItemsUp();
                return;
            }

            front.ClearSlot();

            if (!ConveyorManager.Instance.TrySendStackToBelt(stackToSend))
            {
                front.PlaceStack(stackToSend);
                stackToSend.transform.localPosition = Vector3.zero;
                return;
            }

            ShiftColumnItemsUp();
        }

        private void ShiftColumnItemsUp()
        {
            for (int i = 1; i < slots.Count; i++)
            {
                QueueSlot currentSlot = slots[i];
                QueueSlot previousSlot = slots[i - 1];

                if (currentSlot.IsOccupied)
                {
                    StackItem itemToMove = currentSlot.CurrentStack;

                    currentSlot.ClearSlot();

                    itemToMove.transform.SetParent(previousSlot.transform);

                    itemToMove.transform.DOLocalMove(Vector3.zero, shiftDuration)
                        .OnComplete(() => { previousSlot.PlaceStack(itemToMove); });
                }
            }
        }
    }
}