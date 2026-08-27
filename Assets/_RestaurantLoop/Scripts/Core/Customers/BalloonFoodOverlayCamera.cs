using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Renders balloon food after the base camera so opaque balloon sprites cannot depth-clip it.
    /// </summary>
    public static class BalloonFoodOverlayCamera
    {
        public const string LayerName = "BalloonFood";

        private static Camera baseCamera;
        private static Camera overlayCamera;

        public static void Register(GameObject foodDisplay)
        {
            int layer = LayerMask.NameToLayer(LayerName);
            if (layer < 0)
            {
                Debug.LogError($"Order balloons require a '{LayerName}' layer.");
                return;
            }

            SetLayerRecursively(foodDisplay.transform, layer);
            EnsureConfigured();
        }

        public static void EnsureConfigured()
        {
            Camera currentBaseCamera = Camera.main;
            if (currentBaseCamera == null)
            {
                return;
            }

            int layer = LayerMask.NameToLayer(LayerName);
            if (layer < 0)
            {
                return;
            }

            if (overlayCamera == null)
            {
                GameObject overlayObject = new GameObject("Balloon Food Overlay Camera");
                Object.DontDestroyOnLoad(overlayObject);
                overlayCamera = overlayObject.AddComponent<Camera>();
            }

            SyncWithBaseCamera(currentBaseCamera, layer);

            UniversalAdditionalCameraData currentBaseData = currentBaseCamera.GetUniversalAdditionalCameraData();
            UniversalAdditionalCameraData overlayData = overlayCamera.GetUniversalAdditionalCameraData();
            overlayData.renderType = CameraRenderType.Overlay;
            overlayData.renderPostProcessing = false;

            if (!currentBaseData.cameraStack.Contains(overlayCamera))
            {
                currentBaseData.cameraStack.Add(overlayCamera);
            }

            baseCamera = currentBaseCamera;
        }

        private static void SyncWithBaseCamera(Camera currentBaseCamera, int layer)
        {
            if (baseCamera != currentBaseCamera)
            {
                overlayCamera.CopyFrom(currentBaseCamera);
                baseCamera = currentBaseCamera;
            }

            overlayCamera.transform.SetPositionAndRotation(currentBaseCamera.transform.position, currentBaseCamera.transform.rotation);
            overlayCamera.orthographic = currentBaseCamera.orthographic;
            overlayCamera.orthographicSize = currentBaseCamera.orthographicSize;
            overlayCamera.fieldOfView = currentBaseCamera.fieldOfView;
            overlayCamera.nearClipPlane = currentBaseCamera.nearClipPlane;
            overlayCamera.farClipPlane = currentBaseCamera.farClipPlane;
            currentBaseCamera.cullingMask &= ~(1 << layer);
            overlayCamera.cullingMask = 1 << layer;
            overlayCamera.clearFlags = CameraClearFlags.Depth;
            overlayCamera.depth = currentBaseCamera.depth + 1f;
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;

            for (int index = 0; index < root.childCount; index++)
            {
                SetLayerRecursively(root.GetChild(index), layer);
            }
        }
    }
}
