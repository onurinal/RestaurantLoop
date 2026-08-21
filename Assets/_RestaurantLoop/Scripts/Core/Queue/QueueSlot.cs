namespace RestaurantLoop.Core
{
    /// <summary>
    /// Slot implementation for the Queue system.
    /// </summary>
    public class QueueSlot : BaseSlot
    {
        public override void OnStackTapped(StackItem stack)
        {
            QueueColumn column = GetComponentInParent<QueueColumn>();

            if (column != null && column.FrontSlot == this)
            {
                column.TrySendFrontStackToBelt();
            }
        }
    }
}