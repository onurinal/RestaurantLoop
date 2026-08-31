using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Owns the optional timed-customer countdown and its lightweight world-space UI.
    /// The UI remains hidden while waiting in the inner crowd and pops up with an attention-grabbing
    /// animation only when seated at an active edge slot.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Customer))]
    public sealed class TimedCustomerAgent : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Customer customer;
        [SerializeField] private GameObject timerRoot;
        [SerializeField] private Image timerFillImage;
        [SerializeField] private RectTransform pulseTarget;

        [Header("Low Time Alert")]
        [SerializeField, Range(0.05f, 0.9f)] private float warningNormalizedThreshold = 0.25f;
        [SerializeField, Min(0f)] private float warningSecondsThreshold = 3f;
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
        private Tween popTween;

        public bool IsTimed => isTimed;
        public float RemainingTime => remainingTime;
        public float Duration => duration;

        private void Awake()
        {
            if (customer == null) customer = GetComponent<Customer>();
            if (pulseTarget == null && timerRoot != null) pulseTarget = timerRoot.transform as RectTransform;
            ConfigureRadialClockImage();
            if (pulseTarget != null) pulseBaseScale = pulseTarget.localScale;
            pulsePhase = Mathf.Abs(GetInstanceID() % 360) * Mathf.Deg2Rad;
            SetTimerVisible(false);
        }

        private void OnEnable()
        {
            KillPopTween();
            if (pulseTarget != null) pulseTarget.localScale = pulseBaseScale;
            SetTimerVisible(isTimed && timerStarted && !completed);
            RefreshVisuals();
        }

        private void OnDisable()
        {
            KillPopTween();
            if (pulseTarget != null) pulseTarget.localScale = pulseBaseScale;
            SetTimerVisible(false);
        }

        private void Update()
        {
            if (!isTimed || completed || customer == null) return;

            if (customer.IsServed)
            {
                MarkServed();
                return;
            }

            // Start countdown and pop UI only when promoted/seated at active edge slot
            if (!timerStarted)
            {
                if (!CanStartCountdown()) return;
                timerStarted = true;
                ActivateAndPlayPopAnimation();
            }

            if (LevelManager.Instance != null && !LevelManager.Instance.IsGameActive) return;

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

            if (pulseTarget != null) pulseTarget.localScale = pulseBaseScale;
            SetTimerVisible(false); // Hidden while waiting in inner crowd
            RefreshVisuals();
        }

        public void MarkServed()
        {
            if (!isTimed || completed) return;
            completed = true;
            timerStarted = false;
            KillPopTween();
            SetTimerVisible(false);
            if (pulseTarget != null) pulseTarget.localScale = pulseBaseScale;
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

            if (pulseTarget != null) pulseTarget.DOKill();
        }

        private void Expire()
        {
            if (completed) return;
            completed = true;
            timerStarted = false;
            remainingTime = 0f;
            KillPopTween();
            RefreshVisuals();
            customer.PlayTimedFailureReaction();
            LevelManager.Instance?.OnLevelFailed();
        }

        private void RefreshVisuals()
        {
            if (timerFillImage == null) return;

            float normalized = duration > 0f ? Mathf.Clamp01(remainingTime / duration) : 0f;
            timerFillImage.fillAmount = normalized;

            float warningBoundary = duration > 0f
                ? Mathf.Clamp01(Mathf.Max(warningNormalizedThreshold, warningSecondsThreshold / duration))
                : warningNormalizedThreshold;

            if (normalized > warningBoundary)
            {
                float safeRange = Mathf.Max(0.0001f, 1f - warningBoundary);
                timerFillImage.color = Color.Lerp(warningColor, normalColor, (normalized - warningBoundary) / safeRange);
                if (pulseTarget != null && (popTween == null || !popTween.IsActive()))
                {
                    pulseTarget.localScale = pulseBaseScale;
                }

                return;
            }

            float warningProgress = warningBoundary > 0f ? normalized / warningBoundary : 0f;
            timerFillImage.color = Color.Lerp(criticalColor, warningColor, warningProgress);

            if (pulseTarget != null && (popTween == null || !popTween.IsActive()))
            {
                float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed + pulsePhase) * pulseScaleAmount;
                pulseTarget.localScale = pulseBaseScale * pulse;
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