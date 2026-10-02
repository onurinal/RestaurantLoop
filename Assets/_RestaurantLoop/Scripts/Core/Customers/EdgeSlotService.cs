using System.Collections.Generic;
using UnityEngine;
using Dreamteck.Splines;

namespace RestaurantLoop.Core
{
    public class EdgeSlotService
    {
        public const int TOTAL_EDGE_SLOTS = 20;

        private readonly int slotCount;
        private readonly float alignmentTolerance;
        private readonly float edgeInwardOffset;

        private readonly Customer[] slots;
        private float[] slotSplineDistances;

        public int SlotCount => slotCount;

        public EdgeSlotService(int slotCount, float alignmentTolerance, float edgeInwardOffset)
        {
            this.slotCount = Mathf.Max(1, slotCount);
            this.alignmentTolerance = alignmentTolerance;
            this.edgeInwardOffset = edgeInwardOffset;

            slots = new Customer[this.slotCount];
            slotSplineDistances = new float[this.slotCount];
        }

        public Customer GetCustomerInSlot(int index)
        {
            if (index >= 0 && index < slotCount) return slots[index];
            return null;
        }

        public List<int> GetUnoccupiedSlotIndices()
        {
            List<int> freeIndices = new List<int>();
            for (int i = 0; i < slotCount; i++)
            {
                if (slots[i] == null)
                {
                    freeIndices.Add(i);
                }
            }

            return freeIndices;
        }

        public void RecalculateSplineMapping(ConveyorManager conveyor, ConveyorBuilder builder = null)
        {
            float pathLength = 0f;
            float startDist = 0f;
            float validTravelLength = 0f;
            bool isClockwise = true;

            if (conveyor != null && conveyor.Path != null && conveyor.Path.Length > 0f)
            {
                pathLength = conveyor.Path.Length;
                startDist = conveyor.EntranceDistance;
                validTravelLength = conveyor.GetRequiredTravelDistance();
                isClockwise = conveyor.IsClockwise;
            }
            else if (builder != null && builder.Spline != null)
            {
                pathLength = (float)builder.Spline.CalculateLength();
                startDist = 0f;
                validTravelLength = pathLength;
                isClockwise = true;
            }

            if (pathLength <= 0f || validTravelLength <= 0f) return;

            if (slotSplineDistances == null || slotSplineDistances.Length != slotCount)
            {
                slotSplineDistances = new float[slotCount];
            }

            float step = validTravelLength / (slotCount + 1);

            for (int i = 0; i < slotCount; i++)
            {
                float travelOffset = step * (i + 1);
                float splineDist = (startDist + (isClockwise ? travelOffset : -travelOffset)) % pathLength;
                if (splineDist < 0f) splineDist += pathLength;

                slotSplineDistances[i] = splineDist;
            }
        }

        public Vector3 GetSlotWorldPosition(int index, ConveyorManager conveyor, ConveyorBuilder builder, Vector3 roomCenter, Vector3 fallbackPosition)
        {
            if (index < 0 || index >= slotCount) return fallbackPosition;

            if (slotSplineDistances == null || slotSplineDistances.Length != slotCount)
            {
                RecalculateSplineMapping(conveyor, builder);
            }

            Vector3 beltPoint = Vector3.zero;
            bool hasPosition = false;

            if (conveyor != null && conveyor.Path != null && conveyor.Path.Length > 0f)
            {
                beltPoint = conveyor.Path.GetPosition(slotSplineDistances[index]);
                hasPosition = true;
            }
            else if (builder != null && builder.Spline != null)
            {
                float pathLength = (float)builder.Spline.CalculateLength();
                if (pathLength > 0f)
                {
                    double percent = builder.Spline.Travel(0, slotSplineDistances[index]);
                    beltPoint = builder.Spline.EvaluatePosition(percent);
                    hasPosition = true;
                }
            }

            if (hasPosition)
            {
                Vector3 inwardDir = (roomCenter - beltPoint).normalized;
                inwardDir.y = 0f;

                return beltPoint + inwardDir * edgeInwardOffset;
            }

            return fallbackPosition;
        }

        public Customer FindServiceCandidate(float itemSplineDistance, ItemDataSO itemData, ConveyorManager conveyor)
        {
            if (conveyor == null || conveyor.Path == null || conveyor.Path.Length <= 0f) return null;

            float pathLength = conveyor.Path.Length;
            Customer bestCandidate = null;
            float minDelta = float.MaxValue;

            for (int i = 0; i < slotCount; i++)
            {
                Customer candidate = slots[i];
                if (candidate == null || candidate.IsServed || !candidate.IsEdgeCustomer) continue;
                if (candidate.RequiredData != itemData) continue;

                float delta = Mathf.Abs(itemSplineDistance - slotSplineDistances[i]);
                if (delta > pathLength * 0.5f) delta = pathLength - delta;

                if (delta <= alignmentTolerance && delta < minDelta)
                {
                    minDelta = delta;
                    bestCandidate = candidate;
                }
            }

            return bestCandidate;
        }

        public bool TryGetSlotIndex(Customer customer, out int index)
        {
            for (int i = 0; i < slotCount; i++)
            {
                if (slots[i] == customer)
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        public void Occupy(int index, Customer customer)
        {
            if (index >= 0 && index < slotCount) slots[index] = customer;
        }

        public void Release(int index)
        {
            if (index >= 0 && index < slotCount) slots[index] = null;
        }
    }
}