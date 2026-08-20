using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Manages dynamic rack slot spawning, item placement, and rack reuse dispatching.
    /// </summary>
    public class RackManager : MonoBehaviour
    {
        public static RackManager Instance { get; private set; }

        [Header("Prefabs & Anchors")]
        [SerializeField] private RackSlot slotPrefab;
        [SerializeField] private Transform beltAnchor;
        [SerializeField] private Vector3 offsetFromBelt = new Vector3(0f, 0f, -7f);
        [SerializeField] private bool lockToWorldCenterX = true;

        [Header("Layout Settings")]
        [SerializeField] private int initialSlotCount = 5;
        [SerializeField] private float slotSpacing = 1.1f;

        private readonly List<RackSlot> rackSlots = new List<RackSlot>();

        public Vector3 CenterPosition => GetCalculatedCenterPosition();

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
        }

        private void Start()
        {
            BuildRackLayout(initialSlotCount);
        }

        public Vector3 GetCalculatedCenterPosition()
        {
            Vector3 origin = beltAnchor != null ? beltAnchor.position + offsetFromBelt : transform.position;

            if (lockToWorldCenterX)
            {
                origin.x = 0f;
            }

            return origin;
        }

        public void BuildRackLayout(int slotCount)
        {
            ClearExistingSlots();

            if (slotPrefab == null)
            {
                return;
            }

            Vector3 originPosition = GetCalculatedCenterPosition();
            transform.position = originPosition;

            float startX = originPosition.x - (((slotCount - 1) * slotSpacing) / 2f);

            for (int i = 0; i < slotCount; i++)
            {
                Vector3 slotPosition = new Vector3(startX + (i * slotSpacing), originPosition.y, originPosition.z);
                RackSlot newSlot = Instantiate(slotPrefab, slotPosition, Quaternion.identity, transform);
                newSlot.gameObject.name = $"RackSlot_{i + 1}";

                rackSlots.Add(newSlot);
            }
        }

        public bool TryAddStackToRack(StackItem stack)
        {
            RackSlot emptySlot = GetFirstEmptySlot();

            if (emptySlot == null)
            {
                Debug.LogWarning("Rack is completely full!");
                return false;
            }

            ConveyorController.Instance.RemoveStackFromBelt(stack);
            emptySlot.PlaceStack(stack);

            stack.JumpToSlot(emptySlot.transform, () => { ConveyorController.Instance.ReleaseCapacity(); });

            return true;
        }

        public bool TrySendRackStackToBelt(StackItem stack)
        {
            if (stack == null || stack.IsJumping)
            {
                return false;
            }

            RackSlot targetSlot = GetSlotContainingStack(stack);

            if (targetSlot == null)
            {
                return false;
            }

            if (!ConveyorController.Instance.TryReserveSlot())
            {
                stack.Shake();
                return false;
            }

            stack.transform.DOKill();
            stack.transform.localPosition = Vector3.zero;

            targetSlot.ClearSlot();
            stack.transform.SetParent(null);

            SplineConveyorPath path = ConveyorController.Instance.Path;
            Vector3 entrancePosition = path.GetPosition(ConveyorController.Instance.EntranceDistance);

            stack.JumpToConveyor(entrancePosition, () => { ConveyorController.Instance.TryAddStack(stack); });

            return true;
        }

        private RackSlot GetSlotContainingStack(StackItem stack)
        {
            for (int i = 0; i < rackSlots.Count; i++)
            {
                if (rackSlots[i].CurrentStack == stack)
                {
                    return rackSlots[i];
                }
            }

            return null;
        }

        private RackSlot GetFirstEmptySlot()
        {
            for (int i = 0; i < rackSlots.Count; i++)
            {
                if (!rackSlots[i].IsOccupied)
                {
                    return rackSlots[i];
                }
            }

            return null;
        }

        private void ClearExistingSlots()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            rackSlots.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = GetCalculatedCenterPosition();
            Gizmos.color = Color.yellow;

            float startX = center.x - (((initialSlotCount - 1) * slotSpacing) / 2f);

            for (int i = 0; i < initialSlotCount; i++)
            {
                Vector3 slotPos = new Vector3(startX + (i * slotSpacing), center.y, center.z);
                Gizmos.DrawWireCube(slotPos, Vector3.one * 0.8f);
            }
        }
    }
}