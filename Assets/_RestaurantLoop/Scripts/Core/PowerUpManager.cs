using System;
using System.Collections;
using UnityEngine;

namespace RestaurantLoop.Core
{
    public enum PowerUpType
    {
        AddStack,
        Shuffle,
        Hand,
        ClearColor
    }

    /// <summary>
    /// Owns the temporary power-up state for one level attempt. Nothing here is
    /// persisted: every level load starts with one use of each power-up.
    /// </summary>
    public sealed class PowerUpManager : MonoBehaviour
    {
        public static PowerUpManager Instance { get; private set; }

        [SerializeField]
        private const int UsesPerAttempt = 99;

        private int addStackUses;
        private int shuffleUses;
        private int handUses;
        private int clearColorUses;
        private float timeScaleBeforeHandSelection = 1f;
        private float timeScaleBeforeClearColor = 1f;
        private Coroutine clearColorResolutionRoutine;

        [SerializeField, Min(0f)] private float clearColorResolutionDuration = 1.3f;

        public bool IsHandSelectionActive { get; private set; }
        public bool IsClearColorSelectionActive { get; private set; }
        public bool IsClearColorResolving { get; private set; }

        public event Action StateChanged;
        public event Action<bool> HandSelectionChanged;
        public event Action<bool> ClearColorSelectionChanged;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            ResetForNewAttempt();
        }

        private void Start()
        {
            SubscribeToLevelEvents();
            ResetForNewAttempt();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            UnsubscribeFromLevelEvents();
            EndHandSelection();
            EndClearColorResolution();
        }

        public int GetRemainingUses(PowerUpType powerUp)
        {
            switch (powerUp)
            {
                case PowerUpType.AddStack: return addStackUses;
                case PowerUpType.Shuffle: return shuffleUses;
                case PowerUpType.Hand: return handUses;
                case PowerUpType.ClearColor: return clearColorUses;
                default: return 0;
            }
        }

        public bool CanUseAddStack => IsAttemptPlaying && !IsInteractionLocked && addStackUses > 0 && ConveyorManager.Instance != null;

        public bool CanUseShuffle => IsAttemptPlaying && !IsInteractionLocked && shuffleUses > 0 &&
            QueueManager.Instance != null && QueueManager.Instance.CanShuffleQueuedStacks;

        public bool CanBeginHandSelection => IsAttemptPlaying && !IsInteractionLocked && handUses > 0 &&
            ConveyorManager.Instance != null && ConveyorManager.Instance.CanAcceptStack &&
            ConveyorManager.Instance.IsEntranceClear() && QueueManager.Instance != null &&
            QueueManager.Instance.HasSelectableDeeperStack;

        public bool CanBeginClearColorSelection => IsAttemptPlaying && !IsInteractionLocked && clearColorUses > 0 &&
            CrowdManager.Instance != null && !CrowdManager.Instance.IsSpawningCustomers &&
            ((QueueManager.Instance != null && QueueManager.Instance.HasClearColorSelectableStack) ||
             (RackManager.Instance != null && RackManager.Instance.HasClearColorSelectableStack));

        public bool TryUseAddStack()
        {
            if (!CanUseAddStack || !ConveyorManager.Instance.TryAddAttemptCapacity()) return false;

            addStackUses--;
            StateChanged?.Invoke();
            return true;
        }

        public bool TryUseShuffle()
        {
            if (!CanUseShuffle || !QueueManager.Instance.TryShuffleQueuedStacks()) return false;

            shuffleUses--;
            StateChanged?.Invoke();
            return true;
        }

        public bool BeginHandSelection()
        {
            if (!CanBeginHandSelection) return false;

            IsHandSelectionActive = true;
            timeScaleBeforeHandSelection = Time.timeScale;
            Time.timeScale = 0f;
            QueueManager.Instance.SetHandSelectionVisuals(true);
            HandSelectionChanged?.Invoke(true);
            StateChanged?.Invoke();
            return true;
        }

        public bool TrySelectHandStack(StackItem stack)
        {
            if (!IsHandSelectionActive || QueueManager.Instance == null) return false;

            if (!QueueManager.Instance.TrySendHandSelectedStack(stack)) return false;

            handUses--;
            EndHandSelection();
            StateChanged?.Invoke();
            return true;
        }

        public void CancelHandSelection()
        {
            if (!IsHandSelectionActive) return;
            EndHandSelection();
            StateChanged?.Invoke();
        }

        public bool BeginClearColorSelection()
        {
            if (!CanBeginClearColorSelection) return false;

            IsClearColorSelectionActive = true;
            timeScaleBeforeClearColor = Time.timeScale;
            Time.timeScale = 0f;
            QueueManager.Instance?.SetClearColorSelectionVisuals(true);
            RackManager.Instance?.SetClearColorSelectionVisuals(true);
            ClearColorSelectionChanged?.Invoke(true);
            StateChanged?.Invoke();
            return true;
        }

        public bool TrySelectClearColorStack(StackItem stack)
        {
            if (!IsClearColorSelectionActive || stack == null || !IsClearColorTarget(stack)) return false;

            ItemDataSO selectedData = stack.Data;
            if (selectedData == null || CrowdManager.Instance == null || !CrowdManager.Instance.HasRemainingDemand(selectedData)) return false;

            clearColorUses--;
            EndClearColorSelection(restoreTimeScale: false);
            IsClearColorResolving = true;
            clearColorResolutionRoutine = StartCoroutine(ResolveClearColorRoutine(selectedData, stack));
            StateChanged?.Invoke();
            return true;
        }

        public void CancelClearColorSelection()
        {
            if (!IsClearColorSelectionActive) return;

            EndClearColorSelection(restoreTimeScale: true);
            StateChanged?.Invoke();
        }

        public void ResetForNewAttempt()
        {
            EndHandSelection();
            EndClearColorResolution();
            addStackUses = UsesPerAttempt;
            shuffleUses = UsesPerAttempt;
            handUses = UsesPerAttempt;
            clearColorUses = UsesPerAttempt;
            StateChanged?.Invoke();
        }

        private bool IsAttemptPlaying => LevelManager.Instance == null || LevelManager.Instance.IsGameActive;
        private bool IsInteractionLocked => IsHandSelectionActive || IsClearColorSelectionActive || IsClearColorResolving;

        private void EndHandSelection()
        {
            if (!IsHandSelectionActive) return;

            QueueManager.Instance?.SetHandSelectionVisuals(false);
            Time.timeScale = timeScaleBeforeHandSelection;
            IsHandSelectionActive = false;
            HandSelectionChanged?.Invoke(false);
        }

        private bool IsClearColorTarget(StackItem stack)
        {
            return (QueueManager.Instance != null && QueueManager.Instance.IsClearColorSelectableStack(stack)) ||
                   (RackManager.Instance != null && RackManager.Instance.IsClearColorSelectableStack(stack));
        }

        private IEnumerator ResolveClearColorRoutine(ItemDataSO selectedData, StackItem visualSource)
        {
            bool resolved = CrowdManager.Instance != null && CrowdManager.Instance.ResolveFoodTypeForClearColor(selectedData, visualSource);

            if (resolved)
            {
                QueueManager.Instance?.RemoveStacksByData(selectedData);
                RackManager.Instance?.RemoveStacksByData(selectedData);
                ConveyorManager.Instance?.RemoveStacksByData(selectedData);
                yield return new WaitForSecondsRealtime(clearColorResolutionDuration);
                CrowdManager.Instance?.CompleteClearColorResolution();
            }

            FinishClearColorResolution();
        }

        private void EndClearColorSelection(bool restoreTimeScale)
        {
            if (!IsClearColorSelectionActive) return;

            QueueManager.Instance?.SetClearColorSelectionVisuals(false);
            RackManager.Instance?.SetClearColorSelectionVisuals(false);
            IsClearColorSelectionActive = false;
            if (restoreTimeScale) Time.timeScale = timeScaleBeforeClearColor;
            ClearColorSelectionChanged?.Invoke(false);
        }

        private void FinishClearColorResolution()
        {
            if (!IsClearColorResolving) return;

            IsClearColorResolving = false;
            clearColorResolutionRoutine = null;
            Time.timeScale = timeScaleBeforeClearColor;
            StateChanged?.Invoke();
        }

        private void EndClearColorResolution()
        {
            if (clearColorResolutionRoutine != null) StopCoroutine(clearColorResolutionRoutine);
            clearColorResolutionRoutine = null;

            if (IsClearColorSelectionActive)
            {
                EndClearColorSelection(restoreTimeScale: true);
            }
            else if (IsClearColorResolving)
            {
                IsClearColorResolving = false;
                Time.timeScale = timeScaleBeforeClearColor;
            }
        }

        private void SubscribeToLevelEvents()
        {
            if (LevelManager.Instance == null) return;

            LevelManager.Instance.OnLevelLoaded += HandleLevelLoaded;
            LevelManager.Instance.OnLevelWon += EndHandSelection;
            LevelManager.Instance.OnLevelLost += EndHandSelection;
        }

        private void UnsubscribeFromLevelEvents()
        {
            if (LevelManager.Instance == null) return;

            LevelManager.Instance.OnLevelLoaded -= HandleLevelLoaded;
            LevelManager.Instance.OnLevelWon -= EndHandSelection;
            LevelManager.Instance.OnLevelLost -= EndHandSelection;
        }

        private void HandleLevelLoaded(int _) => ResetForNewAttempt();
    }
}
