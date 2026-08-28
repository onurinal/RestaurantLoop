using UnityEngine;

namespace RestaurantLoop.Infrastructure
{
    /// <summary>
    /// Dynamically scales the Camera's Orthographic Size based on iPhone 12 Pro Max as the baseline.
    /// Runs in both Edit Mode and Play Mode to allow instant testing in Unity Simulator.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class OrthographicCameraScaler : MonoBehaviour
    {
        [Header("Reference Device Settings (iPhone 12 Pro Max)")]
        [Tooltip("Reference screen width aspect (1284 for iPhone 12 Pro Max).")]
        [SerializeField] private float referenceWidth = 1284f;

        [Tooltip("Reference screen height aspect (2778 for iPhone 12 Pro Max).")]
        [SerializeField] private float referenceHeight = 2778f;

        [Tooltip("Desired Orthographic Size on the reference device.")]
        [SerializeField] private float referenceOrthoSize = 20f;

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

            // Algorithm: Scale Orthographic Size inversely to aspect ratio changes to lock horizontal width
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