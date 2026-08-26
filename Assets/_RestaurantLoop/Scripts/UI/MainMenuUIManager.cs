using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using RestaurantLoop.Audio; 

namespace RestaurantLoop.UI
{
    public class MainMenuUIManager : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject settingsPanel;

        [Header("Main Menu Elements")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private TextMeshProUGUI levelText;

        [Header("Settings Elements")]
        [SerializeField] private Button closeSettingsButton;
        [SerializeField] private Slider musicSlider; 
        [SerializeField] private Slider sfxSlider;   

        private void Start()
        {
            // Initial panel state
            settingsPanel.SetActive(false);

            // Add button listeners for core logic
            playButton.onClick.AddListener(StartGame);
            settingsButton.onClick.AddListener(OpenSettings);
            closeSettingsButton.onClick.AddListener(CloseSettings);

            // Add button listeners for tap sound
            playButton.onClick.AddListener(PlayTapSound);
            settingsButton.onClick.AddListener(PlayTapSound);
            closeSettingsButton.onClick.AddListener(PlayTapSound);

            // Load saved audio levels from device (defaults to 1f, max volume)
            musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1f);
            sfxSlider.value = PlayerPrefs.GetFloat("SfxVolume", 1f);

            // Bind functions to slider value changes
            musicSlider.onValueChanged.AddListener(UpdateMusicVolume);
            sfxSlider.onValueChanged.AddListener(UpdateSfxVolume);
            
            // MVP Simplification: Always start from Level 1 on fresh boot
            levelText.text = "Level 1";
        }

        private void PlayTapSound()
        {
            // If AudioManager exists, play the tap sound
            if (AudioManager.Instance != null && AudioManager.Instance.tapSound != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.tapSound);
            }
        }

        private void StartGame()
        {
            // Load the main gameplay scene
            SceneManager.LoadScene("Onur-2"); 
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
            playButton.onClick.RemoveAllListeners();
            settingsButton.onClick.RemoveAllListeners();
            closeSettingsButton.onClick.RemoveAllListeners();
            musicSlider.onValueChanged.RemoveAllListeners();
            sfxSlider.onValueChanged.RemoveAllListeners();
        }
    }
}