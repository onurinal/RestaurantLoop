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
        private int activeEdgeSlotCount = 6;

        private readonly List<ItemDataSO> unspawnedDemandPool = new List<ItemDataSO>();
        private readonly Dictionary<ItemDataSO, int> remainingDemandPerType = new Dictionary<ItemDataSO, int>();

        public int ActiveEdgeSlotCount => activeEdgeSlotCount;
        public float AlignmentTolerance => alignmentTolerance;
        public float EdgeInwardOffset => edgeInwardOffset;

        public bool IsSpawningCustomers => entranceSequencer != null && entranceSequencer.IsRunning;
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
            edgeSlots = new EdgeSlotService(activeEdgeSlotCount, alignmentTolerance, edgeInwardOffset);
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

            edgeSlots = new EdgeSlotService(activeEdgeSlotCount, alignmentTolerance, edgeInwardOffset);

            ConveyorManager conveyor = GetConveyor();
            edgeSlots.RecalculateSplineMapping(conveyor, conveyorBuilder);

            Vector2 bounds = conveyorBuilder != null ? new Vector2(conveyorBuilder.Width, conveyorBuilder.Height) : new Vector2(10f, 15f);
            boardGrid.InitializeGrid(GetRoomCenter(), bounds, activeEdgeSlotCount, GetSplineEdgeSlotWorldPosition);
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

            if (entranceSequenceCoroutine != null) StopCoroutine(entranceSequenceCoroutine);
            entranceSequenceCoroutine = StartCoroutine(RunEntranceSequence());
        }

        private IEnumerator RunEntranceSequence()
        {
            if (entranceGate != null)
            {
                entranceGate.ResetGateImmediate();
                entranceGate.OpenGate();
                yield return new WaitForSeconds(gateOpenDelay);
            }

            yield return entranceSequencer.Run(PopUnspawnedDemand, GetCustomerPrefab, transform, GetOuterSpawnPosition(),
                GetConveyorEntranceWorldPosition(), GetRoomCenter(), edgeSlots, centralCrowd, maxVisibleCrowdCount,
                GetEdgeSlotWorldPosition, GetEdgeSlotYRotation, (customer, slotIndex) =>
                {
                    boardGrid.SetEdgeCellFood(slotIndex, customer.RequiredData);
                    EdgeCustomerReplacementStarted?.Invoke(customer, slotIndex);
                });

            if (entranceGate != null)
            {
                entranceGate.CloseGate();
            }
        }

        public void OnCustomerServed(Customer customer)
        {
            if (customer == null || !edgeSlots.TryGetSlotIndex(customer, out int slotIndex)) return;
            DecrementDemandForType(customer.RequiredData);
            edgeSlots.Release(slotIndex);
            boardGrid.ClearEdgeCell(slotIndex);
            CustomerExitCompleted?.Invoke(customer, slotIndex);
            PromoteCrowdToEdgeSlot(slotIndex);
        }

        private void PromoteCrowdToEdgeSlot(int edgeSlotIndex)
        {
            CentralCrowdSlot visibleSlot = centralCrowd.GetRandomVisibleSlot();
            if (visibleSlot == null || visibleSlot.OccupyingCustomer == null) return;

            Customer customer = visibleSlot.OccupyingCustomer;
            visibleSlot.OccupyingCustomer = null;
            edgeSlots.Occupy(edgeSlotIndex, customer);
            boardGrid.SetEdgeCellFood(edgeSlotIndex, customer.RequiredData);

            customer.MoveToEdgeSlot(GetEdgeSlotWorldPosition(edgeSlotIndex), GetEdgeSlotYRotation(edgeSlotIndex), GetRoomCenter());

            EdgeCustomerReplacementStarted?.Invoke(customer, edgeSlotIndex);
        }

        public void ClearCrowd()
        {
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

        private void DecrementDemandForType(ItemDataSO data)
        {
            if (data == null) return;
            TotalRemainingDemand = Mathf.Max(0, TotalRemainingDemand - 1);
            OnDemandChanged?.Invoke(TotalRemainingDemand, remainingDemandPerType);
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
                LevelManager lm = LevelManager.Instance != null ? LevelManager.Instance : FindFirstObjectByType<LevelManager>();
                int drawCount = activeEdgeSlotCount;

                if (lm != null && lm.CurrentLevel != null)
                {
                    drawCount = lm.CurrentLevel.activeEdgeSlotCount;
                }

                if (boardGrid == null) boardGrid = GetComponent<DiningBoardGrid>();

                Vector2 cellSize = boardGrid != null ? boardGrid.GetBoardCellSize() : new Vector2(1.2f, 1.2f);
                Vector3 visualCellSize = new Vector3(cellSize.x * 0.82f, 0.04f, cellSize.y * 0.82f);

                for (int i = 0; i < drawCount; i++)
                {
                    Vector3 slotPos = GetEdgeSlotWorldPosition(i);

                    Gizmos.color = activeEdgeCellColor;
                    Gizmos.DrawWireCube(slotPos + Vector3.up * 0.02f, visualCellSize + new Vector3(0.02f, 0.02f, 0.02f));

                    Gizmos.color = new Color(activeEdgeCellColor.r, activeEdgeCellColor.g, activeEdgeCellColor.b, 0.35f);
                    Gizmos.DrawCube(slotPos + Vector3.up * 0.02f, visualCellSize);
                }
            }
        }
#endif
    }
}