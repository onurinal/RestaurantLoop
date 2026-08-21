using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Manages inner crowd matrix grid and pulls the closest available inner customer to fill cleared edge slots.
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
            Customer bestCandidate = null;
            float minDelta = float.MaxValue;

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

                if (delta <= alignmentTolerance && delta < minDelta)
                {
                    minDelta = delta;
                    bestCandidate = candidate;
                }
            }

            return bestCandidate;
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

        /// <summary>
        /// Scans all inner non-edge slots to find the mathematically closest available inner customer and shifts them to the cleared edge slot.
        /// </summary>
        private void ShiftInnerCustomers(int servedRow, int servedCol)
        {
            int bestR = -1;
            int bestC = -1;
            float minSqrDist = float.MaxValue;

            // Search the entire inner core for the nearest unserved inner customer
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    if (!IsEdgeSlot(r, c))
                    {
                        Customer innerCustomer = grid[r, c];
                        if (innerCustomer != null && !innerCustomer.IsServed)
                        {
                            float sqrDist = (r - servedRow) * (r - servedRow) + (c - servedCol) * (c - servedCol);
                            if (sqrDist < minSqrDist)
                            {
                                minSqrDist = sqrDist;
                                bestR = r;
                                bestC = c;
                            }
                        }
                    }
                }
            }

            // Move the best candidate into the vacant edge slot
            if (bestR != -1 && bestC != -1)
            {
                Customer innerCustomer = grid[bestR, bestC];

                grid[servedRow, servedCol] = innerCustomer;
                grid[bestR, bestC] = null;

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