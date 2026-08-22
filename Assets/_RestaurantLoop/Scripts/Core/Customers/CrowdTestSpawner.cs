using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RestaurantLoop.Core
{
    [System.Serializable]
    public class StationConfig
    {
        public ItemDataSO itemData;
        public int remainingCount = 15;
    }

    /// <summary>
    /// Configures level stations data and triggers CrowdManager dynamic layout initialization.
    /// </summary>
    public class CrowdTestSpawner : MonoBehaviour
    {
        [Header("Level Station Configurations")]
        [SerializeField] private List<StationConfig> stationConfigs = new List<StationConfig>();

        public List<StationConfig> StationConfigs => stationConfigs;

        private void Start()
        {
            InitializeStations();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            SceneView.RepaintAll();
#endif
        }

        public void InitializeStations()
        {
            if (CrowdManager.Instance != null && stationConfigs != null)
            {
                CrowdManager.Instance.SetupStations(stationConfigs);
            }
        }
    }
}