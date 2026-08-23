using System.Collections.Generic;
using UnityEngine;
using RestaurantLoop.Infrastructure;

namespace RestaurantLoop.Core
{
    public class QueueManager : MonoBehaviour
    {
        public static QueueManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private QueueSpawner queueSpawner;

        private List<QueueColumn> columns = new List<QueueColumn>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (queueSpawner == null)
            {
                queueSpawner = GetComponent<QueueSpawner>();
            }
        }

        public void SetupQueue(LevelDataSO levelData)
        {
            ClearQueue();

            if (queueSpawner == null || levelData == null || levelData.queueStackConfigs == null) return;

            int cols = levelData.columnCount;
            int rows = levelData.calculatedRowCount;
            var stackConfigs = levelData.queueStackConfigs;

            columns = queueSpawner.SpawnQueueLayout(cols, rows);

            int currentStackIndex = 0;

            foreach (var col in columns)
            {
                for (int r = 0; r < rows; r++)
                {
                    if (currentStackIndex >= stackConfigs.Count) break;

                    QueueSlot slot = col.transform.GetChild(r).GetComponent<QueueSlot>();
                    if (slot != null)
                    {
                        QueueStackConfig config = stackConfigs[currentStackIndex];

                        if (config.itemData != null && config.itemData.StackPrefab != null)
                        {
                            GameObject stackObj = PoolManager.Instance.Spawn(config.itemData.StackPrefab, slot.transform.position, Quaternion.identity, slot.transform);
                            stackObj.transform.localPosition = Vector3.zero; 
                            
                            StackItem newStack = stackObj.GetComponent<StackItem>();

                            if (newStack != null)
                            {
                                newStack.Initialize(config.itemData, config.itemCount);
                                slot.PlaceStack(newStack);
                            }
                        }

                        currentStackIndex++;
                    }
                }
            }
        }

        public void ClearQueue()
        {
            foreach (var col in columns)
            {
                if (col != null)
                {
                    // Clean up spawned stack items back to the pool before destroying the column parent
                    StackItem[] childStacks = col.GetComponentsInChildren<StackItem>();
                    foreach (var stack in childStacks)
                    {
                        if (stack != null)
                        {
                            PoolManager.Instance.Despawn(stack.gameObject);
                        }
                    }

                    Destroy(col.gameObject);
                }
            }

            columns.Clear();
        }
    }
}