using System;
using UnityEngine;
using DG.Tweening;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Controls presentation, scaling animations, and food model display for the customer order balloon.
    /// Uses UI Image components for World Space Canvas rendering.
    /// </summary>
    public class OrderBalloon : MonoBehaviour
    {
        [Header("3D Food Display")]
        [Tooltip("Presentation-only food prefab.")]
        [SerializeField] private GameObject foodDisplayPrefab;
        [SerializeField] private Vector3 foodLocalPosition = new Vector3(0f, 0f, -0.35f);
        [SerializeField] private Vector3 foodLocalRotation = Vector3.zero;

        [Tooltip("Leave at 0 to use the prefab's default Transform scale.")]
        [SerializeField, Min(0f)] private float overrideFoodScale = 0f;

        [Tooltip("Rotation speed of the food along its local Y-axis.")]
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
        private Vector3 authoredScale = Vector3.one;
        private bool isAuthoredScaleCaptured;
        private Tween scaleTween;

        private void Awake()
        {
            CaptureAuthoredScale();
            ApplyCameraOffset();
            CreateFoodDisplay();
        }

        private void OnEnable()
        {
            CaptureAuthoredScale();
            PlayIn();
        }

        private void OnDisable()
        {
            ResetScale();
        }

        private void LateUpdate()
        {
            if (foodDisplayTransform == null) return;

            if (rotationSpeed > 0f)
            {
                foodDisplayTransform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
            }
        }

        public void PlayIn()
        {
            KillTween();
            CaptureAuthoredScale();

            if (!animateScale)
            {
                transform.localScale = authoredScale;
                return;
            }

            transform.localScale = authoredScale * showStartScale;
            scaleTween = transform.DOScale(authoredScale, showDuration)
                .SetEase(showEase, showEaseOvershoot)
                .SetTarget(this);
        }

        public void PlayOut(Action onComplete)
        {
            KillTween();
            CaptureAuthoredScale();

            if (!animateScale)
            {
                transform.localScale = authoredScale;
                onComplete?.Invoke();
                return;
            }

            scaleTween = transform.DOScale(authoredScale * hideEndScale, hideDuration)
                .SetEase(hideEase, hideEaseOvershoot)
                .SetTarget(this)
                .OnComplete(() =>
                {
                    transform.localScale = authoredScale;
                    onComplete?.Invoke();
                });
        }

        public void ResetScale()
        {
            KillTween();
            CaptureAuthoredScale();
            transform.localScale = authoredScale;
        }

        private void KillTween()
        {
            if (scaleTween != null && scaleTween.IsActive())
            {
                scaleTween.Kill();
            }
            scaleTween = null;
            transform.DOKill();
        }

        private void CaptureAuthoredScale()
        {
            if (isAuthoredScaleCaptured) return;

            // Capture initial scale before any animation or pooling modification
            if (transform.localScale != Vector3.zero)
            {
                authoredScale = transform.localScale;
            }
            else
            {
                authoredScale = Vector3.one;
            }

            isAuthoredScaleCaptured = true;
        }

        private void ApplyCameraOffset()
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                transform.position -= mainCam.transform.forward * cameraOffsetDistance;
            }
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
    }
}