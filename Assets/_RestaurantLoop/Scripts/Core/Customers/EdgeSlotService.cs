using UnityEngine;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Owns the active edge slot array and its mapping onto the conveyor spline.
    /// Responsible for slot world positions, occupancy, and belt-item service queries.
    /// </summary>
    public class EdgeSlotService
    {
        private readonly int slotCount;
        private readonly float alignmentTolerance;
        private readonly float edgeInwardOffset;

        private readonly Customer[] slots;
        private float[] slotSplineDistances;

        public int SlotCount => slotCount;

        public EdgeSlotService(int slotCount, float alignmentTolerance, float edgeInwardOffset)
        {
            this.slotCount = slotCount;
            this.alignmentTolerance = alignmentTolerance;
            this.edgeInwardOffset = edgeInwardOffset;

            slots = new Customer[slotCount];
            slotSplineDistances = new float[slotCount];
        }

        public void RecalculateSplineMapping(ConveyorManager conveyor)
        {
            if (conveyor == null || conveyor.Path == null || conveyor.Path.Length <= 0f) return;

            if (slotSplineDistances == null || slotSplineDistances.Length != slotCount)
            {
                slotSplineDistances = new float[slotCount];
            }

            float pathLength = conveyor.Path.Length;
            float startDist = conveyor.EntranceDistance;
            float validTravelLength = conveyor.GetRequiredTravelDistance();
            float step = validTravelLength / (slotCount + 1);

            for (int i = 0; i < slotCount; i++)
            {
                float travelOffset = step * (i + 1);
                float splineDist = (startDist + (conveyor.IsClockwise ? travelOffset : -travelOffset)) % pathLength;
                if (splineDist < 0f) splineDist += pathLength;

                slotSplineDistances[i] = splineDist;
            }
        }

        public Vector3 GetSlotWorldPosition(int index, ConveyorManager conveyor, Vector3 roomCenter, Vector3 fallbackPosition)
        {
            if (conveyor != null && conveyor.Path != null && conveyor.Path.Length > 0f)
            {
                if (slotSplineDistances == null || slotSplineDistances.Length != slotCount || slotSplineDistances[index] == 0f)
                {
                    RecalculateSplineMapping(conveyor);
                }

                Vector3 beltPoint = conveyor.Path.GetPosition(slotSplineDistances[index]);

                Vector3 inwardDir = (roomCenter - beltPoint).normalized;
                inwardDir.y = 0f;

                return beltPoint + inwardDir * edgeInwardOffset;
            }

            return fallbackPosition;
        }

        /// <summary>
        /// Finds the best edge-slot customer eligible to receive an item currently at the given belt spline distance.
        /// </summary>
        public Customer FindServiceCandidate(float itemSplineDistance, ItemDataSO itemData, ConveyorManager conveyor)
        {
            if (conveyor == null || conveyor.Path == null) return null;

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

        public void Occupy(int index, Customer customer) => slots[index] = customer;

        public void Release(int index) => slots[index] = null;
    }
}