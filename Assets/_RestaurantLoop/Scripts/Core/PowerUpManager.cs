using System;
using System.Collections;
using UnityEngine;
using DG.Tweening;
using RestaurantLoop.Infrastructure;

namespace RestaurantLoop.Core
{
    public enum PowerUpType
    {
        AddStack,
        Shuffle,
        Hand,
        ClearColor
    }

    /// <summary>
    /// Owns the temporary power-up state for one level attempt. Nothing here is
    /// persisted: every level load starts with 99 uses of each power-up.
    /// </summary>
    public sealed class PowerUpManager : MonoBehaviour
    {
        public static PowerUpManager Instance { get; private set; }

        [SerializeField]
        private const int UsesPerAttempt = 99;

        private int addStackUses;
        private int shuffleUses;
        private int handUses;
        private int clearColorUses;
        private float timeScaleBeforeHandSelection = 1f;
        private float timeScaleBeforeClearColor = 1f;
        private Coroutine clearColorResolutionRoutine;

        [SerializeField, Min(0f)] private float clearColorResolutionDuration = 1.3f;

        [Header("Selection Camera")]
        [Tooltip("Legacy fallback distance applied along Camera.main's local Y axis only when the queue bounds cannot be read. Use a negative value to lower the camera.")]
        [SerializeField] private float selectionCameraLocalYOffset = -12f;
        [Tooltip("Clear space reserved at the bottom of the viewport while selecting a queue stack. This keeps the deepest stack clear of the power-up UI.")]
        [SerializeField, Range(0f, 0.45f)] private float selectionCameraBottomViewportPadding = 0.1f;
        [SerializeField, Min(0f)] private float selectionCameraMoveDuration = 0.45f;

        [Header("Slot Outline Feedback")]
        [Tooltip("Shared color for blocked-tap pulses and Hand/Clear Color slot guidance.")]
        [SerializeField] private Color interactionOutlineColor = new Color(141f / 255f, 242f / 255f, 122f / 255f, 1f);

        [Tooltip("Lowest opacity reached by the looping Hand/Clear Color guidance pulse.")]
        [SerializeField, Range(0f, 1f)] private float interactionOutlineGuidanceMinimumAlpha = 0.3f;
        [Tooltip("Seconds for each bright-to-dim or dim-to-bright guidance transition.")]
        [SerializeField, Min(0.01f)] private float interactionOutlineGuidanceHalfCycleDuration = 0.7f;
        [Tooltip("Seconds for a blocked-tap outline flash to brighten.")]
        [SerializeField, Min(0.01f)] private float interactionOutlineRejectionFadeInDuration = 0.5f;
        [Tooltip("Seconds for a blocked-tap outline flash to fade out.")]
        [SerializeField, Min(0.01f)] private float interactionOutlineRejectionFadeOutDuration = 1.25f;
        [Tooltip("Seconds between repeated blocked-tap flashes.")]
        [SerializeField, Min(0f)] private float interactionOutlineRejectionGapDuration = 0.25f;
        [Tooltip("Number of flashes shown after tapping a locked deeper queue stack.")]
        [SerializeField, Min(1)] private int interactionOutlineRejectionFlashCount = 1;

        private Transform selectionCameraTransform;
        private Vector3 selectionCameraBaseWorldPosition;
        private Tween selectionCameraTween;
        private bool hasSelectionCameraBasePosition;
        private bool isSelectionCameraOffsetApplied;
        private bool warnedMissingSelectionCamera;

        public bool IsHandSelectionActive { get; private set; }
        public bool IsClearColorSelectionActive { get; private set; }
        public bool IsClearColorResolving { get; private set; }
        public bool HasRackOutlinePriority => IsClearColorSelectionActive || IsClearColorResolving;
        public Color InteractionOutlineColor => interactionOutlineColor;
        public SlotOutlineAnimationSettings InteractionOutlineAnimationSettings =>
            new SlotOutlineAnimationSettings(
                interactionOutlineGuidanceMinimumAlpha,
                interactionOutlineGuidanceHalfCycleDuration,
                interactionOutlineRejectionFadeInDuration,
                interactionOutlineRejectionFadeOutDuration,
                interactionOutlineRejectionGapDuration,
                interactionOutlineRejectionFlashCount);

        public event Action StateChanged;
        public event Action<bool> HandSelectionChanged;
        public event Action<bool> ClearColorSelectionChanged;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            CaptureSelectionCameraBasePosition();
            ResetForNewAttempt();
        }

        private void Start()
        {
            SubscribeToLevelEvents();
            ResetForNewAttempt();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            UnsubscribeFromLevelEvents();
            EndHandSelection();
            EndClearColorResolution();
            RestoreSelectionCameraImmediate();
        }

        public int GetRemainingUses(PowerUpType powerUp)
        {
            switch (powerUp)
            {
                case PowerUpType.AddStack: return addStackUses;
                case PowerUpType.Shuffle: return shuffleUses;
                case PowerUpType.Hand: return handUses;
                case PowerUpType.ClearColor: return clearColorUses;
                default: return 0;
            }
        }

        public bool CanUseAddStack => IsAttemptPlaying && IsCrowdReady && !IsInteractionLocked && addStackUses > 0 && ConveyorManager.Instance != null;

        public bool CanUseShuffle => IsAttemptPlaying && IsCrowdReady && !IsInteractionLocked && shuffleUses > 0 &&
            QueueManager.Instance != null && QueueManager.Instance.CanShuffleQueuedStacks;

        public bool CanBeginHandSelection => IsAttemptPlaying && IsCrowdReady && !IsInteractionLocked && handUses > 0 &&
            ConveyorManager.Instance != null && ConveyorManager.Instance.CanAcceptStack &&
            QueueManager.Instance != null &&
            QueueManager.Instance.HasSelectableDeeperStack;

        public bool CanBeginClearColorSelection => IsAttemptPlaying && IsCrowdReady && !IsInteractionLocked && clearColorUses > 0 &&
            ((QueueManager.Instance != null && QueueManager.Instance.HasClearColorSelectableStack) ||
             (RackManager.Instance != null && RackManager.Instance.HasClearColorSelectableStack));

        public bool TryUseAddStack()
        {
            if (!CanUseAddStack || !ConveyorManager.Instance.TryAddAttemptCapacity()) return false;

            addStackUses--;
            StateChanged?.Invoke();
            return true;
        }

        public bool TryUseShuffle()
        {
            if (!CanUseShuffle || !QueueManager.Instance.TryShuffleQueuedStacks()) return false;

            shuffleUses--;
            StateChanged?.Invoke();
            return true;
        }

        public bool BeginHandSelection()
        {
            if (!CanBeginHandSelection) return false;

            IsHandSelectionActive = true;
            timeScaleBeforeHandSelection = Time.timeScale;
            Time.timeScale = 0f;
            QueueManager.Instance.SetHandSelectionVisuals(true);
            MoveSelectionCamera(isSelecting: true);
            HandSelectionChanged?.Invoke(true);
            StateChanged?.Invoke();
            return true;
        }

        public bool TrySelectHandStack(StackItem stack)
        {
            if (!IsHandSelectionActive || QueueManager.Instance == null) return false;

            if (!QueueManager.Instance.TrySendHandSelectedStack(stack)) return false;

            handUses--;
            EndHandSelection();
            StateChanged?.Invoke();
            return true;
        }

        public void CancelHandSelection()
        {
            if (!IsHandSelectionActive) return;
            EndHandSelection();
            StateChanged?.Invoke();
        }

        public bool BeginClearColorSelection()
        {
            if (!CanBeginClearColorSelection) return false;

            IsClearColorSelectionActive = true;
            timeScaleBeforeClearColor = Time.timeScale;
            Time.timeScale = 0f;
            QueueManager.Instance?.SetClearColorSelectionVisuals(true);
            RackManager.Instance?.SetClearColorSelectionVisuals(true);
            MoveSelectionCamera(isSelecting: true);
            ClearColorSelectionChanged?.Invoke(true);
            StateChanged?.Invoke();
            return true;
        }

        public bool TrySelectClearColorStack(StackItem stack)
        {
            if (!IsClearColorSelectionActive || stack == null || !IsClearColorTarget(stack)) return false;

            ItemDataSO selectedData = stack.Data;
            if (selectedData == null || CrowdManager.Instance == null || !CrowdManager.Instance.HasRemainingDemand(selectedData)) return false;

            clearColorUses--;
            EndClearColorSelection(restoreTimeScale: false);
            IsClearColorResolving = true;
            clearColorResolutionRoutine = StartCoroutine(ResolveClearColorRoutine(selectedData, stack));
            StateChanged?.Invoke();
            return true;
        }

        public void CancelClearColorSelection()
        {
            if (!IsClearColorSelectionActive) return;

            EndClearColorSelection(restoreTimeScale: true);
            StateChanged?.Invoke();
        }

        public void ResetForNewAttempt()
        {
            EndHandSelection();
            EndClearColorResolution();
            addStackUses = UsesPerAttempt;
            shuffleUses = UsesPerAttempt;
            handUses = UsesPerAttempt;
            clearColorUses = UsesPerAttempt;
            StateChanged?.Invoke();
        }

        private bool IsAttemptPlaying => LevelManager.Instance == null || LevelManager.Instance.IsGameActive;
        private bool IsCrowdReady => CrowdManager.Instance != null && !CrowdManager.Instance.IsSpawningCustomers;
        private bool IsInteractionLocked => IsHandSelectionActive || IsClearColorSelectionActive || IsClearColorResolving;

        private void EndHandSelection()
        {
            if (!IsHandSelectionActive) return;

            QueueManager.Instance?.SetHandSelectionVisuals(false);
            Time.timeScale = timeScaleBeforeHandSelection;
            MoveSelectionCamera(isSelecting: false);
            IsHandSelectionActive = false;
            HandSelectionChanged?.Invoke(false);
        }

        private bool IsClearColorTarget(StackItem stack)
        {
            return (QueueManager.Instance != null && QueueManager.Instance.IsClearColorSelectableStack(stack)) ||
                   (RackManager.Instance != null && RackManager.Instance.IsClearColorSelectableStack(stack));
        }

        private IEnumerator ResolveClearColorRoutine(ItemDataSO selectedData, StackItem visualSource)
        {
            bool resolved = CrowdManager.Instance != null && CrowdManager.Instance.ResolveFoodTypeForClearColor(selectedData, visualSource);

            if (resolved)
            {
                QueueManager.Instance?.RemoveStacksByData(selectedData);
                RackManager.Instance?.RemoveStacksByData(selectedData);
                ConveyorManager.Instance?.RemoveStacksByData(selectedData);
                yield return new WaitForSecondsRealtime(clearColorResolutionDuration);
                CrowdManager.Instance?.CompleteClearColorResolution();
            }

            FinishClearColorResolution();
        }

        private void EndClearColorSelection(bool restoreTimeScale)
        {
            if (!IsClearColorSelectionActive) return;

            QueueManager.Instance?.SetClearColorSelectionVisuals(false);
            RackManager.Instance?.SetClearColorSelectionVisuals(false);
            IsClearColorSelectionActive = false;
            if (restoreTimeScale) Time.timeScale = timeScaleBeforeClearColor;
            MoveSelectionCamera(isSelecting: false);
            ClearColorSelectionChanged?.Invoke(false);
        }

        private void FinishClearColorResolution()
        {
            if (!IsClearColorResolving) return;

            IsClearColorResolving = false;
            clearColorResolutionRoutine = null;
            Time.timeScale = timeScaleBeforeClearColor;
            StateChanged?.Invoke();
        }

        private void EndClearColorResolution()
        {
            if (clearColorResolutionRoutine != null) StopCoroutine(clearColorResolutionRoutine);
            clearColorResolutionRoutine = null;

            if (IsClearColorSelectionActive)
            {
                EndClearColorSelection(restoreTimeScale: true);
            }
            else if (IsClearColorResolving)
            {
                IsClearColorResolving = false;
                Time.timeScale = timeScaleBeforeClearColor;
                MoveSelectionCamera(isSelecting: false);
            }
        }

        private void CaptureSelectionCameraBasePosition()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                selectionCameraTransform = mainCamera.transform;
                selectionCameraBaseWorldPosition = selectionCameraTransform.position;
                hasSelectionCameraBasePosition = true;
            }
        }

        private void MoveSelectionCamera(bool isSelecting)
        {
            if (isSelecting && (QueueManager.Instance == null || !QueueManager.Instance.HasColumnWithAtLeastOccupiedStacks(3)))
            {
                return;
            }

            if (!isSelecting && !isSelectionCameraOffsetApplied) return;

            if (selectionCameraTransform == null)
            {
                CaptureSelectionCameraBasePosition();
            }

            if (selectionCameraTransform == null)
            {
                if (!warnedMissingSelectionCamera)
                {
                    Debug.LogWarning("[PowerUpManager] Camera.main was not found; selection will not move the camera.", this);
                    warnedMissingSelectionCamera = true;
                }

                return;
            }

            if (!hasSelectionCameraBasePosition)
            {
                selectionCameraBaseWorldPosition = selectionCameraTransform.position;
                hasSelectionCameraBasePosition = true;
            }

            selectionCameraTween?.Kill();
            float localYOffset = isSelecting ? GetSelectionCameraLocalYOffset() : 0f;
            Vector3 targetPosition = selectionCameraBaseWorldPosition +
                (isSelecting ? selectionCameraTransform.up * localYOffset : Vector3.zero);
            selectionCameraTween = selectionCameraTransform.DOMove(targetPosition, selectionCameraMoveDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
            isSelectionCameraOffsetApplied = isSelecting;
        }

        private float GetSelectionCameraLocalYOffset()
        {
            Camera mainCamera = selectionCameraTransform != null
                ? selectionCameraTransform.GetComponent<Camera>()
                : null;
            QueueManager queueManager = QueueManager.Instance;
            if (mainCamera == null || !mainCamera.orthographic || queueManager == null ||
                !queueManager.TryGetOccupiedStackBounds(out Bounds queueBounds))
            {
                return selectionCameraLocalYOffset;
            }

            // OrthographicCameraScaler makes the visible world height aspect-dependent. Refresh
            // it before measuring so the selection offset always uses the active device framing.
            OrthographicCameraScaler scaler = mainCamera.GetComponent<OrthographicCameraScaler>();
            scaler?.RecalculateCameraBounds();

            float queueBottomInCameraSpace = GetLowestCameraLocalY(queueBounds, selectionCameraTransform);
            float visibleBottomInCameraSpace = -mainCamera.orthographicSize +
                (mainCamera.orthographicSize * 2f * selectionCameraBottomViewportPadding);

            // Moving the camera down (negative local Y) raises the queue on screen. Do not move
            // up when the whole occupied queue already clears the reserved viewport area.
            return Mathf.Min(0f, queueBottomInCameraSpace - visibleBottomInCameraSpace);
        }

        private float GetLowestCameraLocalY(Bounds bounds, Transform cameraTransform)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            float lowest = float.PositiveInfinity;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                        float localY = Vector3.Dot(corner - selectionCameraBaseWorldPosition, cameraTransform.up);
                        lowest = Mathf.Min(lowest, localY);
                    }
                }
            }

            return lowest;
        }

        private void RestoreSelectionCameraImmediate()
        {
            selectionCameraTween?.Kill();
            selectionCameraTween = null;
            isSelectionCameraOffsetApplied = false;

            if (selectionCameraTransform != null)
            {
                selectionCameraTransform.position = selectionCameraBaseWorldPosition;
            }
        }

        private void SubscribeToLevelEvents()
        {
            if (LevelManager.Instance == null) return;

            LevelManager.Instance.OnLevelLoaded += HandleLevelLoaded;
            LevelManager.Instance.OnLevelWon += EndHandSelection;
            LevelManager.Instance.OnLevelLost += EndHandSelection;
        }

        private void UnsubscribeFromLevelEvents()
        {
            if (LevelManager.Instance == null) return;

            LevelManager.Instance.OnLevelLoaded -= HandleLevelLoaded;
            LevelManager.Instance.OnLevelWon -= EndHandSelection;
            LevelManager.Instance.OnLevelLost -= EndHandSelection;
        }

        private void HandleLevelLoaded(int _) => ResetForNewAttempt();
    }
}
