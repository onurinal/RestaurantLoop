namespace RestaurantLoop.Core
{
    /// <summary>
    /// Slot implementation for the Queue system.
    /// </summary>
    public class QueueSlot : BaseSlot
    {
        public override void OnStackTapped(StackItem stack)
        {
            GetComponentInParent<QueueColumn>()?.TrySendFrontStackToBelt();
        }
    }
}