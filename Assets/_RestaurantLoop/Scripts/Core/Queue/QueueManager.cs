using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Manages input interactions, queue initialization, and dispatching items to belt or rack.
    /// </summary>
    public class QueueManager : MonoBehaviour
    {
        public static QueueManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private QueueSpawner queueSpawner;
        [SerializeField] private SplineConveyorPath path;
        [SerializeField] private StackItem testStackPrefab;

        [Header("Configuration")]
        [SerializeField] private int initialColumnCount = 3;
        [SerializeField] private int initialRowCount = 3;

        private List<QueueColumn> columns = new List<QueueColumn>();
        private Camera mainCamera;

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

            mainCamera = Camera.main;
        }

        private void Start()
        {
            BuildAndPopulateQueue();
        }

        private void Update()
        {
            HandlePlayerInput();
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

        private void HandlePlayerInput()
        {
            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                StackItem clickedStack = hit.collider.GetComponent<StackItem>();

                if (clickedStack == null || clickedStack.IsJumping)
                {
                    return;
                }

                for (int i = 0; i < columns.Count; i++)
                {
                    QueueColumn column = columns[i];

                    if (column.FrontSlot != null && column.FrontSlot.CurrentStack == clickedStack)
                    {
                        column.TrySendFrontStackToBelt(path);
                        return;
                    }
                }

                if (RackManager.Instance != null)
                {
                    RackManager.Instance.TrySendRackStackToBelt(clickedStack, path);
                }
            }
        }
    }
}