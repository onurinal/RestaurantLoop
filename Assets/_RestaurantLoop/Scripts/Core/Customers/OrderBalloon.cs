using UnityEngine;

namespace RestaurantLoop.Core
{
    public class OrderBalloon : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer bgRenderer;

        private void Awake()
        {
            EnsureRenderer();
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

        public void SetColorAndState(Color baseColor, bool isEdge)
        {
            EnsureRenderer();

            if (bgRenderer == null)
                return;

            bgRenderer.color = baseColor;
        }
    }
}