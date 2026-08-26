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

        public event Action OnLevelWon;
        public event Action OnLevelLost;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                
                // Read the saved level from device storage (defaults to 1).
                // Subtract 1 to map the human-readable level to the 0-based list index.
                currentLevelIndex = PlayerPrefs.GetInt("CurrentLevel", 1) - 1;
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

            Debug.Log($"<color=cyan>[LEVEL START]</color> Loaded Level Index: {currentLevelIndex} (UI Level: {currentLevelIndex + 1})");
        }

        /// <summary>
        /// Triggered when a stack on the conveyor reaches the exit while the Rack is completely full.
        /// </summary>
        public void ReportRackOverflow()
        {
            if (CurrentState != LevelState.Playing) return;

            CurrentState = LevelState.Lost;
            Debug.LogError("<color=red>[LEVEL FAILED]</color> Conveyor stack reached exit while Rack is full!");
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
            
            // Save the new level to PlayerPrefs, adding 1 for the human-readable UI format.
            PlayerPrefs.SetInt("CurrentLevel", currentLevelIndex + 1);
            PlayerPrefs.Save();
            
            LoadCurrentLevel();
        }
    }
}