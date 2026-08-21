using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Core
{
    [System.Serializable]
    public class CustomerSpawnConfig
    {
        public bool isEnabled = true;
        public int row;
        public int column;
        public ItemDataSO itemData;
    }

    /// <summary>
    /// Configurable spawner allowing custom level layout selection and random item assignment on rebuild or runtime start.
    /// </summary>
    public class CrowdTestSpawner : MonoBehaviour
    {
        [SerializeField] private Customer customerPrefab;
        [SerializeField] private bool assignRandomItemsOnRebuild = true;
        [SerializeField] private ItemDataSO[] availableItems;
        [SerializeField] private List<CustomerSpawnConfig> spawnConfigs = new List<CustomerSpawnConfig>();

        public List<CustomerSpawnConfig> SpawnConfigs => spawnConfigs;
        public bool AssignRandomItemsOnRebuild => assignRandomItemsOnRebuild;
        public ItemDataSO[] AvailableItems => availableItems;

        private void Start()
        {
            if (CrowdManager.Instance == null || customerPrefab == null)
            {
                return;
            }

            SpawnConfiguredCrowd();
        }

        public void SpawnConfiguredCrowd()
        {
            foreach (var config in spawnConfigs)
            {
                if (!config.isEnabled)
                {
                    continue;
                }

                ItemDataSO dataToAssign = config.itemData;

                // Fallback to random selection from availableItems if enabled or config is unassigned
                if ((dataToAssign == null || assignRandomItemsOnRebuild) && availableItems != null && availableItems.Length > 0)
                {
                    dataToAssign = availableItems[Random.Range(0, availableItems.Length)];
                }

                if (dataToAssign == null)
                {
                    continue;
                }

                Vector3 spawnPos = CrowdManager.Instance.GetSlotWorldPosition(config.row, config.column);
                Customer customer = Instantiate(customerPrefab, spawnPos, customerPrefab.transform.rotation, transform);

                customer.Initialize(dataToAssign);
                CrowdManager.Instance.RegisterCustomer(customer, config.row, config.column);
            }
        }

        public void GenerateGridConfigs(int rows, int cols)
        {
            spawnConfigs.Clear();

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    ItemDataSO selectedData = null;

                    if (assignRandomItemsOnRebuild && availableItems != null && availableItems.Length > 0)
                    {
                        selectedData = availableItems[Random.Range(0, availableItems.Length)];
                    }

                    spawnConfigs.Add(new CustomerSpawnConfig
                    {
                        isEnabled = true,
                        row = r,
                        column = c,
                        itemData = selectedData
                    });
                }
            }
        }
    }
}