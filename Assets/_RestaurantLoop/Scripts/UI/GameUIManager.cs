using UnityEngine;
using UnityEngine.UI;

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

            // Add button listeners
            gameSettingsButton.onClick.AddListener(OpenSettings);
            closeSettingsButton.onClick.AddListener(CloseSettings);
            
            // Bind functions to slider value changes
            musicSlider.onValueChanged.AddListener(UpdateMusicVolume);
            sfxSlider.onValueChanged.AddListener(UpdateSfxVolume);
        }

        private void OpenSettings() => settingsPanel.SetActive(true);
        private void CloseSettings() => settingsPanel.SetActive(false);

        private void UpdateMusicVolume(float value)
        {
            // Save the slider value (between 0 and 1) to device
            PlayerPrefs.SetFloat("MusicVolume", value);
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