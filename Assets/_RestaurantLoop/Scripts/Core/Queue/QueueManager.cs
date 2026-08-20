using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Manages queue grid initialization and item population at level start.
    /// </summary>
    public class QueueManager : MonoBehaviour
    {
        public static QueueManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private QueueSpawner queueSpawner;
        [SerializeField] private StackItem testStackPrefab;

        [Header("Configuration")]
        [SerializeField] private int initialColumnCount = 3;
        [SerializeField] private int initialRowCount = 3;

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
            if (queueSpawner == null || testStackPrefab == null)
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
                    if (slot != null)
                    {
                        StackItem newStack = Instantiate(testStackPrefab, slot.transform.position, Quaternion.identity);
                        slot.PlaceStack(newStack);
                    }
                }
            }
        }
    }
}