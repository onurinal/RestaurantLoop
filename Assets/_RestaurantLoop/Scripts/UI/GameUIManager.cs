using UnityEngine;
using UnityEngine.UI;
using RestaurantLoop.Audio; // Added to access AudioManager

namespace RestaurantLoop.UI
{
    public class GameUIManager : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject settingsPanel;

        [Header("In-Game Elements")]
        [SerializeField] private Button gameSettingsButton;

        [Header("Settings Elements")]
        [SerializeField] private Button closeSettingsButton;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;

        private void Start()
        {
            // Initial panel state
            settingsPanel.SetActive(false);

            // Load saved audio levels from device (syncs with main menu)
            musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1f);
            sfxSlider.value = PlayerPrefs.GetFloat("SfxVolume", 1f);

            // Add button listeners for core logic
            gameSettingsButton.onClick.AddListener(OpenSettings);
            closeSettingsButton.onClick.AddListener(CloseSettings);
            
            // Add button listeners for tap sound
            gameSettingsButton.onClick.AddListener(PlayTapSound);
            closeSettingsButton.onClick.AddListener(PlayTapSound);

            // Bind functions to slider value changes
            musicSlider.onValueChanged.AddListener(UpdateMusicVolume);
            sfxSlider.onValueChanged.AddListener(UpdateSfxVolume);
        }

        private void PlayTapSound()
        {
            // If AudioManager exists, play the tap sound
            if (AudioManager.Instance != null && AudioManager.Instance.tapSound != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.tapSound);
            }
        }

        private void OpenSettings() => settingsPanel.SetActive(true);
        private void CloseSettings() => settingsPanel.SetActive(false);

        private void UpdateMusicVolume(float value)
        {
            // Save the slider value (between 0 and 1) to device
            PlayerPrefs.SetFloat("MusicVolume", value);

            // Update the playing music volume instantly
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetMusicVolume(value);
            }
        }

        private void UpdateSfxVolume(float value)
        {
            // Save the slider value (between 0 and 1) to device
            PlayerPrefs.SetFloat("SfxVolume", value);
        }

        private void OnDestroy()
        {
            // Clean up listeners to prevent memory leaks
            gameSettingsButton.onClick.RemoveAllListeners();
            closeSettingsButton.onClick.RemoveAllListeners();
            musicSlider.onValueChanged.RemoveAllListeners();
            sfxSlider.onValueChanged.RemoveAllListeners();
        }
    }
}