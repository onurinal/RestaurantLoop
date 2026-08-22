using System;
using System.Collections.Generic;
using UnityEngine;

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
    /// Manages active edge slots and automatically arranges station prefabs dynamically centered inside the conveyor bounds.
    /// </summary>
    public class CrowdManager : MonoBehaviour
    {
        public static CrowdManager Instance { get; private set; }

        [Header("Active Edge Setup")]
        [SerializeField] private int activeEdgeSlotCount = 6;
        [SerializeField] private float alignmentTolerance = 1.2f;
        [SerializeField] private float marginX = 3.5f;
        [SerializeField] private float marginZ = 4.0f;

        [Header("Central Station Layout Setup")]
        [SerializeField] private CustomerStationView stationPrefab;
        [Range(1, 5)] [SerializeField] private int maxStationsPerRow = 2;
        [SerializeField] private float stationSpacingX = 2.5f;
        [SerializeField] private float stationSpacingZ = 2.5f;

        [Header("Prefabs & References")]
        [SerializeField] private Customer customerPrefab;
        [SerializeField] private ConveyorBuilder conveyorBuilder;
        [SerializeField] private SplineConveyorPath conveyorPath;

        private Customer[] activeEdgeSlots;
        private float[] slotSplineDistances;
        private List<CustomerStation> stations = new List<CustomerStation>();

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
            if (conveyorPath == null)
            {
                conveyorPath = FindFirstObjectByType<SplineConveyorPath>();
            }

            if (conveyorPath == null || conveyorPath.Length <= 0f) return;

            float pathLength = conveyorPath.Length;

            for (int i = 0; i < activeEdgeSlotCount; i++)
            {
                Vector3 slotPos = GetEdgeSlotWorldPosition(i);
                slotSplineDistances[i] = FindClosestDistanceOnSpline(slotPos, pathLength);
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
                viewInstance.Initialize(cfg.itemData, cfg.remainingCount);

                CustomerStation station = new CustomerStation
                {
                    itemData = cfg.itemData,
                    remainingCount = cfg.remainingCount,
                    position = spawnPos,
                    view = viewInstance
                };

                stations.Add(station);
            }

            PopulateInitialEdgeSlots();
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

        private void PopulateInitialEdgeSlots()
        {
            for (int i = 0; i < activeEdgeSlotCount; i++)
            {
                TryFillEdgeSlot(i);
            }
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
            List<CustomerStation> validStations = stations.FindAll(s => s.remainingCount > 0);
            if (validStations.Count == 0) return null;

            return validStations[UnityEngine.Random.Range(0, validStations.Count)];
        }

        public Customer CheckServiceForBeltItem(float itemSplineDistance, ItemDataSO itemData)
        {
            if (conveyorPath == null) return null;

            float pathLength = conveyorPath.Length;
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

        public Vector3 GetEdgeSlotWorldPosition(int index)
        {
            GetInnerBounds(out float minX, out float maxX, out float minZ, out float maxZ);

            float perimeter = 2 * ((maxX - minX) + (maxZ - minZ));
            float step = perimeter / activeEdgeSlotCount;
            float currentDist = index * step;

            float width = maxX - minX;
            float height = maxZ - minZ;

            if (currentDist <= width)
                return new Vector3(minX + currentDist, transform.position.y, minZ);
            currentDist -= width;

            if (currentDist <= height)
                return new Vector3(maxX, transform.position.y, minZ + currentDist);
            currentDist -= height;

            if (currentDist <= width)
                return new Vector3(maxX - currentDist, transform.position.y, maxZ);
            currentDist -= width;

            return new Vector3(minX, transform.position.y, maxZ - currentDist);
        }

        private float FindClosestDistanceOnSpline(Vector3 worldPos, float pathLength)
        {
            float bestDist = 0f;
            float minSqrMag = float.MaxValue;
            int samples = 120;

            for (int i = 0; i < samples; i++)
            {
                float dist = (i / (float)samples) * pathLength;
                Vector3 samplePos = conveyorPath.GetPosition(dist);
                float sqrMag = (samplePos - worldPos).sqrMagnitude;

                if (sqrMag < minSqrMag)
                {
                    minSqrMag = sqrMag;
                    bestDist = dist;
                }
            }

            return bestDist;
        }

        private void GetInnerBounds(out float minX, out float maxX, out float minZ, out float maxZ)
        {
            Vector3 center = conveyorBuilder != null ? conveyorBuilder.CenterPosition : transform.position;

            float halfW = (conveyorBuilder != null ? conveyorBuilder.Width * 0.5f : 7.5f) - marginX;
            float halfH = (conveyorBuilder != null ? conveyorBuilder.Height * 0.5f : 10f) - marginZ;

            minX = center.x - halfW;
            maxX = center.x + halfW;
            minZ = center.z - halfH;
            maxZ = center.z + halfH;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < activeEdgeSlotCount; i++)
            {
                Gizmos.DrawWireSphere(GetEdgeSlotWorldPosition(i), 0.4f);
            }

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
