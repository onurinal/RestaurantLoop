using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using RestaurantLoop.UI; 

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

        public bool HasClearColorSelectableStack
        {
            get
            {
                if (CrowdManager.Instance == null) return false;

                for (int i = 0; i < rackSlots.Count; i++)
                {
                    if (IsClearColorSelectableStack(rackSlots[i]?.CurrentStack)) return true;
                }

                return false;
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

                GameObject slotObj = Instantiate(slotPrefab.gameObject, slotPosition, Quaternion.identity, transform);
                RackSlot newSlot = slotObj.GetComponent<RackSlot>();
                newSlot.gameObject.name = $"RackSlot_{i + 1}";
                newSlot.ClearSlot();

                rackSlots.Add(newSlot);
            }
        }

        public bool TryAddStackToRack(StackItem stack)
        {
            RackSlot emptySlot = GetFirstEmptySlot();

            if (emptySlot == null)
            {
                Debug.LogWarning("[RackManager] Rack is completely full!");
                return false;
            }

            ConveyorManager.Instance.RemoveStackFromBelt(stack);
            ConveyorManager.Instance.ReleaseCapacity();

            emptySlot.PlaceStack(stack);
            stack.JumpToSlot(emptySlot.transform);

            StackAssignedToRack?.Invoke(stack, emptySlot);

            // --- TUTORIAL STEP 3 TRIGGER: ITEM ARRIVED IN RACK (LEVEL 1) ---
            if (LevelManager.Instance != null && LevelManager.Instance.CurrentLevelNumber == 1
                && TutorialManager.Instance != null
                && TutorialManager.Instance.CurrentStep == TutorialManager.TutorialStep.WaitUntilInRack)
            {
                TutorialManager.Instance.StartStepTapRack(emptySlot.transform);
            }
            // -----------------------------------------------------------------

            return true;
        }

        public bool TrySendRackStackToBelt(StackItem stack)
        {
            if (stack == null || stack.IsJumping) return false;

            RackSlot targetSlot = GetSlotContainingStack(stack);
            if (targetSlot == null) return false;

            if (!ConveyorManager.Instance.CanAcceptStack)
            {
                stack.Shake();
                ConveyorManager.Instance.NotifyCapacityRejected();
                return false;
            }

            bool accepted = ConveyorManager.Instance.TrySendStackToBelt(stack);
            if (accepted)
            {
                targetSlot.ClearSlot();
                RackStackRedeploymentStarted?.Invoke(stack, targetSlot);
                ShiftItemsLeft();

                // --- TUTORIAL STEP 3 COMPLETE: PLAYER TAPPED RACK ITEM BACK TO BELT ---
                if (TutorialManager.Instance != null
                    && TutorialManager.Instance.CurrentStep == TutorialManager.TutorialStep.TapRackToConveyor)
                {
                    TutorialManager.Instance.HideTutorial();
                }
                // ------------------------------------------------------------------------

                return true;
            }

            stack.Shake();
            return false;
        }

        private void ShiftItemsLeft(bool ignoreTimeScale = false)
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
                                StackItem.KillTweensInHierarchy(stackToMove.gameObject);
                                stackToMove.transform.SetParent(rackSlots[i].transform);
                                Tween shiftTween = stackToMove.transform.DOMove(rackSlots[i].transform.position, shiftAnimationDuration)
                                    .SetEase(Ease.OutQuad);
                                if (ignoreTimeScale) shiftTween.SetUpdate(true);
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
                if (rackSlots[i] != null)
                {
                    if (rackSlots[i].CurrentStack != null)
                    {
                        StackItem.KillTweensInHierarchy(rackSlots[i].CurrentStack.gameObject);
                        Destroy(rackSlots[i].CurrentStack.gameObject);
                    }

                    rackSlots[i].ClearSlot();
                }
            }
        }

        public bool IsClearColorSelectableStack(StackItem stack)
        {
            if (stack == null || stack.IsJumping || CrowdManager.Instance == null ||
                !CrowdManager.Instance.HasRemainingDemand(stack.Data)) return false;

            return GetSlotContainingStack(stack) != null;
        }

        public void SetClearColorSelectionVisuals(bool active)
        {
            for (int i = 0; i < rackSlots.Count; i++)
            {
                StackItem stack = rackSlots[i] != null ? rackSlots[i].CurrentStack : null;
                if (stack != null) stack.SetHandSelectionHighlight(active && IsClearColorSelectableStack(stack));
            }
        }

        public int RemoveStacksByData(ItemDataSO data)
        {
            if (data == null) return 0;

            int removedCount = 0;
            for (int i = 0; i < rackSlots.Count; i++)
            {
                RackSlot slot = rackSlots[i];
                StackItem stack = slot != null ? slot.CurrentStack : null;
                if (stack == null || stack.Data != data) continue;

                slot.ClearSlot();
                StackItem.KillTweensInHierarchy(stack.gameObject);
                Destroy(stack.gameObject);
                removedCount++;
            }

            if (removedCount > 0) ShiftItemsLeft(ignoreTimeScale: true);
            return removedCount;
        }

        private RackSlot GetSlotContainingStack(StackItem stack)
        {
            for (int i = 0; i < rackSlots.Count; i++)
            {
                if (rackSlots[i] != null && rackSlots[i].CurrentStack == stack) return rackSlots[i];
            }
            return null;
        }

        private RackSlot GetFirstEmptySlot()
        {
            for (int i = 0; i < rackSlots.Count; i++)
            {
                if (rackSlots[i] != null && !rackSlots[i].IsOccupied) return rackSlots[i];
            }
            return null;
        }

        /// <summary>
        /// Returns the first occupied rack slot's RectTransform or Transform for tutorial pointing.
        /// </summary>
        public Transform GetFirstOccupiedRackSlotTransform()
        {
            for (int i = 0; i < rackSlots.Count; i++)
            {
                if (rackSlots[i] != null && rackSlots[i].IsOccupied)
                {
                    return rackSlots[i].transform;
                }
            }
            return null;
        }

        private void ClearExistingSlots()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                StackItem.KillTweensInHierarchy(child.gameObject);
                Destroy(child.gameObject);
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
