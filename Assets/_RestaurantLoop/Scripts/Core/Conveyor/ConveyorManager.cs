using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using RestaurantLoop.Infrastructure;

namespace RestaurantLoop.Core
{
    public class ConveyorManager : MonoBehaviour
    {
        public static ConveyorManager Instance { get; private set; }

        [SerializeField] private SplineConveyorPath path;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private int maxCapacity = 5;
        [SerializeField] private bool isClockwise = true;

        [Header("Spline Points")]
        [Range(0f, 1f)] [SerializeField] private float entranceRatio = 0f;
        [Range(0f, 1f)] [SerializeField] private float exitRatio = 0.8f;

        private readonly List<StackItem> activeStacks = new List<StackItem>();
        private int occupiedCapacity = 0;

        public bool CanAcceptStack => occupiedCapacity < maxCapacity;
        public bool IsClockwise => isClockwise;
        public float MoveSpeed => moveSpeed;
        public SplineConveyorPath Path => path;
        public int OccupiedCapacity => occupiedCapacity;
        public int MaxCapacity => maxCapacity;

        public event Action<int, int> CapacityChanged;
        public event Action<StackItem> StackEnteredBelt;

        public float EntranceDistance => entranceRatio * (path != null ? path.Length : 0f);
        public float ExitDistance => exitRatio * (path != null ? path.Length : 0f);

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (path != null) path.Initialize();
        }

        private void Update()
        {
            for (int i = activeStacks.Count - 1; i >= 0; i--)
            {
                activeStacks[i].MoveAlongBelt(path, moveSpeed, isClockwise, Time.deltaTime);
            }
        }

        public bool TryReserveSlot()
        {
            if (!CanAcceptStack) return false;

            occupiedCapacity++;
            CapacityChanged?.Invoke(occupiedCapacity, maxCapacity);
            return true;
        }

        public bool TryAddStack(StackItem stack)
        {
            activeStacks.Add(stack);
            stack.InitializeOnBelt(path, EntranceDistance, GetRequiredTravelDistance());
            StackEnteredBelt?.Invoke(stack);
            return true;
        }

        public void RemoveStackFromBelt(StackItem stack)
        {
            activeStacks.Remove(stack);
        }

        public void ReleaseCapacity()
        {
            if (occupiedCapacity > 0)
            {
                occupiedCapacity--;
                CapacityChanged?.Invoke(occupiedCapacity, maxCapacity);
            }
        }

        public bool TrySendStackToBelt(StackItem stack)
        {
            if (stack == null || stack.RemainingItemCount <= 0)
            {
                return false;
            }

            if (!TryReserveSlot())
            {
                stack.Shake();
                return false;
            }

            stack.transform.SetParent(null, true);

            Vector3 entrancePosition = path.GetPosition(EntranceDistance);
            stack.JumpToConveyor(entrancePosition, () => TryAddStack(stack));
            return true;
        }

        /// <summary>
        /// Checks whether total active stacks across Queue, Rack, Belt, and Mid-Air jumps can fit entirely on the belt.
        /// </summary>
        public bool ShouldKeepLoopingOnBelt()
        {
            // High-performance static registry query with 0ms overhead and zero GC allocation
            return StackItem.TotalActiveStackCount <= maxCapacity;
        }

        public void OnStackCompletedBeltLoop(StackItem stack)
        {
            if (stack == null || stack.RemainingItemCount <= 0) return;

            // Auto-Loop: Initiate DOTween jump from current exit position to entrance position
            if (ShouldKeepLoopingOnBelt())
            {
                Vector3 entrancePosition = path.GetPosition(EntranceDistance);

                stack.JumpToConveyor(entrancePosition, () =>
                {
                    // Reset spline tracking distance only after landing at the entrance
                    stack.InitializeOnBelt(path, EntranceDistance, GetRequiredTravelDistance());
                });
                return;
            }

            // Default Rack Transfer
            RemoveStackFromBelt(stack);
            bool addedToRack = RackManager.Instance != null && RackManager.Instance.TryAddStackToRack(stack);

            if (!addedToRack)
            {
                LevelManager.Instance?.ReportRackOverflow();
            }
        }

        public void ClearAllItems()
        {
            for (int i = activeStacks.Count - 1; i >= 0; i--)
            {
                if (activeStacks[i] != null)
                {
                    activeStacks[i].transform.DOKill();
                    PoolManager.Instance.Despawn(activeStacks[i].gameObject);
                }
            }

            activeStacks.Clear();
            occupiedCapacity = 0;
            CapacityChanged?.Invoke(occupiedCapacity, maxCapacity);
        }

        public float GetRequiredTravelDistance()
        {
            float totalLength = path != null ? path.Length : 0f;
            if (totalLength <= 0f) return 0f;

            float entry = EntranceDistance;
            float exit = ExitDistance;

            if (isClockwise)
                return exit > entry ? exit - entry : (totalLength - entry) + exit;

            return entry > exit ? entry - exit : entry + (totalLength - exit);
        }

        private void OnDrawGizmosSelected()
        {
            if (path == null) path = GetComponent<SplineConveyorPath>();
            if (path == null) return;

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(path.GetPosition(EntranceDistance), 0.35f);

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(path.GetPosition(ExitDistance), 0.35f);
        }
    }
}