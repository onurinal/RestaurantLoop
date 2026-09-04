using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace RestaurantLoop.UI
{
    /// <summary>
    /// Owns only the visual state of the vibration switch. Preference and haptic behavior remain in
    /// VibrationManager, while UI managers decide when the state changes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VibrationSwitchView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Slider slider;
        [SerializeField] private Image background;
        [SerializeField] private RectTransform handle;

        [Header("State Colors")]
        [SerializeField] private Color enabledColor = new Color(0.18f, 0.78f, 0.34f, 1f);
        [SerializeField] private Color disabledColor = new Color(0.86f, 0.22f, 0.25f, 1f);

        [Header("Transition")]
        [SerializeField, Min(0f)] private float colorDuration = 0.16f;
        [SerializeField, Range(1f, 1.3f)] private float handlePunchScale = 1.12f;
        [SerializeField, Min(0f)] private float handlePunchDuration = 0.14f;

        private Vector3 handleBaseScale = Vector3.one;
        private bool currentState;
        private bool hasState;
        private Tween colorTween;
        private Sequence handleTween;

        private void Awake()
        {
            ResolveReferences();
            CacheHandleScale();
        }

        public void Initialize(Slider sourceSlider)
        {
            if (slider == null) slider = sourceSlider;
            ResolveReferences();
            CacheHandleScale();
        }

        public void SetState(bool enabled, bool animate)
        {
            ResolveReferences();
            KillTweensAndRestoreHandle();
            currentState = enabled;
            hasState = true;

            slider?.SetValueWithoutNotify(enabled ? 1f : 0f);

            Color targetColor = enabled ? enabledColor : disabledColor;
            if (background != null)
            {
                if (animate && colorDuration > 0f)
                {
                    colorTween = background.DOColor(targetColor, colorDuration)
                        .SetEase(Ease.OutCubic)
                        .SetUpdate(true)
                        .OnComplete(() => colorTween = null);
                }
                else
                {
                    background.color = targetColor;
                }
            }

            if (animate && handle != null && handlePunchDuration > 0f)
            {
                handleTween = DOTween.Sequence()
                    .Append(handle.DOScale(handleBaseScale * handlePunchScale, handlePunchDuration * 0.45f)
                        .SetEase(Ease.OutQuad))
                    .Append(handle.DOScale(handleBaseScale, handlePunchDuration * 0.55f)
                        .SetEase(Ease.OutBack))
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        handleTween = null;
                        RestoreHandleScale();
                    });
            }
        }

        private void ResolveReferences()
        {
            if (slider == null) slider = GetComponent<Slider>();
            if (slider == null) return;

            if (background == null)
            {
                Transform backgroundTransform = slider.transform.Find("Background");
                if (backgroundTransform != null) background = backgroundTransform.GetComponent<Image>();
            }

            if (handle == null) handle = slider.handleRect;
        }

        private void CacheHandleScale()
        {
            if (handle != null) handleBaseScale = handle.localScale;
        }

        private void KillTweensAndRestoreHandle()
        {
            colorTween?.Kill(false);
            handleTween?.Kill(false);
            colorTween = null;
            handleTween = null;
            RestoreHandleScale();
        }

        private void RestoreHandleScale()
        {
            if (handle != null) handle.localScale = handleBaseScale;
        }

        private void OnDisable()
        {
            KillTweensAndRestoreHandle();
            if (!hasState) return;

            slider?.SetValueWithoutNotify(currentState ? 1f : 0f);
            if (background != null)
            {
                background.color = currentState ? enabledColor : disabledColor;
            }
        }
    }
}
