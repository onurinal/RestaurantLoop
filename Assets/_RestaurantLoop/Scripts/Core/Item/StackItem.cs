using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;
using RestaurantLoop.Infrastructure;
using RestaurantLoop.Audio;

namespace RestaurantLoop.Core
{
    public enum StackVisualMode
    {
        SingleWithUI,
        Stacked
    }

    [RequireComponent(typeof(BoxCollider))]
    public class StackItem : MonoBehaviour, IInteractable
    {
        [Header("Item Configuration")]
        [SerializeField] private ItemDataSO itemData;
        [SerializeField] private int remainingCount = 10;
        [SerializeField] private float jumpPower = 1.5f;
        [SerializeField] private float jumpDuration = 0.5f;

        [Header("Visual Setup")]
        [SerializeField] private Transform visualContainer;
        [SerializeField] private GameObject singleMeshModel;
        [SerializeField] private float yOffset = 1.0f;

        [Header("Rotation Overrides")]
        [Tooltip("Local rotation applied in Queue slots (Single Mode).")]
        [SerializeField] private Vector3 singleModeRotation = Vector3.zero;
        [Tooltip("Local rotation applied on belt (Stacked Mode). Keep X and Z at 0 to avoid spiraling.")]
        [SerializeField] private Vector3 stackedModeRotation = Vector3.zero;

        [Header("UI Setup")]
        [SerializeField] private TMP_Text countText;
        [Tooltip("Text offset in Queue and Rack slots (Single Mode).")]
        [SerializeField] private Vector3 singleModeTextOffset = new Vector3(0f, 0.25f, -1.1f);
        [Tooltip("Interpolation speed for smooth text position transitions around conveyor corners.")]
        [SerializeField] private float textOffsetLerpSpeed = 12f;

        private float currentDistance;
        private float traveledDistance;
        private float targetTravelDistance;
        private float serviceCooldown;
        private bool isWaitingForRack;

        private StackVisualMode currentMode = StackVisualMode.SingleWithUI;
        private readonly List<GameObject> spawnedStackedItems = new List<GameObject>();
        private Camera mainCamera;

        public int RemainingItemCount => remainingCount;
        public bool IsJumping { get; private set; }
        public bool IsWaitingForRack => isWaitingForRack;
        public ItemDataSO Data => itemData;

        public event Action<StackItem, Customer, ItemDataSO> FoodCommittedToCustomer;
        public event Action<StackItem> StackDepleted;

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
                // Always face camera
                countText.transform.rotation = mainCamera.transform.rotation;

                // Dynamically interpolate text local position smoothly on corners
                UpdateTextOffsetByRotation();
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

            // Smoothly interpolate current position toward the target offset position
            countText.transform.localPosition = Vector3.Lerp(
                countText.transform.localPosition,
                targetOffset,
                Time.deltaTime * textOffsetLerpSpeed
            );
        }

        public void Initialize(ItemDataSO data, int count)
        {
            itemData = data;
            remainingCount = Mathf.Max(0, count);
            currentMode = StackVisualMode.SingleWithUI;
            RefreshVisuals();
        }

        public void SetItemCount(int newCount)
        {
            remainingCount = Mathf.Max(0, newCount);

            if (currentMode == StackVisualMode.Stacked && spawnedStackedItems.Count > remainingCount)
            {
                int itemsToRemove = spawnedStackedItems.Count - remainingCount;
                for (int i = 0; i < itemsToRemove; i++)
                {
                    int lastIndex = spawnedStackedItems.Count - 1;
                    if (lastIndex >= 0)
                    {
                        if (spawnedStackedItems[lastIndex] != null) PoolManager.Instance.Despawn(spawnedStackedItems[lastIndex]);
                        spawnedStackedItems.RemoveAt(lastIndex);
                    }
                }

                UpdateCountText(true);
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

            UpdateCountText(true);
        }

        private void ClearStackedVisuals()
        {
            foreach (var item in spawnedStackedItems)
            {
                if (item != null) PoolManager.Instance.Despawn(item);
            }

            spawnedStackedItems.Clear();
        }

        private void UpdateCountText(bool show)
        {
            if (countText == null) return;

            if (show && remainingCount > 0)
            {
                countText.gameObject.SetActive(true);
                countText.text = remainingCount.ToString();
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
            if (IsJumping || (CrowdManager.Instance != null && CrowdManager.Instance.IsSpawningCustomers)) return;

            if (AudioManager.Instance != null && AudioManager.Instance.tapSound != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.tapSound);
            }

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

            if (serviceCooldown > 0f) serviceCooldown -= deltaTime;

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

            if (serviceCooldown <= 0f) CheckForNearbyCustomer();
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

                    if (AudioManager.Instance != null && AudioManager.Instance.boardClickSound != null)
                        AudioManager.Instance.PlaySFX(AudioManager.Instance.boardClickSound);

                    onComplete?.Invoke();
                });
        }

        public void JumpToSlot(Transform slotTransform, Action onComplete = null)
        {
            IsJumping = true;
            transform.DOKill();

            transform.DORotateQuaternion(slotTransform.rotation, jumpDuration);

            transform.DOJump(slotTransform.position, jumpPower, 1, jumpDuration)
                .OnComplete(() =>
                {
                    IsJumping = false;
                    transform.SetParent(slotTransform);
                    transform.localPosition = Vector3.zero;
                    transform.localRotation = Quaternion.identity;

                    SetVisualMode(StackVisualMode.SingleWithUI);

                    if (AudioManager.Instance != null && AudioManager.Instance.rackDropSound != null)
                        AudioManager.Instance.PlaySFX(AudioManager.Instance.rackDropSound);

                    onComplete?.Invoke();
                });
        }

        private void CheckForNearbyCustomer()
        {
            if (IsJumping || itemData == null || CrowdManager.Instance == null || remainingCount <= 0) return;

            Customer targetCustomer = CrowdManager.Instance.CheckServiceForBeltItem(currentDistance, itemData);
            if (targetCustomer == null) return;

            serviceCooldown = 0.5f;
            remainingCount--;

            if (singleMeshModel != null)
            {
                GameObject flyingItem = PoolManager.Instance.Spawn(singleMeshModel, transform.position, Quaternion.identity);

                if (AudioManager.Instance != null && AudioManager.Instance.throwSound != null)
                    AudioManager.Instance.PlaySFX(AudioManager.Instance.throwSound);

                flyingItem.transform.DOJump(targetCustomer.transform.position, 2f, 1, 0.35f)
                    .OnComplete(() => { PoolManager.Instance.Despawn(flyingItem); });
            }

            SetItemCount(remainingCount);
            FoodCommittedToCustomer?.Invoke(this, targetCustomer, itemData);
            targetCustomer.ReceiveItem(this, () => { CrowdManager.Instance.OnCustomerServed(targetCustomer); });

            if (remainingCount <= 0) DepleteAndDestroy();
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