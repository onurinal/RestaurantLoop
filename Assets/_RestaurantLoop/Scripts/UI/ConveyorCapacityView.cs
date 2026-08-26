using TMPro;
using UnityEngine;
using RestaurantLoop.Core;

namespace RestaurantLoop.UI
{
    /// <summary>
    /// Presentation-only capacity label. It observes ConveyorManager and owns no gameplay state.
    /// </summary>
    public sealed class ConveyorCapacityView : MonoBehaviour
    {
        [SerializeField] private ConveyorManager conveyor;
        [SerializeField] private TMP_Text countText;

        private void OnEnable()
        {
            if (conveyor == null)
            {
                return;
            }

            conveyor.CapacityChanged += Refresh;
            Refresh(conveyor.OccupiedCapacity, conveyor.MaxCapacity);
        }

        private void OnDisable()
        {
            if (conveyor != null)
            {
                conveyor.CapacityChanged -= Refresh;
            }
        }

        private void Refresh(int occupied, int maximum)
        {
            if (countText != null)
            {
                countText.text = $"{occupied}/{maximum}";
            }
        }
    }
}