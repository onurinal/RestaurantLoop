using UnityEngine;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Represents central station data model and visual view binding.
    /// </summary>
    public class CustomerStation
    {
        public ItemDataSO itemData;
        public int remainingCount;
        public Vector3 position;
        public CustomerStationView view;

        public void UpdateUI()
        {
            if (view != null)
            {
                view.UpdateCount(remainingCount);
            }
        }
    }
}