using System.Collections.Generic;
using UnityEngine;
using TMPro;
using RestaurantLoop.Infrastructure;

namespace RestaurantLoop.Core
{
    public class StackItemVisuals : MonoBehaviour
    {
        [Header("Visual Setup")]
        [SerializeField] private Transform visualContainer;
        [SerializeField] private GameObject singleMeshModel;
        [SerializeField] private float yOffset = 1.0f;

        [Header("Rotation Overrides")]
        [Tooltip("Local rotation applied in Queue/Rack slots (Single Mode).")]
        [SerializeField] private Vector3 singleModeRotation = Vector3.zero;
        [Tooltip("Local rotation applied on belt (Stacked Mode).")]
        [SerializeField] private Vector3 stackedModeRotation = Vector3.zero;

        [Header("UI Setup")]
        [SerializeField] private TMP_Text countText;
        [Tooltip("Text offset in Queue and Rack slots (Single Mode).")]
        [SerializeField] private Vector3 singleModeTextOffset = new Vector3(0f, 0.25f, -1.1f);
        [Tooltip("Interpolation speed for smooth text position transitions around conveyor corners.")]
        [SerializeField] private float textOffsetLerpSpeed = 12f;

        private readonly List<GameObject> spawnedStackedItems = new List<GameObject>();
        private Camera mainCamera;
        private StackVisualMode currentMode = StackVisualMode.SingleWithUI;

        public GameObject SingleMeshModel => singleMeshModel;

        private void Awake()
        {
            mainCamera = Camera.main;
            if (countText == null) countText = GetComponentInChildren<TMP_Text>(true);
            if (singleMeshModel == null) singleMeshModel = transform.GetComponentInChildren<MeshRenderer>(true)?.gameObject;
        }

        private void LateUpdate()
        {
            if (countText != null && countText.gameObject.activeSelf && mainCamera != null)
            {
                countText.transform.rotation = mainCamera.transform.rotation;
                UpdateTextOffsetByRotation();
            }
        }

        public void RefreshVisuals(StackVisualMode mode, int remainingCount)
        {
            currentMode = mode;
            ClearStackedVisuals();

            if (currentMode == StackVisualMode.SingleWithUI)
            {
                if (singleMeshModel != null)
                {
                    singleMeshModel.SetActive(true);
                    singleMeshModel.transform.localRotation = Quaternion.Euler(singleModeRotation);
                }
            }
            else
            {
                if (singleMeshModel != null) singleMeshModel.SetActive(false);

                float baseOffsetY = singleMeshModel != null ? singleMeshModel.transform.localPosition.y : 0f;

                for (int i = 0; i < remainingCount; i++)
                {
                    if (singleMeshModel == null) continue;

                    GameObject item = PoolManager.Instance.Spawn(singleMeshModel, Vector3.zero, Quaternion.identity, visualContainer);
                    item.transform.localPosition = new Vector3(0f, (i * yOffset) + baseOffsetY, 0f);
                    item.transform.localRotation = Quaternion.Euler(stackedModeRotation);
                    item.transform.localScale = singleMeshModel.transform.localScale;
                    spawnedStackedItems.Add(item);
                }
            }

            UpdateCountText(true, remainingCount);
        }

        public void SetItemCountVisuals(int count, StackVisualMode mode)
        {
            currentMode = mode;
            if (currentMode == StackVisualMode.Stacked && spawnedStackedItems.Count > count)
            {
                int itemsToRemove = spawnedStackedItems.Count - count;
                for (int i = 0; i < itemsToRemove; i++)
                {
                    int lastIndex = spawnedStackedItems.Count - 1;
                    if (lastIndex >= 0)
                    {
                        if (spawnedStackedItems[lastIndex] != null) PoolManager.Instance.Despawn(spawnedStackedItems[lastIndex]);
                        spawnedStackedItems.RemoveAt(lastIndex);
                    }
                }

                UpdateCountText(true, count);
            }
            else
            {
                RefreshVisuals(mode, count);
            }
        }

        public void UpdateCountText(bool show, int count)
        {
            if (countText == null) return;

            if (show && count > 0)
            {
                countText.gameObject.SetActive(true);
                countText.text = count.ToString();
            }
            else
            {
                countText.gameObject.SetActive(false);
            }
        }

        private void UpdateTextOffsetByRotation()
        {
            if (countText == null) return;

            Vector3 targetOffset;

            if (currentMode == StackVisualMode.SingleWithUI)
            {
                targetOffset = singleModeTextOffset;
            }
            else
            {
                float yAngle = (transform.eulerAngles.y % 360f + 360f) % 360f;

                if (yAngle >= 0f && yAngle < 90f)
                {
                    targetOffset = new Vector3(0f, 0.25f, -1.1f);
                }
                else if (yAngle >= 90f && yAngle < 180f)
                {
                    targetOffset = new Vector3(1.1f, 0.25f, 0f);
                }
                else if (yAngle >= 180f && yAngle < 270f)
                {
                    targetOffset = new Vector3(0f, 0.25f, 1.1f);
                }
                else
                {
                    targetOffset = new Vector3(-1.1f, 0.25f, 0f);
                }
            }

            countText.transform.localPosition = Vector3.Lerp(
                countText.transform.localPosition,
                targetOffset,
                Time.deltaTime * textOffsetLerpSpeed
            );
        }

        public void ClearStackedVisuals()
        {
            foreach (var item in spawnedStackedItems)
            {
                if (item != null) PoolManager.Instance.Despawn(item);
            }

            spawnedStackedItems.Clear();
        }
    }
}