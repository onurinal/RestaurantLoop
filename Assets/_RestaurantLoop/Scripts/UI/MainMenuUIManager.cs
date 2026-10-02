using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using DG.Tweening;
using RestaurantLoop.Audio;
using RestaurantLoop.Infrastructure;

namespace RestaurantLoop.UI
{
    public class MainMenuUIManager : MonoBehaviour
    {
        // Matches LevelManager's PlayerPrefs key. The tutorial is sequence index 0,
        // so the menu starts by presenting the first regular level to a new player.
        private const string HighestUnlockedLevelIndexPreferenceKey = "RestaurantLoop.HighestUnlockedLevelIndex";

        [Header("Panels")]
        [Tooltip("The dark background panel that appears instantly behind the settings menu")]
        [SerializeField] private GameObject darkOverlayPanel;
        [SerializeField] private GameObject settingsPanel;
        
        [Header("Confirmation Panel")]
        [SerializeField] private GameObject confirmLeavePanel;

        [Header("Settings Panel Pop Animation")]
        [SerializeField, Min(0f)] private float panelPopInDuration = 0.25f;
        [SerializeField, Min(0f)] private float panelPopOutDuration = 0.16f;
        [SerializeField, Range(0.1f, 1f)] private float panelPopInStartScale = 0.75f;
        [SerializeField, Range(0.1f, 1f)] private float panelPopOutEndScale = 0.75f;
        [SerializeField] private Ease panelPopInEase = Ease.OutBack;
        [SerializeField] private Ease panelPopOutEase = Ease.InBack;

        [Header("Main Menu Elements")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private TextMeshProUGUI levelText;

        [Header("Settings & Leave Elements")]
        [SerializeField] private Button closeSettingsButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private Button confirmLeaveYesButton;
        [SerializeField] private Button confirmLeaveNoButton;

        [Header("Sliders & Slider Buttons")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Slider vibrationSlider;
        [Tooltip("Invisible button placed over the vibration slider to detect taps")]
        [SerializeField] private Button vibrationSliderButton;
        [SerializeField] private VibrationSwitchView vibrationSwitchView;

        [Header("Audio Toggle Buttons")]
        [SerializeField] private Button musicToggleButton;
        [SerializeField] private Button sfxToggleButton;
        [SerializeField] private Image musicToggleImage;
        [SerializeField] private Image sfxToggleImage;

        [Header("Audio Toggle Sprites")]
        [SerializeField] private Sprite musicOnSprite;
        [SerializeField] private Sprite musicOffSprite;
        [SerializeField] private Sprite sfxOnSprite;
        [SerializeField] private Sprite sfxOffSprite;

        private bool isMusicOn = true;
        private bool isSfxOn = true;
        private Vector3 settingsPanelBaseScale;
        private Vector3 confirmLeavePanelBaseScale;

        private void Awake()
        {
            settingsPanelBaseScale = GetPanelScale(settingsPanel);
            confirmLeavePanelBaseScale = GetPanelScale(confirmLeavePanel);
        }

        private void Start()
        {
            if (darkOverlayPanel != null) darkOverlayPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (confirmLeavePanel != null) confirmLeavePanel.SetActive(false);

            playButton.onClick.AddListener(StartGame);
            settingsButton.onClick.AddListener(OpenSettings);
            closeSettingsButton.onClick.AddListener(CloseSettings);

            playButton.onClick.AddListener(PlayTapSound);
            settingsButton.onClick.AddListener(PlayTapSound);
            closeSettingsButton.onClick.AddListener(PlayTapSound);

            float savedMusicVol = PlayerPrefs.GetFloat("MusicVolume", 1f);
            float savedSfxVol = PlayerPrefs.GetFloat("SfxVolume", 1f);

            isMusicOn = savedMusicVol > 0f;
            isSfxOn = savedSfxVol > 0f;

            if (musicSlider != null) musicSlider.value = savedMusicVol;
            if (sfxSlider != null) sfxSlider.value = savedSfxVol;
            ResolveVibrationControls();
            bool vibrationEnabled = VibrationManager.Instance == null ||
                                    VibrationManager.Instance.IsVibrationEnabled;
            vibrationSwitchView?.SetState(vibrationEnabled, false);

            UpdateMusicButtonVisual();
            UpdateSfxButtonVisual();

            if (musicSlider != null) musicSlider.onValueChanged.AddListener(UpdateMusicVolume);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(UpdateSfxVolume);
            if (vibrationSlider != null) vibrationSlider.onValueChanged.AddListener(UpdateVibration);

            if (musicToggleButton != null) musicToggleButton.onClick.AddListener(ToggleMusic);
            if (sfxToggleButton != null) sfxToggleButton.onClick.AddListener(ToggleSFX);

            // Invisible tap target for the vibration switch
            if (vibrationSliderButton != null)
            {
                vibrationSliderButton.onClick.AddListener(ToggleVibrationSliderState);
                vibrationSliderButton.onClick.AddListener(PlayTapSound);
            }

            // Leave confirmation buttons
            if (leaveButton != null)
            {
                leaveButton.onClick.AddListener(OpenConfirmLeavePanel);
                leaveButton.onClick.AddListener(PlayTapSound);
            }
            if (confirmLeaveYesButton != null)
            {
                confirmLeaveYesButton.onClick.AddListener(QuitGame);
                confirmLeaveYesButton.onClick.AddListener(PlayTapSound);
            }
            if (confirmLeaveNoButton != null)
            {
                confirmLeaveNoButton.onClick.AddListener(CloseConfirmLeavePanel);
                confirmLeaveNoButton.onClick.AddListener(PlayTapSound);
            }

            UpdateLevelText();
        }

        private void UpdateLevelText()
        {
            if (levelText == null) return;

            int highestUnlockedLevel = Mathf.Max(
                1,
                PlayerPrefs.GetInt(HighestUnlockedLevelIndexPreferenceKey, 1));
            levelText.text = $"LEVEL {highestUnlockedLevel}";
        }

        private void PlayTapSound()
        {
            if (AudioManager.Instance != null && AudioManager.Instance.tapSound != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.tapSound);
            }
        }

        private void StartGame()
        {
            SceneManager.LoadScene("Gameplay");
        }

        private void OpenSettings()
        {
            // Enable dark overlay instantly without animation
            if (darkOverlayPanel != null)
                darkOverlayPanel.SetActive(true);

            ShowPanel(settingsPanel, settingsPanelBaseScale);
        }

        private void CloseSettings()
        {
            HidePanel(settingsPanel, settingsPanelBaseScale, () =>
            {
                // Instantly hide the overlay when the panel animation is done
                if (darkOverlayPanel != null)
                    darkOverlayPanel.SetActive(false);
            });
        }

        private void OpenConfirmLeavePanel()
        {
            ShowPanel(confirmLeavePanel, confirmLeavePanelBaseScale);
        }

        private void CloseConfirmLeavePanel()
        {
            HidePanel(confirmLeavePanel, confirmLeavePanelBaseScale);
        }

        private void QuitGame()
        {
            Debug.Log("Exiting Game from Main Menu...");
            Application.Quit();
        }

        // --- Shared Panel Animation Logic ---
        private void ShowPanel(GameObject panel, Vector3 baseScale)
        {
            if (panel == null) return;

            Transform panelTransform = panel.transform;
            panelTransform.DOKill();
            panel.SetActive(true);
            panelTransform.localScale = Vector3.Scale(baseScale, Vector3.one * panelPopInStartScale);
            
            panelTransform.DOScale(baseScale, panelPopInDuration).SetEase(panelPopInEase).SetUpdate(true);
        }

        private void HidePanel(GameObject panel, Vector3 baseScale, System.Action onComplete = null)
        {
            if (panel == null || !panel.activeSelf)
            {
                onComplete?.Invoke();
                return;
            }

            Transform panelTransform = panel.transform;
            panelTransform.DOKill();
            panelTransform
                .DOScale(Vector3.Scale(baseScale, Vector3.one * panelPopOutEndScale), panelPopOutDuration)
                .SetEase(panelPopOutEase)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    panel.SetActive(false);
                    panelTransform.localScale = baseScale;
                    onComplete?.Invoke();
                });
        }

        private static Vector3 GetPanelScale(GameObject panel)
        {
            return panel != null ? panel.transform.localScale : Vector3.one;
        }

        // --- Toggle Logic ---
        private void ToggleVibrationSliderState()
        {
            bool currentState = VibrationManager.Instance != null
                ? VibrationManager.Instance.IsVibrationEnabled
                : vibrationSlider == null || vibrationSlider.value > 0.5f;
            UpdateVibration(currentState ? 0f : 1f);
        }

        private void ToggleMusic()
        {
            float targetVolume = isMusicOn ? 0f : 1f;

            if (musicSlider != null) 
                musicSlider.value = targetVolume;
            else 
                UpdateMusicVolume(targetVolume);
            
            PlayTapSound();
        }

        private void ToggleSFX()
        {
            float targetVolume = isSfxOn ? 0f : 1f;

            if (sfxSlider != null) 
                sfxSlider.value = targetVolume;
            else 
                UpdateSfxVolume(targetVolume);

            PlayTapSound();
        }

        private void UpdateMusicButtonVisual()
        {
            if (musicToggleImage != null)
                musicToggleImage.sprite = isMusicOn ? musicOnSprite : musicOffSprite;
        }

        private void UpdateSfxButtonVisual()
        {
            if (sfxToggleImage != null)
                sfxToggleImage.sprite = isSfxOn ? sfxOnSprite : sfxOffSprite;
        }

        // --- Value Update Logic ---
        private void UpdateMusicVolume(float value)
        {
            PlayerPrefs.SetFloat("MusicVolume", value);
            if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(value);

            // Keep the toggle state and icon synchronized with slider changes.
            isMusicOn = value > 0f;
            UpdateMusicButtonVisual();
        }

        private void UpdateSfxVolume(float value)
        {
            PlayerPrefs.SetFloat("SfxVolume", value);
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetSfxVolume(value);
            }

            // Keep the toggle state and icon synchronized with slider changes.
            isSfxOn = value > 0f;
            UpdateSfxButtonVisual();
        }

        private void UpdateVibration(float value)
        {
            bool enabled = value > 0.5f;
            VibrationManager.Instance?.SetVibrationEnabled(enabled);
            vibrationSwitchView?.SetState(enabled, true);
        }

        private void ResolveVibrationControls()
        {
            if (vibrationSlider == null && settingsPanel != null)
            {
                Slider[] sliders = settingsPanel.GetComponentsInChildren<Slider>(true);
                for (int i = 0; i < sliders.Length; i++)
                {
                    if (sliders[i].transform.parent != null &&
                        sliders[i].transform.parent.name == "Vibration_Symbol")
                    {
                        vibrationSlider = sliders[i];
                        break;
                    }
                }
            }

            if (vibrationSliderButton == null && vibrationSlider != null)
            {
                Transform buttonTransform = vibrationSlider.transform.parent.Find("Button");
                if (buttonTransform != null)
                {
                    vibrationSliderButton = buttonTransform.GetComponent<Button>();
                }
            }

            if (vibrationSwitchView == null && vibrationSlider != null)
            {
                vibrationSwitchView = vibrationSlider.GetComponent<VibrationSwitchView>();
                if (vibrationSwitchView == null)
                {
                    vibrationSwitchView = vibrationSlider.gameObject.AddComponent<VibrationSwitchView>();
                }
            }

            vibrationSwitchView?.Initialize(vibrationSlider);
        }

        private void OnDestroy()
        {
            if (playButton != null) playButton.onClick.RemoveAllListeners();
            if (settingsButton != null) settingsButton.onClick.RemoveAllListeners();
            if (closeSettingsButton != null) closeSettingsButton.onClick.RemoveAllListeners();
            
            if (musicSlider != null) musicSlider.onValueChanged.RemoveAllListeners();
            if (sfxSlider != null) sfxSlider.onValueChanged.RemoveAllListeners();
            if (vibrationSlider != null) vibrationSlider.onValueChanged.RemoveAllListeners();
            
            if (musicToggleButton != null) musicToggleButton.onClick.RemoveAllListeners();
            if (sfxToggleButton != null) sfxToggleButton.onClick.RemoveAllListeners();
            if (vibrationSliderButton != null) vibrationSliderButton.onClick.RemoveAllListeners();
            
            if (leaveButton != null) leaveButton.onClick.RemoveAllListeners();
            if (confirmLeaveYesButton != null) confirmLeaveYesButton.onClick.RemoveAllListeners();
            if (confirmLeaveNoButton != null) confirmLeaveNoButton.onClick.RemoveAllListeners();
        }
    }
}
