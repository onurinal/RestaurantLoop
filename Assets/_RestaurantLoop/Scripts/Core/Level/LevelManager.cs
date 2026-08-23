using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Main entry singleton that coordinates level setup across domain managers from loaded LevelDataSO assets.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Header("Level Sequence")]
        [SerializeField] private List<LevelDataSO> levelSequence = new List<LevelDataSO>();
        [SerializeField] private int currentLevelIndex = 0;

        public LevelDataSO CurrentLevel => levelSequence.Count > 0 ? levelSequence[currentLevelIndex % levelSequence.Count] : null;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            LoadCurrentLevel();
        }

        public void LoadCurrentLevel()
        {
            LevelDataSO data = CurrentLevel;
            if (data == null) return;

            // 1. CrowdManager Setup
            if (CrowdManager.Instance != null)
            {
                List<StationConfig> crowdConfigs = new List<StationConfig>();
                foreach (var cfg in data.stationConfigs)
                {
                    crowdConfigs.Add(new StationConfig
                    {
                        itemData = cfg.itemData,
                        remainingCount = cfg.totalCustomerCount
                    });
                }

                CrowdManager.Instance.SetupStations(crowdConfigs);
            }

            // 2. QueueManager Setup
            if (QueueManager.Instance != null)
            {
                QueueManager.Instance.SetupQueue(data);
            }
        }

        public void CompleteLevel()
        {
            currentLevelIndex++;
            LoadCurrentLevel();
        }
    }
}