using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
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

        [Header("Buttons & Sliders")]
        [SerializeField] private Button gameSettingsButton;
        [SerializeField] private Button closeSettingsButton;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Button winMainMenuButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button loseMainMenuButton;

        private void Start()
        {
            // Initial panel states
            settingsPanel.SetActive(false);
            winPanel.SetActive(false);
            losePanel.SetActive(false);

            // Load saved audio levels from device
            musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1f);
            sfxSlider.value = PlayerPrefs.GetFloat("SfxVolume", 1f);

            // Bind settings button events
            gameSettingsButton.onClick.AddListener(OpenSettings);
            gameSettingsButton.onClick.AddListener(PlayTapSound);
            closeSettingsButton.onClick.AddListener(CloseSettings);
            closeSettingsButton.onClick.AddListener(PlayTapSound);

            // Bind slider events
            musicSlider.onValueChanged.AddListener(UpdateMusicVolume);
            sfxSlider.onValueChanged.AddListener(UpdateSfxVolume);

            // Subscribe to LevelManager events for win/lose panels
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.OnLevelWon += ShowWinPanel;
                LevelManager.Instance.OnLevelLost += ShowLosePanel;
            }

            // Bind game state button events
            nextLevelButton.onClick.AddListener(OnNextLevelClicked);
            nextLevelButton.onClick.AddListener(PlayTapSound);
            retryButton.onClick.AddListener(OnRetryClicked);
            retryButton.onClick.AddListener(PlayTapSound);
            winMainMenuButton.onClick.AddListener(OnMainMenuClicked);
            winMainMenuButton.onClick.AddListener(PlayTapSound);
            loseMainMenuButton.onClick.AddListener(OnMainMenuClicked);
            loseMainMenuButton.onClick.AddListener(PlayTapSound);
        }

        private void PlayTapSound()
        {
            // Play UI tap sound if AudioManager is present
            if (AudioManager.Instance != null && AudioManager.Instance.tapSound != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.tapSound);
            }
        }

        // --- Settings Logic ---
        private void OpenSettings() => settingsPanel.SetActive(true);
        private void CloseSettings() => settingsPanel.SetActive(false);

        private void UpdateMusicVolume(float value)
        {
            PlayerPrefs.SetFloat("MusicVolume", value);
            if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(value);
        }

        private void UpdateSfxVolume(float value) => PlayerPrefs.SetFloat("SfxVolume", value);

        // --- Game State Logic ---
        private void ShowWinPanel() => winPanel.SetActive(true);
        private void ShowLosePanel() => losePanel.SetActive(true);

        private void OnNextLevelClicked()
        {
            winPanel.SetActive(false);
            if (LevelManager.Instance != null) LevelManager.Instance.CompleteLevel();
        }

        private void OnRetryClicked() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        private void OnMainMenuClicked() => SceneManager.LoadScene("MainMenu");

        private void OnDestroy()
        {
            // Unsubscribe from events to prevent memory leaks
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.OnLevelWon -= ShowWinPanel;
                LevelManager.Instance.OnLevelLost -= ShowLosePanel;
            }

            // Clean up button listeners
            gameSettingsButton.onClick.RemoveAllListeners();
            closeSettingsButton.onClick.RemoveAllListeners();
            musicSlider.onValueChanged.RemoveAllListeners();
            sfxSlider.onValueChanged.RemoveAllListeners();
            nextLevelButton.onClick.RemoveAllListeners();
            retryButton.onClick.RemoveAllListeners();
            winMainMenuButton.onClick.RemoveAllListeners();
            loseMainMenuButton.onClick.RemoveAllListeners();
        }
    }
}