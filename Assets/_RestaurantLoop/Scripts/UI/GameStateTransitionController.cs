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

    [DisallowMultipleComponent]
    public sealed class GameStateTransitionController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private TMP_FontAsset bannerFont;

        [Header("Win Sequence Settings")]
        [SerializeField] private WinBannerSettings winSequence = new WinBannerSettings();
        [SerializeField] private Color winOverlayColor = new Color(0.11f, 0.06f, 0.18f, 0.52f);

        [Header("Win Letter Sprites & Sizing")]
        [Tooltip("Assign letter sprites in order (e.g., R, E, S, T, A, U, R, A, N, T, L, O, O, P).")]
        [SerializeField] private List<Sprite> winLetterSprites = new List<Sprite>();
        [Tooltip("On-screen width and height for each letter sprite.")]
        [SerializeField] private Vector2 winLetterSize = new Vector2(110f, 110f);

        [Header("Modular Pixel Spacing & Line Breaks")]
        [Tooltip("0-based letter indices after which a LINE BREAK is inserted. E.g., '9' pushes everything after 'RESTAURANT' to the next line.")]
        [SerializeField] private List<int> lineBreakAfterIndices = new List<int> { 9 };
        [Tooltip("Horizontal pixel distance between first line letter centers (RESTAURANT).")]
        [SerializeField, Min(0f)] private float letterSpacingX = 85f;
        [Tooltip("Horizontal pixel distance for second line letter centers (LOOP). Set to 0 to use default spacing.")]
        [SerializeField, Min(0f)] private float secondLineLetterSpacingX = 110f;
        [Tooltip("Vertical pixel distance pushing the second line downwards (negative value moves down).")]
        [SerializeField] private float lineSpacingY = -120f;

        [Header("Screen Safety & Arc Layout Settings")]
        [Tooltip("Maximum percentage of canvas width the text is allowed to occupy (0.85 = 85%). Prevents hitting screen edges.")]
        [SerializeField, Range(0.5f, 0.95f)] private float maxScreenWidthPercent = 0.90f;
        [Tooltip("Vertical position offset relative to the screen center.")]
        [SerializeField] private float winTextCenterOffsetY = 280f;
        [Tooltip("Arch height in pixels. Set to 0 for completely FLAT text, or 30-40 for a subtle curve.")]
        [SerializeField] private float arcHeightY = 35f;
        [Tooltip("Maximum tilt/rotation of letters at the far outer edges (in degrees).")]
        [SerializeField, Range(0f, 45f)] private float maxEdgeRotation = 10f;

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
        private RectTransform failureBanner;
        private Image failureBannerBackgroundImage;
        private TMP_Text failureText;
        private Sequence activeSequence;

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

        private void BuildWinLetters()
        {
            letters.Clear();
            if (winLetterSprites == null || winLetterSprites.Count == 0) return;

            List<List<Sprite>> lines = new List<List<Sprite>>();
            List<Sprite> currentLine = new List<Sprite>();

            for (int i = 0; i < winLetterSprites.Count; i++)
            {
                if (winLetterSprites[i] == null) continue;
                currentLine.Add(winLetterSprites[i]);

                if (lineBreakAfterIndices != null && lineBreakAfterIndices.Contains(i))
                {
                    lines.Add(currentLine);
                    currentLine = new List<Sprite>();
                }
            }

            if (currentLine.Count > 0)
            {
                lines.Add(currentLine);
            }

            if (lines.Count == 0) return;

            float maxLineWidth = 0f;
            for (int lineIdx = 0; lineIdx < lines.Count; lineIdx++)
            {
                var line = lines[lineIdx];
                if (line.Count == 0) continue;

                float activeSpacing = (lineIdx > 0 && secondLineLetterSpacingX > 0f) ? secondLineLetterSpacingX : letterSpacingX;
                float lineWidth = (line.Count - 1) * activeSpacing;
                if (lineWidth > maxLineWidth) maxLineWidth = lineWidth;
            }

            int globalIndex = 0;

            for (int lineIdx = 0; lineIdx < lines.Count; lineIdx++)
            {
                List<Sprite> lineSprites = lines[lineIdx];
                int lineCount = lineSprites.Count;
                if (lineCount == 0) continue;

                float activeSpacing = (lineIdx > 0 && secondLineLetterSpacingX > 0f) ? secondLineLetterSpacingX : letterSpacingX;

                float currentLineWidth = (lineCount - 1) * activeSpacing;
                float startX = -currentLineWidth * 0.5f;

                for (int i = 0; i < lineCount; i++)
                {
                    Sprite letterSprite = lineSprites[i];

                    GameObject imageObject = new GameObject($"LetterSprite_{globalIndex:00}", typeof(RectTransform),
                        typeof(CanvasRenderer), typeof(Image));
                    imageObject.transform.SetParent(winContentRoot, false);

                    Image image = imageObject.GetComponent<Image>();
                    image.sprite = letterSprite;
                    image.preserveAspect = true;
                    image.raycastTarget = false;

                    RectTransform rect = imageObject.GetComponent<RectTransform>();
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = winLetterSize;

                    float x = startX + (i * activeSpacing);

                    float normalizedX = maxLineWidth > 0f ? (x / (maxLineWidth * 0.5f)) : 0f;
                    normalizedX = Mathf.Clamp(normalizedX, -1f, 1f);

                    float y = (1f - (normalizedX * normalizedX)) * arcHeightY + winTextCenterOffsetY + (lineIdx * lineSpacingY);
                    float rotZ = -normalizedX * maxEdgeRotation;

                    rect.anchoredPosition = new Vector2(x, y);
                    rect.localRotation = Quaternion.Euler(0f, 0f, rotZ);

                    letters.Add(rect);
                    globalIndex++;
                }
            }

            float totalMaxSpan = maxLineWidth + winLetterSize.x;
            float canvasWidth = overlayRoot != null && overlayRoot.rect.width > 1f ? overlayRoot.rect.width : 1080f;
            float maxAllowedWidth = canvasWidth * maxScreenWidthPercent;

            if (totalMaxSpan > maxAllowedWidth && totalMaxSpan > 0f)
            {
                float fitScale = maxAllowedWidth / totalMaxSpan;
                winContentRoot.localScale = new Vector3(fitScale, fitScale, 1f);
            }
            else
            {
                winContentRoot.localScale = Vector3.one;
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
                Destroy(winContentRoot.GetChild(i).gameObject);
            }

            BuildWinLetters();

            for (int i = 0; i < letters.Count; i++)
            {
                RectTransform letter = letters[i];
                letter.DOKill();
                letter.localScale = Vector3.zero;
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