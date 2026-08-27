using UnityEngine;

namespace RestaurantLoop.Core
{
    public class OrderBalloon : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer bgRenderer;

        [Header("3D Food Display")]
        [Tooltip("Presentation-only food prefab. Use a centered pivot and renderer(s) only; do not use the gameplay stack prefab.")]
        [SerializeField] private GameObject foodDisplayPrefab;
        [SerializeField] private Vector3 foodLocalPosition = new Vector3(0f, 0f, -0.35f);
        [SerializeField, Min(0.01f)] private float foodScale = 0.7f;
        [SerializeField, Min(0f)] private float rotationSpeed = 20f;

        private Transform foodDisplayTransform;

        private void Awake()
        {
            EnsureRenderer();
            CreateFoodDisplay();
        }

        private void LateUpdate()
        {
            if (foodDisplayTransform == null)
            {
                return;
            }

            if (rotationSpeed > 0f)
            {
                foodDisplayTransform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
            }

            BalloonFoodOverlayCamera.EnsureConfigured();
        }

        private void EnsureRenderer()
        {
            if (bgRenderer != null)
                return;

            bgRenderer = GetComponent<SpriteRenderer>();

            if (bgRenderer == null)
            {
                bgRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        private void CreateFoodDisplay()
        {
            if (foodDisplayPrefab == null || foodDisplayTransform != null)
            {
                return;
            }

            GameObject foodDisplay = Instantiate(foodDisplayPrefab, transform);
            foodDisplay.name = "Food Display";
            foodDisplayTransform = foodDisplay.transform;
            foodDisplayTransform.localPosition = foodLocalPosition;
            foodDisplayTransform.localRotation = Quaternion.identity;
            foodDisplayTransform.localScale = Vector3.one * foodScale;

            BalloonFoodOverlayCamera.Register(foodDisplay);
        }

        public void SetColorAndState(Color baseColor, bool isEdge)
        {
            EnsureRenderer();

            if (bgRenderer == null)
                return;

            bgRenderer.color = baseColor;
        }
    }
}
