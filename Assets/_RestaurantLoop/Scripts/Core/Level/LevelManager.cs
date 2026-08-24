using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Core
{
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

            // Notice: MaxVisibleCrowdCount is now managed internally by CrowdManager
            if (CrowdManager.Instance != null)
            {
                CrowdManager.Instance.SetupCrowd(data.customerDemands);
            }

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