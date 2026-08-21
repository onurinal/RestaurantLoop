using UnityEngine;

namespace RestaurantLoop.Core
{
    public abstract class BaseSlot : MonoBehaviour
    {
        public bool IsOccupied { get; protected set; }
        public StackItem CurrentStack { get; protected set; }

        public virtual void PlaceStack(StackItem stack)
        {
            CurrentStack = stack;
            IsOccupied = stack != null;

            if (stack != null)
            {
                stack.transform.SetParent(transform);
            }
        }

        public virtual void ClearSlot()
        {
            CurrentStack = null;
            IsOccupied = false;
        }

        public abstract void OnStackTapped(StackItem stack);
    }
}