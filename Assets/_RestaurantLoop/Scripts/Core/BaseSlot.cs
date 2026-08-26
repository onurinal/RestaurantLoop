using UnityEngine;

namespace RestaurantLoop.Core
{
    public abstract class BaseSlot : MonoBehaviour
    {
        [Header("Cell Visual")]
        [SerializeField] private FoodCell cellVisual;

        public bool IsOccupied { get; protected set; }
        public StackItem CurrentStack { get; protected set; }

        protected virtual void Awake()
        {
            if (cellVisual == null)
            {
                cellVisual = GetComponentInChildren<FoodCell>(true);
            }

            cellVisual?.Clear();
        }

        public virtual void PlaceStack(StackItem stack)
        {
            CurrentStack = stack;
            IsOccupied = stack != null;

            if (stack != null)
            {
                stack.transform.SetParent(transform);
                cellVisual?.SetFood(stack.Data);
            }
            else
            {
                cellVisual?.Clear();
            }
        }

        public virtual void ClearSlot()
        {
            CurrentStack = null;
            IsOccupied = false;
            cellVisual?.Clear();
        }

        public abstract void OnStackTapped(StackItem stack);
    }
}
