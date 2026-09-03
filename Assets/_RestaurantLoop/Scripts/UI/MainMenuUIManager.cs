using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using DG.Tweening;
using RestaurantLoop.Audio;

namespace RestaurantLoop.UI
{
    public class MainMenuUIManager : MonoBehaviour
    {
        // Matches LevelManager's PlayerPrefs key. The tutorial is sequence index 0,
        // so the menu starts by presenting the first regular level to a new player.
        private const string HighestUnlockedLevelIndexPreferenceKey = "RestaurantLoop.HighestUnlockedLevelIndex";

        [Header("Panels")]
        [Tooltip("The dark background panel that appears instantly behind the settings menu")]
        [SerializeField] private GameObject darkOverlayPanel; // ADDED
        [SerializeField] private GameObject settingsPanel;

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

        [Header("Settings Elements")]
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

        private bool isMusicOn = true;
        private bool isSfxOn = true;
        private Vector3 settingsPanelBaseScale;

        private void Awake()
        {
            settingsPanelBaseScale = settingsPanel != null ? settingsPanel.transform.localScale : Vector3.one;
        }

        private void Start()
        {
            if (darkOverlayPanel != null) darkOverlayPanel.SetActive(false);
            settingsPanel.SetActive(false);

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

            UpdateMusicButtonVisual();
            UpdateSfxButtonVisual();

            if (musicSlider != null) musicSlider.onValueChanged.AddListener(UpdateMusicVolume);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(UpdateSfxVolume);

            if (musicToggleButton != null) musicToggleButton.onClick.AddListener(ToggleMusic);
            if (sfxToggleButton != null) sfxToggleButton.onClick.AddListener(ToggleSFX);

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
            if (settingsPanel == null) return;

            // Enable dark overlay instantly without animation
            if (darkOverlayPanel != null)
                darkOverlayPanel.SetActive(true);

            Transform panelTransform = settingsPanel.transform;
            panelTransform.DOKill();
            settingsPanel.SetActive(true);
            panelTransform.localScale = Vector3.Scale(settingsPanelBaseScale, Vector3.one * panelPopInStartScale);
            panelTransform
                .DOScale(settingsPanelBaseScale, panelPopInDuration)
                .SetEase(panelPopInEase)
                .SetUpdate(true);
        }

        private void CloseSettings()
        {
            if (settingsPanel == null || !settingsPanel.activeSelf) return;

            Transform panelTransform = settingsPanel.transform;
            panelTransform.DOKill();
            panelTransform
                .DOScale(Vector3.Scale(settingsPanelBaseScale, Vector3.one * panelPopOutEndScale), panelPopOutDuration)
                .SetEase(panelPopOutEase)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    settingsPanel.SetActive(false);
                    panelTransform.localScale = settingsPanelBaseScale;

                    // Instantly hide the overlay when the panel animation is done
                    if (darkOverlayPanel != null)
                        darkOverlayPanel.SetActive(false);
                });
        }

        // --- Toggle Logic ---
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
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetSfxVolume(value);
            }
        }

        private void OnDestroy()
        {
            playButton.onClick.RemoveAllListeners();
            settingsButton.onClick.RemoveAllListeners();
            closeSettingsButton.onClick.RemoveAllListeners();
            if (musicSlider != null) musicSlider.onValueChanged.RemoveAllListeners();
            if (sfxSlider != null) sfxSlider.onValueChanged.RemoveAllListeners();
            if (musicToggleButton != null) musicToggleButton.onClick.RemoveAllListeners();
            if (sfxToggleButton != null) sfxToggleButton.onClick.RemoveAllListeners();
        }
    }
}
