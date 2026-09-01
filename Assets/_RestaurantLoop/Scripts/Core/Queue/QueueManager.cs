using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using System;
using RestaurantLoop.Infrastructure;

namespace RestaurantLoop.Core
{
    public class QueueManager : MonoBehaviour
    {
        public static QueueManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private QueueSpawner queueSpawner;

        [Header("Queue Count Labels")]
        [Tooltip("Count-label opacity for queue stacks behind the front row. Selection power-ups temporarily restore full opacity.")]
        [SerializeField, Range(0f, 1f)] private float nonFrontRowCountTextOpacity = 0.7f;

        [Header("Spawn Lock Visuals")]
        [Tooltip("Brightness applied to queue slots and stacks while customers are entering.")]
        [SerializeField, Range(0f, 1f)] private float spawningBrightness = 0.6f;

        private List<QueueColumn> columns = new List<QueueColumn>();
        private bool spawnLockVisualInitialized;
        private bool isSpawnVisualLocked;

        private Color InteractionOutlineColor => PowerUpManager.Instance != null
            ? PowerUpManager.Instance.InteractionOutlineColor
            : Color.white;
        private SlotOutlineAnimationSettings InteractionOutlineAnimationSettings => PowerUpManager.Instance != null
            ? PowerUpManager.Instance.InteractionOutlineAnimationSettings
            : SlotOutlineAnimationSettings.Default;

        public event Action QueueChanged;

        public int RemainingStackCount
        {
            get
            {
                int count = 0;
                foreach (var col in columns)
                {
                    if (col != null) count += col.OccupiedSlotCount;
                }

                return count;
            }
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (queueSpawner == null) queueSpawner = GetComponent<QueueSpawner>();
        }

        private void Update()
        {
            RefreshSpawnLockVisuals();
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
                            GameObject stackObj = PoolManager.Instance != null
                                ? PoolManager.Instance.Spawn(
                                    config.itemData.StackPrefab,
                                    slot.transform.position,
                                    Quaternion.identity,
                                    slot.transform)
                                : Instantiate(
                                    config.itemData.StackPrefab,
                                    slot.transform.position,
                                    Quaternion.identity,
                                    slot.transform);

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

            RefreshSpawnLockVisuals(force: true);
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
                        if (slot != null) slot.ClearSlot();
                    }

                    StackItem[] childStacks = col.GetComponentsInChildren<StackItem>(true);
                    foreach (var stack in childStacks)
                    {
                        if (stack != null)
                        {
                            StackItem.ReleaseToPool(stack);
                        }
                    }

                    StackItem.KillTweensInHierarchy(col.gameObject);
                    Destroy(col.gameObject);
                }
            }

            columns.Clear();
            spawnLockVisualInitialized = false;
            isSpawnVisualLocked = false;
            NotifyQueueChanged();
        }

        public StackItem GetFirstFrontRowStack()
        {
            foreach (var column in columns)
            {
                if (column == null) continue;

                QueueSlot[] childSlots = column.GetComponentsInChildren<QueueSlot>(true);
                foreach (var slot in childSlots)
                {
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

                    // Cached slot list: this is evaluated every frame from the power-up UI,
                    // where GetComponentsInChildren allocated an array per column per frame.
                    IReadOnlyList<QueueSlot> childSlots = column.Slots;
                    for (int i = 0; i < childSlots.Count; i++)
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

        public bool HasColumnWithAtLeastOccupiedStacks(int minimumCount)
        {
            minimumCount = Mathf.Max(1, minimumCount);

            foreach (QueueColumn column in columns)
            {
                if (column != null && column.OccupiedSlotCount >= minimumCount) return true;
            }

            return false;
        }

        /// <summary>
        /// Briefly points the player to every normal-play source after they tap a locked deeper
        /// queue stack: occupied front-row queue slots and occupied rack slots.
        /// </summary>
        public void PulseAvailableMoveTargets()
        {
            foreach (QueueColumn column in columns)
            {
                QueueSlot frontSlot = column != null ? column.FrontSlot : null;
                StackItem stack = frontSlot != null ? frontSlot.CurrentStack : null;
                if (stack != null && !stack.IsJumping)
                {
                    frontSlot.PulseInteractionOutline(InteractionOutlineColor, InteractionOutlineAnimationSettings);
                }
            }

            RackManager.Instance?.PulseOccupiedSlots(InteractionOutlineColor, InteractionOutlineAnimationSettings);
        }

        /// <summary>
        /// Gets the world-space bounds of every occupied queue stack. This is intentionally
        /// calculated on demand because selection framing is entered only when a power-up is
        /// activated, not every frame.
        /// </summary>
        public bool TryGetOccupiedStackBounds(out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            foreach (QueueColumn column in columns)
            {
                if (column == null) continue;

                IReadOnlyList<QueueSlot> slots = column.Slots;
                for (int i = 0; i < slots.Count; i++)
                {
                    StackItem stack = slots[i] != null ? slots[i].CurrentStack : null;
                    if (stack == null) continue;

                    // Include the root position even when a stack has no renderers, then grow
                    // the bounds to its visible meshes so framing also accounts for stack size.
                    EncapsulateBounds(ref bounds, ref hasBounds,
                        new Bounds(stack.transform.position, Vector3.zero));

                    Renderer[] renderers = stack.GetComponentsInChildren<Renderer>(true);
                    for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    {
                        Renderer renderer = renderers[rendererIndex];
                        if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                        {
                            EncapsulateBounds(ref bounds, ref hasBounds, renderer.bounds);
                        }
                    }
                }
            }

            return hasBounds;
        }

        public bool HasClearColorSelectableStack
        {
            get
            {
                if (IsTransitioning || CrowdManager.Instance == null) return false;

                foreach (QueueColumn column in columns)
                {
                    if (column == null) continue;

                    IReadOnlyList<QueueSlot> childSlots = column.Slots;
                    for (int i = 0; i < childSlots.Count; i++)
                    {
                        if (IsClearColorSelectableStack(childSlots[i]?.CurrentStack)) return true;
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

            for (int i = 0; i < slots.Count; i++) slots[i].ClearSlot();

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

                IReadOnlyList<QueueSlot> childSlots = column.Slots;
                for (int i = 0; i < childSlots.Count; i++)
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
            if (!active)
            {
                ClearSelectionHighlights();
                RefreshQueueCountTextOpacity(false);
                return;
            }

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
                    stack.SetHandSelectionHighlight(false);
                    slot.SetInteractionOutlineGuidance(eligible, InteractionOutlineColor, InteractionOutlineAnimationSettings);
                }
            }

            RefreshQueueCountTextOpacity(active);
        }

        private void ClearSelectionHighlights()
        {
            foreach (QueueColumn column in columns)
            {
                if (column == null) continue;

                StackItem[] stacks = column.GetComponentsInChildren<StackItem>(true);
                for (int i = 0; i < stacks.Length; i++)
                {
                    stacks[i].SetHandSelectionHighlight(false);
                }

                IReadOnlyList<QueueSlot> slots = column.Slots;
                for (int i = 0; i < slots.Count; i++)
                {
                    slots[i]?.SetInteractionOutlineGuidance(false, InteractionOutlineColor,
                        InteractionOutlineAnimationSettings);
                }
            }
        }

        public bool IsClearColorSelectableStack(StackItem stack)
        {
            if (stack == null || stack.IsJumping || CrowdManager.Instance == null ||
                !CrowdManager.Instance.HasRemainingDemand(stack.Data)) return false;

            foreach (QueueColumn column in columns)
            {
                if (column == null) continue;

                // This runs inside HasClearColorSelectableStack's own per-column loop, so the
                // old GetComponentsInChildren call made the pair allocate O(columns^2) arrays.
                IReadOnlyList<QueueSlot> childSlots = column.Slots;
                for (int i = 0; i < childSlots.Count; i++)
                {
                    if (childSlots[i] != null && childSlots[i].CurrentStack == stack) return true;
                }
            }

            return false;
        }

        public void SetClearColorSelectionVisuals(bool active)
        {
            foreach (QueueColumn column in columns)
            {
                if (column == null) continue;

                QueueSlot[] childSlots = column.GetComponentsInChildren<QueueSlot>(true);
                for (int i = 0; i < childSlots.Length; i++)
                {
                    QueueSlot slot = childSlots[i];
                    StackItem stack = slot != null ? slot.CurrentStack : null;
                    if (stack != null) stack.SetHandSelectionHighlight(false);
                    slot?.SetInteractionOutlineGuidance(active && IsClearColorSelectableStack(stack),
                        InteractionOutlineColor, InteractionOutlineAnimationSettings);
                }
            }

            RefreshQueueCountTextOpacity(active);
        }

        public int RemoveStacksByData(ItemDataSO data)
        {
            if (data == null) return 0;

            int removedCount = 0;
            foreach (QueueColumn column in columns)
            {
                if (column == null) continue;
                removedCount += column.RemoveStacksByData(data);
            }

            if (removedCount > 0) NotifyQueueChanged();
            return removedCount;
        }

        public void NotifyQueueChanged()
        {
            RefreshQueueCountTextOpacity();
            QueueChanged?.Invoke();
        }

        private void RefreshSpawnLockVisuals(bool force = false)
        {
            bool shouldLock = CrowdManager.Instance != null && CrowdManager.Instance.IsSpawningCustomers;
            if (!force && spawnLockVisualInitialized && shouldLock == isSpawnVisualLocked) return;

            spawnLockVisualInitialized = true;
            isSpawnVisualLocked = shouldLock;

            foreach (QueueColumn column in columns)
            {
                if (column == null) continue;

                QueueSlot[] slots = column.GetComponentsInChildren<QueueSlot>(true);
                for (int i = 0; i < slots.Length; i++)
                {
                    slots[i].SetSpawnLockedVisual(isSpawnVisualLocked, spawningBrightness);
                }
            }

            RefreshQueueCountTextOpacity();
        }

        private static void EncapsulateBounds(ref Bounds aggregate, ref bool hasBounds, Bounds next)
        {
            if (!hasBounds)
            {
                aggregate = next;
                hasBounds = true;
                return;
            }

            aggregate.Encapsulate(next);
        }

        private void RefreshQueueCountTextOpacity(bool? selectionModeActive = null)
        {
            bool useFullOpacity = selectionModeActive ??
                                  (PowerUpManager.Instance != null &&
                                   (PowerUpManager.Instance.IsHandSelectionActive || PowerUpManager.Instance.IsClearColorSelectionActive));

            foreach (QueueColumn column in columns)
            {
                if (column == null) continue;

                QueueSlot[] childSlots = column.GetComponentsInChildren<QueueSlot>(true);
                for (int i = 0; i < childSlots.Length; i++)
                {
                    QueueSlot slot = childSlots[i];
                    StackItem stack = slot != null ? slot.CurrentStack : null;
                    if (stack == null) continue;

                    float opacity = !useFullOpacity && column.IsDeeperSlot(slot)
                        ? nonFrontRowCountTextOpacity
                        : 1f;
                    if (isSpawnVisualLocked) opacity *= spawningBrightness;
                    stack.SetCountTextOpacity(opacity);
                }
            }
        }

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
