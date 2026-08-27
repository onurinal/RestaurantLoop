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
            RackManager.Instance?.TrySendRackStackToBelt(stack);
        }
    }
}