using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;
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

        [Header("Stack Transition Animation")]
        [Tooltip("Delay between individual food items appearing or collapsing.")]
        [SerializeField, Min(0f)] private float itemTransitionStagger = 0.035f;
        [SerializeField, Min(0.01f)] private float itemTransitionDuration = 0.12f;
        [SerializeField, Min(0f)] private float rotationTransitionDuration = 0.14f;

        [Header("UI Setup")]
        [SerializeField] private TMP_Text countText;
        [Tooltip("Interpolation speed for smooth text position transitions around conveyor corners.")]
        [SerializeField] private float textOffsetLerpSpeed = 12f;

        private readonly List<GameObject> spawnedStackedItems = new List<GameObject>();
        private Camera mainCamera;
        private StackVisualMode currentMode = StackVisualMode.SingleWithUI;
        private float activeTextDistance = 1.1f;
        private Vector3 singleModelBaseLocalPosition;
        private Vector3 singleModelBaseLocalScale;
        private Tween selectionHighlightTween;
        private Vector3 selectionBaseLocalScale;
        private bool hasSelectionBaseScale;
        private Color countTextBaseColor;
        private bool hasCountTextBaseColor;
        private Tween collapsedItemsReleaseTween;
        private TweenCallback collapsedItemsReleaseCallback;

        public GameObject SingleMeshModel => singleMeshModel;
        public Transform VisualContainer => visualContainer;

        private void OnDisable()
        {
            ResetForPoolRelease();
        }

        /// <summary>
        /// Pulses only the food visual while a power-up is choosing a stack.
        /// This deliberately avoids scaling StackItem itself: a rack stack can
        /// have a differently-scaled slot parent from a queue stack.
        /// </summary>
        public void SetSelectionHighlight(bool highlighted)
        {
            Transform target = GetSelectionHighlightTarget();
            if (target == null) return;

            selectionHighlightTween?.Kill();
            selectionHighlightTween = null;

            if (!highlighted)
            {
                if (hasSelectionBaseScale)
                {
                    target.localScale = selectionBaseLocalScale;
                    hasSelectionBaseScale = false;
                }

                return;
            }

            if (!hasSelectionBaseScale)
            {
                selectionBaseLocalScale = target.localScale;
                hasSelectionBaseScale = true;
            }

            target.localScale = selectionBaseLocalScale;
            selectionHighlightTween = target.DOScale(selectionBaseLocalScale * 1.08f, 0.35f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
        }

        /// <summary>
        /// Applies a multiplier to the authored count-label alpha. The original
        /// color is retained so returning to full opacity is lossless.
        /// </summary>
        public void SetCountTextOpacity(float opacity)
        {
            if (countText == null) return;

            if (!hasCountTextBaseColor)
            {
                countTextBaseColor = countText.color;
                hasCountTextBaseColor = true;
            }

            Color color = countTextBaseColor;
            color.a *= Mathf.Clamp01(opacity);
            countText.color = color;
        }

        private void Awake()
        {
            collapsedItemsReleaseCallback = ReleaseCollapsedStackedItems;
            mainCamera = Camera.main;
            if (countText == null) countText = GetComponentInChildren<TMP_Text>(true);
            if (singleMeshModel == null) singleMeshModel = transform.GetComponentInChildren<MeshRenderer>(true)?.gameObject;

            if (countText != null)
            {
                countTextBaseColor = countText.color;
                hasCountTextBaseColor = true;
            }

            if (singleMeshModel != null)
            {
                singleModelBaseLocalPosition = singleMeshModel.transform.localPosition;
                singleModelBaseLocalScale = singleMeshModel.transform.localScale;
            }
        }

        private void LateUpdate()
        {
            if (countText != null && countText.gameObject.activeSelf && mainCamera != null)
            {
                countText.transform.rotation = mainCamera.transform.rotation;
                UpdateTextOffsetByRotation();
            }
        }

        public void SetItemData(ItemDataSO itemData)
        {
            // Dynamically assign text offset distance based on ItemDataSO configuration
            activeTextDistance = itemData != null ? itemData.UITextOffsetDistance : 1.1f;
        }

        private Transform GetSelectionHighlightTarget()
        {
            // Selection applies only to the actual food mesh.  Do not use the
            // optional VisualContainer here: it is not part of the scale contract
            // for stack prefabs.
            return singleMeshModel != null ? singleMeshModel.transform : null;
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
                    singleMeshModel.transform.localPosition = singleModelBaseLocalPosition;
                    singleMeshModel.transform.localScale = singleModelBaseLocalScale;
                    singleMeshModel.transform.localRotation = Quaternion.Euler(singleModeRotation);
                }
            }
            else
            {
                BuildStackImmediately(remainingCount);
            }

            UpdateCountText(true, remainingCount);
        }

        public void SetItemCountVisuals(int count, StackVisualMode mode)
        {
            currentMode = mode;
            if (currentMode == StackVisualMode.Stacked)
            {
                int visualCount = GetStackedVisualCount();
                while (visualCount > count)
                {
                    if (spawnedStackedItems.Count > 0)
                    {
                        int lastIndex = spawnedStackedItems.Count - 1;
                        GameObject item = spawnedStackedItems[lastIndex];
                        spawnedStackedItems.RemoveAt(lastIndex);
                        if (item != null)
                        {
                            item.transform.DOKill();
                            PoolManager.Instance.Despawn(item);
                        }
                    }
                    else if (singleMeshModel != null)
                    {
                        singleMeshModel.SetActive(false);
                    }

                    visualCount--;
                }

                while (visualCount < count)
                {
                    AddStackedItem(visualCount, false);
                    visualCount++;
                }

                UpdateCountText(true, count);
            }
            else
            {
                RefreshVisuals(mode, count);
            }
        }

        /// <summary>Builds the belt stack from its existing single food model, bottom to top.</summary>
        public void TransitionToStacked(int remainingCount)
        {
            currentMode = StackVisualMode.Stacked;
            ClearStackedVisuals();
            UpdateCountText(true, remainingCount);

            if (singleMeshModel == null || remainingCount <= 0)
            {
                if (singleMeshModel != null) singleMeshModel.SetActive(false);
                return;
            }

            ResetSingleModelForStack();
            TweenSingleModelRotation(stackedModeRotation);

            for (int itemIndex = 1; itemIndex < remainingCount; itemIndex++)
            {
                float delay = (itemIndex - 1) * itemTransitionStagger;
                AddStackedItem(itemIndex, true, delay);
            }
        }

        /// <summary>Collapses a belt stack from the top down while it travels to a rack slot.</summary>
        public void CollapseToSingle()
        {
            currentMode = StackVisualMode.SingleWithUI;
            DOTween.Kill(this);

            if (singleMeshModel == null) return;

            ResetSingleModelForStack();
            TweenSingleModelRotation(singleModeRotation);

            float lastReleaseDelay = -1f;
            for (int itemIndex = spawnedStackedItems.Count - 1, collapseOrder = 0; itemIndex >= 0; itemIndex--, collapseOrder++)
            {
                GameObject item = spawnedStackedItems[itemIndex];
                if (item == null) continue;

                float delay = collapseOrder * itemTransitionStagger;
                item.transform.DOKill();
                item.transform.DOLocalMove(GetStackedLocalPosition(itemIndex), itemTransitionDuration).SetDelay(delay).SetEase(Ease.InQuad);
                item.transform.DOScale(Vector3.zero, itemTransitionDuration).SetDelay(delay).SetEase(Ease.InQuad);
                lastReleaseDelay = Mathf.Max(lastReleaseDelay, delay);
            }

            collapsedItemsReleaseTween?.Kill();
            collapsedItemsReleaseTween = null;
            if (lastReleaseDelay >= 0f)
            {
                collapsedItemsReleaseTween = DOVirtual
                    .DelayedCall(lastReleaseDelay + itemTransitionDuration, collapsedItemsReleaseCallback)
                    .SetTarget(this);
            }
        }

        public void UpdateCountText(bool show, int count)
        {
            if (countText == null) return;

            if (show && count > 0)
            {
                countText.gameObject.SetActive(true);
                countText.SetText("{0}", count);
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
                // Single Mode (Queue and Rack slots) also scales dynamically with activeTextDistance
                targetOffset = new Vector3(0f, 0.25f, -activeTextDistance);
            }
            else
            {
                float yAngle = (transform.eulerAngles.y % 360f + 360f) % 360f;

                if (yAngle >= 0f && yAngle < 90f)
                {
                    targetOffset = new Vector3(0f, 0.25f, -activeTextDistance);
                }
                else if (yAngle >= 90f && yAngle < 180f)
                {
                    targetOffset = new Vector3(activeTextDistance, 0.25f, 0f);
                }
                else if (yAngle >= 180f && yAngle < 270f)
                {
                    targetOffset = new Vector3(0f, 0.25f, activeTextDistance);
                }
                else
                {
                    targetOffset = new Vector3(-activeTextDistance, 0.25f, 0f);
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
            DOTween.Kill(this);
            collapsedItemsReleaseTween = null;
            foreach (var item in spawnedStackedItems)
            {
                if (item != null)
                {
                    item.transform.DOKill();
                    ReleaseVisualToPool(item);
                }
            }

            spawnedStackedItems.Clear();
        }

        /// <summary>Clears pooled duplicate food visuals and transient selection state.</summary>
        public void ResetForPoolRelease()
        {
            selectionHighlightTween?.Kill();
            selectionHighlightTween = null;
            hasSelectionBaseScale = false;
            ClearStackedVisuals();
        }

        private void BuildStackImmediately(int count)
        {
            if (singleMeshModel == null) return;

            if (count <= 0)
            {
                singleMeshModel.SetActive(false);
                return;
            }

            ResetSingleModelForStack();
            singleMeshModel.transform.localRotation = Quaternion.Euler(stackedModeRotation);
            for (int itemIndex = 1; itemIndex < count; itemIndex++) AddStackedItem(itemIndex, false);
        }

        private void AddStackedItem(int index, bool animate, float delay = 0f)
        {
            if (singleMeshModel == null || !singleMeshModel.activeInHierarchy) return;

            GameObject item = PoolManager.Instance.Spawn(singleMeshModel, Vector3.zero, Quaternion.identity, visualContainer);
            Transform itemTransform = item.transform;
            Vector3 targetPosition = GetStackedLocalPosition(index);

            itemTransform.localRotation = Quaternion.Euler(stackedModeRotation);
            itemTransform.localScale = singleModelBaseLocalScale;
            itemTransform.localPosition = animate
                ? GetStackedLocalPosition(index - 1)
                : targetPosition;

            spawnedStackedItems.Add(item);

            if (!animate) return;

            itemTransform.localScale = Vector3.zero;
            itemTransform.DOLocalMove(targetPosition, itemTransitionDuration).SetDelay(delay).SetEase(Ease.OutBack);
            itemTransform.DOScale(singleModelBaseLocalScale, itemTransitionDuration).SetDelay(delay).SetEase(Ease.OutBack);
        }

        private int GetStackedVisualCount()
        {
            return (singleMeshModel != null && singleMeshModel.activeSelf ? 1 : 0) + spawnedStackedItems.Count;
        }

        private Vector3 GetStackedLocalPosition(int itemIndex)
        {
            return singleModelBaseLocalPosition + Vector3.up * (itemIndex * yOffset);
        }

        private void ResetSingleModelForStack()
        {
            singleMeshModel.transform.DOKill();
            singleMeshModel.SetActive(true);
            singleMeshModel.transform.localPosition = singleModelBaseLocalPosition;
            singleMeshModel.transform.localScale = singleModelBaseLocalScale;
        }

        private void TweenSingleModelRotation(Vector3 targetRotation)
        {
            singleMeshModel.transform.DOKill();
            if (rotationTransitionDuration <= 0f)
            {
                singleMeshModel.transform.localRotation = Quaternion.Euler(targetRotation);
                return;
            }

            singleMeshModel.transform.DOLocalRotate(targetRotation, rotationTransitionDuration).SetEase(Ease.OutQuad);
        }

        private void ReleaseCollapsedStackedItems()
        {
            collapsedItemsReleaseTween = null;
            for (int i = spawnedStackedItems.Count - 1; i >= 0; i--)
            {
                GameObject item = spawnedStackedItems[i];
                if (item != null)
                {
                    item.transform.DOKill();
                    ReleaseVisualToPool(item);
                }
            }

            spawnedStackedItems.Clear();
        }

        private static void ReleaseVisualToPool(GameObject item)
        {
            if (item == null) return;

            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Despawn(item);
            }
            else
            {
                Destroy(item);
            }
        }
    }
}
