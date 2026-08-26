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
        [SerializeField] private int activeEdgeSlotCount = 6;
        [SerializeField] private float alignmentTolerance = 1.2f;
        [Tooltip("How far inside the conveyor the playable board begins.")]
        [SerializeField] private float edgeInwardOffset = 2.5f;

        [Header("Dining Board Grid")]
        [SerializeField] private FoodCell gridCellPrefab;
        [Min(1)] [SerializeField] private int boardColumns = 7;
        [Min(1)] [SerializeField] private int boardRows = 8;
        [SerializeField] private Vector2 boardInset = new Vector2(2.5f, 2.5f);
        [Range(0.5f, 1f)] [SerializeField] private float cellFill = 0.82f;
        [Tooltip("Keeps the floor cells just below customer feet while remaining above the restaurant floor.")]
        [SerializeField] private float gridCellYOffset = -0.005f;

        [Header("Inner Crowd Footprint")]
        [SerializeField] private Vector3 crowdCenterOffset = Vector3.zero;
        [Min(1)] [SerializeField] private int innerFootprintColumns = 3;
        [Min(1)] [SerializeField] private int innerFootprintRows = 4;
        [SerializeField] private float minCustomerDistance = 0.7f;
        [Range(10, 100)] [SerializeField] private int maxVisibleCrowdCount = 100;

        [Header("Entrance Sequence Setup")]
        [SerializeField] private float spawnInterval = 0.15f;
        [SerializeField] private float moveDuration = 1.2f;
        [SerializeField] private float pathJitterAmount = 0.5f;
        [SerializeField] private Vector3 outerSpawnOffset = new Vector3(0f, 0f, -3.0f);

        [Header("Customer Prefabs")]
        [SerializeField] private Customer customerPrefab;
        [Header("References")]
        [SerializeField] private ConveyorBuilder conveyorBuilder;

        private EdgeSlotService edgeSlots;
        private CentralCrowdService centralCrowd;
        private CustomerEntranceSequencer entranceSequencer;
        private Coroutine entranceSequenceCoroutine;
        private Transform gridVisualRoot;
        private readonly List<FoodCell> boardCells = new List<FoodCell>();
        private readonly List<Vector3> boardCellPositions = new List<Vector3>();
        private readonly List<int> edgeSlotCellIndices = new List<int>();
        private readonly List<ItemDataSO> unspawnedDemandPool = new List<ItemDataSO>();
        private readonly Dictionary<ItemDataSO, int> remainingDemandPerType = new Dictionary<ItemDataSO, int>();

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

        private void Start() => InitializeGridSplineMapping();

        private void OnValidate()
        {
#if UNITY_EDITOR
            SceneView.RepaintAll();
#endif
        }

        public void InitializeGridSplineMapping()
        {
            if (!Application.isPlaying || edgeSlots == null || edgeSlots.SlotCount != activeEdgeSlotCount)
                edgeSlots = new EdgeSlotService(activeEdgeSlotCount, alignmentTolerance, edgeInwardOffset);

            edgeSlots.RecalculateSplineMapping(GetConveyor());
            RecalculateBoardGrid();
            PositionBoardCells();
        }

        public void SetupCrowd(List<CustomerDemandConfig> demands)
        {
            ClearCrowd();
            InitializeGridSplineMapping();
            BuildBoardVisuals();

            TotalRemainingDemand = 0;
            remainingDemandPerType.Clear();
            foreach (var cfg in demands)
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
            centralCrowd.SetupLayout(CrowdLayoutType.Rectangular, centralCrowdCount, GetRoomCenter(), crowdCenterOffset,
                GetInnerFootprintSize(), 0f, minCustomerDistance);
            NotifyDemandChanged();

            if (entranceSequenceCoroutine != null) StopCoroutine(entranceSequenceCoroutine);
            entranceSequenceCoroutine = StartCoroutine(RunEntranceSequence());
        }

        private IEnumerator RunEntranceSequence()
        {
            yield return entranceSequencer.Run(PopUnspawnedDemand, GetCustomerPrefab, transform, GetOuterSpawnPosition(),
                GetConveyorEntranceWorldPosition(), GetRoomCenter(), edgeSlots, centralCrowd, maxVisibleCrowdCount,
                GetEdgeSlotWorldPosition, GetEdgeSlotYRotation, (customer, slotIndex) =>
                {
                    SetEdgeCellFood(slotIndex, customer.RequiredData);
                    EdgeCustomerReplacementStarted?.Invoke(customer, slotIndex);
                });
        }

        public void OnCustomerServed(Customer customer)
        {
            if (customer == null || !edgeSlots.TryGetSlotIndex(customer, out int slotIndex)) return;
            DecrementDemandForType(customer.RequiredData);
            edgeSlots.Release(slotIndex);
            ClearEdgeCell(slotIndex);
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
            SetEdgeCellFood(edgeSlotIndex, customer.RequiredData);
            customer.MoveToEdgeSlot(GetEdgeSlotWorldPosition(edgeSlotIndex), GetEdgeSlotYRotation(edgeSlotIndex), GetRoomCenter());
            EdgeCustomerReplacementStarted?.Invoke(customer, edgeSlotIndex);
            RevealHiddenCrowdCustomer();
        }

        private void RevealHiddenCrowdCustomer()
        {
            CentralCrowdSlot slot = centralCrowd.GetFirstHiddenOccupiedSlot();
            if (slot != null && slot.OccupyingCustomer != null) slot.OccupyingCustomer.gameObject.SetActive(true);
        }

        private ItemDataSO PopUnspawnedDemand()
        {
            if (unspawnedDemandPool.Count == 0) return null;
            ItemDataSO data = unspawnedDemandPool[0];
            unspawnedDemandPool.RemoveAt(0);
            return data;
        }

        private Customer GetCustomerPrefab(ItemDataSO data) => data != null && data.CustomerPrefab != null ? data.CustomerPrefab : customerPrefab;

        private void DecrementDemandForType(ItemDataSO data)
        {
            if (data == null) return;
            TotalRemainingDemand = Mathf.Max(0, TotalRemainingDemand - 1);
            if (remainingDemandPerType.ContainsKey(data)) remainingDemandPerType[data] = Mathf.Max(0, remainingDemandPerType[data] - 1);
            NotifyDemandChanged();
        }

        private void NotifyDemandChanged() => OnDemandChanged?.Invoke(TotalRemainingDemand, remainingDemandPerType);

        public void ClearCrowd()
        {
            ClearBoardCellMaterials();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Customer customer = transform.GetChild(i).GetComponent<Customer>();
                if (customer != null) PoolManager.Instance.Despawn(customer.gameObject);
            }
            centralCrowd?.Clear();
            unspawnedDemandPool.Clear();
        }

        public float GetEdgeSlotYRotation(int index)
        {
            Vector3 direction = GetEdgeSlotWorldPosition(index) - GetRoomCenter();
            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f ? Quaternion.LookRotation(direction).eulerAngles.y : 0f;
        }

        public Vector3 GetOuterSpawnPosition() => EntrancePathUtility.GetOuterSpawnPosition(GetConveyorEntranceWorldPosition(), outerSpawnOffset);
        public Vector3 GetConveyorEntranceWorldPosition() => EntrancePathUtility.GetConveyorGapCenter(GetConveyor(), transform.position);

        public Vector3 GetEdgeSlotWorldPosition(int index)
        {
            if (index >= 0 && index < edgeSlotCellIndices.Count)
            {
                int boardIndex = edgeSlotCellIndices[index];
                if (boardIndex >= 0 && boardIndex < boardCellPositions.Count) return boardCellPositions[boardIndex];
            }
            return GetSplineEdgeSlotWorldPosition(index);
        }

        public Customer CheckServiceForBeltItem(float distance, ItemDataSO data) => edgeSlots.FindServiceCandidate(distance, data, GetConveyor());
        private ConveyorManager GetConveyor() => ConveyorManager.Instance != null ? ConveyorManager.Instance : FindFirstObjectByType<ConveyorManager>();

        private Vector3 GetRoomCenter()
        {
            Vector3 center = conveyorBuilder != null ? conveyorBuilder.CenterPosition : transform.position;
            center.y = 0f;
            return center;
        }

        private Vector3 GetSplineEdgeSlotWorldPosition(int index)
        {
            Vector3 position = edgeSlots.GetSlotWorldPosition(index, GetConveyor(), GetRoomCenter(), transform.position);
            position.y = 0f;
            return position;
        }

        private Vector2 GetBoardWorldSize()
        {
            float width = conveyorBuilder != null ? conveyorBuilder.Width : 10f;
            float height = conveyorBuilder != null ? conveyorBuilder.Height : 15f;
            return new Vector2(Mathf.Max(0.1f, width - boardInset.x * 2f), Mathf.Max(0.1f, height - boardInset.y * 2f));
        }

        private Vector2 GetBoardCellSize()
        {
            Vector2 size = GetBoardWorldSize();
            return new Vector2(size.x / Mathf.Max(1, boardColumns), size.y / Mathf.Max(1, boardRows));
        }

        private Vector2 GetInnerFootprintSize()
        {
            Vector2 cellSize = GetBoardCellSize();
            return new Vector2(innerFootprintColumns * cellSize.x, innerFootprintRows * cellSize.y);
        }

        private void RecalculateBoardGrid()
        {
            boardCellPositions.Clear();
            edgeSlotCellIndices.Clear();
            Vector2 boardSize = GetBoardWorldSize();
            Vector2 cellSize = GetBoardCellSize();
            Vector3 bottomLeft = GetRoomCenter() - new Vector3(boardSize.x * 0.5f, 0f, boardSize.y * 0.5f);

            for (int row = 0; row < boardRows; row++)
            for (int column = 0; column < boardColumns; column++)
                boardCellPositions.Add(bottomLeft + new Vector3((column + 0.5f) * cellSize.x, 0f, (row + 0.5f) * cellSize.y));

            List<int> perimeter = new List<int>();
            for (int row = 0; row < boardRows; row++)
            for (int column = 0; column < boardColumns; column++)
                if (row == 0 || row == boardRows - 1 || column == 0 || column == boardColumns - 1)
                    perimeter.Add(ToBoardIndex(column, row));

            for (int slot = 0; slot < edgeSlots.SlotCount; slot++)
            {
                int candidateListIndex = FindNearestCandidate(GetSplineEdgeSlotWorldPosition(slot), perimeter);
                edgeSlotCellIndices.Add(candidateListIndex >= 0 ? perimeter[candidateListIndex] : -1);
                if (candidateListIndex >= 0) perimeter.RemoveAt(candidateListIndex);
            }
        }

        private int FindNearestCandidate(Vector3 target, List<int> candidates)
        {
            int bestListIndex = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                Vector3 delta = boardCellPositions[candidates[i]] - target;
                delta.y = 0f;
                if (delta.sqrMagnitude < bestDistance)
                {
                    bestDistance = delta.sqrMagnitude;
                    bestListIndex = i;
                }
            }
            return bestListIndex;
        }

        private int ToBoardIndex(int column, int row) => row * boardColumns + column;

        private void BuildBoardVisuals()
        {
            DestroyBoardVisuals();
            if (gridCellPrefab == null) return;
            gridVisualRoot = new GameObject("Dining Board Grid").transform;
            gridVisualRoot.SetParent(transform, false);
            Vector2 cellSize = GetBoardCellSize() * cellFill;

            for (int i = 0; i < boardCellPositions.Count; i++)
            {
                FoodCell cell = Instantiate(gridCellPrefab, gridVisualRoot);
                cell.name = $"Cell {i % boardColumns + 1}-{i / boardColumns + 1}";
                cell.transform.position = boardCellPositions[i] + Vector3.up * gridCellYOffset;
                cell.transform.rotation = Quaternion.identity;
                cell.transform.localScale = Vector3.Scale(cell.transform.localScale, new Vector3(cellSize.x, 1f, cellSize.y));
                cell.Clear();
                boardCells.Add(cell);
            }
        }

        private void DestroyBoardVisuals()
        {
            boardCells.Clear();
            if (gridVisualRoot != null)
            {
                Destroy(gridVisualRoot.gameObject);
                gridVisualRoot = null;
            }
        }

        private void PositionBoardCells()
        {
            for (int i = 0; i < boardCells.Count && i < boardCellPositions.Count; i++)
                if (boardCells[i] != null) boardCells[i].transform.position = boardCellPositions[i] + Vector3.up * gridCellYOffset;
        }

        private void SetEdgeCellFood(int slotIndex, ItemDataSO itemData)
        {
            if (slotIndex < 0 || slotIndex >= edgeSlotCellIndices.Count) return;
            int boardIndex = edgeSlotCellIndices[slotIndex];
            if (boardIndex >= 0 && boardIndex < boardCells.Count) boardCells[boardIndex]?.SetFood(itemData);
        }

        private void ClearEdgeCell(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= edgeSlotCellIndices.Count) return;
            int boardIndex = edgeSlotCellIndices[slotIndex];
            if (boardIndex >= 0 && boardIndex < boardCells.Count) boardCells[boardIndex]?.Clear();
        }

        private void ClearBoardCellMaterials()
        {
            foreach (FoodCell cell in boardCells) cell?.Clear();
        }

        private void OnDrawGizmos()
        {
            InitializeGridSplineMapping();
            Vector2 size = GetBoardWorldSize();
            Gizmos.color = new Color(1f, 0.75f, 0.15f, 0.5f);
            Gizmos.DrawWireCube(GetRoomCenter(), new Vector3(size.x, 0.05f, size.y));
        }
    }
}
