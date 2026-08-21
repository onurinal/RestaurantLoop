using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Manages inner crowd matrix grid and shifts EXACTLY ONE inner customer outward toward cleared edge slots.
    /// </summary>
    public class CrowdManager : MonoBehaviour
    {
        public static CrowdManager Instance { get; private set; }

        [Header("Grid Setup")]
        [SerializeField] private int columns = 5;
        [SerializeField] private int rows = 5;

        [Header("Mechanics")]
        [SerializeField] private bool enableRowShift = true;
        [SerializeField] private float alignmentTolerance = 1.2f;

        [Header("Margins")]
        [SerializeField] private float marginX = 3.5f;
        [SerializeField] private float marginZ = 4.0f;

        [Header("References")]
        [SerializeField] private ConveyorBuilder conveyorBuilder;
        [SerializeField] private SplineConveyorPath conveyorPath;

        private Customer[,] grid;
        private float[,] slotSplineDistances;
        private readonly List<Vector2Int> edgeCoordinates = new List<Vector2Int>();

        public int Columns => columns;
        public int Rows => rows;

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

            grid = new Customer[rows, columns];
            slotSplineDistances = new float[rows, columns];
            CacheEdgeCoordinates();
        }

        private void Start()
        {
            InitializeGridSplineMapping();
        }

        /// <summary>
        /// True if the given grid coordinate sits on the outer ring of the crowd grid.
        /// Only edge slots are servable, so this is the single source of truth for that check.
        /// </summary>
        private bool IsEdgeSlot(int row, int col)
        {
            return row == 0 || row == rows - 1 || col == 0 || col == columns - 1;
        }

        private void CacheEdgeCoordinates()
        {
            edgeCoordinates.Clear();

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    if (IsEdgeSlot(r, c))
                    {
                        edgeCoordinates.Add(new Vector2Int(r, c));
                    }
                }
            }
        }

        public void InitializeGridSplineMapping()
        {
            if (conveyorPath == null)
            {
                conveyorPath = FindFirstObjectByType<SplineConveyorPath>();
            }

            if (conveyorPath == null || conveyorPath.Length <= 0f)
            {
                return;
            }

            float pathLength = conveyorPath.Length;

            foreach (Vector2Int coord in edgeCoordinates)
            {
                Vector3 slotWorldPos = GetSlotWorldPosition(coord.x, coord.y);
                slotSplineDistances[coord.x, coord.y] = FindClosestDistanceOnSpline(slotWorldPos, pathLength);
            }
        }

        public Customer CheckServiceForBeltItem(float itemSplineDistance, ItemDataSO itemData)
        {
            if (conveyorPath == null)
            {
                return null;
            }

            float pathLength = conveyorPath.Length;

            foreach (Vector2Int coord in edgeCoordinates)
            {
                int r = coord.x;
                int c = coord.y;

                Customer candidate = grid[r, c];
                if (candidate == null || candidate.IsServed || !candidate.IsEdgeCustomer)
                {
                    continue;
                }

                if (candidate.RequiredData != itemData)
                {
                    continue;
                }

                float targetSplineDistance = slotSplineDistances[r, c];
                float delta = Mathf.Abs(itemSplineDistance - targetSplineDistance);

                if (delta > pathLength * 0.5f)
                {
                    delta = pathLength - delta;
                }

                if (delta <= alignmentTolerance)
                {
                    return candidate;
                }
            }

            return null;
        }

        public void RegisterCustomer(Customer customer, int row, int col)
        {
            if (row < 0 || row >= rows || col < 0 || col >= columns)
            {
                return;
            }

            grid[row, col] = customer;
            customer.SetEdgeStatus(IsEdgeSlot(row, col));
        }

        public void OnCustomerServed(Customer customer)
        {
            if (customer == null)
            {
                return;
            }

            int targetRow = -1;
            int targetCol = -1;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    if (grid[r, c] == customer)
                    {
                        targetRow = r;
                        targetCol = c;
                        break;
                    }
                }
            }

            if (targetRow != -1 && targetCol != -1)
            {
                grid[targetRow, targetCol] = null;

                if (enableRowShift)
                {
                    ShiftInnerCustomers(targetRow, targetCol);
                }
            }
        }

        private void ShiftInnerCustomers(int servedRow, int servedCol)
        {
            int innerR = servedRow;
            int innerC = servedCol;

            // Target the adjacent inner slot opposite to the cleared edge
            if (servedRow == 0) innerR = 1;
            else if (servedRow == rows - 1) innerR = rows - 2;

            if (servedCol == 0) innerC = 1;
            else if (servedCol == columns - 1) innerC = columns - 2;

            // Strictly verify that the source position is an inner slot (NOT an edge slot)
            bool isTargetInner = (innerR > 0 && innerR < rows - 1 && innerC > 0 && innerC < columns - 1);

            if (!isTargetInner)
            {
                return; // Grid has no inner layers or target is an edge slot
            }

            Customer innerCustomer = grid[innerR, innerC];

            // Shift ONLY the single inner customer to the edge slot
            if (innerCustomer != null && !innerCustomer.IsServed)
            {
                grid[servedRow, servedCol] = innerCustomer;
                grid[innerR, innerC] = null;

                innerCustomer.SetEdgeStatus(true);

                Vector3 targetWorldPos = GetSlotWorldPosition(servedRow, servedCol);
                innerCustomer.transform.DOMove(targetWorldPos, 0.3f);
            }
        }

        public Vector3 GetSlotWorldPosition(int row, int col)
        {
            GetInnerBounds(out float minX, out float maxX, out float minZ, out float maxZ);

            float tx = columns > 1 ? (float)col / (columns - 1) : 0.5f;
            float tz = rows > 1 ? (float)row / (rows - 1) : 0.5f;

            float xPos = Mathf.Lerp(minX, maxX, tx);
            float zPos = Mathf.Lerp(minZ, maxZ, tz);

            return new Vector3(xPos, transform.position.y, zPos);
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
            Vector3 center = transform.position;

            if (conveyorBuilder != null)
            {
                center = conveyorBuilder.CenterPosition;
            }

            float halfW = (conveyorBuilder != null ? conveyorBuilder.Width * 0.5f : 7.5f) - marginX;
            float halfH = (conveyorBuilder != null ? conveyorBuilder.Height * 0.5f : 10f) - marginZ;

            minX = center.x - halfW;
            maxX = center.x + halfW;
            minZ = center.z - halfH;
            maxZ = center.z + halfH;
        }

        private void OnDrawGizmosSelected()
        {
            GetInnerBounds(out float minX, out float maxX, out float minZ, out float maxZ);

            Vector3 center = new Vector3((minX + maxX) * 0.5f, transform.position.y, (minZ + maxZ) * 0.5f);
            Vector3 size = new Vector3(maxX - minX, 0.1f, maxZ - minZ);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(center, size);

            Gizmos.color = Color.cyan;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    Vector3 slotPos = GetSlotWorldPosition(r, c);
                    Gizmos.DrawWireSphere(slotPos, 0.35f);
                }
            }
        }
    }
}