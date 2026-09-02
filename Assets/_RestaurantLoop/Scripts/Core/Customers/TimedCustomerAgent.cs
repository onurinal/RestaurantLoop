using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using RestaurantLoop.Audio;
using TMPro;

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
        [SerializeField] private RectTransform pulseTarget;

        [Header("Low Time Alert")]
        [Tooltip("Normalized remaining time percentage (0.05 to 0.9) to trigger the warning state. 0.5 = 50%.")]
        [SerializeField, Range(0.05f, 0.9f)] private float warningNormalizedThreshold = 0.5f;
        [Tooltip("Second-based threshold override (disabled when set to 0).")]
        [SerializeField, Min(0f)] private float warningSecondsThreshold = 0f;
        [SerializeField, Min(0f)] private float pulseSpeed = 8f;
        [SerializeField, Range(0f, 0.3f)] private float pulseScaleAmount = 0.1f;
        [SerializeField] private Color normalColor = new Color(0.18f, 0.85f, 0.25f, 1f);
        [SerializeField] private Color warningColor = new Color(1f, 0.78f, 0.08f, 1f);
        [SerializeField] private Color criticalColor = new Color(0.95f, 0.08f, 0.06f, 1f);

        [Header("Pop-Up Attention Animation")]
        [SerializeField] private float popDuration = 0.4f;
        [SerializeField] private float punchAmount = 0.35f;

        [Header("Final Countdown")]
        [SerializeField] private TMP_Text countdownText;
        [Tooltip("Position offset for the countdown text. Use a negative Z value to render in front of other UI elements.")]
        [SerializeField] private Vector3 countdownOffset = new Vector3(0f, 125f, -1f);
        [SerializeField, Min(0.01f)] private float countdownPopDuration = 0.28f;
        [SerializeField, Min(0f)] private float countdownVisibleDuration = 0.45f;
        [Tooltip("Font size override for the final countdown digits.")]
        [SerializeField, Min(1f)] private float countdownFontSize = 92f;
        [SerializeField, Range(0f, 20f)] private float countdownTiltAngle = 8f;

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
        private Sequence countdownTween;
        private int lastCountdownDigit;

        public bool IsTimed => isTimed;
        public float RemainingTime => remainingTime;
        public float Duration => duration;

        private void Awake()
        {
            if (customer == null) customer = GetComponent<Customer>();
            if (pulseTarget == null && timerRoot != null) pulseTarget = timerRoot.transform as RectTransform;
            ConfigureRadialClockImage();
            EnsureCountdownText();
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

            // Stop sounds and halt countdown immediately if the level is no longer active
            if (LevelManager.Instance != null && !LevelManager.Instance.IsGameActive)
            {
                StopWarningSoundTracker();
                return;
            }

            remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
            RefreshVisuals();

            int countdownDigit = Mathf.CeilToInt(remainingTime);
            if (remainingTime <= 3f && countdownDigit >= 1 && countdownDigit != lastCountdownDigit)
            {
                lastCountdownDigit = countdownDigit;
                ShowFinalCountdownDigit(countdownDigit);
            }

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
            lastCountdownDigit = 0;

            StopWarningSoundTracker();
            hasPlayedWarningSound = false;
            isCriticalSoundActive = false;

            if (pulseTarget != null) pulseTarget.localScale = pulseBaseScale;
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
            if (pulseTarget != null) pulseBaseScale = pulseTarget.localScale;

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

            countdownTween?.Kill();
            countdownTween = null;
            if (countdownText != null)
            {
                countdownText.DOKill();
                countdownText.gameObject.SetActive(false);
            }
        }

        private void ShowFinalCountdownDigit(int digit)
        {
            if (countdownText == null) return;

            RectTransform target = countdownText.rectTransform;
            target.anchoredPosition3D = countdownOffset; // Override position (X, Y, Z) with Inspector offset

            countdownTween?.Kill();
            target.DOKill();
            countdownText.DOKill();
            countdownText.text = digit.ToString();
            countdownText.gameObject.SetActive(true);
            countdownText.alpha = 1f;
            target.localScale = Vector3.zero;
            target.localRotation = Quaternion.identity;

            countdownTween = DOTween.Sequence()
                .SetUpdate(true)
                .SetTarget(target)
                .Append(target.DOScale(1.15f, countdownPopDuration * 0.68f).SetEase(Ease.OutBack))
                .Append(target.DOScale(1f, countdownPopDuration * 0.32f).SetEase(Ease.OutQuad))
                .AppendInterval(countdownVisibleDuration)
                .Append(countdownText.DOFade(0f, 0.16f).SetEase(Ease.InQuad))
                .Join(target.DOScale(0.72f, 0.16f).SetEase(Ease.InBack))
                .OnComplete(() =>
                {
                    countdownTween = null;
                    if (countdownText != null) countdownText.gameObject.SetActive(false);
                });

            customer?.PlayCountdownUrgency(countdownTiltAngle, countdownPopDuration);
        }

        private void EnsureCountdownText()
        {
            if (countdownText != null)
            {
                // Force configured offset and font size onto existing countdown text element
                countdownText.rectTransform.anchoredPosition3D = countdownOffset;
                if (countdownFontSize > 0f) countdownText.fontSize = countdownFontSize;
                return;
            }

            if (timerRoot == null) return;

            // Dynamically instantiate UI text if none was assigned in Inspector
            GameObject textObject = new GameObject("FinalCountdownText", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(timerRoot.transform, false);
            countdownText = textObject.GetComponent<TextMeshProUGUI>();
            countdownText.font = TMP_Settings.defaultFontAsset;
            countdownText.fontSize = countdownFontSize;
            countdownText.fontStyle = FontStyles.Bold;
            countdownText.color = Color.white;
            countdownText.outlineColor = Color.black;
            countdownText.outlineWidth = 0.24f;
            countdownText.alignment = TextAlignmentOptions.Center;
            countdownText.raycastTarget = false;

            RectTransform rect = countdownText.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(180f, 130f);
            rect.anchoredPosition3D = countdownOffset;
            textObject.SetActive(false);
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

            if (!hasPlayedWarningSound)
            {
                hasPlayedWarningSound = true;
                isWarningSoundActive = true;
                if (AudioManager.Instance != null) AudioManager.Instance.StartTimedWarning();
            }

            float criticalBoundary = warningBoundary * 0.4f;
            if (normalized <= criticalBoundary && !isCriticalSoundActive)
            {
                isCriticalSoundActive = true;
                if (AudioManager.Instance != null) AudioManager.Instance.StartCriticalWarning();
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