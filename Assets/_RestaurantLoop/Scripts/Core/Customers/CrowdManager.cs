using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RestaurantLoop.Core
{
    public class CustomerStation
    {
        public ItemDataSO itemData;
        public int remainingCount;
        public Vector3 position;
        public CustomerStationView view;

        public void UpdateUI()
        {
            if (view != null)
            {
                view.UpdateCount(remainingCount);
            }
        }
    }

    /// <summary>
    /// Manages active edge slots along the conveyor spline, station layouts, and organic entrance queue spawning.
    /// </summary>
    public class CrowdManager : MonoBehaviour
    {
        public static CrowdManager Instance { get; private set; }

        [Header("Active Edge Setup")]
        [SerializeField] private int activeEdgeSlotCount = 6;
        [SerializeField] private float alignmentTolerance = 1.2f;
        [SerializeField] private float edgeInwardOffset = 1.5f;

        [Header("Central Station Layout Setup")]
        [SerializeField] private CustomerStationView stationPrefab;
        [Range(1, 5)] [SerializeField] private int maxStationsPerRow = 2;
        [SerializeField] private float stationSpacingX = 2.5f;
        [SerializeField] private float stationSpacingZ = 2.5f;

        [Header("Entrance Sequence Setup")]
        [SerializeField] private float spawnInterval = 0.22f;
        [SerializeField] private float moveDuration = 1.4f;
        [SerializeField] private float pathJitterAmount = 0.4f;
        [Tooltip("XYZ offset relative to the conveyor gap center.")]
        [SerializeField] private Vector3 outerSpawnOffset = new Vector3(0f, 0f, -3.0f);

        [Header("Prefabs & References")]
        [SerializeField] private Customer customerPrefab;
        [SerializeField] private ConveyorBuilder conveyorBuilder;

        private Customer[] activeEdgeSlots;
        private float[] slotSplineDistances;
        private List<CustomerStation> stations = new List<CustomerStation>();
        private Coroutine entranceSequenceCoroutine;

        public int ActiveEdgeSlotCount => activeEdgeSlotCount;

        /// <summary>Raised after a replacement customer is assigned to an edge slot and starts moving toward it.</summary>
        public event Action<Customer, int> EdgeCustomerReplacementStarted;

        /// <summary>Raised after an exiting customer releases its edge slot.</summary>
        public event Action<Customer, int> CustomerExitCompleted;

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

            activeEdgeSlots = new Customer[activeEdgeSlotCount];
            slotSplineDistances = new float[activeEdgeSlotCount];
        }

        private void Start()
        {
            InitializeGridSplineMapping();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            SceneView.RepaintAll();
#endif
        }

        public void InitializeGridSplineMapping()
        {
            ConveyorManager conveyor = ConveyorManager.Instance != null ? ConveyorManager.Instance : FindFirstObjectByType<ConveyorManager>();
            if (conveyor == null || conveyor.Path == null || conveyor.Path.Length <= 0f) return;

            if (slotSplineDistances == null || slotSplineDistances.Length != activeEdgeSlotCount)
            {
                slotSplineDistances = new float[activeEdgeSlotCount];
            }

            float pathLength = conveyor.Path.Length;
            float startDist = conveyor.EntranceDistance;
            float validTravelLength = conveyor.GetRequiredTravelDistance();

            float step = validTravelLength / (activeEdgeSlotCount + 1);

            for (int i = 0; i < activeEdgeSlotCount; i++)
            {
                float travelOffset = step * (i + 1);
                float splineDist = (startDist + (conveyor.IsClockwise ? travelOffset : -travelOffset)) % pathLength;
                if (splineDist < 0f) splineDist += pathLength;

                slotSplineDistances[i] = splineDist;
            }
        }

        public void SetupStations(List<StationConfig> configs)
        {
            ClearExistingStations();

            if (configs == null || configs.Count == 0 || stationPrefab == null) return;

            List<Vector3> layoutPositions = CalculateStationPositions(configs.Count);

            for (int i = 0; i < configs.Count; i++)
            {
                StationConfig cfg = configs[i];
                Vector3 spawnPos = layoutPositions[i];

                CustomerStationView viewInstance = Instantiate(stationPrefab, spawnPos, Quaternion.identity, transform);
                viewInstance.Initialize(cfg.itemData, 0);

                CustomerStation station = new CustomerStation
                {
                    itemData = cfg.itemData,
                    remainingCount = 0,
                    position = spawnPos,
                    view = viewInstance
                };

                stations.Add(station);
            }

            if (entranceSequenceCoroutine != null) StopCoroutine(entranceSequenceCoroutine);
            entranceSequenceCoroutine = StartCoroutine(SpawnLevelEntranceRoutine(configs));
        }

        private IEnumerator SpawnLevelEntranceRoutine(List<StationConfig> configs)
        {
            Vector3 spawnPos = GetOuterSpawnPosition();

            List<ItemDataSO> incomingQueue = new List<ItemDataSO>();
            Dictionary<ItemDataSO, int> stationTargetCounts = new Dictionary<ItemDataSO, int>();

            foreach (var cfg in configs)
            {
                stationTargetCounts[cfg.itemData] = cfg.remainingCount;
                for (int i = 0; i < cfg.remainingCount; i++)
                {
                    incomingQueue.Add(cfg.itemData);
                }
            }

            // Shuffle queue randomly to avoid monochromatic blocks
            for (int i = incomingQueue.Count - 1; i > 0; i--)
            {
                int randomIndex = UnityEngine.Random.Range(0, i + 1);
                var temp = incomingQueue[i];
                incomingQueue[i] = incomingQueue[randomIndex];
                incomingQueue[randomIndex] = temp;
            }

            int edgeSlotIndex = 0;

            for (int i = 0; i < incomingQueue.Count; i++)
            {
                ItemDataSO customerData = incomingQueue[i];
                Customer newCustomer = Instantiate(customerPrefab, spawnPos, customerPrefab.transform.rotation, transform);
                newCustomer.Initialize(customerData);

                if (edgeSlotIndex < activeEdgeSlotCount)
                {
                    int slotIdx = edgeSlotIndex++;
                    activeEdgeSlots[slotIdx] = newCustomer;

                    Vector3 edgeTargetPos = GetEdgeSlotWorldPosition(slotIdx);
                    Vector3[] waypoints = BuildOrganicPath(spawnPos, edgeTargetPos);

                    newCustomer.MoveAlongPath(waypoints, moveDuration, true);

                    EdgeCustomerReplacementStarted?.Invoke(newCustomer, slotIdx);
                    stationTargetCounts[customerData]--;
                }
                else
                {
                    CustomerStation targetStation = stations.Find(s => s.itemData == customerData);
                    if (targetStation != null)
                    {
                        Vector3 tableTargetPos = targetStation.position;
                        Vector3[] waypoints = BuildOrganicPath(spawnPos, tableTargetPos);

                        newCustomer.MoveAlongPath(waypoints, moveDuration, false, () =>
                        {
                            targetStation.remainingCount++;
                            targetStation.UpdateUI();
                            Destroy(newCustomer.gameObject);
                        });
                    }
                }

                yield return new WaitForSeconds(spawnInterval);
            }
        }

        private Vector3[] BuildOrganicPath(Vector3 startPos, Vector3 targetPos)
        {
            Vector3 gapCenter = GetConveyorEntranceWorldPosition();
            Vector3 roomCenter = conveyorBuilder != null ? conveyorBuilder.CenterPosition : transform.position;

            Vector3 inboundDirection = (roomCenter - gapCenter).normalized;
            Vector3 perpendicularDirection = Vector3.Cross(inboundDirection, Vector3.up);

            float jitter = UnityEngine.Random.Range(-pathJitterAmount, pathJitterAmount);
            Vector3 intermediateLandingPos = gapCenter + (inboundDirection * 2.0f) + (perpendicularDirection * jitter);

            return new Vector3[] { startPos, gapCenter, intermediateLandingPos, targetPos };
        }

        public Vector3 GetOuterSpawnPosition()
        {
            Vector3 gapPos = GetConveyorEntranceWorldPosition();
            return gapPos + outerSpawnOffset;
        }

        public Vector3 GetConveyorEntranceWorldPosition()
        {
            ConveyorManager conveyor = ConveyorManager.Instance != null ? ConveyorManager.Instance : FindFirstObjectByType<ConveyorManager>();

            if (conveyor != null && conveyor.Path != null)
            {
                Vector3 entrancePos = conveyor.Path.GetPosition(conveyor.EntranceDistance);
                Vector3 exitPos = conveyor.Path.GetPosition(conveyor.ExitDistance);
                return (entrancePos + exitPos) * 0.5f;
            }

            return transform.position;
        }

        public Vector3 GetEdgeSlotWorldPosition(int index)
        {
            ConveyorManager conveyor = ConveyorManager.Instance != null ? ConveyorManager.Instance : FindFirstObjectByType<ConveyorManager>();

            if (conveyor != null && conveyor.Path != null && conveyor.Path.Length > 0f)
            {
                if (slotSplineDistances == null || slotSplineDistances.Length != activeEdgeSlotCount || slotSplineDistances[index] == 0f)
                {
                    InitializeGridSplineMapping();
                }

                float splineDist = slotSplineDistances[index];
                Vector3 beltPoint = conveyor.Path.GetPosition(splineDist);

                Vector3 roomCenter = conveyorBuilder != null ? conveyorBuilder.CenterPosition : transform.position;
                Vector3 inwardDir = (roomCenter - beltPoint).normalized;
                inwardDir.y = 0f;

                return beltPoint + inwardDir * edgeInwardOffset;
            }

            return transform.position;
        }

        private void ClearExistingStations()
        {
            foreach (var st in stations)
            {
                if (st.view != null) Destroy(st.view.gameObject);
            }
            stations.Clear();
        }

        public List<Vector3> CalculateStationPositions(int count)
        {
            List<Vector3> positions = new List<Vector3>();
            if (count <= 0) return positions;

            Vector3 center = conveyorBuilder != null ? conveyorBuilder.CenterPosition : transform.position;
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

        private void TryFillEdgeSlot(int slotIndex)
        {
            CustomerStation availableStation = GetNextAvailableStation();
            if (availableStation == null) return;

            availableStation.remainingCount--;
            availableStation.UpdateUI();

            Vector3 spawnPos = availableStation.position;
            Vector3 targetPos = GetEdgeSlotWorldPosition(slotIndex);

            Customer newCustomer = Instantiate(customerPrefab, spawnPos, customerPrefab.transform.rotation, transform);
            newCustomer.Initialize(availableStation.itemData);

            activeEdgeSlots[slotIndex] = newCustomer;
            newCustomer.MoveToEdgeSlot(targetPos, null);
            EdgeCustomerReplacementStarted?.Invoke(newCustomer, slotIndex);
        }

        private CustomerStation GetNextAvailableStation()
        {
            int validCount = 0;
            for (int i = 0; i < stations.Count; i++)
            {
                if (stations[i].remainingCount > 0) validCount++;
            }

            if (validCount == 0) return null;

            int randomIndex = UnityEngine.Random.Range(0, validCount);
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

        public Customer CheckServiceForBeltItem(float itemSplineDistance, ItemDataSO itemData)
        {
            ConveyorManager conveyor = ConveyorManager.Instance != null ? ConveyorManager.Instance : FindFirstObjectByType<ConveyorManager>();
            if (conveyor == null || conveyor.Path == null) return null;

            float pathLength = conveyor.Path.Length;
            Customer bestCandidate = null;
            float minDelta = float.MaxValue;

            for (int i = 0; i < activeEdgeSlotCount; i++)
            {
                Customer candidate = activeEdgeSlots[i];
                if (candidate == null || candidate.IsServed || !candidate.IsEdgeCustomer) continue;
                if (candidate.RequiredData != itemData) continue;

                float targetSplineDistance = slotSplineDistances[i];
                float delta = Mathf.Abs(itemSplineDistance - targetSplineDistance);

                if (delta > pathLength * 0.5f) delta = pathLength - delta;

                if (delta <= alignmentTolerance && delta < minDelta)
                {
                    minDelta = delta;
                    bestCandidate = candidate;
                }
            }

            return bestCandidate;
        }

        public void OnCustomerServed(Customer customer)
        {
            if (customer == null) return;

            for (int i = 0; i < activeEdgeSlotCount; i++)
            {
                if (activeEdgeSlots[i] == customer)
                {
                    activeEdgeSlots[i] = null;
                    CustomerExitCompleted?.Invoke(customer, i);
                    TryFillEdgeSlot(i);
                    break;
                }
            }
        }

        private void OnDrawGizmos()
        {
            InitializeGridSplineMapping();

            Gizmos.color = Color.green;
            for (int i = 0; i < activeEdgeSlotCount; i++)
            {
                Gizmos.DrawWireSphere(GetEdgeSlotWorldPosition(i), 0.4f);
            }

            // Outer Spawn Point (Yellow)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(GetOuterSpawnPosition(), 0.5f);

            // Calculated Conveyor Gap Center (Cyan)
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(GetConveyorEntranceWorldPosition(), 0.5f);

            CrowdTestSpawner spawner = FindFirstObjectByType<CrowdTestSpawner>();
            if (spawner == null || spawner.StationConfigs == null || spawner.StationConfigs.Count == 0)
            {
                return;
            }

            List<Vector3> previewPositions = CalculateStationPositions(spawner.StationConfigs.Count);
            Gizmos.color = Color.magenta;

            foreach (var pos in previewPositions)
            {
                Gizmos.DrawWireCube(pos + Vector3.up * 0.5f, new Vector3(1.2f, 1f, 1.2f));
            }
        }
    }
}