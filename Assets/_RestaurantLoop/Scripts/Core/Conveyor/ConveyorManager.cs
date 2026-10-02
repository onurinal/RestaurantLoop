using System;
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
        private readonly List<StackItem> pendingPlayerEntries = new List<StackItem>();
        private readonly List<StackItem> returningEntryStacks = new List<StackItem>();
        private readonly HashSet<StackItem> hoveringPlayerEntries = new HashSet<StackItem>();
        private StackItem inboundEntryStack;
        private bool inboundEntryIsReturn;
        private Action inboundEntryCompletedCallback;
        private int occupiedCapacity = 0;
        private int attemptCapacityBonus;

        public bool CanAcceptStack => occupiedCapacity < MaxCapacity;
        public bool IsClockwise => isClockwise;
        public float MoveSpeed => moveSpeed;
        public SplineConveyorPath Path => path;
        public int OccupiedCapacity => occupiedCapacity;
        public int AuthoredMaxCapacity => maxCapacity;
        public int MaxCapacity => maxCapacity + attemptCapacityBonus;

        public event Action<int, int> CapacityChanged;
        public event Action CapacityIncreased;
        public event Action CapacityRejected;
        public event Action<StackItem> StackEnteredBelt;

        public float EntranceDistance => entranceRatio * (path != null ? path.Length : 0f);
        public float ExitDistance => exitRatio * (path != null ? path.Length : 0f);

        private void Awake()
        {
            inboundEntryCompletedCallback = HandleInboundEntryCompleted;

            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (path != null) path.Initialize();
        }

        private void Update()
        {
            // Pause conveyor movement if game is inactive or level state is Won/Lost
            if (LevelManager.Instance != null && !LevelManager.Instance.IsGameActive) return;

            float deltaTime = Time.deltaTime;
            for (int i = activeStacks.Count - 1; i >= 0; i--)
            {
                if (activeStacks[i] != null)
                {
                    activeStacks[i].MoveAlongBelt(path, moveSpeed, isClockwise, deltaTime);
                }
            }
        }

        private void LateUpdate()
        {
            if (LevelManager.Instance != null && !LevelManager.Instance.IsGameActive) return;
            AdvanceEntranceScheduler();
        }

        public bool IsEntranceClear()
        {
            return inboundEntryStack == null && IsEntrancePhysicallyClear();
        }

        private bool IsEntrancePhysicallyClear()
        {
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
        /// Announces a player-facing rejection only while the conveyor is at capacity.
        /// Gameplay callers use this after deciding that a tap was rejected for capacity.
        /// </summary>
        public void NotifyCapacityRejected()
        {
            if (!CanAcceptStack)
            {
                CapacityRejected?.Invoke();
            }
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
            CapacityIncreased?.Invoke();
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
            activeStacks.Remove(stack);
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

            if (!CanAcceptStack)
            {
                NotifyCapacityRejected();
                return false;
            }

            if (!TryReserveSlot())
            {
                return false;
            }

            stack.transform.DOKill();
            stack.transform.SetParent(null, true);
            pendingPlayerEntries.Add(stack);

            // Only player stacks wait. Returning conveyor stacks are scheduled
            // first in LateUpdate, after all movement and input for the frame.
            if (pendingPlayerEntries.Count > 1 || inboundEntryStack != null ||
                returningEntryStacks.Count > 0 || !IsEntrancePhysicallyClear())
            {
                StartPlayerHover(stack);
            }

            return true;
        }

        public bool ShouldKeepLoopingOnBelt()
        {
            if (LevelManager.Instance != null && LevelManager.Instance.IsTutorialLevel)
            {
                return false;
            }

            return StackItem.TotalActiveStackCount <= MaxCapacity;
        }

        public void OnStackCompletedBeltLoop(StackItem stack)
        {
            if (stack == null || stack.RemainingItemCount <= 0) return;

            if (ShouldKeepLoopingOnBelt())
            {
                RemoveStackFromBelt(stack);
                returningEntryStacks.Add(stack);

                // Input normally runs before the conveyor update. If a new
                // stack began a direct entry this frame, it yields to the
                // returning belt stack before either can occupy the entrance.
                RerouteInboundPlayerToHover();
                return;
            }

            RemoveStackFromBelt(stack);
            bool addedToRack = RackManager.Instance != null && RackManager.Instance.TryAddStackToRack(stack);

            if (!addedToRack)
            {
                // Return overflow to the stack pool immediately to prevent ghost objects floating in scene.
                StackItem.ReleaseToPool(stack);

                LevelManager.Instance?.ReportRackOverflow();
            }
        }

        public void ClearAllItems()
        {
            StopAllCoroutines();

            for (int i = activeStacks.Count - 1; i >= 0; i--)
            {
                if (activeStacks[i] != null)
                {
                    StackItem.ReleaseToPool(activeStacks[i]);
                }
            }

            activeStacks.Clear();
            pendingPlayerEntries.Clear();
            returningEntryStacks.Clear();
            hoveringPlayerEntries.Clear();
            inboundEntryStack = null;
            occupiedCapacity = 0;
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

            for (int i = pendingPlayerEntries.Count - 1; i >= 0; i--)
            {
                StackItem stack = pendingPlayerEntries[i];
                if (stack == null || stack.Data != data) continue;

                pendingPlayerEntries.RemoveAt(i);
                returningEntryStacks.Remove(stack);
                hoveringPlayerEntries.Remove(stack);
                if (inboundEntryStack == stack) inboundEntryStack = null;
                ReleaseCapacity();
                DestroyStackForClearColor(stack);
                removedCount++;
            }

            for (int i = returningEntryStacks.Count - 1; i >= 0; i--)
            {
                StackItem stack = returningEntryStacks[i];
                if (stack == null || stack.Data != data) continue;

                returningEntryStacks.RemoveAt(i);
                if (inboundEntryStack == stack) inboundEntryStack = null;
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

        private Vector3 GetQueuedEntrancePosition(StackItem stack)
        {
            int queueIndex = Mathf.Max(0, pendingPlayerEntries.IndexOf(stack));
            float height = queuedEntranceHoverHeight + (queueIndex * queuedEntranceHoverStackSpacing);
            return path.GetPosition(EntranceDistance) + (Vector3.up * height);
        }

        private void AdvanceEntranceScheduler()
        {
            RemoveInvalidEntryRequests();
            if (inboundEntryStack != null) return;

            // Conveyor stacks retain priority because they are continuing an
            // existing belt reservation, not asking to join the belt anew.
            if (returningEntryStacks.Count > 0)
            {
                StartWaitingPlayerVisuals();
                if (IsEntrancePhysicallyClear()) StartReturningEntry(returningEntryStacks[0]);
                return;
            }

            if (pendingPlayerEntries.Count == 0) return;

            StackItem playerStack = pendingPlayerEntries[0];
            if (IsEntrancePhysicallyClear())
            {
                StartPlayerEntry(playerStack);
            }
            else if (!hoveringPlayerEntries.Contains(playerStack))
            {
                StartPlayerHover(playerStack);
            }
        }

        private void StartPlayerHover(StackItem stack)
        {
            if (stack == null || !stack.gameObject.activeInHierarchy || hoveringPlayerEntries.Contains(stack)) return;

            hoveringPlayerEntries.Add(stack);
            stack.JumpToConveyor(GetQueuedEntrancePosition(stack), null);
        }

        private void StartWaitingPlayerVisuals()
        {
            for (int i = 0; i < pendingPlayerEntries.Count; i++)
            {
                StartPlayerHover(pendingPlayerEntries[i]);
            }
        }

        private void StartPlayerEntry(StackItem stack)
        {
            if (stack == null || !stack.gameObject.activeInHierarchy) return;

            inboundEntryStack = stack;
            inboundEntryIsReturn = false;

            Vector3 entrancePosition = path.GetPosition(EntranceDistance);
            if (hoveringPlayerEntries.Remove(stack))
            {
                stack.MoveToConveyor(entrancePosition, queuedEntranceLandingDuration, inboundEntryCompletedCallback);
            }
            else
            {
                stack.JumpToConveyor(entrancePosition, inboundEntryCompletedCallback);
            }
        }

        private void StartReturningEntry(StackItem stack)
        {
            if (stack == null || !stack.gameObject.activeInHierarchy) return;

            inboundEntryStack = stack;
            inboundEntryIsReturn = true;
            Vector3 entrancePosition = path.GetPosition(EntranceDistance);
            stack.JumpToConveyor(entrancePosition, inboundEntryCompletedCallback);
        }

        private void HandleInboundEntryCompleted()
        {
            StackItem completedStack = inboundEntryStack;
            if (inboundEntryIsReturn)
            {
                CompleteReturningEntry(completedStack);
            }
            else
            {
                CompletePlayerEntry(completedStack);
            }
        }

        private void CompletePlayerEntry(StackItem stack)
        {
            if (inboundEntryStack == stack) inboundEntryStack = null;
            inboundEntryIsReturn = false;
            pendingPlayerEntries.Remove(stack);
            hoveringPlayerEntries.Remove(stack);

            if (stack != null && stack.gameObject.activeInHierarchy)
            {
                TryAddStack(stack);
            }
            else
            {
                ReleaseCapacity();
            }

            // --- TUTORIAL STEP 2 TRIGGER: ITEM SUCCESSFULLY LANDED ON CONVEYOR ---
            if (LevelManager.Instance != null && LevelManager.Instance.IsTutorialLevel &&
                TutorialManager.Instance != null &&
                TutorialManager.Instance.CurrentStep == TutorialManager.TutorialStep.TapFoodToConveyor)
            {
                TutorialManager.Instance.EnterStepWaitInRack();
            }
            // --------------------------------------------------------------------------------
        }

        private void CompleteReturningEntry(StackItem stack)
        {
            if (inboundEntryStack == stack) inboundEntryStack = null;
            inboundEntryIsReturn = false;
            returningEntryStacks.Remove(stack);

            if (stack != null && stack.gameObject.activeInHierarchy)
            {
                TryAddStack(stack);
            }
            else
            {
                ReleaseCapacity();
            }
        }

        private void RerouteInboundPlayerToHover()
        {
            if (inboundEntryStack == null || inboundEntryIsReturn) return;

            StackItem playerStack = inboundEntryStack;
            inboundEntryStack = null;
            StackItem.KillTweensInHierarchy(playerStack.gameObject);
            StartPlayerHover(playerStack);
        }

        private void RemoveInvalidEntryRequests()
        {
            for (int i = pendingPlayerEntries.Count - 1; i >= 0; i--)
            {
                StackItem stack = pendingPlayerEntries[i];
                if (stack != null && stack.gameObject.activeInHierarchy) continue;

                pendingPlayerEntries.RemoveAt(i);
                hoveringPlayerEntries.Remove(stack);
                ReleaseCapacity();
            }

            for (int i = returningEntryStacks.Count - 1; i >= 0; i--)
            {
                StackItem stack = returningEntryStacks[i];
                if (stack != null && stack.gameObject.activeInHierarchy) continue;

                returningEntryStacks.RemoveAt(i);
                ReleaseCapacity();
            }
        }

        private static void DestroyStackForClearColor(StackItem stack)
        {
            if (ReferenceEquals(stack, null)) return;

            StackItem.ReleaseToPool(stack);
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