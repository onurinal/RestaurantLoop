using UnityEngine;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Represents an individual slot in the rack area.
    /// </summary>
    public class RackSlot : MonoBehaviour
    {
        public bool IsOccupied { get; private set; }
        public StackItem CurrentStack { get; private set; }

        public void PlaceStack(StackItem stack)
        {
            CurrentStack = stack;
            IsOccupied = true;
        }

        public void ClearSlot()
        {
            CurrentStack = null;
            IsOccupied = false;
        }
    }
}