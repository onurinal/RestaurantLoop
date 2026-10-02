using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using RestaurantLoop.Audio; 
using RestaurantLoop.Infrastructure;
using RestaurantLoop.UI; 

namespace RestaurantLoop.Core
{
    public enum LevelState
    {
        Playing,
        Won,
        Lost
    }

    public enum LevelFailureReason
    {
        TimedCustomerExpired,
        RackOverflow
    }

    public class LevelManager : MonoBehaviour
    {
        private const string HighestUnlockedLevelIndexPreferenceKey = "RestaurantLoop.HighestUnlockedLevelIndex";

        public static LevelManager Instance { get; private set; }

        [Header("Level Sequence")]
        [SerializeField] private List<LevelDataSO> levelSequence = new List<LevelDataSO>();
        [SerializeField] private int currentLevelIndex = 0;

        // Modulo (%) kaldırıldı. Liste sınırları kontrol ediliyor.
        public LevelDataSO CurrentLevel => (levelSequence.Count > 0 && currentLevelIndex < levelSequence.Count) 
            ? levelSequence[currentLevelIndex] 
            : null;

        // Son seviyede olunup olunmadığını kontrol eder
        public bool IsLastLevel => levelSequence.Count > 0 && currentLevelIndex >= levelSequence.Count - 1;

        public LevelState CurrentState { get; private set; } = LevelState.Playing;
        public bool IsGameActive => CurrentState == LevelState.Playing;
        public LevelFailureReason LastFailureReason { get; private set; } = LevelFailureReason.TimedCustomerExpired;

        /// <summary>Sequence index doubles as the player-facing level number; index 0 is the tutorial.</summary>
        public int CurrentLevelNumber => currentLevelIndex;
        public bool IsTutorialLevel => currentLevelIndex == 0;

        public event Action OnLevelWon;
        public event Action OnLevelLost;
        public event Action<int> OnLevelLoaded;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                RestoreHighestUnlockedLevel();
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

        private void OnApplicationPause(bool paused)
        {
            // An active attempt is deliberately never persisted. Reset it as soon as the
            // application backgrounds so returning always shows the authored level start.
            if (paused && CurrentLevel != null)
            {
                LoadCurrentLevel();
            }
        }

        public void LoadCurrentLevel()
        {
            CurrentState = LevelState.Playing;

            LevelDataSO data = CurrentLevel;
            if (data == null) return;

            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.HideTutorial();
            }

            if (ConveyorManager.Instance != null)
            {
                ConveyorManager.Instance.StopAllCoroutines();
            }

            DOTween.KillAll();

            StackItem.ClearAllActiveStacks();

            if (ConveyorManager.Instance != null)
            {
                ConveyorManager.Instance.ClearAllItems();
            }

            if (RackManager.Instance != null)
            {
                RackManager.Instance.ClearAllItems();
                RackManager.Instance.BuildRackLayout(data.rackSlotCount);
            }

            if (CrowdManager.Instance != null)
            {
                CrowdManager.Instance.OnDemandChanged -= HandleDemandChanged;
                CrowdManager.Instance.OnDemandChanged += HandleDemandChanged;
                CrowdManager.Instance.SetupCrowd(data);
            }

            if (QueueManager.Instance != null)
            {
                QueueManager.Instance.SetupQueue(data);
            }

            OnLevelLoaded?.Invoke(CurrentLevelNumber);

            Debug.Log($"<color=cyan>[LEVEL START]</color> Loaded Level Index: {currentLevelIndex} (UI Level: {CurrentLevelNumber})");

            if (IsTutorialLevel)
            {
                StartCoroutine(ShowStartTutorialRoutine());
            }
        }

        private IEnumerator ShowStartTutorialRoutine()
        {
            yield return new WaitForSeconds(0.6f);

            if (TutorialManager.Instance != null && CurrentState == LevelState.Playing)
            {
                StackItem targetFood = null;

                if (QueueManager.Instance != null)
                {
                    targetFood = QueueManager.Instance.GetFirstFrontRowStack();
                }

                if (targetFood == null)
                {
                    StackItem[] allFoods = FindObjectsByType<StackItem>(FindObjectsSortMode.None);
                    foreach (var food in allFoods)
                    {
                        if (food != null && food.gameObject.activeInHierarchy)
                        {
                            targetFood = food;
                            break;
                        }
                    }
                }

                if (targetFood != null)
                {
                    TutorialManager.Instance.StartTutorial(targetFood.transform);
                }
                else
                {
                    Debug.LogWarning("Tutorial: No valid front-row StackItem found to point at!");
                }
            }
        }

        public void ReportRackOverflow()
        {
            FailLevel("Conveyor stack reached exit while Rack is full!", LevelFailureReason.RackOverflow);
        }

        public void OnLevelFailed()
        {
            FailLevel("A timed customer ran out of patience!", LevelFailureReason.TimedCustomerExpired);
        }

        private void FailLevel(string reason, LevelFailureReason failureReason)
        {
            if (CurrentState != LevelState.Playing) return;

            CurrentState = LevelState.Lost;
            LastFailureReason = failureReason;
            VibrationManager.Instance?.PlayLoseVibration();

            if (AudioManager.Instance != null && AudioManager.Instance.levelLoseSound != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.levelLoseSound);
            }

            Debug.LogWarning($"<color=orange>[LEVEL FAILED]</color> {reason}");
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
            VibrationManager.Instance?.PlayWinVibration();
            SaveNextUnlockedLevel();
            
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.HideTutorial();
            }
            
            if (AudioManager.Instance != null && AudioManager.Instance.levelWinSound != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.levelWinSound);
            }
            
            Debug.Log("<color=green>[LEVEL COMPLETED]</color> All customer demands fulfilled!");
            OnLevelWon?.Invoke();
        }

        public void CompleteLevel()
        {
            // Son seviyedeysek index artırma ve yeni level yükleme
            if (IsLastLevel)
            {
                Debug.Log("<color=yellow>[LEVEL MANAGER]</color> Reached the final level. No more levels to load.");
                return;
            }

            currentLevelIndex++;
            LoadCurrentLevel();
        }

        private void RestoreHighestUnlockedLevel()
        {
            if (levelSequence == null || levelSequence.Count == 0) return;

            int savedIndex = PlayerPrefs.GetInt(HighestUnlockedLevelIndexPreferenceKey, currentLevelIndex);
            currentLevelIndex = Mathf.Clamp(Mathf.Max(currentLevelIndex, savedIndex), 0, levelSequence.Count - 1);
        }

        private void SaveNextUnlockedLevel()
        {
            if (levelSequence == null || levelSequence.Count == 0) return;

            int nextUnlockedIndex = Mathf.Min(currentLevelIndex + 1, levelSequence.Count - 1);
            int highestUnlockedIndex = PlayerPrefs.GetInt(HighestUnlockedLevelIndexPreferenceKey, 0);
            if (nextUnlockedIndex <= highestUnlockedIndex) return;

            PlayerPrefs.SetInt(HighestUnlockedLevelIndexPreferenceKey, nextUnlockedIndex);
            PlayerPrefs.Save();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (levelSequence != null && levelSequence.Count > 0)
            {
                currentLevelIndex = Mathf.Clamp(currentLevelIndex, 0, levelSequence.Count - 1);
            }

            UnityEditor.SceneView.RepaintAll();
        }
#endif
    }
}
