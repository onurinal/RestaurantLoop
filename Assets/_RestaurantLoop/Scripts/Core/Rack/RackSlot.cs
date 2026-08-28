namespace RestaurantLoop.Core
{
    /// <summary>
    /// Slot implementation for the Rack system.
    /// Inherits cell visual management and stack placement logic from BaseSlot.
    /// </summary>
    public class RackSlot : BaseSlot
    {
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
