using UnityEngine;

namespace RestaurantLoop.Infrastructure
{
    /// <summary>
    /// Dynamically scales the Camera's Orthographic Size based on Samsung Galaxy S10e as the baseline.
    /// Locks horizontal viewport width across all aspect ratios in both Edit Mode and Play Mode.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class OrthographicCameraScaler : MonoBehaviour
    {
        [Header("Reference Device Settings (Samsung Galaxy S10e)")]
        [Tooltip("Reference screen width aspect (1080 for Samsung Galaxy S10e).")]
        [SerializeField] private float referenceWidth = 1080f;

        [Tooltip("Reference screen height aspect (2280 for Samsung Galaxy S10e).")]
        [SerializeField] private float referenceHeight = 2280f;

        [Tooltip("Desired Orthographic Size on the reference device.")]
        [SerializeField] private float referenceOrthoSize = 18f;

        private Camera targetCamera;
        private int lastScreenWidth;
        private int lastScreenHeight;

        private void OnEnable()
        {
            targetCamera = GetComponent<Camera>();
            RecalculateCameraBounds();
        }

        private void Update()
        {
            // Detect screen resolution changes instantly in Simulator without requiring Play Mode
            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
            {
                RecalculateCameraBounds();
            }
        }

        public void RecalculateCameraBounds()
        {
            if (targetCamera == null || !targetCamera.orthographic) return;

            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;

            if (lastScreenHeight <= 0 || referenceHeight <= 0) return;

            float referenceAspect = referenceWidth / referenceHeight;
            float currentAspect = (float)lastScreenWidth / lastScreenHeight;

            // Scale Orthographic Size inversely to aspect ratio changes to lock horizontal width
            float targetOrthoSize = referenceOrthoSize * (referenceAspect / currentAspect);

            targetCamera.orthographicSize = targetOrthoSize;
        }

        private void OnValidate()
        {
            targetCamera = GetComponent<Camera>();
            RecalculateCameraBounds();
        }
    }
}