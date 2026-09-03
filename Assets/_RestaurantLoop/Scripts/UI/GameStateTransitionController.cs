using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace RestaurantLoop.UI
{
    [Serializable]
    public class WinBannerSettings
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

        [Header("Win Sequence Settings")]
        [SerializeField] private WinBannerSettings winSequence = new WinBannerSettings();
        [SerializeField] private Color winOverlayColor = new Color(0.11f, 0.06f, 0.18f, 0.52f);

        [Header("Win Letter Sprites")]
        [Tooltip("Assign letter sprites in order (R, E, S, T, A, U, R, A, N, T, L, O, O, P).")]
        [SerializeField] private List<Sprite> winLetterSprites = new List<Sprite>();
        [Tooltip("0-based index to break line (9 = after 10th letter 'T' of RESTAURANT).")]
        [SerializeField] private int lineBreakIndex = 9;

        [Header("Line 1 Setup (RESTAURANT)")]
        [SerializeField] private Vector2 line1LetterSize = new Vector2(100f, 100f);
        [Tooltip("Kerning factor based on sprite width. Lower = tighter, Higher = loose.")]
        [SerializeField, Range(0.4f, 1.2f)] private float line1KerningFactor = 0.92f;
        [SerializeField] private float line1ArcHeightY = 24f;
        [SerializeField, Range(0f, 30f)] private float line1MaxEdgeRotation = 5f;
        [Tooltip("Check this if you want left letters (like N) to render on top of right letters (like T).")]
        [SerializeField] private bool line1ReverseRenderOrder = true;

        [Header("Line 2 Setup (LOOP - Main Menu Style)")]
        [SerializeField] private Vector2 line2LetterSize = new Vector2(150f, 150f);
        [Tooltip("Kerning factor based on sprite width for line 2.")]
        [SerializeField, Range(0.4f, 1.2f)] private float line2KerningFactor = 0.88f;
        [SerializeField] private float line2ArcHeightY = 12f;
        [SerializeField] private float line2OffsetY = -108f;
        [SerializeField, Range(0f, 30f)] private float line2MaxEdgeRotation = 2f;
        [SerializeField] private bool line2ReverseRenderOrder = true;

        [Header("Global Layout Settings")]
        [SerializeField] private float winTextCenterOffsetY = 200f;
        [SerializeField, Range(0.5f, 0.98f)] private float maxScreenWidthPercent = 0.90f;

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

            Transform existingOverlay = targetCanvas.transform.Find("GameStateTransitionOverlay");
            if (existingOverlay != null)
            {
                overlayRoot = existingOverlay.GetComponent<RectTransform>();
                overlayBlocker = overlayRoot.GetComponent<Image>();
                winContentRoot = overlayRoot.Find("WinBannerContent") as RectTransform;
                failureBanner = overlayRoot.Find("FailureBanner") as RectTransform;
                if (failureBanner != null)
                {
                    failureBannerBackgroundImage = failureBanner.GetComponent<Image>();
                    failureText = failureBanner.GetComponentInChildren<TMP_Text>();
                }

                return true;
            }

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

            List<Sprite> line1 = new List<Sprite>();
            List<Sprite> line2 = new List<Sprite>();

            for (int i = 0; i < winLetterSprites.Count; i++)
            {
                if (winLetterSprites[i] == null) continue;
                if (i <= lineBreakIndex) line1.Add(winLetterSprites[i]);
                else line2.Add(winLetterSprites[i]);
            }

            int globalIndex = 0;

            float line1Width = BuildLine(line1, line1LetterSize, line1KerningFactor, line1ArcHeightY, winTextCenterOffsetY, line1MaxEdgeRotation,
                line1ReverseRenderOrder, ref globalIndex);
            float line2Width = BuildLine(line2, line2LetterSize, line2KerningFactor, line2ArcHeightY, winTextCenterOffsetY + line2OffsetY, line2MaxEdgeRotation,
                line2ReverseRenderOrder, ref globalIndex);

            float maxLineWidth = Mathf.Max(line1Width, line2Width);

            float canvasWidth = overlayRoot != null && overlayRoot.rect.width > 1f ? overlayRoot.rect.width : 1080f;
            float maxAllowedWidth = canvasWidth * maxScreenWidthPercent;

            if (maxLineWidth > maxAllowedWidth && maxLineWidth > 0f)
            {
                float fitScale = maxAllowedWidth / maxLineWidth;
                winContentRoot.localScale = new Vector3(fitScale, fitScale, 1f);
            }
            else
            {
                winContentRoot.localScale = Vector3.one;
            }
        }

        private float BuildLine(List<Sprite> sprites, Vector2 letterSize, float kerningFactor, float arcHeight, float offsetY, float maxRotation,
            bool reverseOrder, ref int globalIndex)
        {
            int count = sprites.Count;
            if (count == 0) return 0f;

            List<float> scaledWidths = new List<float>(count);
            for (int i = 0; i < count; i++)
            {
                Sprite sprite = sprites[i];
                float aspect = (sprite != null && sprite.rect.height > 0f) ? (sprite.rect.width / sprite.rect.height) : 1f;
                float actualWidth = letterSize.y * aspect;
                scaledWidths.Add(actualWidth);
            }

            List<float> xPositions = new List<float>(count);
            float currentX = 0f;

            for (int i = 0; i < count; i++)
            {
                if (i == 0)
                {
                    xPositions.Add(0f);
                }
                else
                {
                    float previousWidth = scaledWidths[i - 1];
                    float currentWidth = scaledWidths[i];
                    float step = ((previousWidth + currentWidth) * 0.5f) * kerningFactor;
                    currentX += step;
                    xPositions.Add(currentX);
                }
            }

            float totalLineWidth = currentX;
            float startX = -totalLineWidth * 0.5f;

            List<RectTransform> lineRects = new List<RectTransform>();

            for (int i = 0; i < count; i++)
            {
                GameObject imageObject = new GameObject($"LetterSprite_{globalIndex:00}", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                imageObject.transform.SetParent(winContentRoot, false);

                Image image = imageObject.GetComponent<Image>();
                image.sprite = sprites[i];
                image.preserveAspect = true;
                image.raycastTarget = false;

                RectTransform rect = imageObject.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = letterSize;

                float x = startX + xPositions[i];
                float normalizedX = totalLineWidth > 0f ? (x / (totalLineWidth * 0.5f)) : 0f;
                normalizedX = Mathf.Clamp(normalizedX, -1f, 1f);

                float y = (1f - (normalizedX * normalizedX)) * arcHeight + offsetY;
                float rotZ = -normalizedX * maxRotation;

                rect.anchoredPosition = new Vector2(x, y);
                rect.localRotation = Quaternion.Euler(0f, 0f, rotZ);

                lineRects.Add(rect);
                letters.Add(rect);
                globalIndex++;
            }

            if (reverseOrder)
            {
                for (int i = count - 1; i >= 0; i--) lineRects[i].SetAsLastSibling();
            }
            else
            {
                for (int i = 0; i < count; i++) lineRects[i].SetAsLastSibling();
            }

            return totalLineWidth + letterSize.x;
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

            if (winContentRoot != null)
            {
                for (int i = winContentRoot.childCount - 1; i >= 0; i--)
                {
                    GameObject child = winContentRoot.GetChild(i).gameObject;
                    if (Application.isPlaying) Destroy(child);
                    else DestroyImmediate(child);
                }
            }

            BuildWinLetters();

            for (int i = 0; i < letters.Count; i++)
            {
                if (letters[i] == null) continue;
                letters[i].DOKill();
                letters[i].localScale = Vector3.zero;
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