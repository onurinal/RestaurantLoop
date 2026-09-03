using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace RestaurantLoop.UI
{
    [Serializable]
    public class WinBannerSettings
    {
        [Header("Pop In Animation")]
        [Min(0.01f)] public float popInDuration = 0.35f;
        public Ease popInEase = Ease.OutBack;

        [Header("Idle Pulse (While Confetti Plays)")]
        [Tooltip("Subtle breathing scale factor while confetti effect is active.")]
        [Range(1.01f, 1.25f)] public float idlePulseScale = 1.05f;
        [Min(0.1f)] public float idlePulseDuration = 0.55f;

        [Header("Exit Animation")]
        [Min(0.01f)] public float exitDuration = 0.3f;
        public Ease exitEase = Ease.InBack;

        [Tooltip("Fallback hold duration if no firework controller is assigned.")]
        [Min(0f)] public float fallbackHoldDuration = 1.5f;
    }

    [Serializable]
    public class FailureBannerSettings
    {
        [Min(0.01f)] public float slideInDuration = 0.35f;
        [Range(0.2f, 2f)] public float holdDuration = 1f;
        [Min(0.01f)] public float slideOutDuration = 0.32f;
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class GameStateTransitionController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private TMP_FontAsset bannerFont;

        [Tooltip("Assign the WinFireworkFXController here. Banner will stay pulsing while confetti plays and exit when confetti finishes.")]
        [SerializeField] private WinFireworkFXController fireworkController;

        [Header("Win Sequence Settings")]
        [SerializeField] private WinBannerSettings winSequence = new WinBannerSettings();
        [SerializeField] private Color winOverlayColor = new Color(0.11f, 0.06f, 0.18f, 0.52f);

        [Header("Win Single Banner Sprite")]
        [Tooltip("Assign the unified RESTAURANT LOOP graphic sprite here.")]
        [SerializeField] private Sprite winBannerSprite;
        [SerializeField] private Vector2 winBannerSize = new Vector2(750f, 380f);
        [SerializeField] private float winBannerOffsetY = 200f;

        [Header("Failure Sequence Settings")]
        [SerializeField] private FailureBannerSettings failureSequence = new FailureBannerSettings();
        [SerializeField] private Vector2 failureBannerSize = new Vector2(1200f, 244f);
        [SerializeField] private Color failureOverlayColor = new Color(0f, 0f, 0f, 0.5f);

        [Header("Failure Banner Customization")]
        [SerializeField] private Sprite failureBannerSprite;
        [SerializeField] private Color failureBannerBackgroundColor = Color.white;
        [SerializeField] private bool useSlicedBannerImage = true;

        [Header("Failure Text Customization")]
        [SerializeField, Min(10f)] private float failureFontSize = 110f;
        [SerializeField] private Color failureTextColor = Color.white;

        private RectTransform overlayRoot;
        private Image overlayBlocker;
        private RectTransform winBannerRect;
        private Image winBannerImage;
        private RectTransform failureBanner;
        private Image failureBannerBackgroundImage;
        private TMP_Text failureText;

        private Tween activeScaleTween;
        private Tween activePulseTween;
        private Sequence activeFailureSequence;

        public void Initialize(Canvas canvas)
        {
            if (targetCanvas == null) targetCanvas = canvas;
            EnsureOverlay();
        }

        public void PlayWinSequence(Action onFinalComplete)
        {
            if (!EnsureOverlay())
            {
                onFinalComplete?.Invoke();
                return;
            }

            ResetOverlay();
            overlayRoot.gameObject.SetActive(true);
            overlayRoot.SetAsLastSibling();
            overlayBlocker.color = winOverlayColor;

            if (winBannerRect != null)
            {
                winBannerRect.gameObject.SetActive(true);
                winBannerRect.localScale = Vector3.zero;
            }

            if (failureBanner != null) failureBanner.gameObject.SetActive(false);

            activeScaleTween = winBannerRect.DOScale(Vector3.one, winSequence.popInDuration)
                .SetEase(winSequence.popInEase)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    activeScaleTween = null;

                    activePulseTween = winBannerRect.DOScale(Vector3.one * winSequence.idlePulseScale, winSequence.idlePulseDuration)
                        .SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetUpdate(true);

                    if (fireworkController != null)
                    {
                        fireworkController.PlayFireworks(() => PlayWinOutroSequence(onFinalComplete));
                    }
                    else
                    {
                        DOVirtual.DelayedCall(winSequence.fallbackHoldDuration, () => PlayWinOutroSequence(onFinalComplete), ignoreTimeScale: true);
                    }
                });
        }

        private void PlayWinOutroSequence(Action onFinalComplete)
        {
            activePulseTween?.Kill();
            activePulseTween = null;

            if (winBannerRect == null)
            {
                overlayRoot.gameObject.SetActive(false);
                onFinalComplete?.Invoke();
                return;
            }

            activeScaleTween = winBannerRect.DOScale(Vector3.zero, winSequence.exitDuration)
                .SetEase(winSequence.exitEase)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    activeScaleTween = null;
                    overlayRoot.gameObject.SetActive(false);

                    onFinalComplete?.Invoke();
                });
        }

        public void PlayFailureBanner(string failureTextValue, Action onComplete)
        {
            if (!EnsureOverlay())
            {
                onComplete?.Invoke();
                return;
            }

            ResetOverlay();
            UpdateFailureBannerVisuals();

            overlayRoot.gameObject.SetActive(true);
            overlayRoot.SetAsLastSibling();
            overlayBlocker.color = failureOverlayColor;

            if (winBannerRect != null) winBannerRect.gameObject.SetActive(false);
            failureBanner.gameObject.SetActive(true);
            failureText.text = failureTextValue;

            float canvasHeight = overlayRoot.rect.height > 1f ? overlayRoot.rect.height : 1920f;
            float offscreenY = canvasHeight * 0.5f + failureBannerSize.y;
            failureBanner.anchoredPosition = new Vector2(0f, offscreenY);

            activeFailureSequence = DOTween.Sequence().SetUpdate(true).SetTarget(this)
                .Append(failureBanner.DOAnchorPosY(0f, failureSequence.slideInDuration).SetEase(Ease.OutCubic))
                .AppendInterval(failureSequence.holdDuration)
                .Append(failureBanner.DOAnchorPosY(-offscreenY, failureSequence.slideOutDuration).SetEase(Ease.InCubic))
                .OnComplete(() =>
                {
                    activeFailureSequence = null;
                    overlayRoot.gameObject.SetActive(false);
                    onComplete?.Invoke();
                });
        }

        public void ResetImmediate()
        {
            activeScaleTween?.Kill();
            activePulseTween?.Kill();
            activeFailureSequence?.Kill();

            activeScaleTween = null;
            activePulseTween = null;
            activeFailureSequence = null;

            if (overlayRoot != null) overlayRoot.gameObject.SetActive(false);
        }

        private bool EnsureOverlay()
        {
            if (overlayRoot != null && winBannerRect != null) return true;
            if (targetCanvas == null) targetCanvas = GetComponentInChildren<Canvas>(true);
            if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
            if (targetCanvas == null) return false;

            Transform existingOverlay = targetCanvas.transform.Find("GameStateTransitionOverlay");

            // Eski sistemden kalan uyumsuz overlay varsa sahneden otomatik sil
            if (existingOverlay != null && existingOverlay.Find("WinBannerImage") == null)
            {
                if (Application.isPlaying) Destroy(existingOverlay.gameObject);
                else DestroyImmediate(existingOverlay.gameObject);
                existingOverlay = null;
            }

            if (existingOverlay != null)
            {
                overlayRoot = existingOverlay.GetComponent<RectTransform>();
                overlayBlocker = overlayRoot.GetComponent<Image>();

                Transform winContent = overlayRoot.Find("WinBannerImage");
                if (winContent != null)
                {
                    winBannerRect = winContent as RectTransform;
                    winBannerImage = winContent.GetComponent<Image>();
                }

                failureBanner = overlayRoot.Find("FailureBanner") as RectTransform;
                if (failureBanner != null)
                {
                    failureBannerBackgroundImage = failureBanner.GetComponent<Image>();
                    failureText = failureBanner.GetComponentInChildren<TMP_Text>();
                }

                UpdateWinBannerVisuals();
                return true;
            }

            // Temiz Overlay Oluştur
            GameObject rootObject = new GameObject("GameStateTransitionOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rootObject.transform.SetParent(targetCanvas.transform, false);
            overlayRoot = rootObject.GetComponent<RectTransform>();
            Stretch(overlayRoot);
            overlayBlocker = rootObject.GetComponent<Image>();
            overlayBlocker.raycastTarget = true;

            GameObject winBannerObj = new GameObject("WinBannerImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            winBannerObj.transform.SetParent(overlayRoot, false);
            winBannerRect = winBannerObj.GetComponent<RectTransform>();
            winBannerImage = winBannerObj.GetComponent<Image>();
            winBannerImage.raycastTarget = false;

            failureBanner = new GameObject("FailureBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
            failureBanner.SetParent(overlayRoot, false);
            failureBanner.anchorMin = failureBanner.anchorMax = new Vector2(0.5f, 0.5f);
            failureBanner.sizeDelta = failureBannerSize;

            failureBannerBackgroundImage = failureBanner.GetComponent<Image>();
            failureText = CreateText("FailureText", failureBanner, failureFontSize);
            Stretch(failureText.rectTransform);
            failureText.alignment = TextAlignmentOptions.Center;
            failureText.raycastTarget = false;

            UpdateWinBannerVisuals();
            UpdateFailureBannerVisuals();
            rootObject.SetActive(false);
            return true;
        }

        private void UpdateWinBannerVisuals()
        {
            if (winBannerRect != null && winBannerImage != null)
            {
                winBannerRect.anchorMin = winBannerRect.anchorMax = new Vector2(0.5f, 0.5f);
                winBannerRect.sizeDelta = winBannerSize;
                winBannerRect.anchoredPosition = new Vector2(0f, winBannerOffsetY);
                winBannerImage.sprite = winBannerSprite;
                winBannerImage.preserveAspect = true;
            }
        }

        private void UpdateFailureBannerVisuals()
        {
            if (failureBannerBackgroundImage != null)
            {
                failureBanner.sizeDelta = failureBannerSize;
                failureBannerBackgroundImage.sprite = failureBannerSprite;
                failureBannerBackgroundImage.type = (failureBannerSprite != null && useSlicedBannerImage) ? Image.Type.Sliced : Image.Type.Simple;
                failureBannerBackgroundImage.color = failureBannerBackgroundColor;
                failureBannerBackgroundImage.raycastTarget = false;
            }

            if (failureText != null)
            {
                failureText.color = failureTextColor;
                failureText.fontSize = failureFontSize;
            }
        }

        private TMP_Text CreateText(string objectName, Transform parent, float fontSize)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = bannerFont != null ? bannerFont : TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            return text;
        }

        private void ResetOverlay()
        {
            activeScaleTween?.Kill();
            activePulseTween?.Kill();
            activeFailureSequence?.Kill();

            activeScaleTween = null;
            activePulseTween = null;
            activeFailureSequence = null;

            UpdateWinBannerVisuals();

            if (winBannerRect != null)
            {
                winBannerRect.DOKill();
                winBannerRect.localScale = Vector3.zero;
            }

            if (failureBanner != null) failureBanner.DOKill();
        }

        private void OnDisable() => ResetImmediate();

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}