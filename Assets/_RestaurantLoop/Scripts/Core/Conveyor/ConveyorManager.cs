using System;
using System.Collections.Generic;
using UnityEngine;

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

        /// <summary>Raised after conveyor capacity changes through a successful reservation or release.</summary>
        public event Action<int, int> CapacityChanged;

        /// <summary>Raised after a stack has completed its entry jump and is part of the conveyor state.</summary>
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

            // A stack on the belt must no longer belong to its queue/rack slot.
            // Keep its world pose while the entry jump begins.
            stack.transform.SetParent(null, true);

            Vector3 entrancePosition = path.GetPosition(EntranceDistance);
            stack.JumpToConveyor(entrancePosition, () => TryAddStack(stack));
            return true;
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
