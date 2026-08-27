using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace RestaurantLoop.Core
{
    public class QueueManager : MonoBehaviour
    {
        public static QueueManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private QueueSpawner queueSpawner;

        private List<QueueColumn> columns = new List<QueueColumn>();

        /// <summary>
        /// Calculates the total number of remaining active stacks waiting across all queue columns.
        /// </summary>
        public int RemainingStackCount
        {
            get
            {
                int count = 0;
                foreach (var col in columns)
                {
                    if (col != null)
                    {
                        count += col.OccupiedSlotCount;
                    }
                }

                return count;
            }
        }

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
                if (col == null) continue;

                col.InitializeChildSlots();

                for (int r = 0; r < rows; r++)
                {
                    if (currentStackIndex >= stackConfigs.Count) break;

                    QueueSlot slot = col.transform.GetChild(r).GetComponent<QueueSlot>();
                    if (slot != null)
                    {
                        QueueStackConfig config = stackConfigs[currentStackIndex];

                        if (config.itemData != null && config.itemData.StackPrefab != null)
                        {
                            // Instantiate fresh GameObject directly from prefab asset to prevent pooled object pollution
                            GameObject stackObj = Instantiate(
                                config.itemData.StackPrefab,
                                slot.transform.position,
                                Quaternion.identity,
                                slot.transform
                            );

                            StackItem.KillTweensInHierarchy(stackObj);

                            stackObj.transform.localPosition = Vector3.zero;
                            stackObj.transform.localRotation = Quaternion.identity;
                            stackObj.transform.localScale = Vector3.one;

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
            if (columns == null) return;

            foreach (var col in columns)
            {
                if (col != null)
                {
                    QueueSlot[] childSlots = col.GetComponentsInChildren<QueueSlot>(true);
                    foreach (var slot in childSlots)
                    {
                        if (slot != null)
                        {
                            slot.ClearSlot();
                        }
                    }

                    StackItem[] childStacks = col.GetComponentsInChildren<StackItem>(true);
                    foreach (var stack in childStacks)
                    {
                        if (stack != null)
                        {
                            StackItem.KillTweensInHierarchy(stack.gameObject);
                            Destroy(stack.gameObject);
                        }
                    }

                    StackItem.KillTweensInHierarchy(col.gameObject);
                    Destroy(col.gameObject);
                }
            }

            columns.Clear();
        }
    }
}