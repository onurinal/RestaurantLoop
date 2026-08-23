using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

namespace RestaurantLoop.Core
{
    public enum StackVisualMode
    {
        SingleWithUI, // Queue & Rack: 1 Mesh + Text Counter
        Stacked       // Conveyor: Vertical Stack + Top Text Counter
    }

    /// <summary>
    /// Manages item stack movement, visual mode states (Queue/Belt/Rack), item throwing, and dynamic text positioning.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class StackItem : MonoBehaviour, IInteractable
    {
        [SerializeField] private int remainingCount = 10;
        [SerializeField] private float jumpPower = 1.5f;
        [SerializeField] private float jumpDuration = 0.5f;

        [Header("Data")]
        [SerializeField] private ItemDataSO itemData;

        [Header("Visual Settings")]
        [SerializeField] private Transform visualContainer;
        [SerializeField] private GameObject singleMeshModel;
        [SerializeField] private float yOffset = 1.0f;

        [Header("UI Settings")]
        [SerializeField] private TMP_Text countText;
        [SerializeField] private float textHeightOffset = 0.8f;

        private float currentDistance;
        private float traveledDistance;
        private float targetTravelDistance;
        private float serviceCooldown = 0f;
        private bool isWaitingForRack;

        private StackVisualMode currentMode = StackVisualMode.SingleWithUI;
        private readonly List<GameObject> spawnedStackedItems = new List<GameObject>();
        private Camera mainCamera;

        public int RemainingItemCount => remainingCount;
        public bool IsJumping { get; private set; }
        public bool IsWaitingForRack => isWaitingForRack;
        public ItemDataSO Data => itemData;

        /// <summary>Raised after this prototype commits one food item to an eligible customer.</summary>
        public event Action<StackItem, Customer, ItemDataSO> FoodCommittedToCustomer;

        /// <summary>Raised after the final committed food item removes this stack from the conveyor.</summary>
        public event Action<StackItem> StackDepleted;

        private void Awake()
        {
            mainCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (countText != null && countText.gameObject.activeSelf && mainCamera != null)
            {
                countText.transform.rotation = mainCamera.transform.rotation;
            }
        }

        public void InitializeData(ItemDataSO data)
        {
            if (data != null)
            {
                itemData = data;
                RefreshVisuals();
            }
        }

        public void SetItemCount(int newCount)
        {
            remainingCount = Mathf.Max(0, newCount);

            // Optimization: Remove top item directly instead of rebuilding entire stack
            if (currentMode == StackVisualMode.Stacked && spawnedStackedItems.Count > remainingCount)
            {
                int itemsToRemove = spawnedStackedItems.Count - remainingCount;
                for (int i = 0; i < itemsToRemove; i++)
                {
                    int lastIndex = spawnedStackedItems.Count - 1;
                    if (lastIndex >= 0)
                    {
                        if (spawnedStackedItems[lastIndex] != null) Destroy(spawnedStackedItems[lastIndex]);
                        spawnedStackedItems.RemoveAt(lastIndex);
                    }
                }
                float totalStackHeight = remainingCount * yOffset;
                UpdateCountText(true, totalStackHeight + textHeightOffset);
            }
            else
            {
                RefreshVisuals();
            }
        }

        public void SetVisualMode(StackVisualMode mode)
        {
            currentMode = mode;
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            ClearStackedVisuals();

            float modelBaseOffsetY = singleMeshModel != null ? singleMeshModel.transform.localPosition.y : 0f;

            if (currentMode == StackVisualMode.SingleWithUI)
            {
                if (singleMeshModel != null) singleMeshModel.SetActive(true);

                UpdateCountText(true, yOffset + textHeightOffset);
            }
            else
            {
                if (singleMeshModel != null) singleMeshModel.SetActive(false);

                for (int i = 0; i < remainingCount; i++)
                {
                    if (singleMeshModel != null)
                    {
                        GameObject item = Instantiate(singleMeshModel, visualContainer);
                        item.SetActive(true);

                        item.transform.localPosition = new Vector3(0f, (i * yOffset) + modelBaseOffsetY, 0f);
                        item.transform.localRotation = singleMeshModel.transform.localRotation;
                        item.transform.localScale = singleMeshModel.transform.localScale;
                        spawnedStackedItems.Add(item);
                    }
                }

                float totalStackHeight = remainingCount * yOffset;
                UpdateCountText(true, totalStackHeight + textHeightOffset);
            }
        }

        private void ClearStackedVisuals()
        {
            foreach (var item in spawnedStackedItems)
            {
                if (item != null) Destroy(item);
            }
            spawnedStackedItems.Clear();
        }

        private void UpdateCountText(bool show, float targetYOffset)
        {
            if (countText == null) return;

            if (show && remainingCount > 0)
            {
                countText.gameObject.SetActive(true);
                countText.text = remainingCount.ToString();
                countText.transform.localPosition = new Vector3(0f, targetYOffset, 0f);
            }
            else
            {
                countText.gameObject.SetActive(false);
            }
        }

        public void Shake()
        {
            if (IsJumping || DOTween.IsTweening(transform)) return;

            transform.DOKill();
            transform.localPosition = Vector3.zero;

            transform.DOShakePosition(0.2f, 0.12f, 10, 90f)
                .OnComplete(() => { transform.localPosition = Vector3.zero; });
        }

        public void OnTap()
        {
            if (IsJumping) return;

            // Ignore taps while the entrance sequence is still walking customers to their slots/stations,
            // so items can't be sent to the belt before there's anyone there to serve.
            if (CrowdManager.Instance != null && CrowdManager.Instance.IsSpawningCustomers) return;

            GetComponentInParent<BaseSlot>()?.OnStackTapped(this);
        }

        public void InitializeOnBelt(SplineConveyorPath path, float startDistance, float totalDistanceToExit)
        {
            currentDistance = startDistance;
            traveledDistance = 0f;
            targetTravelDistance = totalDistanceToExit;
            IsJumping = false;
            isWaitingForRack = false;
            serviceCooldown = 0f;

            SetVisualMode(StackVisualMode.Stacked);
            UpdateTransform(path, true);
        }

        public void MoveAlongBelt(SplineConveyorPath path, float speed, bool isClockwise, float deltaTime)
        {
            if (this == null || IsJumping) return;

            if (isWaitingForRack)
            {
                OnExitReached();
                return;
            }

            if (serviceCooldown > 0f)
            {
                serviceCooldown -= deltaTime;
            }

            float remainingTravelDistance = Mathf.Max(0f, targetTravelDistance - traveledDistance);
            float stepDistance = Mathf.Min(Mathf.Abs(speed * deltaTime), remainingTravelDistance);
            float moveDelta = isClockwise ? stepDistance : -stepDistance;
            currentDistance = (currentDistance + moveDelta) % path.Length;
            if (currentDistance < 0f) currentDistance += path.Length;

            traveledDistance += stepDistance;
            UpdateTransform(path, isClockwise);

            if (traveledDistance >= targetTravelDistance)
            {
                OnExitReached();
                return;
            }

            if (serviceCooldown <= 0f)
            {
                CheckForNearbyCustomer();
            }
        }

        public void JumpToConveyor(Vector3 targetPosition, Action onComplete)
        {
            IsJumping = true;
            transform.DOKill();
            transform.DOJump(targetPosition, jumpPower, 1, jumpDuration)
                .OnComplete(() =>
                {
                    IsJumping = false;
                    SetVisualMode(StackVisualMode.Stacked);
                    onComplete?.Invoke();
                });
        }

        public void JumpToSlot(Transform slotTransform, Action onComplete = null)
        {
            IsJumping = true;
            transform.DOKill();
            transform.DOJump(slotTransform.position, jumpPower, 1, jumpDuration)
                .OnComplete(() =>
                {
                    IsJumping = false;
                    transform.SetParent(slotTransform);
                    transform.localPosition = Vector3.zero;

                    SetVisualMode(StackVisualMode.SingleWithUI);
                    onComplete?.Invoke();
                });
        }

        private void CheckForNearbyCustomer()
        {
            if (IsJumping || itemData == null || CrowdManager.Instance == null || remainingCount <= 0) return;

            Customer targetCustomer = CrowdManager.Instance.CheckServiceForBeltItem(currentDistance, itemData);

            if (targetCustomer != null)
            {
                serviceCooldown = 0.5f;
                remainingCount--;

                if (singleMeshModel != null)
                {
                    GameObject flyingItem = Instantiate(singleMeshModel, transform.position, Quaternion.identity);
                    flyingItem.SetActive(true);

                    flyingItem.transform.DOJump(targetCustomer.transform.position, 2f, 1, 0.35f)
                        .OnComplete(() => { Destroy(flyingItem); });
                }

                SetItemCount(remainingCount);
                FoodCommittedToCustomer?.Invoke(this, targetCustomer, itemData);
                targetCustomer.ReceiveItem(this, () => { CrowdManager.Instance.OnCustomerServed(targetCustomer); });

                if (remainingCount <= 0)
                {
                    DepleteAndDestroy();
                }
            }
        }

        private void DepleteAndDestroy()
        {
            IsJumping = true;
            isWaitingForRack = false;
            ConveyorManager.Instance.RemoveStackFromBelt(this);
            ConveyorManager.Instance.ReleaseCapacity();
            StackDepleted?.Invoke(this);
            Destroy(gameObject);
        }

        private void UpdateTransform(SplineConveyorPath path, bool isClockwise)
        {
            transform.position = path.GetPosition(currentDistance);
            Vector3 direction = path.GetDirection(currentDistance, isClockwise);
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        private void OnExitReached()
        {
            if (remainingCount <= 0)
            {
                DepleteAndDestroy();
                return;
            }

            RackManager rack = RackManager.Instance;
            if (rack == null || !rack.HasAvailableSlot)
            {
                IsJumping = false;
                isWaitingForRack = true;
                return;
            }

            isWaitingForRack = false;
            IsJumping = true;

            if (!rack.TryAddStackToRack(this))
            {
                IsJumping = false;
                isWaitingForRack = true;
            }
        }
    }
}