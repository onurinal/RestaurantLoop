namespace RestaurantLoop.Core
{
    /// <summary>
    /// Slot implementation for the Queue system.
    /// </summary>
    public class QueueSlot : BaseSlot
    {
        public override void OnStackTapped(StackItem stack)
        {
            if (PowerUpManager.Instance != null && PowerUpManager.Instance.IsClearColorSelectionActive)
            {
                PowerUpManager.Instance.TrySelectClearColorStack(stack);
                return;
            }

            if (PowerUpManager.Instance != null && PowerUpManager.Instance.IsHandSelectionActive)
            {
                PowerUpManager.Instance.TrySelectHandStack(stack);
                return;
            }

            QueueColumn column = GetComponentInParent<QueueColumn>();

            if (column != null && column.FrontSlot == this)
            {
                column.TrySendFrontStackToBelt();
            }
        }
    }
}
