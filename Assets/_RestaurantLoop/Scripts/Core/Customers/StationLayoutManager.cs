using System.Collections.Generic;
using UnityEngine;
using RestaurantLoop.Infrastructure;

namespace RestaurantLoop.Core
{
    public class StationLayoutManager
    {
        private readonly CustomerStationView stationPrefab;
        private readonly int maxStationsPerRow;
        private readonly float stationSpacingX;
        private readonly float stationSpacingZ;

        private readonly List<CustomerStation> stations = new List<CustomerStation>();

        public IReadOnlyList<CustomerStation> Stations => stations;

        public StationLayoutManager(CustomerStationView stationPrefab, int maxStationsPerRow, float stationSpacingX, float stationSpacingZ)
        {
            this.stationPrefab = stationPrefab;
            this.maxStationsPerRow = Mathf.Max(1, maxStationsPerRow);
            this.stationSpacingX = stationSpacingX;
            this.stationSpacingZ = stationSpacingZ;
        }

        public void Setup(List<StationConfig> configs, Transform parent, Vector3 center)
        {
            Clear();

            if (configs == null || configs.Count == 0 || stationPrefab == null) return;

            List<Vector3> layoutPositions = CalculatePositions(configs.Count, center);

            for (int i = 0; i < configs.Count; i++)
            {
                StationConfig cfg = configs[i];
                Vector3 spawnPos = layoutPositions[i];

                GameObject viewObj = PoolManager.Instance.Spawn(stationPrefab.gameObject, spawnPos, Quaternion.identity, parent);
                CustomerStationView viewInstance = viewObj.GetComponent<CustomerStationView>();
                
                viewInstance.Initialize(cfg.itemData, 0);

                stations.Add(new CustomerStation
                {
                    itemData = cfg.itemData,
                    remainingCount = 0,
                    position = spawnPos,
                    view = viewInstance
                });
            }
        }

        public void Clear()
        {
            foreach (var station in stations)
            {
                if (station.view != null) PoolManager.Instance.Despawn(station.view.gameObject);
            }

            stations.Clear();
        }

        public List<Vector3> CalculatePositions(int count, Vector3 center)
        {
            List<Vector3> positions = new List<Vector3>();
            if (count <= 0) return positions;

            int cols = Mathf.Min(count, maxStationsPerRow);
            int rows = Mathf.CeilToInt((float)count / cols);

            for (int i = 0; i < count; i++)
            {
                int r = i / cols;
                int c = i % cols;

                int colsInCurrentRow = (r == rows - 1 && count % cols != 0) ? (count % cols) : cols;

                float x = (c - (colsInCurrentRow - 1) * 0.5f) * stationSpacingX;
                float z = ((rows - 1) * 0.5f - r) * stationSpacingZ;

                positions.Add(center + new Vector3(x, 0f, z));
            }

            return positions;
        }

        public CustomerStation FindStationFor(ItemDataSO itemData) => stations.Find(s => s.itemData == itemData);

        public bool TryConsumeAvailableStation(out CustomerStation station)
        {
            station = GetRandomAvailableStation();
            if (station == null) return false;

            station.remainingCount--;
            station.UpdateUI();
            return true;
        }

        private CustomerStation GetRandomAvailableStation()
        {
            int validCount = 0;
            for (int i = 0; i < stations.Count; i++)
            {
                if (stations[i].remainingCount > 0) validCount++;
            }

            if (validCount == 0) return null;

            int randomIndex = Random.Range(0, validCount);
            int current = 0;

            for (int i = 0; i < stations.Count; i++)
            {
                if (stations[i].remainingCount > 0)
                {
                    if (current == randomIndex) return stations[i];
                    current++;
                }
            }

            return null;
        }
    }
}