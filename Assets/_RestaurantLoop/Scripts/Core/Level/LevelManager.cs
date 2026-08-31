using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using RestaurantLoop.Audio; 
using RestaurantLoop.UI; 

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

        public int CurrentLevelNumber => currentLevelIndex + 1;

        public event Action OnLevelWon;
        public event Action OnLevelLost;
        public event Action<int> OnLevelLoaded;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
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

            // Hide any active tutorial when loading a new level or restarting
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.HideTutorial();
            }

            // 1. Stop all active conveyor coroutines and kill all running DOTween animations in the scene
            if (ConveyorManager.Instance != null)
            {
                ConveyorManager.Instance.StopAllCoroutines();
            }

            DOTween.KillAll();

            // 2. Clear all active stacks across belt, rack, queue, and mid-air jumps
            StackItem.ClearAllActiveStacks();

            // 3. Reset internal capacity counters for conveyor and rack
            if (ConveyorManager.Instance != null)
            {
                ConveyorManager.Instance.ClearAllItems();
            }

            if (RackManager.Instance != null)
            {
                RackManager.Instance.ClearAllItems();
                RackManager.Instance.BuildRackLayout(data.rackSlotCount);
            }

            // 4. Rebuild customer crowd and queue layouts
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

            // --- TUTORIAL SEQUENCE 1: TRIGGER ON LEVEL 1 ---
            if (CurrentLevelNumber == 1)
            {
                StartCoroutine(ShowStartTutorialRoutine());
            }
        }

        private IEnumerator ShowStartTutorialRoutine()
        {
            // Wait briefly to ensure the queue and grid are fully instantiated before finding an item
            yield return new WaitForSeconds(0.6f);

            if (TutorialManager.Instance != null && CurrentState == LevelState.Playing)
            {
                StackItem targetFood = null;

                // Safely fetch strictly the front-row stack from QueueManager
                if (QueueManager.Instance != null)
                {
                    targetFood = QueueManager.Instance.GetFirstFrontRowStack();
                }

                // Fallback mechanism if queue is empty
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
                    // NOTE: Was ShowTutorialAtWorldPosition(...) before — that call never updates
                    // TutorialManager.CurrentStep, so ConveyorManager's step-1->step-2 check
                    // (CurrentStep == TapFoodToConveyor) never passed. StartLevel1Tutorial sends
                    // the exact same message but also correctly sets CurrentStep.
                    TutorialManager.Instance.StartLevel1Tutorial(targetFood.transform);
                }
                else
                {
                    Debug.LogWarning("Tutorial: No valid front-row StackItem found to point at!");
                }
            }
        }

        public void ReportRackOverflow()
        {
            FailLevel("Conveyor stack reached exit while Rack is full!");
        }

        /// <summary>Immediately fails the active level when a timed customer expires.</summary>
        public void OnLevelFailed()
        {
            FailLevel("A timed customer ran out of patience!");
        }

        private void FailLevel(string reason)
        {
            if (CurrentState != LevelState.Playing) return;

            CurrentState = LevelState.Lost;

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
            
            // Hide tutorial just in case it's still active when winning
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.HideTutorial();
            }
            
            // --- PLAY WIN SOUND ---
            if (AudioManager.Instance != null && AudioManager.Instance.levelWinSound != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.levelWinSound);
            }
            // ----------------------
            
            Debug.Log("<color=green>[LEVEL COMPLETED]</color> All customer demands fulfilled!");
            OnLevelWon?.Invoke();
        }

        public void CompleteLevel()
        {
            currentLevelIndex++;
            LoadCurrentLevel();
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
