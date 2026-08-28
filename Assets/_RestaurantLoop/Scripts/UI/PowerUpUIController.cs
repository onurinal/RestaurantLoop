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
        private TMP_Text addStackLabel;
        private TMP_Text shuffleLabel;
        private TMP_Text handLabel;
        private TMP_Text clearColorLabel;

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

        private void Start()
        {
            powerUps = PowerUpManager.Instance != null
                ? PowerUpManager.Instance
                : gameObject.AddComponent<PowerUpManager>();

            WarnAboutMissingPowerUpButtons();
            addStackLabel = GetLabel(addStackButton);
            shuffleLabel = GetLabel(shuffleButton);
            handLabel = GetLabel(handButton);
            clearColorLabel = GetLabel(clearColorButton);

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
            SetHandSelectionOverlayVisible(powerUps.IsHandSelectionActive);
            SetClearColorSelectionOverlayVisible(powerUps.IsClearColorSelectionActive);
        }

        private void Update()
        {
            if (powerUps == null) return;

            bool handAvailability = powerUps.CanBeginHandSelection;
            if (handAvailability == lastHandAvailability) return;

            lastHandAvailability = handAvailability;
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

            SetButton(addStackButton, addStackLabel, "Add\nStack", powerUps.GetRemainingUses(PowerUpType.AddStack), powerUps.CanUseAddStack);
            SetButton(shuffleButton, shuffleLabel, "Shuffle", powerUps.GetRemainingUses(PowerUpType.Shuffle), powerUps.CanUseShuffle);
            SetButton(handButton, handLabel, "Hand", powerUps.GetRemainingUses(PowerUpType.Hand), powerUps.CanBeginHandSelection);
            SetButton(clearColorButton, clearColorLabel, "Clear\nColor", powerUps.GetRemainingUses(PowerUpType.ClearColor), powerUps.CanBeginClearColorSelection);
            lastHandAvailability = powerUps.CanBeginHandSelection;
        }

        private void HandleCapacityChanged(int _, int __) => Refresh();

        private void UseAddStack() => powerUps?.TryUseAddStack();
        private void UseShuffle() => powerUps?.TryUseShuffle();
        private void BeginHandSelection() => powerUps?.BeginHandSelection();
        private void BeginClearColorSelection() => powerUps?.BeginClearColorSelection();
        public void CancelHandSelection() => powerUps?.CancelHandSelection();
        public void CancelClearColorSelection() => powerUps?.CancelClearColorSelection();

        private static TMP_Text GetLabel(Button button)
        {
            return button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        }

        private void WarnAboutMissingPowerUpButtons()
        {
            List<string> missingButtons = new List<string>();
            if (addStackButton == null) missingButtons.Add("Add Stack Button");
            if (shuffleButton == null) missingButtons.Add("Shuffle Button");
            if (handButton == null) missingButtons.Add("Hand Button");
            if (clearColorButton == null) missingButtons.Add("Clear Color Button");

            if (missingButtons.Count > 0)
            {
                Debug.LogWarning($"[PowerUpUIController] Missing inspector button assignments: {string.Join(", ", missingButtons)}.", this);
            }
        }

        private static void SetButton(Button button, TMP_Text label, string title, int uses, bool interactable)
        {
            if (button == null) return;
            button.interactable = interactable;
            if (label != null) label.text = $"{uses}";
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
