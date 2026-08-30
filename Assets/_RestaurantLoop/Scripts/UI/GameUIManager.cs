using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using DG.Tweening;
using RestaurantLoop.Audio;
using RestaurantLoop.Core;

namespace RestaurantLoop.UI
{
    public class GameUIManager : MonoBehaviour
    {
        [Header("Settings Panels")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject winPanel;
        [SerializeField] private GameObject losePanel;

        [Header("Panel Pop Animation")]
        [SerializeField, Min(0f)] private float panelPopInDuration = 0.25f;
        [SerializeField, Min(0f)] private float panelPopOutDuration = 0.16f;
        [SerializeField, Range(0.1f, 1f)] private float panelPopInStartScale = 0.75f;
        [SerializeField, Range(0.1f, 1f)] private float panelPopOutEndScale = 0.75f;
        [SerializeField] private Ease panelPopInEase = Ease.OutBack;
        [SerializeField] private Ease panelPopOutEase = Ease.InBack;

        [Header("In-Game UI Elements")]
        [SerializeField] private TextMeshProUGUI topLevelText;
        [SerializeField] private Button gameSettingsButton;
        [SerializeField] private Button closeSettingsButton;

        [Header("Legacy Audio Sliders (Kept for future use)")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;

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

        [Header("Game State Buttons")]
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Button winMainMenuButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button loseMainMenuButton;

        [Header("Power-Up Buttons")]
        [SerializeField] private Button powerUp1Button;
        [SerializeField] private Button powerUp2Button;
        [SerializeField] private Button powerUp3Button;
        [SerializeField] private Button powerUp4Button;

        private bool isMusicOn = true;
        private bool isSfxOn = true;
        private bool isPanelTransitioning;
        private Vector3 settingsPanelBaseScale;
        private Vector3 winPanelBaseScale;
        private Vector3 losePanelBaseScale;

        private void Awake()
        {
            settingsPanelBaseScale = GetPanelScale(settingsPanel);
            winPanelBaseScale = GetPanelScale(winPanel);
            losePanelBaseScale = GetPanelScale(losePanel);
        }

        private void Start()
        {
            settingsPanel.SetActive(false);
            winPanel.SetActive(false);
            losePanel.SetActive(false);

            // Determine initial audio states based on saved volume (0 means off, >0 means on)
            float savedMusicVol = PlayerPrefs.GetFloat("MusicVolume", 1f);
            float savedSfxVol = PlayerPrefs.GetFloat("SfxVolume", 1f);
            isMusicOn = savedMusicVol > 0f;
            isSfxOn = savedSfxVol > 0f;

            // Update sliders just in case they are active in the hierarchy
            if (musicSlider != null) musicSlider.value = savedMusicVol;
            if (sfxSlider != null) sfxSlider.value = savedSfxVol;

            // Set initial button visuals
            UpdateMusicButtonVisual();
            UpdateSfxButtonVisual();

            // Bind UI Events
            gameSettingsButton.onClick.AddListener(OpenSettings);
            gameSettingsButton.onClick.AddListener(PlayTapSound);
            closeSettingsButton.onClick.AddListener(CloseSettings);
            closeSettingsButton.onClick.AddListener(PlayTapSound);

            // Bind Toggle Button Events
            if (musicToggleButton != null) musicToggleButton.onClick.AddListener(ToggleMusic);
            if (sfxToggleButton != null) sfxToggleButton.onClick.AddListener(ToggleSFX);

            // Bind Legacy Slider Events
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(UpdateMusicVolume);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(UpdateSfxVolume);

            // Subscribe to LevelManager events
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.OnLevelWon += ShowWinPanel;
                LevelManager.Instance.OnLevelLost += ShowLosePanel;
                LevelManager.Instance.OnLevelLoaded += UpdateTopLevelText;
                UpdateTopLevelText(LevelManager.Instance.CurrentLevelNumber);
            }

            // Bind Game State Buttons
            nextLevelButton.onClick.AddListener(OnNextLevelClicked);
            nextLevelButton.onClick.AddListener(PlayTapSound);
            retryButton.onClick.AddListener(OnRetryClicked);
            retryButton.onClick.AddListener(PlayTapSound);
            winMainMenuButton.onClick.AddListener(OnMainMenuClicked);
            winMainMenuButton.onClick.AddListener(PlayTapSound);
            loseMainMenuButton.onClick.AddListener(OnMainMenuClicked);
            loseMainMenuButton.onClick.AddListener(PlayTapSound);

            // Bind Custom Power-Up Audio Events
            if (powerUp1Button != null) powerUp1Button.onClick.AddListener(PlayPowerUp1Sound);
            if (powerUp2Button != null) powerUp2Button.onClick.AddListener(PlayPowerUp2Sound);
            if (powerUp3Button != null) powerUp3Button.onClick.AddListener(PlayPowerUp3Sound);
            if (powerUp4Button != null) powerUp4Button.onClick.AddListener(PlayPowerUp4Sound);
        }

        private void PlayTapSound()
        {
            if (AudioManager.Instance != null && AudioManager.Instance.tapSound != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.tapSound);
            }
        }

        // --- Custom Power-Up Sounds ---
        private void PlayPowerUp1Sound()
        {
            if (AudioManager.Instance != null && AudioManager.Instance.powerUp1Sound != null)
                AudioManager.Instance.PlaySFX(AudioManager.Instance.powerUp1Sound);
        }

        private void PlayPowerUp2Sound()
        {
            if (AudioManager.Instance != null && AudioManager.Instance.powerUp2Sound != null)
                AudioManager.Instance.PlaySFX(AudioManager.Instance.powerUp2Sound);
        }

        private void PlayPowerUp3Sound()
        {
            if (AudioManager.Instance != null && AudioManager.Instance.powerUp3Sound != null)
                AudioManager.Instance.PlaySFX(AudioManager.Instance.powerUp3Sound);
        }

        private void PlayPowerUp4Sound()
        {
            if (AudioManager.Instance != null && AudioManager.Instance.powerUp4Sound != null)
                AudioManager.Instance.PlaySFX(AudioManager.Instance.powerUp4Sound);
        }

        private void UpdateTopLevelText(int levelNumber)
        {
            if (topLevelText != null) topLevelText.text = $"LEVEL {levelNumber}";
        }

        private void OpenSettings() => ShowPanel(settingsPanel, settingsPanelBaseScale);
        private void CloseSettings() => HidePanel(settingsPanel, settingsPanelBaseScale);

        // --- New Toggle Logic ---
        private void ToggleMusic()
        {
            isMusicOn = !isMusicOn;
            float targetVolume = isMusicOn ? 1f : 0f;
            UpdateMusicVolume(targetVolume);

            if (musicSlider != null) musicSlider.value = targetVolume;
            UpdateMusicButtonVisual();
            PlayTapSound();
        }

        private void ToggleSFX()
        {
            isSfxOn = !isSfxOn;
            float targetVolume = isSfxOn ? 1f : 0f;
            UpdateSfxVolume(targetVolume);

            if (sfxSlider != null) sfxSlider.value = targetVolume;
            UpdateSfxButtonVisual();
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

        // --- Legacy Slider Logic ---
        private void UpdateMusicVolume(float value)
        {
            PlayerPrefs.SetFloat("MusicVolume", value);
            if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(value);
        }

        private void UpdateSfxVolume(float value)
        {
            PlayerPrefs.SetFloat("SfxVolume", value);
        }

        private void ShowWinPanel() => ShowPanel(winPanel, winPanelBaseScale);
        private void ShowLosePanel() => ShowPanel(losePanel, losePanelBaseScale);

        private void OnNextLevelClicked()
        {
            if (isPanelTransitioning) return;

            HidePanel(winPanel, winPanelBaseScale, () =>
            {
                if (LevelManager.Instance != null) LevelManager.Instance.CompleteLevel();
            });
        }

        private void OnRetryClicked()
        {
            if (isPanelTransitioning) return;

            HidePanel(losePanel, losePanelBaseScale, () =>
            {
                // Restart current level without reloading the Unity scene
                if (LevelManager.Instance != null)
                {
                    LevelManager.Instance.LoadCurrentLevel();
                }
            });
        }

        private void OnMainMenuClicked()
        {
            if (isPanelTransitioning) return;

            if (winPanel.activeSelf)
            {
                HidePanel(winPanel, winPanelBaseScale, LoadMainMenu);
            }
            else if (losePanel.activeSelf)
            {
                HidePanel(losePanel, losePanelBaseScale, LoadMainMenu);
            }
            else
            {
                LoadMainMenu();
            }
        }

        private void ShowPanel(GameObject panel, Vector3 baseScale)
        {
            if (panel == null) return;

            isPanelTransitioning = false;
            Transform panelTransform = panel.transform;
            panelTransform.DOKill();
            panel.SetActive(true);
            panelTransform.localScale = Vector3.Scale(baseScale, Vector3.one * panelPopInStartScale);
            panelTransform
                .DOScale(baseScale, panelPopInDuration)
                .SetEase(panelPopInEase)
                .SetUpdate(true);
        }

        private void HidePanel(GameObject panel, Vector3 baseScale, System.Action onComplete = null)
        {
            if (panel == null || !panel.activeSelf)
            {
                onComplete?.Invoke();
                return;
            }

            isPanelTransitioning = true;
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
                    isPanelTransitioning = false;
                    onComplete?.Invoke();
                });
        }

        private static Vector3 GetPanelScale(GameObject panel)
        {
            return panel != null ? panel.transform.localScale : Vector3.one;
        }

        private void LoadMainMenu() => SceneManager.LoadScene("MainMenu");

        private void OnDestroy()
        {
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.OnLevelWon -= ShowWinPanel;
                LevelManager.Instance.OnLevelLost -= ShowLosePanel;
                LevelManager.Instance.OnLevelLoaded -= UpdateTopLevelText;
            }

            gameSettingsButton.onClick.RemoveAllListeners();
            closeSettingsButton.onClick.RemoveAllListeners();
            if (musicToggleButton != null) musicToggleButton.onClick.RemoveAllListeners();
            if (sfxToggleButton != null) sfxToggleButton.onClick.RemoveAllListeners();
            if (musicSlider != null) musicSlider.onValueChanged.RemoveAllListeners();
            if (sfxSlider != null) sfxSlider.onValueChanged.RemoveAllListeners();
            nextLevelButton.onClick.RemoveAllListeners();
            retryButton.onClick.RemoveAllListeners();
            winMainMenuButton.onClick.RemoveAllListeners();
            loseMainMenuButton.onClick.RemoveAllListeners();

            // Unbind Power-Up Buttons
            if (powerUp1Button != null) powerUp1Button.onClick.RemoveAllListeners();
            if (powerUp2Button != null) powerUp2Button.onClick.RemoveAllListeners();
            if (powerUp3Button != null) powerUp3Button.onClick.RemoveAllListeners();
            if (powerUp4Button != null) powerUp4Button.onClick.RemoveAllListeners();
        }
    }
}
