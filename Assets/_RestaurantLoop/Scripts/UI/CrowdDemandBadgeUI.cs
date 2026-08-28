using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RestaurantLoop.Core;

namespace RestaurantLoop.UI
{
    public class CrowdDemandBadgeUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI countText;

        public ItemDataSO Data { get; private set; }

        public void Setup(ItemDataSO data, int count)
        {
            Data = data;

            if (iconImage != null && data != null)
            {
                iconImage.sprite = data.BalloonIcon;
            }

            UpdateCount(count);
        }

        public void UpdateCount(int count)
        {
            if (countText != null)
            {
                countText.text = $"x{count}";
            }

            gameObject.SetActive(count > 0);
        }
    }
}