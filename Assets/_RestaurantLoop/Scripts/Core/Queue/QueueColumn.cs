using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Manages an individual queue column and shifts items upward safely in local space.
    /// </summary>
    public class QueueColumn : MonoBehaviour
    {
        [SerializeField] private float shiftDuration = 0.3f;

        private readonly List<QueueSlot> slots = new List<QueueSlot>();

        public QueueSlot FrontSlot => slots.Count > 0 ? slots[0] : null;

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

        public void TrySendFrontStackToBelt(SplineConveyorPath path)
        {
            QueueSlot front = FrontSlot;

            if (front == null || !front.IsOccupied)
            {
                return;
            }

            if (!ConveyorController.Instance.TryReserveSlot())
            {
                front.CurrentStack.Shake();
                return;
            }

            StackItem stackToSend = front.CurrentStack;
            front.ClearSlot();

            Vector3 entrancePosition = path.GetPosition(ConveyorController.Instance.EntranceDistance);

            stackToSend.JumpToConveyor(entrancePosition, () => { ConveyorController.Instance.TryAddStack(stackToSend); });

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

                    previousSlot.PlaceStack(itemToMove);
                    currentSlot.ClearSlot();

                    itemToMove.transform.DOLocalMove(Vector3.zero, shiftDuration);
                }
            }
        }
    }
}