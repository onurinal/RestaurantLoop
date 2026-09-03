using DG.Tweening;
using UnityEngine;

namespace RestaurantLoop.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class GameTitleHover : MonoBehaviour
    {
        [Header("Hover Animation")]
        [SerializeField, Min(0f)] private float verticalDistance = 12f;
        [SerializeField, Min(0.05f)] private float halfCycleDuration = 1.25f;
        [SerializeField] private Ease ease = Ease.InOutSine;
        [SerializeField] private bool useUnscaledTime = true;

        private RectTransform titleRect;
        private Vector2 restingAnchoredPosition;
        private Tween hoverTween;

        private void Awake()
        {
            titleRect = (RectTransform)transform;
        }

        private void OnEnable()
        {
            restingAnchoredPosition = titleRect.anchoredPosition;
            PlayHover();
        }

        private void OnDisable()
        {
            StopHover();
            titleRect.anchoredPosition = restingAnchoredPosition;
        }

        private void OnDestroy()
        {
            StopHover();
        }

        private void PlayHover()
        {
            StopHover();

            if (verticalDistance <= 0f)
            {
                return;
            }

            hoverTween = titleRect
                .DOAnchorPosY(restingAnchoredPosition.y + verticalDistance, halfCycleDuration)
                .SetEase(ease)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(useUnscaledTime)
                .SetLink(gameObject);
        }

        private void StopHover()
        {
            hoverTween?.Kill();
            hoverTween = null;
        }
    }
}
