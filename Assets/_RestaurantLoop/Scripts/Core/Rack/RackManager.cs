using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Manages slot states and assigns available rack positions to stacks.
    /// </summary>
    public class RackManager : MonoBehaviour
    {
        public static RackManager Instance { get; private set; }

        [SerializeField] private List<RackSlot> rackSlots = new List<RackSlot>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Finds an empty slot, registers the stack, and returns the slot target position.
        /// </summary>
        public bool TryAddStackToRack(StackItem stack)
        {
            RackSlot emptySlot = GetFirstEmptySlot();

            if (emptySlot == null)
            {
                Debug.LogWarning("Rack is completely full!");
                return false;
            }

            emptySlot.PlaceStack(stack);

            stack.JumpToSlot(emptySlot.transform);
            return true;
        }

        private RackSlot GetFirstEmptySlot()
        {
            for (int i = 0; i < rackSlots.Count; i++)
            {
                if (!rackSlots[i].IsOccupied)
                {
                    return rackSlots[i];
                }
            }

            return null;
        }
    }
}