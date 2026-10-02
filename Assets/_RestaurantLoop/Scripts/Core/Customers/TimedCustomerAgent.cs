using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using RestaurantLoop.Audio;
using RestaurantLoop.Infrastructure;

namespace RestaurantLoop.Core
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Customer))]
    public sealed class TimedCustomerAgent : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Customer customer;
        [SerializeField] private GameObject timerRoot;
        [SerializeField] private Image timerFillImage;
        [SerializeField] private Image hourglassImage;
        [SerializeField] private RectTransform pulseTarget;

        [Header("Low Time Alert")]
        [Tooltip("Normalized remaining time percentage (0.05 to 0.9) to trigger the warning state. 0.5 = 50%.")]
        [SerializeField, Range(0.05f, 0.9f)] private float warningNormalizedThreshold = 0.5f;
        [SerializeField, Min(0f)] private float pulseSpeed = 8f;
        [SerializeField, Range(0f, 0.3f)] private float pulseScaleAmount = 0.1f;
        [SerializeField] private Color normalColor = new Color(0.18f, 0.85f, 0.25f, 1f);
        [SerializeField] private Color warningColor = new Color(1f, 0.78f, 0.08f, 1f);
        [SerializeField] private Color criticalColor = new Color(0.95f, 0.08f, 0.06f, 1f);

        [Header("Pop-Up Attention Animation")]
        [SerializeField] private float popDuration = 0.4f;
        [SerializeField] private float punchAmount = 0.35f;

        private float duration;
        private float remainingTime;
        private float pulsePhase;
        private Vector3 pulseBaseScale = Vector3.one;
        private bool isTimed;
        private bool timerStarted;
        private bool completed;

        private bool hasPlayedWarningSound;
        private bool isWarningSoundActive;
        private bool isCriticalSoundActive;

        private Tween popTween;

        public bool IsTimed => isTimed;
        public float RemainingTime => remainingTime;
        public float Duration => duration;

        private void Awake()
        {
            if (customer == null) customer = GetComponent<Customer>();
            if (pulseTarget == null && timerRoot != null) pulseTarget = timerRoot.transform as RectTransform;
            ConfigureRadialClockImage();
            
            pulseBaseScale = Vector3.one;
            pulsePhase = Mathf.Abs(GetInstanceID() % 360) * Mathf.Deg2Rad;
            SetTimerVisible(false);
        }

        private void OnEnable()
        {
            KillPopTween();
            SetTimerVisible(isTimed && timerStarted && !completed);
            RefreshVisuals();
        }

        private void OnDisable()
        {
            KillPopTween();
            SetTimerVisible(false);
            StopWarningSoundTracker();
        }

        private void Update()
        {
            if (!isTimed || completed || customer == null) return;

            if (customer.IsServed)
            {
                MarkServed();
                return;
            }

            if (!timerStarted)
            {
                if (!CanStartCountdown()) return;
                timerStarted = true;
                ActivateAndPlayPopAnimation();
            }

            if (LevelManager.Instance != null && !LevelManager.Instance.IsGameActive)
            {
                StopWarningSoundTracker();
                return;
            }

            remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
            RefreshVisuals();

            if (remainingTime <= 0f) Expire();
        }

        public void Configure(bool timed, float timeLimitDuration)
        {
            KillPopTween();
            isTimed = timed && timeLimitDuration > 0f;
            duration = isTimed ? timeLimitDuration : 0f;
            remainingTime = duration;
            timerStarted = false;
            completed = false;

            StopWarningSoundTracker();
            hasPlayedWarningSound = false;
            isCriticalSoundActive = false;

            pulseBaseScale = Vector3.one;
            SetTimerVisible(false);
            RefreshVisuals();
        }

        public void MarkServed()
        {
            if (!isTimed || completed) return;
            completed = true;
            timerStarted = false;
            KillPopTween();
            SetTimerVisible(false);

            StopWarningSoundTracker();
        }

        private void StopWarningSoundTracker()
        {
            if (isWarningSoundActive)
            {
                isWarningSoundActive = false;
                if (AudioManager.Instance != null) AudioManager.Instance.StopTimedWarning();
            }

            if (isCriticalSoundActive)
            {
                isCriticalSoundActive = false;
                if (AudioManager.Instance != null) AudioManager.Instance.StopCriticalWarning();
            }
        }

        private bool CanStartCountdown()
        {
            if (!customer.IsEdgeCustomer) return false;
            if (CrowdManager.Instance != null && CrowdManager.Instance.IsSpawningCustomers) return false;
            return LevelManager.Instance == null || LevelManager.Instance.IsGameActive;
        }

        private void ActivateAndPlayPopAnimation()
        {
            SetTimerVisible(true);

            if (!hasPlayedWarningSound)
            {
                hasPlayedWarningSound = true;
                isWarningSoundActive = true;
                if (AudioManager.Instance != null) AudioManager.Instance.StartTimedWarning();
            }

            if (pulseTarget == null) return;

            KillPopTween();
            pulseTarget.localScale = Vector3.zero;

            popTween = pulseTarget.DOScale(pulseBaseScale, popDuration)
                .SetEase(Ease.OutBack)
                .OnComplete(() => { popTween = pulseTarget.DOPunchScale(pulseBaseScale * punchAmount, 0.35f, 6, 0.5f); });
        }

        private void KillPopTween()
        {
            if (popTween != null && popTween.IsActive())
            {
                popTween.Kill();
                popTween = null;
            }

            if (pulseTarget != null)
            {
                pulseTarget.DOKill();
                pulseTarget.localScale = pulseBaseScale;
            }
        }

        private void Expire()
        {
            if (completed) return;
            completed = true;
            timerStarted = false;
            remainingTime = 0f;
            KillPopTween();
            RefreshVisuals();

            StopWarningSoundTracker();

            customer.PlayTimedFailureReaction();
            LevelManager.Instance?.OnLevelFailed();
        }

        private void RefreshVisuals()
        {
            if (!isTimed || timerFillImage == null) return;

            float normalized = duration > 0f ? Mathf.Clamp01(remainingTime / duration) : 0f;
            timerFillImage.fillAmount = normalized;

            float warningBoundary = warningNormalizedThreshold;
            Color currentColor;

            if (normalized > warningBoundary)
            {
                float safeRange = Mathf.Max(0.0001f, 1f - warningBoundary);
                currentColor = Color.Lerp(warningColor, normalColor, (normalized - warningBoundary) / safeRange);
            }
            else
            {
                float warningProgress = warningBoundary > 0f ? normalized / warningBoundary : 0f;
                currentColor = Color.Lerp(criticalColor, warningColor, warningProgress);
            }

            timerFillImage.color = currentColor;
            if (hourglassImage != null) hourglassImage.color = currentColor;

            if (timerStarted && !completed)
            {
                float criticalBoundary = warningBoundary * 0.4f;
                if (normalized <= criticalBoundary && !isCriticalSoundActive)
                {
                    isCriticalSoundActive = true;
                    VibrationManager.Instance?.PlayTimedWarningVibration();
                    if (AudioManager.Instance != null) AudioManager.Instance.StartCriticalWarning();
                }

                if (pulseTarget != null && (popTween == null || !popTween.IsActive()))
                {
                    float currentPulseSpeed = normalized <= criticalBoundary ? pulseSpeed * 1.4f : pulseSpeed;
                    float pulse = 1f + Mathf.Sin(Time.time * currentPulseSpeed + pulsePhase) * pulseScaleAmount;
                    pulseTarget.localScale = pulseBaseScale * pulse;
                }
            }
        }

        private void SetTimerVisible(bool visible)
        {
            if (timerRoot != null && timerRoot.activeSelf != visible) timerRoot.SetActive(visible);
        }

        private void ConfigureRadialClockImage()
        {
            if (timerFillImage == null) return;
            timerFillImage.type = Image.Type.Filled;
            timerFillImage.fillMethod = Image.FillMethod.Radial360;
            timerFillImage.fillOrigin = (int)Image.Origin360.Top;
            timerFillImage.fillClockwise = true;
            timerFillImage.raycastTarget = false;
        }
    }
}