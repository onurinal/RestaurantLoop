using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using RestaurantLoop.Infrastructure;

namespace RestaurantLoop.Core
{
    public class RackManager : MonoBehaviour
    {
        public static RackManager Instance { get; private set; }

        [Header("Prefabs & Anchors")]
        [SerializeField] private RackSlot slotPrefab;
        [SerializeField] private Transform beltAnchor;
        [SerializeField] private Vector3 offsetFromBelt = new Vector3(0f, 0f, -8f);
        [SerializeField] private bool lockToWorldCenterX = true;

        [Header("Layout Settings")]
        [SerializeField] private int initialSlotCount = 5;
        [SerializeField] private float slotSpacing = 1.1f;

        private readonly List<RackSlot> rackSlots = new List<RackSlot>();

        public Vector3 CenterPosition => GetCalculatedCenterPosition();
        public bool HasAvailableSlot => GetFirstEmptySlot() != null;

        public event Action<StackItem, RackSlot> StackAssignedToRack;
        public event Action<StackItem, RackSlot> RackStackRedeploymentStarted;

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
            if (beltAnchor == null && ConveyorManager.Instance != null)
            {
                beltAnchor = ConveyorManager.Instance.transform;
            }

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
                
                GameObject slotObj = PoolManager.Instance.Spawn(slotPrefab.gameObject, slotPosition, Quaternion.identity, transform);
                RackSlot newSlot = slotObj.GetComponent<RackSlot>();
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

            ConveyorManager.Instance.RemoveStackFromBelt(stack);
            emptySlot.PlaceStack(stack);

            stack.JumpToSlot(emptySlot.transform, () => { ConveyorManager.Instance.ReleaseCapacity(); });
            StackAssignedToRack?.Invoke(stack, emptySlot);

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

            if (!ConveyorManager.Instance.CanAcceptStack)
            {
                stack.Shake();
                return false;
            }

            stack.transform.DOKill();
            stack.transform.localPosition = Vector3.zero;

            targetSlot.ClearSlot();
            stack.transform.SetParent(null);

            bool accepted = ConveyorManager.Instance.TrySendStackToBelt(stack);
            if (accepted)
            {
                RackStackRedeploymentStarted?.Invoke(stack, targetSlot);
            }

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
                PoolManager.Instance.Despawn(transform.GetChild(i).gameObject);
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