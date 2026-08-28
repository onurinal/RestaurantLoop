using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using System;

namespace RestaurantLoop.Core
{
    public class QueueManager : MonoBehaviour
    {
        public static QueueManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private QueueSpawner queueSpawner;

        private List<QueueColumn> columns = new List<QueueColumn>();

        public event Action QueueChanged;

        /// <summary>
        /// Calculates the total number of remaining active stacks waiting across all queue columns.
        /// </summary>
        public int RemainingStackCount
        {
            get
            {
                int count = 0;
                foreach (var col in columns)
                {
                    if (col != null)
                    {
                        count += col.OccupiedSlotCount;
                    }
                }

                return count;
            }
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (queueSpawner == null)
            {
                queueSpawner = GetComponent<QueueSpawner>();
            }
        }

        public void SetupQueue(LevelDataSO levelData)
        {
            ClearQueue();

            if (queueSpawner == null || levelData == null || levelData.queueStackConfigs == null) return;

            int cols = levelData.columnCount;
            int rows = levelData.calculatedRowCount;
            var stackConfigs = levelData.queueStackConfigs;

            columns = queueSpawner.SpawnQueueLayout(cols, rows);

            int currentStackIndex = 0;

            foreach (var col in columns)
            {
                if (col == null) continue;

                col.InitializeChildSlots();

                for (int r = 0; r < rows; r++)
                {
                    if (currentStackIndex >= stackConfigs.Count) break;

                    QueueSlot slot = col.transform.GetChild(r).GetComponent<QueueSlot>();
                    if (slot != null)
                    {
                        QueueStackConfig config = stackConfigs[currentStackIndex];

                        if (config.itemData != null && config.itemData.StackPrefab != null)
                        {
                            // Instantiate fresh GameObject directly from prefab asset to prevent pooled object pollution
                            GameObject stackObj = Instantiate(
                                config.itemData.StackPrefab,
                                slot.transform.position,
                                Quaternion.identity,
                                slot.transform
                            );

                            StackItem.KillTweensInHierarchy(stackObj);

                            stackObj.transform.localPosition = Vector3.zero;
                            stackObj.transform.localRotation = Quaternion.identity;
                            stackObj.transform.localScale = Vector3.one;

                            StackItem newStack = stackObj.GetComponent<StackItem>();

                            if (newStack != null)
                            {
                                newStack.Initialize(config.itemData, config.itemCount);
                                slot.PlaceStack(newStack);
                            }
                        }

                        currentStackIndex++;
                    }
                }
            }
        }

        public void ClearQueue()
        {
            if (columns == null) return;

            foreach (var col in columns)
            {
                if (col != null)
                {
                    QueueSlot[] childSlots = col.GetComponentsInChildren<QueueSlot>(true);
                    foreach (var slot in childSlots)
                    {
                        if (slot != null)
                        {
                            slot.ClearSlot();
                        }
                    }

                    StackItem[] childStacks = col.GetComponentsInChildren<StackItem>(true);
                    foreach (var stack in childStacks)
                    {
                        if (stack != null)
                        {
                            StackItem.KillTweensInHierarchy(stack.gameObject);
                            Destroy(stack.gameObject);
                        }
                    }

                    StackItem.KillTweensInHierarchy(col.gameObject);
                    Destroy(col.gameObject);
                }
            }

            columns.Clear();
            NotifyQueueChanged();
        }

        /// <summary>
        /// Finds and returns the first available front-row stack item, completely avoiding deeper/back stacks for the tutorial.
        /// </summary>
        public StackItem GetFirstFrontRowStack()
        {
            foreach (var column in columns)
            {
                if (column == null) continue;

                QueueSlot[] childSlots = column.GetComponentsInChildren<QueueSlot>(true);
                foreach (var slot in childSlots)
                {
                    // Ensure the slot is occupied and is strictly NOT a deeper/back slot
                    if (slot != null && slot.IsOccupied && slot.CurrentStack != null && !column.IsDeeperSlot(slot))
                    {
                        return slot.CurrentStack;
                    }
                }
            }
            return null;
        }

        public bool HasSelectableDeeperStack
        {
            get
            {
                if (IsTransitioning) return false;

                foreach (QueueColumn column in columns)
                {
                    if (column == null) continue;

                    QueueSlot[] childSlots = column.GetComponentsInChildren<QueueSlot>(true);
                    for (int i = 0; i < childSlots.Length; i++)
                    {
                        QueueSlot slot = childSlots[i];
                        if (slot != null && column.IsDeeperSlot(slot) && slot.IsOccupied &&
                            slot.CurrentStack != null && !slot.CurrentStack.IsJumping)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        public bool CanShuffleQueuedStacks
        {
            get
            {
                if (IsTransitioning) return false;

                List<StackItem> stacks = GetOccupiedStacksInSlotOrder(null);
                if (stacks.Count < 2) return false;

                for (int i = 0; i < stacks.Count; i++)
                {
                    for (int j = i + 1; j < stacks.Count; j++)
                    {
                        if (!AreVisuallyEquivalent(stacks[i], stacks[j])) return true;
                    }
                }

                return false;
            }
        }

        public bool TryShuffleQueuedStacks()
        {
            if (!CanShuffleQueuedStacks) return false;

            List<QueueSlot> slots = new List<QueueSlot>();
            List<StackItem> originalStacks = GetOccupiedStacksInSlotOrder(slots);
            List<StackItem> shuffledStacks = new List<StackItem>(originalStacks);

            const int maxShuffleAttempts = 12;
            bool changed = false;
            for (int attempt = 0; attempt < maxShuffleAttempts && !changed; attempt++)
            {
                FisherYatesShuffle(shuffledStacks);
                changed = HasVisibleOrderChanged(originalStacks, shuffledStacks);
            }

            if (!changed)
            {
                ForceDistinctSwap(shuffledStacks);
                changed = HasVisibleOrderChanged(originalStacks, shuffledStacks);
            }

            if (!changed) return false;

            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].ClearSlot();
            }

            for (int i = 0; i < slots.Count; i++)
            {
                QueueSlot destination = slots[i];
                StackItem stack = shuffledStacks[i];
                stack.transform.DOKill();
                destination.PlaceStack(stack);
                stack.transform.DOLocalMove(Vector3.zero, 0.25f).SetEase(Ease.OutQuad);
            }

            NotifyQueueChanged();
            return true;
        }

        public bool TrySendHandSelectedStack(StackItem stack)
        {
            if (stack == null || IsTransitioning) return false;

            foreach (QueueColumn column in columns)
            {
                if (column == null) continue;

                QueueSlot[] childSlots = column.GetComponentsInChildren<QueueSlot>(true);
                for (int i = 0; i < childSlots.Length; i++)
                {
                    QueueSlot slot = childSlots[i];
                    if (slot != null && slot.CurrentStack == stack)
                    {
                        return column.TrySendStackToBelt(slot, requireDeeperStack: true);
                    }
                }
            }

            return false;
        }

        public void SetHandSelectionVisuals(bool active)
        {
            foreach (QueueColumn column in columns)
            {
                if (column == null) continue;

                QueueSlot[] childSlots = column.GetComponentsInChildren<QueueSlot>(true);
                for (int i = 0; i < childSlots.Length; i++)
                {
                    QueueSlot slot = childSlots[i];
                    StackItem stack = slot != null ? slot.CurrentStack : null;
                    if (stack == null) continue;

                    bool eligible = active && column.IsDeeperSlot(slot) && !stack.IsJumping;
                    stack.SetHandSelectionHighlight(eligible);
                }
            }
        }

        public void NotifyQueueChanged() => QueueChanged?.Invoke();

        private bool IsTransitioning
        {
            get
            {
                foreach (QueueColumn column in columns)
                {
                    if (column != null && column.IsTransitioning) return true;
                }

                return false;
            }
        }

        private List<StackItem> GetOccupiedStacksInSlotOrder(List<QueueSlot> slots)
        {
            List<StackItem> stacks = new List<StackItem>();
            foreach (QueueColumn column in columns)
            {
                if (column == null) continue;

                QueueSlot[] childSlots = column.GetComponentsInChildren<QueueSlot>(true);
                for (int i = 0; i < childSlots.Length; i++)
                {
                    QueueSlot slot = childSlots[i];
                    if (slot == null || !slot.IsOccupied || slot.CurrentStack == null) continue;

                    slots?.Add(slot);
                    stacks.Add(slot.CurrentStack);
                }
            }

            return stacks;
        }

        private static bool AreVisuallyEquivalent(StackItem first, StackItem second)
        {
            return first != null && second != null && first.Data == second.Data &&
                first.RemainingItemCount == second.RemainingItemCount;
        }

        private static bool HasVisibleOrderChanged(List<StackItem> original, List<StackItem> shuffled)
        {
            for (int i = 0; i < original.Count; i++)
            {
                if (!AreVisuallyEquivalent(original[i], shuffled[i])) return true;
            }

            return false;
        }

        private static void FisherYatesShuffle(List<StackItem> stacks)
        {
            for (int i = stacks.Count - 1; i > 0; i--)
            {
                int selectedIndex = UnityEngine.Random.Range(0, i + 1);
                (stacks[i], stacks[selectedIndex]) = (stacks[selectedIndex], stacks[i]);
            }
        }

        private static void ForceDistinctSwap(List<StackItem> stacks)
        {
            for (int i = 0; i < stacks.Count; i++)
            {
                for (int j = i + 1; j < stacks.Count; j++)
                {
                    if (AreVisuallyEquivalent(stacks[i], stacks[j])) continue;
                    (stacks[i], stacks[j]) = (stacks[j], stacks[i]);
                    return;
                }
            }
        }
    }
}