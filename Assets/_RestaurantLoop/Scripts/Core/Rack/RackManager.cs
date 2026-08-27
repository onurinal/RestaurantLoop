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
        [SerializeField] private float shiftAnimationDuration = 0.25f;

        private readonly List<RackSlot> rackSlots = new List<RackSlot>();

        public Vector3 CenterPosition => GetCalculatedCenterPosition();
        public bool HasAvailableSlot => GetFirstEmptySlot() != null;

        public int OccupiedSlotCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < rackSlots.Count; i++)
                {
                    if (rackSlots[i] != null && rackSlots[i].IsOccupied) count++;
                }
                return count;
            }
        }

        public event Action<StackItem, RackSlot> StackAssignedToRack;
        public event Action<StackItem, RackSlot> RackStackRedeploymentStarted;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
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

            if (lockToWorldCenterX) origin.x = 0f;

            return origin;
        }

        public void BuildRackLayout(int slotCount)
        {
            ClearExistingSlots();

            if (slotPrefab == null) return;

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
            ConveyorManager.Instance.ReleaseCapacity();

            emptySlot.PlaceStack(stack);
            stack.JumpToSlot(emptySlot.transform);
            
            StackAssignedToRack?.Invoke(stack, emptySlot);

            return true;
        }

        public bool TrySendRackStackToBelt(StackItem stack)
        {
            if (stack == null || stack.IsJumping) return false;

            RackSlot targetSlot = GetSlotContainingStack(stack);
            if (targetSlot == null) return false;

            if (!ConveyorManager.Instance.CanAcceptStack || !ConveyorManager.Instance.IsEntranceClear())
            {
                stack.Shake();
                return false;
            }

            // Transfer directly to belt FIRST before clearing the slot
            bool accepted = ConveyorManager.Instance.TrySendStackToBelt(stack);
            if (accepted)
            {
                targetSlot.ClearSlot();
                RackStackRedeploymentStarted?.Invoke(stack, targetSlot);
                ShiftItemsLeft();
                return true;
            }

            stack.Shake();
            return false;
        }

        private void ShiftItemsLeft()
        {
            for (int i = 0; i < rackSlots.Count; i++)
            {
                if (!rackSlots[i].IsOccupied)
                {
                    for (int j = i + 1; j < rackSlots.Count; j++)
                    {
                        if (rackSlots[j].IsOccupied)
                        {
                            StackItem stackToMove = rackSlots[j].CurrentStack;
                            
                            rackSlots[j].ClearSlot();
                            rackSlots[i].PlaceStack(stackToMove);

                            if (stackToMove.IsJumping)
                            {
                                stackToMove.JumpToSlot(rackSlots[i].transform);
                            }
                            else
                            {
                                stackToMove.transform.DOKill();
                                stackToMove.transform.SetParent(rackSlots[i].transform);
                                stackToMove.transform.DOMove(rackSlots[i].transform.position, shiftAnimationDuration)
                                    .SetEase(Ease.OutQuad);
                            }

                            break; 
                        }
                    }
                }
            }
        }

        public void ClearAllItems()
        {
            for (int i = 0; i < rackSlots.Count; i++)
            {
                if (rackSlots[i].IsOccupied && rackSlots[i].CurrentStack != null)
                {
                    PoolManager.Instance.Despawn(rackSlots[i].CurrentStack.gameObject);
                    rackSlots[i].ClearSlot();
                }
            }
        }

        private RackSlot GetSlotContainingStack(StackItem stack)
        {
            for (int i = 0; i < rackSlots.Count; i++)
            {
                if (rackSlots[i].CurrentStack == stack) return rackSlots[i];
            }
            return null;
        }

        private RackSlot GetFirstEmptySlot()
        {
            for (int i = 0; i < rackSlots.Count; i++)
            {
                if (!rackSlots[i].IsOccupied) return rackSlots[i];
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