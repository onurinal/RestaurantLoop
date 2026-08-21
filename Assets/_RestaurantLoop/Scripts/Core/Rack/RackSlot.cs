namespace RestaurantLoop.Core
{
    /// <summary>
    /// Slot implementation for the Rack system.
    /// </summary>
    public class RackSlot : BaseSlot
    {
        public override void OnStackTapped(StackItem stack)
        {
            RackManager.Instance?.TrySendRackStackToBelt(stack);
        }
    }
}