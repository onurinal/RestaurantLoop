using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RestaurantLoop.Core
{
    public class DiningBoardGrid : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ConveyorBuilder conveyorBuilder;
        [SerializeField] private FoodCell gridCellPrefab;

        [Header("Cell Dimension & Safety Limits")]
        [Min(2)] [SerializeField] private int boardColumns = 7;
        [Min(2)] [SerializeField] private int boardRows = 8;
        [SerializeField] private Vector2 boardInset = new Vector2(1.2f, 1.2f);
        [Range(0.1f, 1f)] [SerializeField] private float cellFill = 0.82f;
        [SerializeField] private float gridCellYOffset = 0f;

        [Header("Gizmo Settings")]
        [SerializeField] private bool showGridGizmos = true;
        [SerializeField] private Color activeEdgeCellColor = new Color(0f, 1f, 0.3f, 0.9f);
        [SerializeField] private Color cellWireGizmoColor = new Color(0f, 0.8f, 1f, 0.8f);

        private readonly List<FoodCell> activeEdgeCells = new List<FoodCell>();
        private readonly List<Vector3> boardCellPositions = new List<Vector3>();
        private readonly List<int> edgeSlotCellIndices = new List<int>();
        private Transform gridVisualRoot;

        public Vector3 GetCellPosition(int index) => (index >= 0 && index < boardCellPositions.Count) ? boardCellPositions[index] : Vector3.zero;

        private void Awake() => EnsureConveyorReference();

        private void OnValidate()
        {
            EnsureConveyorReference();
            boardColumns = Mathf.Max(2, boardColumns);
            boardRows = Mathf.Max(2, boardRows);
            cellFill = Mathf.Clamp(cellFill, 0.1f, 1f);

#if UNITY_EDITOR
            SceneView.RepaintAll();
#endif
        }

        private void EnsureConveyorReference()
        {
            if (conveyorBuilder == null) conveyorBuilder = FindFirstObjectByType<ConveyorBuilder>();
        }

        public void InitializeGrid(Vector3 roomCenter, Vector2 conveyorBounds, int edgeSlotCount, System.Func<int, Vector3> getSplinePosition)
        {
            EnsureConveyorReference();
            RecalculateGrid(roomCenter, conveyorBounds, edgeSlotCount, getSplinePosition);

            if (Application.isPlaying)
            {
                BuildBoardVisuals();
            }
        }

        public Vector3 GetEdgeSlotCellPosition(int slotIndex, Vector3 fallbackPos)
        {
            if (slotIndex >= 0 && slotIndex < edgeSlotCellIndices.Count)
            {
                int boardIndex = edgeSlotCellIndices[slotIndex];
                if (boardIndex >= 0 && boardIndex < boardCellPositions.Count) return boardCellPositions[boardIndex];
            }

            return fallbackPos;
        }

        public void SetEdgeCellFood(int slotIndex, ItemDataSO itemData)
        {
            if (slotIndex >= 0 && slotIndex < activeEdgeCells.Count)
                activeEdgeCells[slotIndex]?.SetFood(itemData);
        }

        public void ClearEdgeCell(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < activeEdgeCells.Count)
                activeEdgeCells[slotIndex]?.Clear();
        }

        public void ClearAllCellMaterials()
        {
            foreach (var cell in activeEdgeCells) cell?.Clear();
        }

        public Vector2 GetBoardAreaSize()
        {
            EnsureConveyorReference();
            Vector2 bounds = conveyorBuilder != null ? new Vector2(conveyorBuilder.Width, conveyorBuilder.Height) : new Vector2(10f, 15f);
            return new Vector2(Mathf.Max(0.1f, bounds.x - boardInset.x * 2f), Mathf.Max(0.1f, bounds.y - boardInset.y * 2f));
        }

        public Vector2 GetBoardCellSize()
        {
            Vector2 boardArea = GetBoardAreaSize();
            return new Vector2(boardArea.x / boardColumns, boardArea.y / boardRows);
        }

        private void RecalculateGrid(Vector3 roomCenter, Vector2 conveyorBounds, int edgeSlotCount, System.Func<int, Vector3> getSplinePosition)
        {
            boardCellPositions.Clear();
            edgeSlotCellIndices.Clear();

            Vector2 boardSize = GetBoardAreaSize();
            Vector2 cellSize = GetBoardCellSize();
            Vector3 bottomLeft = roomCenter - new Vector3(boardSize.x * 0.5f, 0f, boardSize.y * 0.5f);

            for (int row = 0; row < boardRows; row++)
            for (int col = 0; col < boardColumns; col++)
                boardCellPositions.Add(bottomLeft + new Vector3((col + 0.5f) * cellSize.x, 0f, (row + 0.5f) * cellSize.y));

            if (getSplinePosition == null) return;

            List<int> perimeter = GetPerimeterIndices();

            for (int slot = 0; slot < edgeSlotCount; slot++)
            {
                Vector3 target = getSplinePosition(slot);
                int nearestIdx = FindNearestCandidate(target, perimeter);
                edgeSlotCellIndices.Add(nearestIdx >= 0 ? perimeter[nearestIdx] : -1);
                if (nearestIdx >= 0) perimeter.RemoveAt(nearestIdx);
            }
        }

        private List<int> GetPerimeterIndices()
        {
            List<int> perimeter = new List<int>();
            for (int r = 0; r < boardRows; r++)
            for (int c = 0; c < boardColumns; c++)
                if (r == 0 || r == boardRows - 1 || c == 0 || c == boardColumns - 1)
                    perimeter.Add(r * boardColumns + c);
            return perimeter;
        }

        private int FindNearestCandidate(Vector3 target, List<int> candidates)
        {
            int bestIndex = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                Vector3 delta = boardCellPositions[candidates[i]] - target;
                delta.y = 0f;
                if (delta.sqrMagnitude < bestDist)
                {
                    bestDist = delta.sqrMagnitude;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private void BuildBoardVisuals()
        {
            if (gridVisualRoot != null) Destroy(gridVisualRoot.gameObject);
            activeEdgeCells.Clear();
            if (gridCellPrefab == null) return;

            gridVisualRoot = new GameObject("Dining Board Active Cells").transform;
            gridVisualRoot.SetParent(transform, false);
            Vector2 cellSize = GetBoardCellSize() * cellFill;

            for (int slot = 0; slot < edgeSlotCellIndices.Count; slot++)
            {
                int boardIndex = edgeSlotCellIndices[slot];
                if (boardIndex < 0 || boardIndex >= boardCellPositions.Count) continue;

                FoodCell cell = Instantiate(gridCellPrefab, gridVisualRoot);
                cell.name = $"ActiveEdgeCell_Slot_{slot}";
                cell.transform.position = boardCellPositions[boardIndex] + Vector3.up * gridCellYOffset;
                cell.transform.localScale = Vector3.Scale(cell.transform.localScale, new Vector3(cellSize.x, 1f, cellSize.y));
                cell.Clear();
                activeEdgeCells.Add(cell);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!showGridGizmos) return;

            EnsureConveyorReference();

            Vector3 center = conveyorBuilder != null ? conveyorBuilder.CenterPosition : transform.position;
            center.y = 0f;

            Vector2 boardSize = GetBoardAreaSize();
            Vector2 cellSize = GetBoardCellSize();
            Vector3 bottomLeft = center - new Vector3(boardSize.x * 0.5f, 0f, boardSize.y * 0.5f);

            Vector2 filledCellSize = cellSize * cellFill;
            Vector3 visualCellSize = new Vector3(filledCellSize.x, 0.04f, filledCellSize.y);

            List<int> perimeter = GetPerimeterIndices();

            for (int i = 0; i < perimeter.Count; i++)
            {
                int boardIndex = perimeter[i];
                int r = boardIndex / boardColumns;
                int c = boardIndex % boardColumns;

                Vector3 pos = bottomLeft + new Vector3((c + 0.5f) * cellSize.x, gridCellYOffset, (r + 0.5f) * cellSize.y);

                Gizmos.color = activeEdgeCellColor;
                Gizmos.DrawCube(pos, visualCellSize);

                Gizmos.color = cellWireGizmoColor;
                Gizmos.DrawWireCube(pos, visualCellSize);
            }
        }
#endif
    }
}