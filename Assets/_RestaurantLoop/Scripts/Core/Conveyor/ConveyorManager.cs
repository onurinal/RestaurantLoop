using System;
using System.Collections;
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

        [Header("Safety Clearance")]
        [Tooltip("Minimum safe distance (in spline units) required around the entrance before accepting new stacks.")]
        [SerializeField] private float entranceSafetyBuffer = 1.8f;

        private readonly List<StackItem> activeStacks = new List<StackItem>();
        private int occupiedCapacity = 0;
        private int pendingJumpsCount = 0;

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
            // Pause conveyor movement if game is inactive or level state is Won/Lost
            if (LevelManager.Instance != null && !LevelManager.Instance.IsGameActive) return;

            for (int i = activeStacks.Count - 1; i >= 0; i--)
            {
                if (activeStacks[i] != null)
                {
                    activeStacks[i].MoveAlongBelt(path, moveSpeed, isClockwise, Time.deltaTime);
                }
            }
        }

        public bool IsEntranceClear()
        {
            if (pendingJumpsCount > 0) return false;

            float entranceDist = EntranceDistance;
            float pathLen = path != null ? path.Length : 0f;

            for (int i = 0; i < activeStacks.Count; i++)
            {
                StackItem stack = activeStacks[i];
                if (stack == null || stack.IsJumping) continue;

                float dist = stack.CurrentDistance;
                float delta = Mathf.Abs(dist - entranceDist);

                if (pathLen > 0f)
                {
                    delta = Mathf.Min(delta, pathLen - delta);
                }

                if (delta < entranceSafetyBuffer)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsEntranceClearForAutoLoop()
        {
            if (pendingJumpsCount > 1) return false;

            float entranceDist = EntranceDistance;
            float pathLen = path != null ? path.Length : 0f;

            for (int i = 0; i < activeStacks.Count; i++)
            {
                StackItem stack = activeStacks[i];
                if (stack == null || stack.IsJumping) continue;

                float dist = stack.CurrentDistance;
                float delta = Mathf.Abs(dist - entranceDist);

                if (pathLen > 0f)
                {
                    delta = Mathf.Min(delta, pathLen - delta);
                }

                if (delta < entranceSafetyBuffer)
                {
                    return false;
                }
            }

            return true;
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
            if (stack == null || !stack.gameObject.activeInHierarchy) return false;

            activeStacks.Add(stack);
            stack.InitializeOnBelt(path, EntranceDistance, GetRequiredTravelDistance());
            StackEnteredBelt?.Invoke(stack);
            return true;
        }

        public void RemoveStackFromBelt(StackItem stack)
        {
            if (activeStacks.Contains(stack))
            {
                activeStacks.Remove(stack);
            }
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
            if (stack == null || stack.RemainingItemCount <= 0) return false;

            if (!IsEntranceClear() || !CanAcceptStack)
            {
                return false;
            }

            if (!TryReserveSlot())
            {
                return false;
            }

            stack.transform.DOKill();
            stack.transform.SetParent(null, true);
            pendingJumpsCount++;

            Vector3 entrancePosition = path.GetPosition(EntranceDistance);
            stack.JumpToConveyor(entrancePosition, () =>
            {
                pendingJumpsCount = Mathf.Max(0, pendingJumpsCount - 1);

                if (stack != null && stack.gameObject.activeInHierarchy)
                {
                    TryAddStack(stack);
                }
            });

            return true;
        }

        public bool ShouldKeepLoopingOnBelt()
        {
            return StackItem.TotalActiveStackCount <= maxCapacity;
        }

        public void OnStackCompletedBeltLoop(StackItem stack)
        {
            if (stack == null || stack.RemainingItemCount <= 0) return;

            if (ShouldKeepLoopingOnBelt())
            {
                RemoveStackFromBelt(stack);
                StartCoroutine(Routine_AutoLoopJump(stack));
                return;
            }

            RemoveStackFromBelt(stack);
            bool addedToRack = RackManager.Instance != null && RackManager.Instance.TryAddStackToRack(stack);

            if (!addedToRack)
            {
                // Destroy overflow stack immediately to prevent ghost objects floating in scene
                StackItem.KillTweensInHierarchy(stack.gameObject);
                Destroy(stack.gameObject);

                LevelManager.Instance?.ReportRackOverflow();
            }
        }

        private IEnumerator Routine_AutoLoopJump(StackItem stack)
        {
            pendingJumpsCount++;

            while (!IsEntranceClearForAutoLoop())
            {
                if (stack == null || !stack.gameObject.activeInHierarchy)
                {
                    pendingJumpsCount = Mathf.Max(0, pendingJumpsCount - 1);
                    yield break;
                }

                yield return null;
            }

            if (stack == null || !stack.gameObject.activeInHierarchy)
            {
                pendingJumpsCount = Mathf.Max(0, pendingJumpsCount - 1);
                yield break;
            }

            Vector3 entrancePosition = path.GetPosition(EntranceDistance);

            stack.JumpToConveyor(entrancePosition, () =>
            {
                pendingJumpsCount = Mathf.Max(0, pendingJumpsCount - 1);

                if (stack != null && stack.gameObject.activeInHierarchy)
                {
                    TryAddStack(stack);
                }
            });
        }

        public void ClearAllItems()
        {
            StopAllCoroutines();

            for (int i = activeStacks.Count - 1; i >= 0; i--)
            {
                if (activeStacks[i] != null)
                {
                    StackItem.KillTweensInHierarchy(activeStacks[i].gameObject);
                    DestroyImmediate(activeStacks[i].gameObject);
                }
            }

            activeStacks.Clear();
            occupiedCapacity = 0;
            pendingJumpsCount = 0;
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