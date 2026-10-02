using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RestaurantLoop.Core;

namespace RestaurantLoop.UI
{
    /// <summary>
    /// Binds the existing PowerUP1-4 scene buttons to the attempt-local power-up
    /// state and supplies the small Hand selection prompt/cancel affordance.
    /// </summary>
    public sealed class PowerUpUIController : MonoBehaviour
    {
        [Header("Power-Up Buttons")]
        [SerializeField] private Button addStackButton;
        [SerializeField] private Button shuffleButton;
        [SerializeField] private Button handButton;
        [SerializeField] private Button clearColorButton;

        [Header("Remaining-Use Texts")]
        [Tooltip("Inspector-authored count text under the Add Stack button.")]
        [SerializeField] private TMP_Text addStackCountText;
        [Tooltip("Inspector-authored count text under the Shuffle button.")]
        [SerializeField] private TMP_Text shuffleCountText;
        [Tooltip("Inspector-authored count text under the Hand button.")]
        [SerializeField] private TMP_Text handCountText;
        [Tooltip("Inspector-authored count text under the Clear Color button.")]
        [SerializeField] private TMP_Text clearColorCountText;

        private CanvasGroup addStackCanvasGroup;
        private CanvasGroup shuffleCanvasGroup;
        private CanvasGroup handCanvasGroup;
        private CanvasGroup clearColorCanvasGroup;
        private const float SpawningAlpha = 0.6f;

        [Header("Hand Selection UI")]
        [Tooltip("Authored panel to show while Hand is waiting for a deeper queue stack.")]
        [SerializeField] private GameObject handSelectionPanel;
        [Tooltip("Optional authored prompt text object. It is toggled with the panel if assigned separately.")]
        [SerializeField] private GameObject handSelectionPrompt;
        [Tooltip("Optional authored Cancel button. Assign it to wire cancellation automatically.")]
        [SerializeField] private Button handCancelButton;

        [Header("Clear Color Selection UI")]
        [Tooltip("Authored panel to show while Clear Color is waiting for a queue or rack stack.")]
        [SerializeField] private GameObject clearColorSelectionPanel;
        [Tooltip("Optional authored prompt text object. It is toggled with the panel if assigned separately.")]
        [SerializeField] private GameObject clearColorSelectionPrompt;
        [Tooltip("Optional authored Cancel button. Assign it to wire cancellation automatically.")]
        [SerializeField] private Button clearColorCancelButton;

        private PowerUpManager powerUps;
        private bool lastHandAvailability;
        private bool lastCrowdReady;
        private readonly Dictionary<TMP_Text, int> lastLabelValues = new Dictionary<TMP_Text, int>();

        private void Start()
        {
            powerUps = PowerUpManager.Instance != null
                ? PowerUpManager.Instance
                : gameObject.AddComponent<PowerUpManager>();

            WarnAboutMissingReferences();
            addStackCanvasGroup = GetOrAddCanvasGroup(addStackButton);
            shuffleCanvasGroup = GetOrAddCanvasGroup(shuffleButton);
            handCanvasGroup = GetOrAddCanvasGroup(handButton);
            clearColorCanvasGroup = GetOrAddCanvasGroup(clearColorButton);

            if (addStackButton != null) addStackButton.onClick.AddListener(UseAddStack);
            if (shuffleButton != null) shuffleButton.onClick.AddListener(UseShuffle);
            if (handButton != null) handButton.onClick.AddListener(BeginHandSelection);
            if (clearColorButton != null) clearColorButton.onClick.AddListener(BeginClearColorSelection);
            if (handCancelButton != null) handCancelButton.onClick.AddListener(CancelHandSelection);
            if (clearColorCancelButton != null) clearColorCancelButton.onClick.AddListener(CancelClearColorSelection);

            powerUps.StateChanged += Refresh;
            powerUps.HandSelectionChanged += SetHandSelectionOverlayVisible;
            powerUps.ClearColorSelectionChanged += SetClearColorSelectionOverlayVisible;
            if (QueueManager.Instance != null) QueueManager.Instance.QueueChanged += Refresh;
            if (ConveyorManager.Instance != null) ConveyorManager.Instance.CapacityChanged += HandleCapacityChanged;

            Refresh();
            lastHandAvailability = powerUps.CanBeginHandSelection;
            lastCrowdReady = IsCrowdReady;
            SetHandSelectionOverlayVisible(powerUps.IsHandSelectionActive);
            SetClearColorSelectionOverlayVisible(powerUps.IsClearColorSelectionActive);
        }

        private void Update()
        {
            if (powerUps == null) return;

            bool handAvailability = powerUps.CanBeginHandSelection;
            bool crowdReady = IsCrowdReady;
            if (handAvailability == lastHandAvailability && crowdReady == lastCrowdReady) return;

            lastHandAvailability = handAvailability;
            lastCrowdReady = crowdReady;
            Refresh();
        }

        private void OnDestroy()
        {
            if (powerUps != null)
            {
                powerUps.StateChanged -= Refresh;
                powerUps.HandSelectionChanged -= SetHandSelectionOverlayVisible;
                powerUps.ClearColorSelectionChanged -= SetClearColorSelectionOverlayVisible;
            }

            if (QueueManager.Instance != null) QueueManager.Instance.QueueChanged -= Refresh;
            if (ConveyorManager.Instance != null) ConveyorManager.Instance.CapacityChanged -= HandleCapacityChanged;
            if (addStackButton != null) addStackButton.onClick.RemoveListener(UseAddStack);
            if (shuffleButton != null) shuffleButton.onClick.RemoveListener(UseShuffle);
            if (handButton != null) handButton.onClick.RemoveListener(BeginHandSelection);
            if (clearColorButton != null) clearColorButton.onClick.RemoveListener(BeginClearColorSelection);
            if (handCancelButton != null) handCancelButton.onClick.RemoveListener(CancelHandSelection);
            if (clearColorCancelButton != null) clearColorCancelButton.onClick.RemoveListener(CancelClearColorSelection);
        }

        private void Refresh()
        {
            if (powerUps == null) return;

            bool isSpawning = !IsCrowdReady;
            SetButton(addStackButton, addStackCanvasGroup, addStackCountText,
                powerUps.GetRemainingUses(PowerUpType.AddStack), powerUps.CanUseAddStack, isSpawning);
            SetButton(shuffleButton, shuffleCanvasGroup, shuffleCountText,
                powerUps.GetRemainingUses(PowerUpType.Shuffle), powerUps.CanUseShuffle, isSpawning);
            SetButton(handButton, handCanvasGroup, handCountText,
                powerUps.GetRemainingUses(PowerUpType.Hand), powerUps.CanBeginHandSelection, isSpawning);
            SetButton(clearColorButton, clearColorCanvasGroup, clearColorCountText,
                powerUps.GetRemainingUses(PowerUpType.ClearColor), powerUps.CanBeginClearColorSelection, isSpawning);
            lastHandAvailability = powerUps.CanBeginHandSelection;
            lastCrowdReady = IsCrowdReady;
        }

        private void HandleCapacityChanged(int _, int __) => Refresh();

        private static bool IsCrowdReady => CrowdManager.Instance != null && !CrowdManager.Instance.IsSpawningCustomers;

        private void UseAddStack() => powerUps?.TryUseAddStack();
        private void UseShuffle() => powerUps?.TryUseShuffle();
        private void BeginHandSelection() => powerUps?.BeginHandSelection();
        private void BeginClearColorSelection() => powerUps?.BeginClearColorSelection();
        public void CancelHandSelection() => powerUps?.CancelHandSelection();
        public void CancelClearColorSelection() => powerUps?.CancelClearColorSelection();

        private static CanvasGroup GetOrAddCanvasGroup(Button button)
        {
            if (button == null) return null;
            return button.TryGetComponent(out CanvasGroup canvasGroup)
                ? canvasGroup
                : button.gameObject.AddComponent<CanvasGroup>();
        }

        private void WarnAboutMissingReferences()
        {
            List<string> missingReferences = new List<string>();
            if (addStackButton == null) missingReferences.Add("Add Stack Button");
            if (shuffleButton == null) missingReferences.Add("Shuffle Button");
            if (handButton == null) missingReferences.Add("Hand Button");
            if (clearColorButton == null) missingReferences.Add("Clear Color Button");
            if (addStackCountText == null) missingReferences.Add("Add Stack Count Text");
            if (shuffleCountText == null) missingReferences.Add("Shuffle Count Text");
            if (handCountText == null) missingReferences.Add("Hand Count Text");
            if (clearColorCountText == null) missingReferences.Add("Clear Color Count Text");

            if (missingReferences.Count > 0)
            {
                Debug.LogWarning($"[PowerUpUIController] Missing Inspector assignments: {string.Join(", ", missingReferences)}.", this);
            }
        }

        private void SetButton(
            Button button,
            CanvasGroup canvasGroup,
            TMP_Text label,
            int uses,
            bool interactable,
            bool isSpawning)
        {
            if (button == null) return;

            button.interactable = interactable && !isSpawning;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = isSpawning ? SpawningAlpha : 1f;
                canvasGroup.interactable = !isSpawning;
                // Keep the UI surface blocking raycasts so a disabled button cannot pass
                // the same touch through to gameplay objects behind it.
                canvasGroup.blocksRaycasts = true;
            }

            // Assigning TMP_Text.text marks the text dirty and forces a full glyph/mesh
            // rebuild even when the string is identical, and Refresh runs on every conveyor
            // capacity change. Skipping the redundant assignment renders the same glyphs.
            if (label != null)
            {
                if (!lastLabelValues.TryGetValue(label, out int previous) || previous != uses)
                {
                    lastLabelValues[label] = uses;
                    label.text = uses.ToString();
                }
            }
        }

        private void SetHandSelectionOverlayVisible(bool visible)
        {
            if (handSelectionPanel != null) handSelectionPanel.SetActive(visible);
            if (handSelectionPrompt != null) handSelectionPrompt.SetActive(visible);
            Refresh();
        }

        private void SetClearColorSelectionOverlayVisible(bool visible)
        {
            if (clearColorSelectionPanel != null) clearColorSelectionPanel.SetActive(visible);
            if (clearColorSelectionPrompt != null) clearColorSelectionPrompt.SetActive(visible);
            Refresh();
        }
    }
}
