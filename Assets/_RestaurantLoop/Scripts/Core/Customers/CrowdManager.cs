using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Orchestrates the customer crowd: wires together edge-slot tracking, central station layout,
    /// and the entrance spawn sequence, and answers belt-side service queries.
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
        [SerializeField] private Vector3 outerSpawnOffset = new Vector3(0f, 0f, -3.0f);

        [Header("Prefabs & References")]
        [SerializeField] private Customer customerPrefab;
        [SerializeField] private ConveyorBuilder conveyorBuilder;

        private EdgeSlotService edgeSlots;
        private StationLayoutManager stationLayout;
        private CustomerEntranceSequencer entranceSequencer;
        private Coroutine entranceSequenceCoroutine;

        public int ActiveEdgeSlotCount => edgeSlots.SlotCount;
        public bool IsSpawningCustomers => entranceSequencer.IsRunning;

        public event Action<Customer, int> EdgeCustomerReplacementStarted;
        public event Action<Customer, int> CustomerExitCompleted;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            edgeSlots = new EdgeSlotService(activeEdgeSlotCount, alignmentTolerance, edgeInwardOffset);
            stationLayout = new StationLayoutManager(stationPrefab, maxStationsPerRow, stationSpacingX, stationSpacingZ);
            entranceSequencer = new CustomerEntranceSequencer(spawnInterval, moveDuration, pathJitterAmount, outerSpawnOffset);
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
            edgeSlots?.RecalculateSplineMapping(GetConveyor());
        }

        public void SetupStations(List<StationConfig> configs)
        {
            stationLayout.Setup(configs, transform, GetRoomCenter());

            if (entranceSequenceCoroutine != null) StopCoroutine(entranceSequenceCoroutine);
            entranceSequenceCoroutine = StartCoroutine(RunEntranceSequence(configs));
        }

        private IEnumerator RunEntranceSequence(List<StationConfig> configs)
        {
            Vector3 spawnPos = GetOuterSpawnPosition();
            Vector3 gapCenter = GetConveyorEntranceWorldPosition();
            Vector3 roomCenter = GetRoomCenter();

            yield return entranceSequencer.Run(
                configs,
                customerPrefab,
                transform,
                GetConveyor(),
                spawnPos,
                gapCenter,
                roomCenter,
                edgeSlots,
                stationLayout,
                (customer, slotIndex) => EdgeCustomerReplacementStarted?.Invoke(customer, slotIndex));
        }

        public Vector3 GetOuterSpawnPosition()
        {
            return EntrancePathUtility.GetOuterSpawnPosition(GetConveyorEntranceWorldPosition(), entranceSequencer.OuterSpawnOffset);
        }

        public Vector3 GetConveyorEntranceWorldPosition()
        {
            return EntrancePathUtility.GetConveyorGapCenter(GetConveyor(), transform.position);
        }

        public Vector3 GetEdgeSlotWorldPosition(int index)
        {
            return edgeSlots.GetSlotWorldPosition(index, GetConveyor(), GetRoomCenter(), transform.position);
        }

        public Customer CheckServiceForBeltItem(float itemSplineDistance, ItemDataSO itemData)
        {
            return edgeSlots.FindServiceCandidate(itemSplineDistance, itemData, GetConveyor());
        }

        public void OnCustomerServed(Customer customer)
        {
            if (customer == null) return;
            if (!edgeSlots.TryGetSlotIndex(customer, out int slotIndex)) return;

            edgeSlots.Release(slotIndex);
            CustomerExitCompleted?.Invoke(customer, slotIndex);
            TryFillEdgeSlot(slotIndex);
        }

        private void TryFillEdgeSlot(int slotIndex)
        {
            if (!stationLayout.TryConsumeAvailableStation(out CustomerStation station)) return;

            Vector3 targetPos = GetEdgeSlotWorldPosition(slotIndex);

            Customer newCustomer = Instantiate(customerPrefab, station.position, customerPrefab.transform.rotation, transform);
            newCustomer.Initialize(station.itemData);

            edgeSlots.Occupy(slotIndex, newCustomer);
            newCustomer.MoveToEdgeSlot(targetPos, null);
            EdgeCustomerReplacementStarted?.Invoke(newCustomer, slotIndex);
        }

        public List<Vector3> CalculateStationPositions(int count)
        {
            return stationLayout.CalculatePositions(count, GetRoomCenter());
        }

        private ConveyorManager GetConveyor()
        {
            return ConveyorManager.Instance != null ? ConveyorManager.Instance : FindFirstObjectByType<ConveyorManager>();
        }

        private Vector3 GetRoomCenter()
        {
            return conveyorBuilder != null ? conveyorBuilder.CenterPosition : transform.position;
        }

        private void OnDrawGizmos()
        {
            if (edgeSlots == null) return;

            InitializeGridSplineMapping();

            Gizmos.color = Color.green;
            for (int i = 0; i < edgeSlots.SlotCount; i++)
            {
                Gizmos.DrawWireSphere(GetEdgeSlotWorldPosition(i), 0.4f);
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(GetOuterSpawnPosition(), 0.5f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(GetConveyorEntranceWorldPosition(), 0.5f);

            CrowdTestSpawner spawner = FindFirstObjectByType<CrowdTestSpawner>();
            if (spawner == null || spawner.StationConfigs == null || spawner.StationConfigs.Count == 0) return;

            List<Vector3> previewPositions = CalculateStationPositions(spawner.StationConfigs.Count);
            Gizmos.color = Color.magenta;

            foreach (var pos in previewPositions)
            {
                Gizmos.DrawWireCube(pos + Vector3.up * 0.5f, new Vector3(1.2f, 1f, 1.2f));
            }
        }
    }
}