using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Manages queue grid initialization and populates unique item stack prefabs assigned to ItemDataSO assets.
    /// </summary>
    public class QueueManager : MonoBehaviour
    {
        public static QueueManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private QueueSpawner queueSpawner;
        [SerializeField] private StackItem fallbackStackPrefab;

        [Header("Test Configuration")]
        [SerializeField] private int initialColumnCount = 3;
        [SerializeField] private int initialRowCount = 3;
        [SerializeField] private ItemDataSO[] availableItems;

        private List<QueueColumn> columns = new List<QueueColumn>();

        public int InitialColumnCount => initialColumnCount;
        public int InitialRowCount => initialRowCount;

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
            BuildAndPopulateQueue();
        }

        private void BuildAndPopulateQueue()
        {
            if (queueSpawner == null)
            {
                return;
            }

            columns = queueSpawner.SpawnQueueLayout(initialColumnCount, initialRowCount);

            for (int i = 0; i < columns.Count; i++)
            {
                QueueColumn col = columns[i];

                for (int j = 0; j < col.transform.childCount; j++)
                {
                    QueueSlot slot = col.transform.GetChild(j).GetComponent<QueueSlot>();
                    if (slot == null)
                    {
                        continue;
                    }

                    if (availableItems != null && availableItems.Length > 0)
                    {
                        ItemDataSO selectedData = availableItems[Random.Range(0, availableItems.Length)];

                        if (selectedData != null && selectedData.StackPrefab != null)
                        {
                            GameObject spawnedObj = Instantiate(selectedData.StackPrefab, slot.transform.position, Quaternion.identity);
                            StackItem stackItem = spawnedObj.GetComponent<StackItem>();

                            if (stackItem != null)
                            {
                                stackItem.InitializeData(selectedData);
                                slot.PlaceStack(stackItem);
                                continue;
                            }
                        }
                    }

                    if (fallbackStackPrefab != null)
                    {
                        StackItem fallbackStack = Instantiate(fallbackStackPrefab, slot.transform.position, Quaternion.identity);
                        slot.PlaceStack(fallbackStack);
                    }
                }
            }
        }
    }
}