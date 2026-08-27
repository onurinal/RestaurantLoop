using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using RestaurantLoop.Audio;

namespace RestaurantLoop.Core
{
    public enum StackVisualMode
    {
        SingleWithUI,
        Stacked
    }

    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(StackItemVisuals))]
    [RequireComponent(typeof(StackItemAnimator))]
    [RequireComponent(typeof(StackItemMovement))]
    public class StackItem : MonoBehaviour, IInteractable
    {
        // Static registry tracking all active stacks in play with zero GC allocation
        private static readonly HashSet<StackItem> ActiveStacksInGame = new HashSet<StackItem>();
        public static int TotalActiveStackCount => ActiveStacksInGame.Count;

        [Header("Item Configuration")]
        [SerializeField] private ItemDataSO itemData;
        [SerializeField] private int remainingCount = 10;
        [SerializeField] private float serviceCooldown = 0.08f;

        private StackItemVisuals visuals;
        private StackItemAnimator animator;
        private StackItemMovement movement;

        private StackVisualMode currentMode = StackVisualMode.SingleWithUI;

        public int RemainingItemCount => remainingCount;
        public bool IsJumping => animator != null && animator.IsJumping;
        public bool IsWaitingForRack => movement != null && movement.IsWaitingForRack;
        public float CurrentDistance => movement != null ? movement.CurrentDistance : 0f;
        public ItemDataSO Data => itemData;

        public event Action<StackItem, Customer, ItemDataSO> FoodCommittedToCustomer;
        public event Action<StackItem> StackDepleted;

        private void OnEnable()
        {
            ActiveStacksInGame.Add(this);
        }

        private void OnDisable()
        {
            ActiveStacksInGame.Remove(this);
        }

        private void Awake()
        {
            visuals = GetComponent<StackItemVisuals>();
            animator = GetComponent<StackItemAnimator>();
            movement = GetComponent<StackItemMovement>();
        }

        public void Initialize(ItemDataSO data, int count)
        {
            itemData = data;
            remainingCount = Mathf.Max(0, count);
            currentMode = StackVisualMode.SingleWithUI;

            KillTweensInHierarchy(gameObject);

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            visuals.SetItemData(itemData);
            visuals.CollapseToSingle();
            visuals.RefreshVisuals(currentMode, remainingCount);
        }

        public void SetItemCount(int newCount)
        {
            remainingCount = Mathf.Max(0, newCount);
            visuals.SetItemCountVisuals(remainingCount, currentMode);
        }

        public void SetVisualMode(StackVisualMode mode)
        {
            currentMode = mode;
            visuals.RefreshVisuals(currentMode, remainingCount);
        }

        public void Shake() => animator.Shake();

        public void SetWaitingForRack(bool waiting)
        {
            if (movement != null)
            {
                movement.IsWaitingForRack = waiting;
            }
        }

        public void OnTap()
        {
            if (IsJumping ||
                (CrowdManager.Instance != null && CrowdManager.Instance.IsSpawningCustomers) ||
                (LevelManager.Instance != null && !LevelManager.Instance.IsGameActive)) return;

            if (AudioManager.Instance != null && AudioManager.Instance.tapSound != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.tapSound);
            }

            GetComponentInParent<BaseSlot>()?.OnStackTapped(this);
        }

        public void InitializeOnBelt(SplineConveyorPath path, float startDistance, float totalDistanceToExit)
        {
            serviceCooldown = 0f;

            if (currentMode != StackVisualMode.Stacked)
            {
                currentMode = StackVisualMode.Stacked;
                visuals.TransitionToStacked(remainingCount);
            }

            movement.InitializeOnBelt(path, startDistance, totalDistanceToExit);
        }

        public void MoveAlongBelt(SplineConveyorPath path, float speed, bool isClockwise, float deltaTime)
        {
            if (this == null || IsJumping) return;

            if (serviceCooldown > 0f) serviceCooldown -= deltaTime;

            movement.MoveAlongBelt(path, speed, isClockwise, deltaTime, OnExitReached);

            if (!IsWaitingForRack && serviceCooldown <= 0f)
            {
                CheckForNearbyCustomer();
            }
        }

        public void JumpToConveyor(Vector3 targetPosition, Action onComplete)
        {
            KillTweensInHierarchy(gameObject);

            if (currentMode != StackVisualMode.Stacked)
            {
                currentMode = StackVisualMode.Stacked;
                visuals.TransitionToStacked(remainingCount);
            }

            animator.JumpToConveyor(targetPosition, () =>
            {
                if (AudioManager.Instance != null && AudioManager.Instance.boardClickSound != null)
                    AudioManager.Instance.PlaySFX(AudioManager.Instance.boardClickSound);

                onComplete?.Invoke();
            });
        }

        public void JumpToSlot(Transform slotTransform, Action onComplete = null)
        {
            currentMode = StackVisualMode.SingleWithUI;
            visuals.CollapseToSingle();

            animator.JumpToSlot(slotTransform, () =>
            {
                if (AudioManager.Instance != null && AudioManager.Instance.rackDropSound != null)
                    AudioManager.Instance.PlaySFX(AudioManager.Instance.rackDropSound);

                onComplete?.Invoke();
            });
        }

        public static void ClearAllActiveStacks()
        {
            List<StackItem> stacks = new List<StackItem>(ActiveStacksInGame);
            for (int i = stacks.Count - 1; i >= 0; i--)
            {
                if (stacks[i] != null && stacks[i].gameObject != null)
                {
                    KillTweensInHierarchy(stacks[i].gameObject);
                    Destroy(stacks[i].gameObject);
                }
            }

            ActiveStacksInGame.Clear();
        }

        public static void KillTweensInHierarchy(GameObject target)
        {
            if (target == null) return;

            DOTween.Kill(target);
            Transform[] allTransforms = target.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                if (allTransforms[i] != null)
                {
                    allTransforms[i].DOKill();
                }
            }
        }

        private void CheckForNearbyCustomer()
        {
            if (IsJumping || itemData == null || CrowdManager.Instance == null || remainingCount <= 0) return;

            Customer targetCustomer = CrowdManager.Instance.CheckServiceForBeltItem(movement.CurrentDistance, itemData);
            if (targetCustomer == null) return;

            serviceCooldown = 0.08f;
            remainingCount--;

            if (AudioManager.Instance != null && AudioManager.Instance.throwSound != null)
                AudioManager.Instance.PlaySFX(AudioManager.Instance.throwSound);

            animator.AnimateItemThrowToCustomer(visuals.SingleMeshModel, targetCustomer.transform.position);

            SetItemCount(remainingCount);
            FoodCommittedToCustomer?.Invoke(this, targetCustomer, itemData);
            targetCustomer.ReceiveItem(this, () => { CrowdManager.Instance.OnCustomerServed(targetCustomer); });

            if (remainingCount <= 0) DepleteAndDestroy();
        }

        private void DepleteAndDestroy()
        {
            SetWaitingForRack(false);
            ConveyorManager.Instance.RemoveStackFromBelt(this);
            ConveyorManager.Instance.ReleaseCapacity();
            StackDepleted?.Invoke(this);

            KillTweensInHierarchy(gameObject);
            Destroy(gameObject);
        }

        private void OnExitReached()
        {
            if (remainingCount <= 0)
            {
                DepleteAndDestroy();
                return;
            }

            if (ConveyorManager.Instance != null)
            {
                ConveyorManager.Instance.OnStackCompletedBeltLoop(this);
            }
        }
    }
}