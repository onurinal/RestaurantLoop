using UnityEngine;
using DG.Tweening;

namespace RestaurantLoop.Core
{
    public class OrderBalloon : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer bgRenderer;

        [Header("3D Food Display")]
        [Tooltip("Presentation-only food prefab.")]
        [SerializeField] private GameObject foodDisplayPrefab;
        [SerializeField] private Vector3 foodLocalPosition = new Vector3(0f, 0f, -0.35f);

        [Tooltip("3D görselin balondaki başlangıç açı offset'i (Örn: X açısına -15 vererek tabağı öne yatırabilirsin).")]
        [SerializeField] private Vector3 foodLocalRotation = Vector3.zero;

        [Tooltip("Prefab'ın kendi varsayılan Transform scale değerini kullanmak için 0 bırakın.")]
        [SerializeField, Min(0f)] private float overrideFoodScale = 0f;

        [Tooltip("Yiyeceğin kendi Y ekseninde dönme hızı.")]
        [SerializeField, Min(0f)] private float rotationSpeed = 25f;

        [Header("Scale Animation")]
        [SerializeField] private bool animateScale = true;
        [SerializeField, Range(0f, 1f)] private float showStartScale = 0.45f;
        [SerializeField, Min(0.01f)] private float showDuration = 0.28f;
        [SerializeField] private Ease showEase = Ease.OutBack;
        [SerializeField, Min(0f)] private float showEaseOvershoot = 1.2f;
        [SerializeField, Range(0f, 1f)] private float hideEndScale = 0.45f;
        [SerializeField, Min(0.01f)] private float hideDuration = 0.2f;
        [SerializeField] private Ease hideEase = Ease.InBack;
        [SerializeField, Min(0f)] private float hideEaseOvershoot = 1.1f;

        [Header("Depth Clearance")]
        [SerializeField] private float cameraOffsetDistance = 0.8f;

        private Transform foodDisplayTransform;
        private Vector3 restingLocalScale;
        private Tween scaleTween;

        private void Awake()
        {
            restingLocalScale = transform.localScale;
            EnsureRenderer();
            ApplyCameraOffset();
            CreateFoodDisplay();
        }

        private void OnEnable()
        {
            PlayIn();
        }

        private void OnDisable()
        {
            scaleTween?.Kill();
            scaleTween = null;
            transform.localScale = restingLocalScale;
        }

        private void LateUpdate()
        {
            if (foodDisplayTransform == null) return;

            // Space.Self sayesinde verdiğin localRotation ne olursa olsun kendi dikey ekseninde döner
            if (rotationSpeed > 0f)
            {
                foodDisplayTransform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
            }
        }

        public void PlayIn()
        {
            scaleTween?.Kill();
            scaleTween = null;

            restingLocalScale = transform.localScale;
            if (!animateScale) return;

            transform.localScale = restingLocalScale * showStartScale;
            scaleTween = transform.DOScale(restingLocalScale, showDuration)
                .SetEase(showEase, showEaseOvershoot)
                .SetTarget(this);
        }

        public void PlayOut(System.Action onComplete)
        {
            scaleTween?.Kill();
            scaleTween = null;

            if (!animateScale)
            {
                transform.localScale = restingLocalScale;
                onComplete?.Invoke();
                return;
            }

            scaleTween = transform.DOScale(restingLocalScale * hideEndScale, hideDuration)
                .SetEase(hideEase, hideEaseOvershoot)
                .SetTarget(this)
                .OnComplete(() =>
                {
                    transform.localScale = restingLocalScale;
                    onComplete?.Invoke();
                });
        }

        private void ApplyCameraOffset()
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                transform.position -= mainCam.transform.forward * cameraOffsetDistance;
            }
        }

        private void EnsureRenderer()
        {
            if (bgRenderer != null) return;
            bgRenderer = GetComponent<SpriteRenderer>();
            if (bgRenderer == null) bgRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void CreateFoodDisplay()
        {
            if (foodDisplayPrefab == null || foodDisplayTransform != null) return;

            GameObject foodDisplay = Instantiate(foodDisplayPrefab, transform);
            foodDisplay.name = "Food Display";
            foodDisplayTransform = foodDisplay.transform;

            foodDisplayTransform.localPosition = foodLocalPosition;
            foodDisplayTransform.localRotation = Quaternion.Euler(foodLocalRotation);

            if (overrideFoodScale > 0f)
            {
                foodDisplayTransform.localScale = Vector3.one * overrideFoodScale;
            }
            else
            {
                foodDisplayTransform.localScale = foodDisplayPrefab.transform.localScale;
            }
        }

        public void SetColorAndState(Color baseColor, bool isEdge)
        {
            EnsureRenderer();
            if (bgRenderer != null) bgRenderer.color = baseColor;
        }
    }
}