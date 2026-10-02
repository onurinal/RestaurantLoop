using System;
using UnityEngine;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Slot implementation for the Rack system. Occupancy notifications allow
    /// visual warning systems to react without polling or owning stack state.
    /// </summary>
    public class RackSlot : BaseSlot
    {
        public event Action<RackSlot, bool> OccupancyChanged;

        public override void PlaceStack(StackItem stack)
        {
            bool wasOccupied = IsOccupied;
            base.PlaceStack(stack);
            if (wasOccupied != IsOccupied) OccupancyChanged?.Invoke(this, IsOccupied);
        }

        public override void ClearSlot()
        {
            bool wasOccupied = IsOccupied;
            base.ClearSlot();
            if (wasOccupied != IsOccupied) OccupancyChanged?.Invoke(this, IsOccupied);
        }

        public void SetFullRackWarning(bool active, Color color, SlotOutlineAnimationSettings settings)
        {
            SetInteractionOutlineGuidance(active, color, settings);
        }

        public override void OnStackTapped(StackItem stack)
        {
            if (PowerUpManager.Instance != null && PowerUpManager.Instance.IsClearColorSelectionActive)
            {
                PowerUpManager.Instance.TrySelectClearColorStack(stack);
                return;
            }

            if (PowerUpManager.Instance != null && PowerUpManager.Instance.IsHandSelectionActive) return;
            RackManager.Instance?.TrySendRackStackToBelt(stack);
        }
    }
}
