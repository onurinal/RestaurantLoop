using System;
using UnityEngine;

namespace RestaurantLoop.Core
{
    public enum PowerUpType
    {
        AddStack,
        Shuffle,
        Hand
    }

    /// <summary>
    /// Owns the temporary power-up state for one level attempt. Nothing here is
    /// persisted: every level load starts with one use of each power-up.
    /// </summary>
    public sealed class PowerUpManager : MonoBehaviour
    {
        public static PowerUpManager Instance { get; private set; }

        private const int UsesPerAttempt = 1;

        private int addStackUses;
        private int shuffleUses;
        private int handUses;
        private float timeScaleBeforeHandSelection = 1f;

        public bool IsHandSelectionActive { get; private set; }

        public event Action StateChanged;
        public event Action<bool> HandSelectionChanged;

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
        }

        public int GetRemainingUses(PowerUpType powerUp)
        {
            switch (powerUp)
            {
                case PowerUpType.AddStack: return addStackUses;
                case PowerUpType.Shuffle: return shuffleUses;
                case PowerUpType.Hand: return handUses;
                default: return 0;
            }
        }

        public bool CanUseAddStack => IsAttemptPlaying && addStackUses > 0 && ConveyorManager.Instance != null;

        public bool CanUseShuffle => IsAttemptPlaying && shuffleUses > 0 &&
            QueueManager.Instance != null && QueueManager.Instance.CanShuffleQueuedStacks;

        public bool CanBeginHandSelection => IsAttemptPlaying && !IsHandSelectionActive && handUses > 0 &&
            ConveyorManager.Instance != null && ConveyorManager.Instance.CanAcceptStack &&
            ConveyorManager.Instance.IsEntranceClear() && QueueManager.Instance != null &&
            QueueManager.Instance.HasSelectableDeeperStack;

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

        public void ResetForNewAttempt()
        {
            EndHandSelection();
            addStackUses = UsesPerAttempt;
            shuffleUses = UsesPerAttempt;
            handUses = UsesPerAttempt;
            StateChanged?.Invoke();
        }

        private bool IsAttemptPlaying => LevelManager.Instance == null || LevelManager.Instance.IsGameActive;

        private void EndHandSelection()
        {
            if (!IsHandSelectionActive) return;

            QueueManager.Instance?.SetHandSelectionVisuals(false);
            Time.timeScale = timeScaleBeforeHandSelection;
            IsHandSelectionActive = false;
            HandSelectionChanged?.Invoke(false);
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
