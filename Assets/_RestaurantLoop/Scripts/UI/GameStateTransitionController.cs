using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RestaurantLoop.UI
{
    [Serializable]
    public sealed class WinBannerSettings
    {
        [Min(0f)] public float letterStaggerDelay = 0.055f;
        [Min(0.01f)] public float letterScaleDuration = 0.18f;
        [Tooltip("Hold duration for the text overlay on screen in seconds.")]
        [Min(0f)] public float holdDuration = 1.2f;
        [Min(1f)] public float exitSpiralSpeed = 720f;
        [Tooltip("Duration of the spiral/vortex exit animation in seconds.")]
        [Min(0.01f)] public float exitDuration = 0.8f;
        public Ease exitScaleEase = Ease.InBack;
    }

    [Serializable]
    public sealed class FailureBannerSettings
    {
        [Min(0.01f)] public float slideInDuration = 0.35f;
        [Range(0.2f, 2f)] public float holdDuration = 1f;
        [Min(0.01f)] public float slideOutDuration = 0.32f;
    }

    /// <summary>
    /// Owns presentation-only win/fail overlays. LevelManager remains the sole owner
    /// of gameplay state and invokes these sequences only after a result is confirmed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameStateTransitionController : MonoBehaviour
    {
        private const string WinTitle = "RESTAURANT LOOP";

        [Header("References")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private TMP_FontAsset bannerFont;

        [Header("Win Sequence Settings")]
        [SerializeField] private WinBannerSettings winSequence = new WinBannerSettings();
        [SerializeField] private Sprite optionalMiddleIcon;
        [SerializeField] private Vector2 optionalMiddleIconSize = new Vector2(110f, 110f);
        [SerializeField] private Color winOverlayColor = new Color(0.11f, 0.06f, 0.18f, 0.52f);

        [Header("Win Text Customization")]
        [SerializeField, Min(10f)] private float winLetterFontSize = 120f;
        [SerializeField] private Color winLetterTextColor = Color.white;
        [Tooltip("Extra spacing angle between adjacent letters.")]
        [SerializeField, Range(0f, 20f)] private float letterSpacingAngle = 3f;
        [Tooltip("Extra spacing angle between words (RESTAURANT and LOOP).")]
        [SerializeField, Range(0f, 40f)] private float wordSpacingAngle = 12f;

        [Header("Win Sequence Arc Layout Settings")]
        [Tooltip("Vertical position offset relative to the screen center.")]
        [SerializeField] private float winTextCenterOffsetY = 180f;
        [Tooltip("Total arc angle for the curved text layout in degrees.")]
        [SerializeField, Range(0f, 120f)] private float winArcAngle = 80f;
        [Tooltip("Radius of the curved arc layout.")]
        [SerializeField, Min(100f)] private float winArcRadius = 520f;

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

        private readonly List<RectTransform> letters = new List<RectTransform>();
        private RectTransform overlayRoot;
        private Image overlayBlocker;
        private RectTransform winContentRoot;
        private RectTransform middleIconAnchor;
        private Image middleIconImage;
        private RectTransform failureBanner;
        private Image failureBannerBackgroundImage;
        private TMP_Text failureText;
        private Sequence activeSequence;

        public RectTransform MiddleIconAnchor => middleIconAnchor;

        public void Initialize(Canvas canvas)
        {
            if (targetCanvas == null) targetCanvas = canvas;
            EnsureOverlay();
        }

        public void PlayWinSequence(Action onComplete)
        {
            if (!EnsureOverlay())
            {
                onComplete?.Invoke();
                return;
            }

            ResetOverlay();
            overlayRoot.gameObject.SetActive(true);
            overlayRoot.SetAsLastSibling();
            overlayBlocker.color = winOverlayColor;
            winContentRoot.gameObject.SetActive(true);
            failureBanner.gameObject.SetActive(false);

            activeSequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            float revealEnd = 0f;

            for (int i = 0; i < letters.Count; i++)
            {
                RectTransform letter = letters[i];
                float revealAt = i * winSequence.letterStaggerDelay;
                revealEnd = revealAt + winSequence.letterScaleDuration;
                activeSequence.Insert(revealAt,
                    letter.DOScale(Vector3.one, winSequence.letterScaleDuration).SetEase(Ease.OutBack));
            }

            if (middleIconImage != null && middleIconImage.gameObject.activeSelf)
            {
                middleIconAnchor.localScale = Vector3.zero;
                activeSequence.Insert(revealEnd,
                    middleIconAnchor.DOScale(Vector3.one, winSequence.letterScaleDuration).SetEase(Ease.OutBack));
                revealEnd += winSequence.letterScaleDuration;
            }

            float exitStart = revealEnd + winSequence.holdDuration;
            for (int i = 0; i < letters.Count; i++)
            {
                RectTransform letter = letters[i];
                float exitAt = exitStart + i * winSequence.letterStaggerDelay * 0.22f;
                float rotation = winSequence.exitSpiralSpeed * winSequence.exitDuration;
                activeSequence.Insert(exitAt,
                    letter.DOAnchorPos(Vector2.zero, winSequence.exitDuration).SetEase(Ease.InCubic));
                activeSequence.Insert(exitAt,
                    letter.DORotate(new Vector3(0f, 0f, rotation), winSequence.exitDuration,
                        RotateMode.FastBeyond360).SetRelative().SetEase(Ease.InQuad));
                activeSequence.Insert(exitAt,
                    letter.DOScale(Vector3.zero, winSequence.exitDuration).SetEase(winSequence.exitScaleEase));
            }

            if (middleIconImage != null && middleIconImage.gameObject.activeSelf)
            {
                activeSequence.Insert(exitStart,
                    middleIconAnchor.DORotate(new Vector3(0f, 0f, 360f), winSequence.exitDuration,
                        RotateMode.FastBeyond360));
                activeSequence.Insert(exitStart,
                    middleIconAnchor.DOScale(Vector3.zero, winSequence.exitDuration).SetEase(winSequence.exitScaleEase));
            }

            activeSequence.OnComplete(() =>
            {
                activeSequence = null;
                overlayRoot.gameObject.SetActive(false);
                onComplete?.Invoke();
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
            winContentRoot.gameObject.SetActive(false);
            failureBanner.gameObject.SetActive(true);
            failureText.text = failureTextValue;

            float canvasHeight = overlayRoot.rect.height > 1f ? overlayRoot.rect.height : 1920f;
            float offscreenY = canvasHeight * 0.5f + failureBannerSize.y;
            failureBanner.anchoredPosition = new Vector2(0f, offscreenY);

            activeSequence = DOTween.Sequence().SetUpdate(true).SetTarget(this)
                .Append(failureBanner.DOAnchorPosY(0f, failureSequence.slideInDuration).SetEase(Ease.OutCubic))
                .AppendInterval(failureSequence.holdDuration)
                .Append(failureBanner.DOAnchorPosY(-offscreenY, failureSequence.slideOutDuration).SetEase(Ease.InCubic))
                .OnComplete(() =>
                {
                    activeSequence = null;
                    overlayRoot.gameObject.SetActive(false);
                    onComplete?.Invoke();
                });
        }

        public void ResetImmediate()
        {
            activeSequence?.Kill();
            activeSequence = null;
            if (overlayRoot != null) overlayRoot.gameObject.SetActive(false);
        }

        private bool EnsureOverlay()
        {
            if (overlayRoot != null) return true;
            if (targetCanvas == null) targetCanvas = GetComponentInChildren<Canvas>(true);
            if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
            if (targetCanvas == null) return false;

            GameObject rootObject = new GameObject("GameStateTransitionOverlay", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            rootObject.transform.SetParent(targetCanvas.transform, false);
            overlayRoot = rootObject.GetComponent<RectTransform>();
            Stretch(overlayRoot);
            overlayBlocker = rootObject.GetComponent<Image>();
            overlayBlocker.raycastTarget = true;

            winContentRoot = new GameObject("WinBannerContent", typeof(RectTransform)).GetComponent<RectTransform>();
            winContentRoot.SetParent(overlayRoot, false);
            winContentRoot.anchorMin = winContentRoot.anchorMax = new Vector2(0.5f, 0.5f);
            winContentRoot.sizeDelta = new Vector2(1000f, 320f);
            BuildWinLetters();

            middleIconAnchor = new GameObject("OptionalMiddleIconAnchor", typeof(RectTransform)).GetComponent<RectTransform>();
            middleIconAnchor.SetParent(winContentRoot, false);
            middleIconAnchor.anchorMin = middleIconAnchor.anchorMax = new Vector2(0.5f, 0.5f);
            middleIconAnchor.sizeDelta = optionalMiddleIconSize;
            if (optionalMiddleIcon != null)
            {
                middleIconImage = middleIconAnchor.gameObject.AddComponent<Image>();
                middleIconImage.sprite = optionalMiddleIcon;
                middleIconImage.preserveAspect = true;
                middleIconImage.raycastTarget = false;
            }

            failureBanner = new GameObject("FailureBanner", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image)).GetComponent<RectTransform>();
            failureBanner.SetParent(overlayRoot, false);
            failureBanner.anchorMin = failureBanner.anchorMax = new Vector2(0.5f, 0.5f);
            failureBanner.sizeDelta = failureBannerSize;

            failureBannerBackgroundImage = failureBanner.GetComponent<Image>();

            failureText = CreateText("FailureText", failureBanner, failureFontSize);
            Stretch(failureText.rectTransform);
            failureText.alignment = TextAlignmentOptions.Center;
            failureText.raycastTarget = false;

            UpdateFailureBannerVisuals();

            rootObject.SetActive(false);
            return true;
        }

        private void UpdateFailureBannerVisuals()
        {
            if (failureBannerBackgroundImage != null)
            {
                failureBanner.sizeDelta = failureBannerSize;
                failureBannerBackgroundImage.sprite = failureBannerSprite;
                failureBannerBackgroundImage.type = (failureBannerSprite != null && useSlicedBannerImage)
                    ? Image.Type.Sliced
                    : Image.Type.Simple;
                failureBannerBackgroundImage.color = failureBannerBackgroundColor;
                failureBannerBackgroundImage.raycastTarget = false;
            }

            if (failureText != null)
            {
                failureText.color = failureTextColor;
                failureText.fontSize = failureFontSize;
            }
        }

        private void GetLetterArcTransform(int index, out Vector2 position, out Quaternion rotation)
        {
            int totalLetters = 0;
            int spaceCount = 0;

            for (int i = 0; i < WinTitle.Length; i++)
            {
                if (WinTitle[i] == ' ') spaceCount++;
                else totalLetters++;
            }

            float baseStepAngle = winArcAngle / Math.Max(1, totalLetters - 1 + spaceCount);
            float effectiveLetterStep = baseStepAngle + letterSpacingAngle;

            int spacesBefore = 0;
            int lettersBefore = 0;
            for (int i = 0; i < index; i++)
            {
                if (WinTitle[i] == ' ') spacesBefore++;
                else lettersBefore++;
            }

            float angleFromStart = lettersBefore * effectiveLetterStep + spacesBefore * (effectiveLetterStep + wordSpacingAngle);
            float totalSpan = (totalLetters - 1) * effectiveLetterStep + spaceCount * (effectiveLetterStep + wordSpacingAngle);
            float currentAngle = angleFromStart - (totalSpan * 0.5f);

            float angleRad = currentAngle * Mathf.Deg2Rad;
            float x = winArcRadius * Mathf.Sin(angleRad);
            float y = (winArcRadius * Mathf.Cos(angleRad) - winArcRadius) + winTextCenterOffsetY;

            position = new Vector2(x, y);
            rotation = Quaternion.Euler(0f, 0f, -currentAngle);
        }

        private void BuildWinLetters()
        {
            letters.Clear();
            for (int i = 0; i < WinTitle.Length; i++)
            {
                if (WinTitle[i] == ' ') continue;

                TMP_Text text = CreateText($"Letter_{i:00}", winContentRoot, winLetterFontSize);
                text.text = WinTitle[i].ToString();
                text.color = winLetterTextColor;
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
                RectTransform rect = text.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(winLetterFontSize * 1.1f, winLetterFontSize * 1.4f);

                GetLetterArcTransform(i, out Vector2 arcPos, out Quaternion arcRot);
                rect.anchoredPosition = arcPos;
                rect.localRotation = arcRot;

                letters.Add(rect);
            }
        }

        private TMP_Text CreateText(string objectName, Transform parent, float fontSize)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = bannerFont != null ? bannerFont : TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            return text;
        }

        private void ResetOverlay()
        {
            activeSequence?.Kill();
            activeSequence = null;

            for (int i = winContentRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = winContentRoot.GetChild(i);
                if (child != middleIconAnchor) Destroy(child.gameObject);
            }

            BuildWinLetters();

            for (int i = 0; i < letters.Count; i++)
            {
                RectTransform letter = letters[i];
                letter.DOKill();
                letter.localScale = Vector3.zero;

                TMP_Text text = letter.GetComponent<TMP_Text>();
                if (text != null)
                {
                    text.fontSize = winLetterFontSize;
                    text.color = winLetterTextColor;
                }
            }

            middleIconAnchor?.DOKill();
            if (middleIconAnchor != null)
            {
                middleIconAnchor.localScale = optionalMiddleIcon != null ? Vector3.one : Vector3.zero;
                middleIconAnchor.localRotation = Quaternion.identity;
                middleIconAnchor.gameObject.SetActive(optionalMiddleIcon != null);
            }

            failureBanner?.DOKill();
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