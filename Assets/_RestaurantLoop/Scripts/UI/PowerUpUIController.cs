using TMPro;
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
        private Button addStackButton;
        private Button shuffleButton;
        private Button handButton;
        private Button clearColorButton;
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

        private void Start()
        {
            powerUps = PowerUpManager.Instance != null
                ? PowerUpManager.Instance
                : gameObject.AddComponent<PowerUpManager>();

            addStackButton = FindButton("PowerUP1");
            shuffleButton = FindButton("PowerUP2");
            handButton = FindButton("PowerUP3");
            clearColorButton = FindButton("PowerUP4");
            if (addStackButton != null && addStackButton.transform.parent != null)
            {
                // The authored placeholder container was previously hidden. Keep
                // the scene usable even if a later scene copy retains that flag.
                addStackButton.transform.parent.gameObject.SetActive(true);
            }

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
            SetHandSelectionOverlayVisible(powerUps.IsHandSelectionActive);
            SetClearColorSelectionOverlayVisible(powerUps.IsClearColorSelectionActive);
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
        }

        private void HandleCapacityChanged(int _, int __) => Refresh();

        private void UseAddStack() => powerUps?.TryUseAddStack();
        private void UseShuffle() => powerUps?.TryUseShuffle();
        private void BeginHandSelection() => powerUps?.BeginHandSelection();
        private void BeginClearColorSelection() => powerUps?.BeginClearColorSelection();
        public void CancelHandSelection() => powerUps?.CancelHandSelection();
        public void CancelClearColorSelection() => powerUps?.CancelClearColorSelection();

        private Button FindButton(string name)
        {
            GameObject buttonObject = GameObject.Find(name);
            return buttonObject != null ? buttonObject.GetComponent<Button>() : null;
        }

        private static TMP_Text GetLabel(Button button)
        {
            return button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
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
