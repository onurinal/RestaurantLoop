using UnityEngine;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Holds the state and item hierarchy for an individual queue slot.
    /// </summary>
    public class QueueSlot : MonoBehaviour
    {
        public bool IsOccupied { get; private set; }
        public StackItem CurrentStack { get; private set; }

        public void PlaceStack(StackItem stack)
        {
            CurrentStack = stack;
            IsOccupied = stack != null;

            if (stack != null)
            {
                stack.transform.SetParent(transform);
            }
        }

        public void ClearSlot()
        {
            CurrentStack = null;
            IsOccupied = false;
        }
    }
}