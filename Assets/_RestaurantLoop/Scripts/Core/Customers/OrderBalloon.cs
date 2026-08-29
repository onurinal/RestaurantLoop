using UnityEngine;

using DG.Tweening;

namespace RestaurantLoop.Core
{
    public class OrderBalloon : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer bgRenderer;

        [Header("3D Food Display")]
        [Tooltip("Presentation-only food prefab. Use a centered pivot and renderer(s) only; do not use the gameplay stack prefab.")]
        [SerializeField] private GameObject foodDisplayPrefab;
        [SerializeField] private Vector3 foodLocalPosition = new Vector3(0f, 0f, -0.35f);
        [SerializeField, Min(0.01f)] private float foodScale = 0.7f;
        [SerializeField, Min(0f)] private float rotationSpeed = 20f;

        [Header("Scale Animation")]
        [Tooltip("Scales the balloon in whenever it becomes visible.")]
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
        [Tooltip("Pushes the balloon slightly toward the camera line of sight to prevent 3D customer head clipping.")]
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
            if (foodDisplayTransform == null)
            {
                return;
            }

            if (rotationSpeed > 0f)
            {
                foodDisplayTransform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
            }

            
        }

        /// <summary>Plays the visible balloon scale-in from its authored scale.</summary>
        public void PlayIn()
        {
            scaleTween?.Kill();
            scaleTween = null;

            restingLocalScale = transform.localScale;
            if (!animateScale)
            {
                return;
            }

            transform.localScale = restingLocalScale * showStartScale;
            scaleTween = transform.DOScale(restingLocalScale, showDuration)
                .SetEase(showEase, showEaseOvershoot)
                .SetTarget(this);
        }

        /// <summary>Plays the scale-out, then invokes the caller so it can hide the balloon.</summary>
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
                // Offset balloon transform slightly along the camera's backward vector
                transform.position -= mainCam.transform.forward * cameraOffsetDistance;
            }
        }

        private void EnsureRenderer()
        {
            if (bgRenderer != null)
                return;

            bgRenderer = GetComponent<SpriteRenderer>();

            if (bgRenderer == null)
            {
                bgRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        private void CreateFoodDisplay()
        {
            if (foodDisplayPrefab == null || foodDisplayTransform != null)
            {
                return;
            }

            GameObject foodDisplay = Instantiate(foodDisplayPrefab, transform);
            foodDisplay.name = "Food Display";
            foodDisplayTransform = foodDisplay.transform;
            foodDisplayTransform.localPosition = foodLocalPosition;
            foodDisplayTransform.localRotation = Quaternion.identity;
            foodDisplayTransform.localScale = Vector3.one * foodScale;

            
        }

        public void SetColorAndState(Color baseColor, bool isEdge)
        {
            EnsureRenderer();

            if (bgRenderer == null)
                return;

            bgRenderer.color = baseColor;
        }
    }
}
