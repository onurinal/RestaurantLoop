using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using RestaurantLoop.Infrastructure;
using RestaurantLoop.UI; 

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

        [Header("Queued Entrance Landing")]
        [Tooltip("World-space height above the conveyor entrance where a stack waits for a safe landing gap.")]
        [SerializeField, Min(0f)] private float queuedEntranceHoverHeight = 2.5f;
        [Tooltip("Additional vertical separation for each later stack waiting above the entrance.")]
        [SerializeField, Min(0f)] private float queuedEntranceHoverStackSpacing = 0.7f;
        [SerializeField, Min(0.01f)] private float queuedEntranceLandingDuration = 0.18f;

        private readonly List<StackItem> activeStacks = new List<StackItem>();
        private readonly List<StackItem> pendingJumpStacks = new List<StackItem>();
        private int occupiedCapacity = 0;
        private int pendingJumpsCount = 0;
        private int attemptCapacityBonus;

        public bool CanAcceptStack => occupiedCapacity < MaxCapacity;
        public bool IsClockwise => isClockwise;
        public float MoveSpeed => moveSpeed;
        public SplineConveyorPath Path => path;
        public int OccupiedCapacity => occupiedCapacity;
        public int AuthoredMaxCapacity => maxCapacity;
        public int MaxCapacity => maxCapacity + attemptCapacityBonus;

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
            CapacityChanged?.Invoke(occupiedCapacity, MaxCapacity);
            return true;
        }

        /// <summary>
        /// Raises only this attempt's simultaneous conveyor-stack limit. It never
        /// creates a stack or changes queue/rack state.
        /// </summary>
        public bool TryAddAttemptCapacity(int amount = 1)
        {
            if (amount <= 0) return false;

            attemptCapacityBonus += amount;
            CapacityChanged?.Invoke(occupiedCapacity, MaxCapacity);
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
                CapacityChanged?.Invoke(occupiedCapacity, MaxCapacity);
            }
        }

        public bool TrySendStackToBelt(StackItem stack)
        {
            if (stack == null || stack.RemainingItemCount <= 0) return false;

            // Capacity remains an immediate rejection. Entrance congestion only
            // uses the hover queue when the player tapped while it was blocked.
            if (!CanAcceptStack)
            {
                return false;
            }

            bool entranceWasBlocked = !IsEntranceClear();

            if (!TryReserveSlot())
            {
                return false;
            }

            stack.transform.DOKill();
            stack.transform.SetParent(null, true);
            RegisterPendingJump(stack);

            if (!entranceWasBlocked)
            {
                Vector3 entrancePosition = path.GetPosition(EntranceDistance);
                stack.JumpToConveyor(entrancePosition, () => CompleteEntranceLanding(stack));
            }
            else
            {
                Vector3 queuedEntrancePosition = GetQueuedEntrancePosition(stack);
                stack.JumpToConveyor(queuedEntrancePosition, () =>
                {
                    StartCoroutine(Routine_LandQueuedStack(stack));
                });
            }

            return true;
        }

        private IEnumerator Routine_LandQueuedStack(StackItem stack)
        {
            while (stack != null && stack.gameObject.activeInHierarchy && !IsQueuedStackReadyToLand(stack))
            {
                yield return null;
            }

            if (stack == null || !stack.gameObject.activeInHierarchy) yield break;

            Vector3 entrancePosition = path.GetPosition(EntranceDistance);
            stack.MoveToConveyor(entrancePosition, queuedEntranceLandingDuration, () => CompleteEntranceLanding(stack));
        }

        private void CompleteEntranceLanding(StackItem stack)
        {
            CompletePendingJump(stack);

            if (stack != null && stack.gameObject.activeInHierarchy)
            {
                TryAddStack(stack);
            }

            // --- TUTORIAL STEP 2 TRIGGER: ITEM SUCCESSFULLY LANDED ON CONVEYOR IN LEVEL 1 ---
            if (LevelManager.Instance != null && LevelManager.Instance.CurrentLevelNumber == 1)
            {
                if (TutorialManager.Instance != null && TutorialManager.Instance.CurrentStep == TutorialManager.TutorialStep.TapFoodToConveyor)
                {
                    TutorialManager.Instance.EnterStepWaitInRack();
                }
            }
            // --------------------------------------------------------------------------------
        }

        public bool ShouldKeepLoopingOnBelt()
        {
            return StackItem.TotalActiveStackCount <= MaxCapacity;
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
            RegisterPendingJump(stack);

            // Auto-looping and player-tapped stacks share one FIFO entrance queue.
            // Waiting for the list to contain only this stack deadlocked the belt
            // whenever rapid taps queued stacks behind an auto-looping stack.
            while (!IsPendingStackReadyToEnter(stack))
            {
                if (stack == null || !stack.gameObject.activeInHierarchy)
                {
                    CompletePendingJump(stack);
                    ReleaseCapacity();
                    yield break;
                }

                yield return null;
            }

            if (stack == null || !stack.gameObject.activeInHierarchy)
            {
                CompletePendingJump(stack);
                ReleaseCapacity();
                yield break;
            }

            Vector3 entrancePosition = path.GetPosition(EntranceDistance);

            stack.JumpToConveyor(entrancePosition, () =>
            {
                CompletePendingJump(stack);

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
            pendingJumpStacks.Clear();
            occupiedCapacity = 0;
            pendingJumpsCount = 0;
            attemptCapacityBonus = 0;
            CapacityChanged?.Invoke(occupiedCapacity, MaxCapacity);
        }

        /// <summary>
        /// Removes a food type from both circulating and jump-in-progress conveyor states.
        /// Capacity is released for every reservation exactly once.
        /// </summary>
        public int RemoveStacksByData(ItemDataSO data)
        {
            if (data == null) return 0;

            int removedCount = 0;

            for (int i = activeStacks.Count - 1; i >= 0; i--)
            {
                StackItem stack = activeStacks[i];
                if (stack == null || stack.Data != data) continue;

                activeStacks.RemoveAt(i);
                ReleaseCapacity();
                DestroyStackForClearColor(stack);
                removedCount++;
            }

            for (int i = pendingJumpStacks.Count - 1; i >= 0; i--)
            {
                StackItem stack = pendingJumpStacks[i];
                if (stack == null || stack.Data != data) continue;

                pendingJumpStacks.RemoveAt(i);
                pendingJumpsCount = pendingJumpStacks.Count;
                ReleaseCapacity();
                DestroyStackForClearColor(stack);
                removedCount++;
            }

            return removedCount;
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

        private void RegisterPendingJump(StackItem stack)
        {
            if (stack != null && !pendingJumpStacks.Contains(stack)) pendingJumpStacks.Add(stack);
            pendingJumpsCount = pendingJumpStacks.Count;
        }

        private void CompletePendingJump(StackItem stack)
        {
            for (int i = pendingJumpStacks.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(pendingJumpStacks[i], stack))
                {
                    pendingJumpStacks.RemoveAt(i);
                    break;
                }
            }

            pendingJumpsCount = pendingJumpStacks.Count;
        }

        private Vector3 GetQueuedEntrancePosition(StackItem stack)
        {
            int queueIndex = Mathf.Max(0, pendingJumpStacks.IndexOf(stack));
            float height = queuedEntranceHoverHeight + (queueIndex * queuedEntranceHoverStackSpacing);
            return path.GetPosition(EntranceDistance) + (Vector3.up * height);
        }

        private bool IsQueuedStackReadyToLand(StackItem stack)
        {
            return IsPendingStackReadyToEnter(stack);
        }

        private bool IsPendingStackReadyToEnter(StackItem stack)
        {
            // Pending stacks enter FIFO, preventing overlapping landings while
            // allowing auto-looping stacks and player-tapped stacks to progress
            // through the same queue.
            if (pendingJumpStacks.Count == 0 || pendingJumpStacks[0] != stack) return false;

            float entranceDist = EntranceDistance;
            float pathLen = path != null ? path.Length : 0f;

            for (int i = 0; i < activeStacks.Count; i++)
            {
                StackItem activeStack = activeStacks[i];
                if (activeStack == null || activeStack.IsJumping) continue;

                float delta = Mathf.Abs(activeStack.CurrentDistance - entranceDist);
                if (pathLen > 0f) delta = Mathf.Min(delta, pathLen - delta);
                if (delta < entranceSafetyBuffer) return false;
            }

            return true;
        }

        private static void DestroyStackForClearColor(StackItem stack)
        {
            if (ReferenceEquals(stack, null)) return;

            StackItem.KillTweensInHierarchy(stack.gameObject);
            Destroy(stack.gameObject);
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
