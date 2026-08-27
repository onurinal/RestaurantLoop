using System;
using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Core
{
    public enum LevelState
    {
        Playing,
        Won,
        Lost
    }

    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Header("Level Sequence")]
        [SerializeField] private List<LevelDataSO> levelSequence = new List<LevelDataSO>();
        [SerializeField] private int currentLevelIndex = 0;

        public LevelDataSO CurrentLevel => levelSequence.Count > 0 ? levelSequence[currentLevelIndex % levelSequence.Count] : null;

        public LevelState CurrentState { get; private set; } = LevelState.Playing;
        public bool IsGameActive => CurrentState == LevelState.Playing;

        // Expose human-readable level number (1-based index) for UI
        public int CurrentLevelNumber => currentLevelIndex + 1;

        public event Action OnLevelWon;
        public event Action OnLevelLost;

        // Event to notify UI when a new level loads
        public event Action<int> OnLevelLoaded;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;

                // MVP Simplification: No save state. Always start at index 0 (Level 1) on fresh launch.
                // currentLevelIndex = 0;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            LoadCurrentLevel();
        }

        private void OnDestroy()
        {
            if (CrowdManager.Instance != null)
            {
                CrowdManager.Instance.OnDemandChanged -= HandleDemandChanged;
            }
        }

        public void LoadCurrentLevel()
        {
            CurrentState = LevelState.Playing;

            LevelDataSO data = CurrentLevel;
            if (data == null) return;

            // Clear leftover items in the rack from the previous level to ensure a clean state
            if (RackManager.Instance != null)
            {
                RackManager.Instance.ClearAllItems();
            }

            // Clear leftover items on the conveyor from the previous level
            if (ConveyorManager.Instance != null)
            {
                ConveyorManager.Instance.ClearAllItems();
            }

            if (CrowdManager.Instance != null)
            {
                CrowdManager.Instance.OnDemandChanged -= HandleDemandChanged;
                CrowdManager.Instance.OnDemandChanged += HandleDemandChanged;
                CrowdManager.Instance.SetupCrowd(data.customerDemands);
            }

            if (QueueManager.Instance != null)
            {
                QueueManager.Instance.SetupQueue(data);
            }

            // Notify UI to update the top level text
            OnLevelLoaded?.Invoke(CurrentLevelNumber);

            Debug.Log($"<color=cyan>[LEVEL START]</color> Loaded Level Index: {currentLevelIndex} (UI Level: {CurrentLevelNumber})");
        }

        /// <summary>
        /// Triggered when a stack on the conveyor reaches the exit while the Rack is completely full.
        /// </summary>
        public void ReportRackOverflow()
        {
            if (CurrentState != LevelState.Playing) return;

            CurrentState = LevelState.Lost;

            // Changed from LogError to LogWarning to prevent Error Pause in the editor and Build crashes
            Debug.LogWarning("<color=orange>[LEVEL FAILED]</color> Conveyor stack reached exit while Rack is full!");

            OnLevelLost?.Invoke();
        }

        private void HandleDemandChanged(int remainingDemand, Dictionary<ItemDataSO, int> demandPerType)
        {
            if (CurrentState != LevelState.Playing) return;

            if (remainingDemand <= 0)
            {
                TriggerWin();
            }
        }

        private void TriggerWin()
        {
            if (CurrentState != LevelState.Playing) return;

            CurrentState = LevelState.Won;
            Debug.Log("<color=green>[LEVEL COMPLETED]</color> All customer demands fulfilled!");
            OnLevelWon?.Invoke();
        }

        public void CompleteLevel()
        {
            currentLevelIndex++;

            // MVP Simplification: Removed PlayerPrefs save logic. Progression is session-only.
            LoadCurrentLevel();
        }
    }
}