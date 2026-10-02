using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace RestaurantLoop.UI
{
    [RequireComponent(typeof(Button), typeof(Image))]
    public sealed class ButtonShineFeedback : MonoBehaviour
    {
        [Header("Shine Animation")]
        [SerializeField, Min(0f)] private float repeatDelay = 4.4f;
        [SerializeField, Min(0.05f)] private float sweepDuration = 1.4f;
        [SerializeField, Range(0f, 1f)] private float peakAlpha = 0.28f;
        [SerializeField, Min(1f)] private float shineWidth = 90f;
        [SerializeField, Range(-45f, 45f)] private float shineAngle = -20f;

        private const string ShineChildName = "Button_Shine";

        private RectTransform buttonRect;
        private RectTransform shineRect;
        private ButtonShineGraphic shineGraphic;
        private Sequence shineSequence;

        private void Awake()
        {
            buttonRect = (RectTransform)transform;
            EnsureClipRect();
            EnsureShineGraphic();
        }

        private void OnEnable()
        {
            if (buttonRect == null)
            {
                buttonRect = (RectTransform)transform;
                EnsureClipRect();
                EnsureShineGraphic();
            }

            PlayShineLoop();
        }

        private void OnDisable()
        {
            shineSequence?.Kill();
            shineSequence = null;

            if (shineGraphic != null)
            {
                shineGraphic.SetAlpha(0f);
            }
        }

        private void OnDestroy()
        {
            shineSequence?.Kill();
        }

        private void EnsureClipRect()
        {
            if (GetComponent<RectMask2D>() == null)
            {
                gameObject.AddComponent<RectMask2D>();
            }
        }

        private void EnsureShineGraphic()
        {
            Transform existingShine = transform.Find(ShineChildName);
            if (existingShine == null)
            {
                GameObject shineObject = new GameObject(ShineChildName, typeof(RectTransform), typeof(CanvasRenderer), typeof(ButtonShineGraphic));
                shineObject.transform.SetParent(transform, false);
                existingShine = shineObject.transform;
            }

            shineRect = (RectTransform)existingShine;
            shineGraphic = shineRect.GetComponent<ButtonShineGraphic>();
            shineRect.anchorMin = new Vector2(0.5f, 0.5f);
            shineRect.anchorMax = new Vector2(0.5f, 0.5f);
            shineRect.pivot = new Vector2(0.5f, 0.5f);
            shineRect.localRotation = Quaternion.Euler(0f, 0f, shineAngle);
            shineRect.SetAsFirstSibling();
            shineGraphic.raycastTarget = false;
            shineGraphic.SetAlpha(0f);
            shineGraphic.SetVerticesDirty();
        }

        private void PlayShineLoop()
        {
            shineSequence?.Kill();
            LayoutRebuilder.ForceRebuildLayoutImmediate(buttonRect);

            float buttonWidth = buttonRect.rect.width;
            float buttonHeight = buttonRect.rect.height;
            float travelPadding = shineWidth + buttonHeight;
            float startX = -(buttonWidth * 0.5f) - travelPadding;
            float endX = (buttonWidth * 0.5f) + travelPadding;

            shineRect.sizeDelta = new Vector2(shineWidth, (buttonHeight * 3f) + shineWidth);
            shineRect.anchoredPosition = new Vector2(startX, 0f);
            shineGraphic.SetAlpha(0f);
            shineGraphic.SetVerticesDirty();

            float fadeDuration = sweepDuration * 0.38f;
            shineSequence = DOTween.Sequence()
                .SetUpdate(true)
                .AppendInterval(repeatDelay)
                .AppendCallback(() =>
                {
                    shineRect.anchoredPosition = new Vector2(startX, 0f);
                    shineGraphic.SetAlpha(0f);
                })
                .Append(shineRect.DOAnchorPosX(endX, sweepDuration).SetEase(Ease.InOutSine))
                .Join(shineGraphic.DOFade(peakAlpha, fadeDuration).SetEase(Ease.OutSine))
                .Insert(repeatDelay + sweepDuration - fadeDuration, shineGraphic.DOFade(0f, fadeDuration).SetEase(Ease.InSine))
                .SetLoops(-1, LoopType.Restart)
                .SetLink(gameObject);
        }
    }

    [AddComponentMenu("")]
    public sealed class ButtonShineGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect rect = GetPixelAdjustedRect();
            Color baseColor = color;
            Color transparent = baseColor;
            transparent.a = 0f;

            float left = rect.xMin;
            float right = rect.xMax;
            float innerLeft = Mathf.Lerp(left, right, 0.22f);
            float innerRight = Mathf.Lerp(left, right, 0.78f);

            AddVertex(vertexHelper, new Vector2(left, rect.yMin), transparent);
            AddVertex(vertexHelper, new Vector2(innerLeft, rect.yMin), baseColor);
            AddVertex(vertexHelper, new Vector2(innerRight, rect.yMin), baseColor);
            AddVertex(vertexHelper, new Vector2(right, rect.yMin), transparent);
            AddVertex(vertexHelper, new Vector2(left, rect.yMax), transparent);
            AddVertex(vertexHelper, new Vector2(innerLeft, rect.yMax), baseColor);
            AddVertex(vertexHelper, new Vector2(innerRight, rect.yMax), baseColor);
            AddVertex(vertexHelper, new Vector2(right, rect.yMax), transparent);

            vertexHelper.AddTriangle(0, 4, 1);
            vertexHelper.AddTriangle(1, 4, 5);
            vertexHelper.AddTriangle(1, 5, 2);
            vertexHelper.AddTriangle(2, 5, 6);
            vertexHelper.AddTriangle(2, 6, 3);
            vertexHelper.AddTriangle(3, 6, 7);
        }

        public void SetAlpha(float alpha)
        {
            Color updatedColor = color;
            updatedColor.a = alpha;
            color = updatedColor;
        }

        private static void AddVertex(VertexHelper vertexHelper, Vector2 position, Color vertexColor)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = vertexColor;
            vertex.uv0 = Vector2.zero;
            vertexHelper.AddVert(vertex);
        }
    }
}
