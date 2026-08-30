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
    [RequireComponent(typeof(DiningBoardGrid))]
    public class CrowdManager : MonoBehaviour
    {
        public static CrowdManager Instance { get; private set; }

        public const int TOTAL_EDGE_SLOTS = 20;

        [Header("Active Edge Setup")]
        [Tooltip("Maximum detection distance (in meters) between the food item on the belt and the customer slot for serving.")]
        [SerializeField] private float alignmentTolerance = 1.2f;

        [Tooltip("Inward distance offset of customer slots and tables from the conveyor belt toward the room center.")]
        [SerializeField] private float edgeInwardOffset = 2.5f;

        [Header("Inner Crowd Layout")]
        [SerializeField] private Vector3 crowdCenterOffset = Vector3.zero;
        [Tooltip("Inner crowd area width (X) and length (Y) in world units.")]
        [SerializeField] private Vector2 innerCrowdArea = new Vector2(4f, 5f);
        [SerializeField] private float minCustomerDistance = 0.7f;
        [Range(10, 100)] [SerializeField] private int maxVisibleCrowdCount = 100;

        [Header("Entrance Setup")]
        [SerializeField] private float spawnInterval = 0.15f;
        [SerializeField] private float moveDuration = 1.2f;
        [SerializeField] private float pathJitterAmount = 0.5f;
        [SerializeField] private Vector3 outerSpawnOffset = new Vector3(0f, 0f, -3.0f);
        [SerializeField] private EntranceGate entranceGate;
        [SerializeField] private float gateOpenDelay = 0.4f;

        [Header("Gizmo Settings")]
        [SerializeField] private bool showCrowdGizmos = true;
        [SerializeField] private bool showToleranceGizmos = true;
        [Tooltip("Preview customer count for Scene View")]
        [SerializeField] private int previewCrowdCount = 25;
        [SerializeField] private Color crowdAreaGizmoColor = new Color(1f, 0f, 1f, 0.8f);
        [SerializeField] private Color activeEdgeCellColor = new Color(0f, 1f, 0.3f, 0.9f);

        [Header("References")]
        [SerializeField] private Customer customerPrefab;
        [SerializeField] private ConveyorBuilder conveyorBuilder;

        private DiningBoardGrid boardGrid;
        private EdgeSlotService edgeSlots;
        private CentralCrowdService centralCrowd;
        private CustomerEntranceSequencer entranceSequencer;
        private Coroutine entranceSequenceCoroutine;
        private bool isEntranceSequenceActive;
        private int activeEdgeSlotCount;

        private readonly List<ItemDataSO> unspawnedDemandPool = new List<ItemDataSO>();
        private readonly Dictionary<ItemDataSO, int> remainingDemandPerType = new Dictionary<ItemDataSO, int>();

        public int ActiveEdgeSlotCount => activeEdgeSlotCount;
        public float AlignmentTolerance => alignmentTolerance;
        public float EdgeInwardOffset => edgeInwardOffset;

        /// <summary>
        /// True until all visible customers are seated and the entrance gate is fully closed.
        /// Gameplay input uses this as the authoritative entrance lock.
        /// </summary>
        public bool IsSpawningCustomers => isEntranceSequenceActive;
        public int TotalRemainingDemand { get; private set; }

        public event Action<Customer, int> EdgeCustomerReplacementStarted;
        public event Action<Customer, int> CustomerExitCompleted;
        public event Action<int, Dictionary<ItemDataSO, int>> OnDemandChanged;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            EnsureBuilderReference();
            boardGrid = GetComponent<DiningBoardGrid>();
            edgeSlots = new EdgeSlotService(TOTAL_EDGE_SLOTS, alignmentTolerance, edgeInwardOffset);
            centralCrowd = new CentralCrowdService();
            entranceSequencer = new CustomerEntranceSequencer(spawnInterval, moveDuration, pathJitterAmount, outerSpawnOffset);
        }

        private void EnsureBuilderReference()
        {
            if (conveyorBuilder == null) conveyorBuilder = FindFirstObjectByType<ConveyorBuilder>();
        }

        public void InitializeGridSplineMapping()
        {
            EnsureBuilderReference();
            if (boardGrid == null) boardGrid = GetComponent<DiningBoardGrid>();

            edgeSlots = new EdgeSlotService(TOTAL_EDGE_SLOTS, alignmentTolerance, edgeInwardOffset);

            ConveyorManager conveyor = GetConveyor();
            edgeSlots.RecalculateSplineMapping(conveyor, conveyorBuilder);

            Vector2 bounds = conveyorBuilder != null ? new Vector2(conveyorBuilder.Width, conveyorBuilder.Height) : new Vector2(10f, 15f);
            boardGrid.InitializeGrid(GetRoomCenter(), bounds, TOTAL_EDGE_SLOTS, GetSplineEdgeSlotWorldPosition);
        }

        public void SetupCrowd(LevelDataSO levelData)
        {
            if (levelData == null) return;

            activeEdgeSlotCount = levelData.activeEdgeSlotCount;

            ClearCrowd();
            InitializeGridSplineMapping();

            TotalRemainingDemand = 0;
            remainingDemandPerType.Clear();
            unspawnedDemandPool.Clear();

            // Populate total level demands directly from LevelData
            foreach (var cfg in levelData.customerDemands)
            {
                TotalRemainingDemand += cfg.totalCustomerCount;
                remainingDemandPerType[cfg.itemData] = cfg.totalCustomerCount;
                for (int i = 0; i < cfg.totalCustomerCount; i++) unspawnedDemandPool.Add(cfg.itemData);
            }

            for (int i = unspawnedDemandPool.Count - 1; i > 0; i--)
            {
                int rand = UnityEngine.Random.Range(0, i + 1);
                (unspawnedDemandPool[i], unspawnedDemandPool[rand]) = (unspawnedDemandPool[rand], unspawnedDemandPool[i]);
            }

            int centralCrowdCount = Mathf.Max(0, TotalRemainingDemand - activeEdgeSlotCount);

            centralCrowd.SetupLayout(
                CrowdLayoutType.Rectangular,
                centralCrowdCount,
                GetRoomCenter() + crowdCenterOffset,
                Vector3.zero,
                innerCrowdArea,
                0f,
                minCustomerDistance
            );

            List<int> availableIndices = new List<int>();
            for (int i = 0; i < TOTAL_EDGE_SLOTS; i++) availableIndices.Add(i);

            for (int i = availableIndices.Count - 1; i > 0; i--)
            {
                int rand = UnityEngine.Random.Range(0, i + 1);
                (availableIndices[i], availableIndices[rand]) = (availableIndices[rand], availableIndices[i]);
            }

            List<int> initialSlotIndices = availableIndices.GetRange(0, Mathf.Min(activeEdgeSlotCount, TOTAL_EDGE_SLOTS));

            isEntranceSequenceActive = true;
            entranceSequenceCoroutine = StartCoroutine(RunEntranceSequence(initialSlotIndices));

            // Broadcast initial level demands to UI
            OnDemandChanged?.Invoke(TotalRemainingDemand, remainingDemandPerType);
        }

        private IEnumerator RunEntranceSequence(List<int> initialSlotIndices)
        {
            if (entranceGate != null)
            {
                entranceGate.ResetGateImmediate();
                entranceGate.OpenGate();
                yield return new WaitForSeconds(gateOpenDelay);
            }

            yield return entranceSequencer.Run(PopUnspawnedDemand, GetCustomerPrefab, transform, GetOuterSpawnPosition(),
                GetConveyorEntranceWorldPosition(), GetRoomCenter(), edgeSlots, initialSlotIndices, centralCrowd, maxVisibleCrowdCount,
                GetEdgeSlotWorldPosition, GetEdgeSlotYRotation, (customer, slotIndex) =>
                {
                    boardGrid.SetEdgeCellFood(slotIndex, customer.RequiredData);
                    EdgeCustomerReplacementStarted?.Invoke(customer, slotIndex);
                });

            if (entranceGate != null)
            {
                bool gateClosed = false;
                entranceGate.CloseGate(() => gateClosed = true);
                yield return new WaitUntil(() => gateClosed);
            }

            isEntranceSequenceActive = false;
            entranceSequenceCoroutine = null;
        }

        public void OnCustomerServed(Customer customer)
        {
            if (customer == null || !edgeSlots.TryGetSlotIndex(customer, out int slotIndex)) return;

            // Decrement remaining level demand strictly by 1 when customer finishes eating
            DecrementDemandForType(customer.RequiredData);

            edgeSlots.Release(slotIndex);
            boardGrid.ClearEdgeCell(slotIndex);
            CustomerExitCompleted?.Invoke(customer, slotIndex);
            PromoteCrowdToEdgeSlot(slotIndex);
        }

        public bool HasRemainingDemand(ItemDataSO data)
        {
            return data != null && remainingDemandPerType.TryGetValue(data, out int count) && count > 0;
        }

        /// <summary>
        /// Removes every remaining customer of a food type without broadcasting demand changes yet.
        /// The caller performs the broadcast after Clear Color's presentation has completed.
        /// </summary>
        public bool ResolveFoodTypeForClearColor(ItemDataSO data, StackItem visualSource)
        {
            if (!HasRemainingDemand(data)) return false;

            List<Customer> visibleCustomers = new List<Customer>();
            List<Customer> hiddenCustomers = new List<Customer>();
            List<int> freedEdgeSlots = new List<int>();

            for (int slotIndex = 0; slotIndex < edgeSlots.SlotCount; slotIndex++)
            {
                Customer customer = edgeSlots.GetCustomerInSlot(slotIndex);
                if (customer == null || customer.RequiredData != data) continue;

                edgeSlots.Release(slotIndex);
                boardGrid.ClearEdgeCell(slotIndex);
                freedEdgeSlots.Add(slotIndex);
                CollectClearColorCustomer(customer, visibleCustomers, hiddenCustomers);
            }

            foreach (CentralCrowdSlot slot in centralCrowd.Slots)
            {
                Customer customer = slot.OccupyingCustomer;
                if (customer == null || customer.RequiredData != data) continue;

                slot.OccupyingCustomer = null;
                CollectClearColorCustomer(customer, visibleCustomers, hiddenCustomers);
            }

            unspawnedDemandPool.RemoveAll(item => item == data);

            for (int i = 0; i < visibleCustomers.Count; i++)
            {
                Customer customer = visibleCustomers[i];
                visualSource?.PlayClearColorThrow(customer.transform.position);
                customer.ResolveByClearColor();
            }

            for (int i = 0; i < hiddenCustomers.Count; i++)
            {
                PoolManager.Instance.Despawn(hiddenCustomers[i].gameObject);
            }

            // Matching inner-crowd slots were cleared first, so promotions cannot select the resolved food type.
            for (int i = 0; i < freedEdgeSlots.Count; i++)
            {
                PromoteCrowdToEdgeSlot(freedEdgeSlots[i]);
            }

            int resolvedDemand = remainingDemandPerType[data];
            remainingDemandPerType[data] = 0;
            TotalRemainingDemand = Mathf.Max(0, TotalRemainingDemand - resolvedDemand);
            return true;
        }

        public void CompleteClearColorResolution()
        {
            OnDemandChanged?.Invoke(TotalRemainingDemand, remainingDemandPerType);
        }

        private void DecrementDemandForType(ItemDataSO data)
        {
            if (data == null) return;

            TotalRemainingDemand = Mathf.Max(0, TotalRemainingDemand - 1);

            if (remainingDemandPerType.ContainsKey(data))
            {
                remainingDemandPerType[data] = Mathf.Max(0, remainingDemandPerType[data] - 1);
            }

            OnDemandChanged?.Invoke(TotalRemainingDemand, remainingDemandPerType);
        }

        private static void CollectClearColorCustomer(Customer customer, List<Customer> visibleCustomers, List<Customer> hiddenCustomers)
        {
            if (customer == null) return;

            if (customer.gameObject.activeInHierarchy) visibleCustomers.Add(customer);
            else hiddenCustomers.Add(customer);
        }

        private void PromoteCrowdToEdgeSlot(int freedSlotIndex)
        {
            CentralCrowdSlot visibleSlot = centralCrowd.GetRandomVisibleSlot();
            if (visibleSlot == null || visibleSlot.OccupyingCustomer == null) return;

            List<int> freeSlotIndices = edgeSlots.GetUnoccupiedSlotIndices();
            if (freeSlotIndices.Count == 0) return;

            int targetSlotIndex = freeSlotIndices[UnityEngine.Random.Range(0, freeSlotIndices.Count)];

            Customer customer = visibleSlot.OccupyingCustomer;
            visibleSlot.OccupyingCustomer = null;
            edgeSlots.Occupy(targetSlotIndex, customer);

            customer.MoveToEdgeSlot(
                GetEdgeSlotWorldPosition(targetSlotIndex),
                GetEdgeSlotYRotation(targetSlotIndex),
                GetRoomCenter(),
                onComplete: () => { boardGrid.SetEdgeCellFood(targetSlotIndex, customer.RequiredData); });

            EdgeCustomerReplacementStarted?.Invoke(customer, targetSlotIndex);
        }

        public Dictionary<ItemDataSO, int> GetRemainingLevelDemands()
        {
            return remainingDemandPerType;
        }

        public void ClearCrowd()
        {
            if (entranceSequenceCoroutine != null)
            {
                StopCoroutine(entranceSequenceCoroutine);
                entranceSequenceCoroutine = null;
            }

            entranceSequencer?.Stop();
            isEntranceSequenceActive = false;
            boardGrid?.ClearAllCellMaterials();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Customer customer = transform.GetChild(i).GetComponent<Customer>();
                if (customer != null) PoolManager.Instance.Despawn(customer.gameObject);
            }

            centralCrowd?.Clear();
            unspawnedDemandPool.Clear();
            entranceGate?.ResetGateImmediate();
        }

        public Vector3 GetEdgeSlotWorldPosition(int index)
        {
            if (boardGrid == null) boardGrid = GetComponent<DiningBoardGrid>();
            return boardGrid != null
                ? boardGrid.GetEdgeSlotCellPosition(index, GetSplineEdgeSlotWorldPosition(index))
                : GetSplineEdgeSlotWorldPosition(index);
        }

        public Vector3 GetSplineEdgeSlotWorldPosition(int index) => edgeSlots != null
            ? edgeSlots.GetSlotWorldPosition(index, GetConveyor(), conveyorBuilder, GetRoomCenter(), transform.position)
            : transform.position;

        public float GetEdgeSlotYRotation(int index) => Quaternion.LookRotation(GetEdgeSlotWorldPosition(index) - GetRoomCenter()).eulerAngles.y;

        public Vector3 GetOuterSpawnPosition() => EntrancePathUtility.GetOuterSpawnPosition(GetConveyorEntranceWorldPosition(), outerSpawnOffset);
        public Vector3 GetConveyorEntranceWorldPosition() => EntrancePathUtility.GetConveyorGapCenter(GetConveyor(), transform.position);

        public Customer CheckServiceForBeltItem(float distance, ItemDataSO data) =>
            edgeSlots != null ? edgeSlots.FindServiceCandidate(distance, data, GetConveyor()) : null;

        private ItemDataSO PopUnspawnedDemand()
        {
            if (unspawnedDemandPool.Count == 0) return null;
            ItemDataSO data = unspawnedDemandPool[0];
            unspawnedDemandPool.RemoveAt(0);
            return data;
        }

        private Customer GetCustomerPrefab(ItemDataSO data) => data != null && data.CustomerPrefab != null ? data.CustomerPrefab : customerPrefab;
        private ConveyorManager GetConveyor() => ConveyorManager.Instance != null ? ConveyorManager.Instance : FindFirstObjectByType<ConveyorManager>();

        private Vector3 GetRoomCenter()
        {
            EnsureBuilderReference();
            Vector3 center = conveyorBuilder != null ? conveyorBuilder.CenterPosition : transform.position;
            center.y = 0f;
            return center;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (showCrowdGizmos)
            {
                CentralCrowdService gizmoService = centralCrowd;
                if (gizmoService == null || gizmoService.Count == 0)
                {
                    gizmoService = new CentralCrowdService();
                    gizmoService.SetupLayout(
                        CrowdLayoutType.Rectangular,
                        previewCrowdCount,
                        GetRoomCenter(),
                        crowdCenterOffset,
                        innerCrowdArea,
                        0f,
                        minCustomerDistance
                    );
                }

                gizmoService.DrawGizmos(
                    CrowdLayoutType.Rectangular,
                    GetRoomCenter(),
                    crowdCenterOffset,
                    innerCrowdArea,
                    0f
                );
            }

            if (showToleranceGizmos)
            {
                EnsureBuilderReference();
                if (boardGrid == null) boardGrid = GetComponent<DiningBoardGrid>();

                if (!Application.isPlaying || edgeSlots == null)
                {
                    InitializeGridSplineMapping();
                }

                Vector2 cellSize = boardGrid != null ? boardGrid.GetBoardCellSize() : new Vector2(1.2f, 1.2f);
                Vector3 visualCellSize = new Vector3(cellSize.x * 0.82f, 0.04f, cellSize.y * 0.82f);

                for (int i = 0; i < TOTAL_EDGE_SLOTS; i++)
                {
                    Vector3 slotPos = GetEdgeSlotWorldPosition(i);

                    Gizmos.color = activeEdgeCellColor;
                    Gizmos.DrawWireCube(slotPos + Vector3.up * 0.02f, visualCellSize);

                    Gizmos.color = new Color(activeEdgeCellColor.r, activeEdgeCellColor.g, activeEdgeCellColor.b, 0.25f);
                    Gizmos.DrawCube(slotPos + Vector3.up * 0.02f, visualCellSize);
                }
            }
        }
#endif
    }
}