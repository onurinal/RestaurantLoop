using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Core
{
    public enum CrowdLayoutType
    {
        Rectangular,
        Circular
    }

    public class CentralCrowdSlot
    {
        public Vector3 Position;
        public float YRotation;
        public Customer OccupyingCustomer;
        public bool IsOccupied => OccupyingCustomer != null;
        public bool IsVisible => OccupyingCustomer != null && OccupyingCustomer.gameObject.activeSelf;
    }

    /// <summary>
    /// Handles organic layout generation for both rectangular and circular shapes with collision guards and fixed seeds.
    /// </summary>
    public class CentralCrowdService
    {
        private readonly List<CentralCrowdSlot> slots = new List<CentralCrowdSlot>();

        public IReadOnlyList<CentralCrowdSlot> Slots => slots;
        public int Count => slots.Count;

        public void SetupLayout(CrowdLayoutType layoutType, int totalCount, Vector3 center, Vector3 offset, Vector2 areaSize, float radius, float minDistance,
            int seed = 12345)
        {
            slots.Clear();
            Vector3 targetCenter = center + offset;

            Random.State previousState = Random.state;
            Random.InitState(seed);

            if (layoutType == CrowdLayoutType.Rectangular)
            {
                GenerateRectangular(totalCount, targetCenter, areaSize, minDistance);
            }
            else
            {
                GenerateCircular(totalCount, targetCenter, radius, minDistance);
            }

            Random.state = previousState;
        }

        private void GenerateRectangular(int totalCount, Vector3 targetCenter, Vector2 areaSize, float minDistance)
        {
            int maxAttemptsPerPoint = 50;

            for (int i = 0; i < totalCount; i++)
            {
                Vector3 candidatePos = Vector3.zero;
                bool validPositionFound = false;

                for (int attempt = 0; attempt < maxAttemptsPerPoint; attempt++)
                {
                    float rx = Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f);
                    float rz = Random.Range(-areaSize.y * 0.5f, areaSize.y * 0.5f);
                    candidatePos = targetCenter + new Vector3(rx, 0f, rz);

                    if (IsPositionValid(candidatePos, minDistance))
                    {
                        validPositionFound = true;
                        break;
                    }
                }

                if (!validPositionFound)
                {
                    float rx = Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f);
                    float rz = Random.Range(-areaSize.y * 0.5f, areaSize.y * 0.5f);
                    candidatePos = targetCenter + new Vector3(rx, 0f, rz);
                }

                slots.Add(new CentralCrowdSlot
                {
                    Position = candidatePos,
                    YRotation = Random.Range(0f, 360f),
                    OccupyingCustomer = null
                });
            }
        }

        private void GenerateCircular(int totalCount, Vector3 targetCenter, float radius, float minDistance)
        {
            int maxAttemptsPerPoint = 50;

            for (int i = 0; i < totalCount; i++)
            {
                Vector3 candidatePos = Vector3.zero;
                bool validPositionFound = false;

                for (int attempt = 0; attempt < maxAttemptsPerPoint; attempt++)
                {
                    float dist = radius * Mathf.Sqrt(Random.Range(0.05f, 1f));
                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    candidatePos = targetCenter + new Vector3(dist * Mathf.Cos(angle), 0f, dist * Mathf.Sin(angle));

                    if (IsPositionValid(candidatePos, minDistance))
                    {
                        validPositionFound = true;
                        break;
                    }
                }

                if (!validPositionFound)
                {
                    float dist = radius * Mathf.Sqrt(Random.Range(0.05f, 1f));
                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    candidatePos = targetCenter + new Vector3(dist * Mathf.Cos(angle), 0f, dist * Mathf.Sin(angle));
                }

                slots.Add(new CentralCrowdSlot
                {
                    Position = candidatePos,
                    YRotation = Random.Range(0f, 360f),
                    OccupyingCustomer = null
                });
            }
        }

        private bool IsPositionValid(Vector3 pos, float minDist)
        {
            float sqrMinDist = minDist * minDist;
            foreach (var slot in slots)
            {
                if ((slot.Position - pos).sqrMagnitude < sqrMinDist)
                {
                    return false;
                }
            }

            return true;
        }

        public CentralCrowdSlot GetRandomVisibleSlot()
        {
            List<CentralCrowdSlot> visibleSlots = slots.FindAll(s => s.IsVisible);
            if (visibleSlots.Count == 0) return null;

            return visibleSlots[Random.Range(0, visibleSlots.Count)];
        }

        public CentralCrowdSlot GetFirstHiddenOccupiedSlot()
        {
            return slots.Find(s => s.IsOccupied && !s.IsVisible);
        }

        public void Clear()
        {
            slots.Clear();
        }

        public void DrawGizmos(CrowdLayoutType layoutType, Vector3 center, Vector3 offset, Vector2 areaSize, float radius)
        {
            Vector3 targetCenter = center + offset;

            Gizmos.color = Color.magenta;
            if (layoutType == CrowdLayoutType.Rectangular)
            {
                Gizmos.DrawWireCube(targetCenter, new Vector3(areaSize.x, 0.1f, areaSize.y));
            }
            else
            {
                DrawWireCircle(targetCenter, radius);
            }

            foreach (var slot in slots)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(slot.Position, 0.25f);

                Vector3 forward = Quaternion.Euler(0f, slot.YRotation, 0f) * Vector3.forward;
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(slot.Position, forward * 0.5f);
            }
        }

        private static void DrawWireCircle(Vector3 center, float radius, int segments = 32)
        {
            float step = (Mathf.PI * 2f) / segments;
            Vector3 lastPoint = center + new Vector3(radius, 0f, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * step;
                Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(lastPoint, nextPoint);
                lastPoint = nextPoint;
            }
        }
    }
}