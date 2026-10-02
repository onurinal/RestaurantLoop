using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace RestaurantLoop.UI
{
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        public enum TutorialStep
        {
            None,
            TapFoodToConveyor,   // Step 1: Tap the board item
            WaitUntilInRack,     // Step 2: Wait until item travels to rack (Hand hidden)
            TapRackToConveyor    // Step 3: Tap the rack item to send it back
        }

        private TutorialStep currentStep = TutorialStep.None;
        public TutorialStep CurrentStep 
        { 
            get => currentStep; 
            private set
            {
                if (currentStep != value)
                {
                    currentStep = value;
                    Debug.Log($"<color=magenta>[TUTORIAL]</color> Step Changed To: <color=yellow>{currentStep}</color>");
                }
            }
        }

        [Header("UI References")]
        [SerializeField] private GameObject tutorialContainer;
        [SerializeField] private TextMeshProUGUI tutorialText;
        [SerializeField] private RectTransform handPointer;
        [SerializeField] private GameObject raycastBlockerPanel; // Tüm ekranı kaplayan, arkaya tıklamayı engelleyen UI Image paneli

        [Header("Animation Settings")]
        [SerializeField] private float tapAnimationDuration = 0.4f;
        [SerializeField] private float tapScaleAmount = 0.85f;
        [SerializeField] private Vector2 handWorldOffset = new Vector2(30f, -50f); 
        [SerializeField] private float pointerScaleMultiplier = 2.5f; // İstediğin 2.5 kat büyüklük

        // Aktif olarak tıklanmasına izin verilen target referansları (Input kontrolü için dışarıdan sorgulanabilir)
        public Transform CurrentAllowedTarget { get; private set; } = null;

        private Sequence handSequence;
        private Camera mainCamera;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            mainCamera = Camera.main;
        }

        private void Start()
        {
            HideTutorial();
        }

        /// <summary>
        /// Step 1: Starts the tutorial pointing at the initial board food.
        /// </summary>
        public void StartTutorial(Transform targetFoodTransform)
        {
            CurrentStep = TutorialStep.TapFoodToConveyor;
            CurrentAllowedTarget = targetFoodTransform;

            // Step 1'de blocker açık olacak ama hedef objenin tıklanmasına izin vereceğiz 
            // (Ya da blocker tamamen kapatılıp sadece istenmeyen objeler filtrelenecek)
            SetBlockerActive(false); 

            ShowTutorialAtWorldPosition("Tap a food to place into the conveyor!", targetFoodTransform);
            Debug.Log("<color=cyan>[TUTORIAL]</color> Started Step 1: Pointing at board food.");
        }

        /// <summary>
        /// Step 2: Hides the hand while the item travels along the belt into the rack. Blocks all clicks.
        /// </summary>
        public void EnterStepWaitInRack()
        {
            CurrentStep = TutorialStep.WaitUntilInRack;
            CurrentAllowedTarget = null;
            
            if (handPointer != null) handPointer.gameObject.SetActive(false);
            if (tutorialContainer != null) tutorialContainer.SetActive(true);
            if (tutorialText != null) tutorialText.text = "Watch the item go into the rack!";
            
            // Step 2'de oyuncunun hiçbir şeye tıklamaması için blocker'ı açıyoruz!
            SetBlockerActive(true);

            Debug.Log("<color=cyan>[TUTORIAL]</color> Started Step 2: Hand hidden, waiting for item to reach rack. Clicks blocked.");
        }

        /// <summary>
        /// Step 3: Points the hand at the item now sitting in the rack slot.
        /// </summary>
        public void StartStepTapRack(Transform rackSlotTransform)
        {
            CurrentStep = TutorialStep.TapRackToConveyor;
            CurrentAllowedTarget = rackSlotTransform;

            // Step 3'te tekrar serbest bırakıyoruz, sadece rack'tekine tıklanabilecek
            SetBlockerActive(false);

            if (handPointer != null) handPointer.gameObject.SetActive(true);
            ShowTutorialAtWorldPosition("Tap the item in the rack to send it back!", rackSlotTransform);
            
            Debug.Log("<color=cyan>[TUTORIAL]</color> Started Step 3: Pointing at rack item.");
        }

        public void ShowTutorialAtWorldPosition(string message, Transform targetTransform)
        {
            if (targetTransform == null || mainCamera == null)
            {
                Debug.LogWarning("<color=orange>[TUTORIAL]</color> TargetTransform or MainCamera is null!");
                return;
            }

            if (tutorialContainer != null) tutorialContainer.SetActive(true);
            if (handPointer != null) handPointer.gameObject.SetActive(true);
            if (tutorialText != null) tutorialText.text = message;

            Vector3 screenPosition = mainCamera.WorldToScreenPoint(targetTransform.position);
            handPointer.position = screenPosition + (Vector3)handWorldOffset;

            AnimateHand();
        }

        public void ShowTutorialAtUIPosition(string message, RectTransform targetUIElement)
        {
            if (targetUIElement == null) return;

            if (tutorialContainer != null) tutorialContainer.SetActive(true);
            if (handPointer != null) handPointer.gameObject.SetActive(true);
            if (tutorialText != null) tutorialText.text = message;

            handPointer.position = targetUIElement.position + (Vector3)handWorldOffset;

            AnimateHand();
        }

        public void HideTutorial()
        {
            if (CurrentStep != TutorialStep.None)
            {
                Debug.Log("<color=magenta>[TUTORIAL]</color> <color=green>Tutorial Completed & Hidden!</color>");
            }

            CurrentStep = TutorialStep.None;
            CurrentAllowedTarget = null;
            SetBlockerActive(false);

            if (tutorialContainer != null) tutorialContainer.SetActive(false);
            handSequence?.Kill();
        }

        private void SetBlockerActive(bool active)
        {
            if (raycastBlockerPanel != null)
            {
                raycastBlockerPanel.SetActive(active);
            }
        }

        private void AnimateHand()
        {
            handSequence?.Kill();
            if (handPointer == null) return;

            // İstediğin gibi pointer boyutunu 2.5 katına sabitliyoruz ve animasyonu bunun üzerinden yürütüyoruz
            Vector3 baseScale = Vector3.one * pointerScaleMultiplier;
            handPointer.localScale = baseScale;

            float scaledTapAmount = tapScaleAmount * pointerScaleMultiplier;

            handSequence = DOTween.Sequence();
            handSequence.Append(handPointer.DOScale(scaledTapAmount, tapAnimationDuration).SetEase(Ease.InOutQuad))
                        .Append(handPointer.DOScale(baseScale.x, tapAnimationDuration).SetEase(Ease.InOutQuad))
                        .SetLoops(-1);
        }

        private void OnDestroy()
        {
            handSequence?.Kill();
        }
    }
}
