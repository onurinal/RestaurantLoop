using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RestaurantLoop.UI
{
    [RequireComponent(typeof(Button))]
    public sealed class ButtonPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Header("Press Animation")]
        [SerializeField, Range(0.75f, 1f)] private float pressedScale = 0.9f;
        [SerializeField, Min(0f)] private float pressDuration = 0.08f;
        [SerializeField, Min(0f)] private float releaseDuration = 0.2f;
        [SerializeField] private Ease pressEase = Ease.OutQuad;
        [SerializeField] private Ease releaseEase = Ease.OutBack;

        private Button button;
        private Vector3 restingScale;
        private Tween scaleTween;
        private bool isPressed;

        private void Awake()
        {
            button = GetComponent<Button>();
            restingScale = transform.localScale;
        }

        private void OnEnable()
        {
            restingScale = transform.localScale;
        }

        private void OnDisable()
        {
            isPressed = false;
            scaleTween?.Kill();
            scaleTween = null;
            transform.localScale = restingScale;
        }

        private void OnDestroy()
        {
            scaleTween?.Kill();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (button == null || !button.interactable) return;

            isPressed = true;
            AnimateTo(restingScale * pressedScale, pressDuration, pressEase);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (isPressed) Release();
        }

        private void Release()
        {
            if (!isPressed) return;

            isPressed = false;
            AnimateTo(restingScale, releaseDuration, releaseEase);
        }

        private void AnimateTo(Vector3 targetScale, float duration, Ease ease)
        {
            scaleTween?.Kill();
            scaleTween = transform.DOScale(targetScale, duration)
                .SetEase(ease)
                .SetUpdate(true);
        }
    }
}
