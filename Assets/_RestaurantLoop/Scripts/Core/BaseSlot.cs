using UnityEngine;

namespace RestaurantLoop.Core
{
    public abstract class BaseSlot : MonoBehaviour
    {
        [Header("Cell Visual")]
        [SerializeField] private FoodCell cellVisual;

        public bool IsOccupied { get; protected set; }
        public StackItem CurrentStack { get; protected set; }
        public FoodCell CellVisual => cellVisual;

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

        /// <summary>
        /// Releases this slot's stack and cell as one moving queue unit without changing
        /// the cell material or despawning either object.
        /// </summary>
        public StackItem DetachStackForShift(out FoodCell movingCell)
        {
            StackItem movingStack = CurrentStack;
            movingCell = cellVisual;
            CurrentStack = null;
            cellVisual = null;
            IsOccupied = false;
            return movingStack;
        }

        /// <summary>Releases the consumed stack reference and transfers cell ownership to the caller.</summary>
        public FoodCell DetachConsumedStack(StackItem consumedStack)
        {
            if (CurrentStack != consumedStack) return null;

            FoodCell consumedCell = cellVisual;
            CurrentStack = null;
            cellVisual = null;
            IsOccupied = false;
            return consumedCell;
        }

        /// <summary>Transfers ownership of an unoccupied slot's leftover cell for safe cleanup.</summary>
        public FoodCell DetachOrphanedCell()
        {
            if (IsOccupied || CurrentStack != null) return null;

            FoodCell orphanedCell = cellVisual;
            cellVisual = null;
            return orphanedCell;
        }

        /// <summary>Adopts a stack and its existing cell after a queue-column shift completes.</summary>
        public void PlaceShiftedStack(StackItem stack, FoodCell movingCell)
        {
            CurrentStack = stack;
            cellVisual = movingCell;
            IsOccupied = stack != null;

            if (movingCell != null)
            {
                Transform cellTransform = movingCell.transform;
                cellTransform.SetParent(transform, true);
                cellTransform.localPosition = Vector3.zero;
            }

            if (stack != null)
            {
                Transform stackTransform = stack.transform;
                stackTransform.SetParent(transform, true);
                stackTransform.localPosition = Vector3.zero;
            }
        }

        public abstract void OnStackTapped(StackItem stack);
    }
}
