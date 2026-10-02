using TMPro;
using UnityEngine;
using DG.Tweening;
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

        [Header("Capacity Rejection Feedback")]
        [SerializeField] private Color capacityRejectedColor = Color.red;
        [SerializeField, Min(0.01f)] private float rejectionFeedbackDuration = 0.3f;
        [SerializeField, Min(0f)] private float rejectionShakeDegrees = 12f;
        [SerializeField, Min(1)] private int rejectionShakeVibrato = 10;

        [Header("Capacity Increase Feedback")]
        [SerializeField, Min(1f)] private float capacityIncreaseScale = 1.25f;
        [SerializeField, Min(0.01f)] private float capacityIncreaseDuration = 0.15f;

        private Color defaultTextColor;
        private Quaternion defaultTextRotation;
        private Vector3 defaultTextScale;
        private Tween rejectionFeedbackTween;
        private Tween capacityIncreaseFeedbackTween;

        private void Awake()
        {
            if (countText == null) return;

            defaultTextColor = countText.color;
            defaultTextRotation = countText.rectTransform.localRotation;
            defaultTextScale = countText.rectTransform.localScale;
        }

        private void OnEnable()
        {
            if (conveyor == null)
            {
                return;
            }

            conveyor.CapacityChanged += Refresh;
            conveyor.CapacityIncreased += PlayCapacityIncreasedFeedback;
            conveyor.CapacityRejected += PlayCapacityRejectedFeedback;
            Refresh(conveyor.OccupiedCapacity, conveyor.MaxCapacity);
        }

        private void OnDisable()
        {
            if (conveyor != null)
            {
                conveyor.CapacityChanged -= Refresh;
                conveyor.CapacityIncreased -= PlayCapacityIncreasedFeedback;
                conveyor.CapacityRejected -= PlayCapacityRejectedFeedback;
            }

            rejectionFeedbackTween?.Kill(false);
            capacityIncreaseFeedbackTween?.Kill(false);
            rejectionFeedbackTween = null;
            capacityIncreaseFeedbackTween = null;
            ResetFeedbackVisuals();
            ResetCapacityIncreaseVisuals();
        }

        private void Refresh(int occupied, int maximum)
        {
            if (countText != null)
            {
                countText.text = $"{occupied}/{maximum}";
            }
        }

        private void PlayCapacityRejectedFeedback()
        {
            if (countText == null) return;

            rejectionFeedbackTween?.Kill(false);
            rejectionFeedbackTween = null;
            ResetFeedbackVisuals();

            Color rejectedColor = capacityRejectedColor;
            rejectedColor.a = defaultTextColor.a;
            countText.color = rejectedColor;

            rejectionFeedbackTween = DOTween.Sequence()
                .Join(countText.rectTransform.DOShakeRotation(
                    rejectionFeedbackDuration,
                    new Vector3(0f, 0f, rejectionShakeDegrees),
                    rejectionShakeVibrato,
                    90f,
                    false))
                .Append(countText.DOColor(defaultTextColor, rejectionFeedbackDuration))
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    rejectionFeedbackTween = null;
                    ResetFeedbackVisuals();
                });
        }

        private void PlayCapacityIncreasedFeedback()
        {
            if (countText == null) return;

            capacityIncreaseFeedbackTween?.Kill(false);
            capacityIncreaseFeedbackTween = null;
            ResetCapacityIncreaseVisuals();

            capacityIncreaseFeedbackTween = DOTween.Sequence()
                .Append(countText.rectTransform.DOScale(defaultTextScale * capacityIncreaseScale, capacityIncreaseDuration)
                    .SetEase(Ease.OutBack))
                .Append(countText.rectTransform.DOScale(defaultTextScale, capacityIncreaseDuration)
                    .SetEase(Ease.InOutQuad))
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    capacityIncreaseFeedbackTween = null;
                    ResetCapacityIncreaseVisuals();
                });
        }

        private void ResetFeedbackVisuals()
        {
            if (countText == null) return;

            countText.rectTransform.localRotation = defaultTextRotation;
            countText.color = defaultTextColor;
        }

        private void ResetCapacityIncreaseVisuals()
        {
            if (countText != null)
            {
                countText.rectTransform.localScale = defaultTextScale;
            }
        }

        private void OnDestroy()
        {
            rejectionFeedbackTween?.Kill(false);
            capacityIncreaseFeedbackTween?.Kill(false);
        }
    }
}
