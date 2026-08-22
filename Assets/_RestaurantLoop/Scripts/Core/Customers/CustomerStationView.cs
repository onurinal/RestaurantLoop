using UnityEngine;
using TMPro;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Visual view attached to the Station Prefab. Controls table UI text, height offset positioning, billboarding, and dynamic material color assignment.
    /// </summary>
    public class CustomerStationView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text counterText;
        [SerializeField] private MeshRenderer tableRenderer;

        [Header("UI Offset Settings")]
        [SerializeField] private float textHeightOffset = 1.2f;

        private Camera mainCamera;

        private void Awake()
        {
            mainCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (counterText != null && counterText.gameObject.activeSelf && mainCamera != null)
            {
                counterText.transform.rotation = mainCamera.transform.rotation;
            }
        }

        public void Initialize(ItemDataSO data, int count)
        {
            if (tableRenderer != null && data != null)
            {
                tableRenderer.material.color = data.UIColor;
            }

            PositionText();
            UpdateCount(count);
        }

        public void UpdateCount(int count)
        {
            if (counterText != null)
            {
                counterText.gameObject.SetActive(count > 0);
                if (count > 0)
                {
                    counterText.text = $"x{count}";
                    PositionText();
                }
            }
        }

        private void PositionText()
        {
            if (counterText != null)
            {
                counterText.transform.localPosition = new Vector3(0f, textHeightOffset, 0f);
            }
        }
    }
}