using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Core
{
    [System.Serializable]
    public struct QueueItemSetup
    {
        public ItemDataSO itemData; 
        public int itemCount;       
    }

    /// <summary>
    /// Manages queue grid initialization and populates unique item stack prefabs manually configured per level.
    /// </summary>
    public class QueueManager : MonoBehaviour
    {
        public static QueueManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private QueueSpawner queueSpawner;
        [SerializeField] private StackItem fallbackStackPrefab;

        [Header("Grid Configuration")]
        [SerializeField] private int initialColumnCount = 3;
        [SerializeField] private int initialRowCount = 8;

        [Header("Level Design: Manual Sequence")]
        [Tooltip("Queue içindeki elemanları sırayla buradan ayarlayın (Sol üstten başlayarak dizer)")]
        [SerializeField] private List<QueueItemSetup> manualQueueSequence;

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
            if (queueSpawner == null) return;

            columns = queueSpawner.SpawnQueueLayout(initialColumnCount, initialRowCount);
            
            int sequenceIndex = 0; 

            for (int i = 0; i < columns.Count; i++)
            {
                QueueColumn col = columns[i];

                for (int j = 0; j < col.transform.childCount; j++)
                {
                    QueueSlot slot = col.transform.GetChild(j).GetComponent<QueueSlot>();
                    if (slot == null) continue;

                    if (manualQueueSequence != null && sequenceIndex < manualQueueSequence.Count)
                    {
                        QueueItemSetup setup = manualQueueSequence[sequenceIndex];

                        if (setup.itemData != null && setup.itemData.StackPrefab != null)
                        {
                            GameObject spawnedObj = Instantiate(setup.itemData.StackPrefab, slot.transform.position, Quaternion.identity);
                            StackItem stackItem = spawnedObj.GetComponent<StackItem>();

                            if (stackItem != null)
                            {
                                stackItem.InitializeData(setup.itemData);
                                
                                // ARTIK AKTİF VE ÇALIŞIYOR!
                                stackItem.SetItemCount(setup.itemCount); 
                                
                                slot.PlaceStack(stackItem);
                                sequenceIndex++; 
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