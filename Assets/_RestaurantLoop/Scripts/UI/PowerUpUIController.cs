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
        private GameObject handSelectionOverlay;
        private GameObject clearColorSelectionOverlay;

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

            BuildHandSelectionOverlay();
            BuildClearColorSelectionOverlay();
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

        private void BuildHandSelectionOverlay()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null || handSelectionOverlay != null) return;

            handSelectionOverlay = new GameObject("Hand Selection Overlay", typeof(RectTransform));
            RectTransform overlayRect = handSelectionOverlay.GetComponent<RectTransform>();
            overlayRect.SetParent(canvas.transform, false);
            overlayRect.anchorMin = new Vector2(0.5f, 1f);
            overlayRect.anchorMax = new Vector2(0.5f, 1f);
            overlayRect.anchoredPosition = new Vector2(0f, -250f);
            overlayRect.sizeDelta = new Vector2(560f, 120f);

            Image background = handSelectionOverlay.AddComponent<Image>();
            background.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);

            GameObject promptObject = new GameObject("Prompt", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform promptRect = promptObject.GetComponent<RectTransform>();
            promptRect.SetParent(overlayRect, false);
            promptRect.anchorMin = new Vector2(0f, 0f);
            promptRect.anchorMax = new Vector2(0.65f, 1f);
            promptRect.offsetMin = new Vector2(18f, 0f);
            promptRect.offsetMax = Vector2.zero;
            TextMeshProUGUI prompt = promptObject.GetComponent<TextMeshProUGUI>();
            prompt.font = handLabel != null ? handLabel.font : TMP_Settings.defaultFontAsset;
            prompt.fontSize = 28f;
            prompt.alignment = TextAlignmentOptions.Center;
            prompt.text = "Choose a deeper stack";

            GameObject cancelObject = new GameObject("Cancel", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform cancelRect = cancelObject.GetComponent<RectTransform>();
            cancelRect.SetParent(overlayRect, false);
            cancelRect.anchorMin = new Vector2(0.7f, 0.15f);
            cancelRect.anchorMax = new Vector2(0.96f, 0.85f);
            cancelRect.offsetMin = Vector2.zero;
            cancelRect.offsetMax = Vector2.zero;
            Image cancelImage = cancelObject.GetComponent<Image>();
            cancelImage.color = new Color(0.95f, 0.62f, 0.15f, 1f);
            Button cancelButton = cancelObject.GetComponent<Button>();
            cancelButton.targetGraphic = cancelImage;
            cancelButton.onClick.AddListener(powerUps.CancelHandSelection);

            GameObject cancelTextObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform cancelTextRect = cancelTextObject.GetComponent<RectTransform>();
            cancelTextRect.SetParent(cancelRect, false);
            cancelTextRect.anchorMin = Vector2.zero;
            cancelTextRect.anchorMax = Vector2.one;
            cancelTextRect.offsetMin = Vector2.zero;
            cancelTextRect.offsetMax = Vector2.zero;
            TextMeshProUGUI cancelText = cancelTextObject.GetComponent<TextMeshProUGUI>();
            cancelText.font = handLabel != null ? handLabel.font : TMP_Settings.defaultFontAsset;
            cancelText.fontSize = 26f;
            cancelText.color = Color.white;
            cancelText.alignment = TextAlignmentOptions.Center;
            cancelText.text = "Cancel";

            handSelectionOverlay.SetActive(false);
        }

        private void SetHandSelectionOverlayVisible(bool visible)
        {
            if (handSelectionOverlay != null) handSelectionOverlay.SetActive(visible);
            Refresh();
        }

        private void BuildClearColorSelectionOverlay()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null || clearColorSelectionOverlay != null) return;

            clearColorSelectionOverlay = new GameObject("Clear Color Selection Overlay", typeof(RectTransform), typeof(Image));
            RectTransform overlayRect = clearColorSelectionOverlay.GetComponent<RectTransform>();
            overlayRect.SetParent(canvas.transform, false);
            overlayRect.anchorMin = new Vector2(0.5f, 1f);
            overlayRect.anchorMax = new Vector2(0.5f, 1f);
            overlayRect.anchoredPosition = new Vector2(0f, -250f);
            overlayRect.sizeDelta = new Vector2(640f, 120f);
            clearColorSelectionOverlay.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.9f);

            GameObject promptObject = new GameObject("Prompt", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform promptRect = promptObject.GetComponent<RectTransform>();
            promptRect.SetParent(overlayRect, false);
            promptRect.anchorMin = new Vector2(0f, 0f);
            promptRect.anchorMax = new Vector2(0.7f, 1f);
            promptRect.offsetMin = new Vector2(18f, 0f);
            promptRect.offsetMax = Vector2.zero;
            TextMeshProUGUI prompt = promptObject.GetComponent<TextMeshProUGUI>();
            prompt.font = clearColorLabel != null ? clearColorLabel.font : TMP_Settings.defaultFontAsset;
            prompt.fontSize = 28f;
            prompt.alignment = TextAlignmentOptions.Center;
            prompt.text = "Tap a queue or rack stack";

            GameObject cancelObject = new GameObject("Cancel", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform cancelRect = cancelObject.GetComponent<RectTransform>();
            cancelRect.SetParent(overlayRect, false);
            cancelRect.anchorMin = new Vector2(0.74f, 0.15f);
            cancelRect.anchorMax = new Vector2(0.96f, 0.85f);
            cancelRect.offsetMin = Vector2.zero;
            cancelRect.offsetMax = Vector2.zero;
            Image cancelImage = cancelObject.GetComponent<Image>();
            cancelImage.color = new Color(0.95f, 0.62f, 0.15f, 1f);
            Button cancelButton = cancelObject.GetComponent<Button>();
            cancelButton.targetGraphic = cancelImage;
            cancelButton.onClick.AddListener(powerUps.CancelClearColorSelection);

            GameObject cancelTextObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform cancelTextRect = cancelTextObject.GetComponent<RectTransform>();
            cancelTextRect.SetParent(cancelRect, false);
            cancelTextRect.anchorMin = Vector2.zero;
            cancelTextRect.anchorMax = Vector2.one;
            cancelTextRect.offsetMin = Vector2.zero;
            cancelTextRect.offsetMax = Vector2.zero;
            TextMeshProUGUI cancelText = cancelTextObject.GetComponent<TextMeshProUGUI>();
            cancelText.font = prompt.font;
            cancelText.fontSize = 26f;
            cancelText.color = Color.white;
            cancelText.alignment = TextAlignmentOptions.Center;
            cancelText.text = "Cancel";

            clearColorSelectionOverlay.SetActive(false);
        }

        private void SetClearColorSelectionOverlayVisible(bool visible)
        {
            if (clearColorSelectionOverlay != null) clearColorSelectionOverlay.SetActive(visible);
            Refresh();
        }
    }
}
