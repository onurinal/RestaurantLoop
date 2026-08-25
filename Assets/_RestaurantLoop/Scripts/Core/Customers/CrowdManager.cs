using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RestaurantLoop.Infrastructure;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RestaurantLoop.Core
{
    public class CrowdManager : MonoBehaviour
    {
        public static CrowdManager Instance { get; private set; }

        [Header("Active Edge Setup")]
        [Tooltip("How many customers can eat at the belt simultaneously.")]
        [SerializeField] private int activeEdgeSlotCount = 6;
        [Tooltip("Acceptable distance margin for a customer to detect and grab food from the belt.")]
        [SerializeField] private float alignmentTolerance = 1.2f;
        [Tooltip("Distance from the belt where the customer stands/sits.")]
        [SerializeField] private float edgeInwardOffset = 1.5f;

        [Header("Central Crowd Layout Type & Limits")]
        [SerializeField] private CrowdLayoutType layoutType = CrowdLayoutType.Circular;
        [SerializeField] private Vector3 crowdCenterOffset = Vector3.zero;

        [Tooltip("Used when LayoutType is set to Rectangular.")]
        [SerializeField] private Vector2 crowdAreaSize = new Vector2(4f, 6f);

        [Tooltip("Used when LayoutType is set to Circular.")]
        [SerializeField] private float crowdRadius = 4f;

        [SerializeField] private float minCustomerDistance = 0.7f;

        [Tooltip("Maximum number of visible customer models present in the central crowd area at once.")]
        [Range(10, 100)]
        [SerializeField] private int maxVisibleCrowdCount = 100;

        [Header("Entrance Sequence Setup")]
        [SerializeField] private float spawnInterval = 0.15f;
        [SerializeField] private float moveDuration = 1.2f;
        [SerializeField] private float pathJitterAmount = 0.5f;
        [SerializeField] private Vector3 outerSpawnOffset = new Vector3(0f, 0f, -3.0f);

        [Header("Customer Prefabs")]
        [Tooltip("Used for food types whose dedicated character art has not been created yet.")]
        [SerializeField] private Customer customerPrefab;

        [Header("References")]
        [SerializeField] private ConveyorBuilder conveyorBuilder;

        private EdgeSlotService edgeSlots;
        private CentralCrowdService centralCrowd;
        private CustomerEntranceSequencer entranceSequencer;
        private Coroutine entranceSequenceCoroutine;

        private List<ItemDataSO> unspawnedDemandPool = new List<ItemDataSO>();
        private Dictionary<ItemDataSO, int> remainingDemandPerType = new Dictionary<ItemDataSO, int>();

        public int ActiveEdgeSlotCount => edgeSlots != null ? edgeSlots.SlotCount : activeEdgeSlotCount;
        public bool IsSpawningCustomers => entranceSequencer != null && entranceSequencer.IsRunning;
        public int TotalRemainingDemand { get; private set; }

        public event Action<Customer, int> EdgeCustomerReplacementStarted;
        public event Action<Customer, int> CustomerExitCompleted;
        public event Action<int, Dictionary<ItemDataSO, int>> OnDemandChanged;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            edgeSlots = new EdgeSlotService(activeEdgeSlotCount, alignmentTolerance, edgeInwardOffset);
            centralCrowd = new CentralCrowdService();
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
            if (!Application.isPlaying || edgeSlots == null || edgeSlots.SlotCount != activeEdgeSlotCount)
            {
                edgeSlots = new EdgeSlotService(activeEdgeSlotCount, alignmentTolerance, edgeInwardOffset);
            }

            edgeSlots.RecalculateSplineMapping(GetConveyor());
        }

        public void SetupCrowd(List<CustomerDemandConfig> demands)
        {
            ClearCrowd();

            TotalRemainingDemand = 0;
            remainingDemandPerType.Clear();

            foreach (var cfg in demands)
            {
                TotalRemainingDemand += cfg.totalCustomerCount;
                remainingDemandPerType[cfg.itemData] = cfg.totalCustomerCount;

                for (int i = 0; i < cfg.totalCustomerCount; i++)
                {
                    unspawnedDemandPool.Add(cfg.itemData);
                }
            }

            for (int i = unspawnedDemandPool.Count - 1; i > 0; i--)
            {
                int rand = UnityEngine.Random.Range(0, i + 1);
                (unspawnedDemandPool[i], unspawnedDemandPool[rand]) = (unspawnedDemandPool[rand], unspawnedDemandPool[i]);
            }

            int centralCrowdCount = Mathf.Max(0, TotalRemainingDemand - activeEdgeSlotCount);
            if (centralCrowd == null) centralCrowd = new CentralCrowdService();

            centralCrowd.SetupLayout(layoutType, centralCrowdCount, GetRoomCenter(), crowdCenterOffset, crowdAreaSize, crowdRadius, minCustomerDistance);

            NotifyDemandChanged();

            if (entranceSequenceCoroutine != null) StopCoroutine(entranceSequenceCoroutine);
            entranceSequenceCoroutine = StartCoroutine(RunEntranceSequence());
        }

        private IEnumerator RunEntranceSequence()
        {
            Vector3 spawnPos = GetOuterSpawnPosition();
            Vector3 gapCenter = GetConveyorEntranceWorldPosition();
            Vector3 roomCenter = GetRoomCenter();

            yield return entranceSequencer.Run(
                PopUnspawnedDemand,
                GetCustomerPrefab,
                transform,
                spawnPos,
                gapCenter,
                roomCenter,
                edgeSlots,
                centralCrowd,
                maxVisibleCrowdCount,
                GetEdgeSlotWorldPosition,
                (c, slotIdx) => EdgeCustomerReplacementStarted?.Invoke(c, slotIdx));
        }

        public void OnCustomerServed(Customer customer)
        {
            if (customer == null) return;
            if (!edgeSlots.TryGetSlotIndex(customer, out int slotIndex)) return;

            DecrementDemandForType(customer.RequiredData);
            edgeSlots.Release(slotIndex);
            CustomerExitCompleted?.Invoke(customer, slotIndex);

            PromoteCrowdToEdgeSlot(slotIndex);
        }

        private void PromoteCrowdToEdgeSlot(int edgeSlotIndex)
        {
            CentralCrowdSlot visibleSlot = centralCrowd.GetRandomVisibleSlot();

            if (visibleSlot != null && visibleSlot.OccupyingCustomer != null)
            {
                Customer promotedCustomer = visibleSlot.OccupyingCustomer;
                visibleSlot.OccupyingCustomer = null;

                edgeSlots.Occupy(edgeSlotIndex, promotedCustomer);
                Vector3 edgePos = GetEdgeSlotWorldPosition(edgeSlotIndex);

                promotedCustomer.MoveToEdgeSlot(edgePos);
                EdgeCustomerReplacementStarted?.Invoke(promotedCustomer, edgeSlotIndex);

                RevealHiddenCrowdCustomer();
            }
        }

        private void RevealHiddenCrowdCustomer()
        {
            CentralCrowdSlot hiddenSlot = centralCrowd.GetFirstHiddenOccupiedSlot();
            if (hiddenSlot != null && hiddenSlot.OccupyingCustomer != null)
            {
                hiddenSlot.OccupyingCustomer.gameObject.SetActive(true);
            }
        }

        private ItemDataSO PopUnspawnedDemand()
        {
            if (unspawnedDemandPool.Count == 0) return null;
            ItemDataSO data = unspawnedDemandPool[0];
            unspawnedDemandPool.RemoveAt(0);
            return data;
        }

        private Customer GetCustomerPrefab(ItemDataSO itemData)
        {
            return itemData != null && itemData.CustomerPrefab != null
                ? itemData.CustomerPrefab
                : customerPrefab;
        }

        private void DecrementDemandForType(ItemDataSO data)
        {
            if (data == null) return;
            TotalRemainingDemand = Mathf.Max(0, TotalRemainingDemand - 1);

            if (remainingDemandPerType.ContainsKey(data))
            {
                remainingDemandPerType[data] = Mathf.Max(0, remainingDemandPerType[data] - 1);
            }

            NotifyDemandChanged();
        }

        private void NotifyDemandChanged()
        {
            OnDemandChanged?.Invoke(TotalRemainingDemand, remainingDemandPerType);
        }

        public void ClearCrowd()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Customer c = transform.GetChild(i).GetComponent<Customer>();
                if (c != null) PoolManager.Instance.Despawn(c.gameObject);
            }

            if (centralCrowd != null) centralCrowd.Clear();
            unspawnedDemandPool.Clear();
        }

        public Vector3 GetOuterSpawnPosition() => EntrancePathUtility.GetOuterSpawnPosition(GetConveyorEntranceWorldPosition(), outerSpawnOffset);
        public Vector3 GetConveyorEntranceWorldPosition() => EntrancePathUtility.GetConveyorGapCenter(GetConveyor(), transform.position);
        public Vector3 GetEdgeSlotWorldPosition(int index) => edgeSlots.GetSlotWorldPosition(index, GetConveyor(), GetRoomCenter(), transform.position);
        public Customer CheckServiceForBeltItem(float dist, ItemDataSO data) => edgeSlots.FindServiceCandidate(dist, data, GetConveyor());

        private ConveyorManager GetConveyor() => ConveyorManager.Instance != null ? ConveyorManager.Instance : FindFirstObjectByType<ConveyorManager>();
        private Vector3 GetRoomCenter() => conveyorBuilder != null ? conveyorBuilder.CenterPosition : transform.position;

        private void OnDrawGizmos()
        {
            InitializeGridSplineMapping();

            if (edgeSlots != null)
            {
                Gizmos.color = Color.green;
                for (int i = 0; i < edgeSlots.SlotCount; i++)
                {
                    Gizmos.DrawWireSphere(GetEdgeSlotWorldPosition(i), 0.4f);
                }
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(GetOuterSpawnPosition(), 0.5f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(GetConveyorEntranceWorldPosition(), 0.5f);

            if (centralCrowd == null) centralCrowd = new CentralCrowdService();

            // Always use maxVisibleCrowdCount directly from this component for Gizmo preview
            if (!Application.isPlaying || centralCrowd.Count == 0)
            {
                centralCrowd.SetupLayout(layoutType, maxVisibleCrowdCount, GetRoomCenter(), crowdCenterOffset, crowdAreaSize, crowdRadius, minCustomerDistance);
            }

            centralCrowd.DrawGizmos(layoutType, GetRoomCenter(), crowdCenterOffset, crowdAreaSize, crowdRadius);
        }
    }
}
