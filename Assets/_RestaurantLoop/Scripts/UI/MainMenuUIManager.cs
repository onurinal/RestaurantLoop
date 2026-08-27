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

        private void Start()
        {
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
            
            levelText.text = "Level 1";
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
            SceneManager.LoadScene("Onur-2"); 
        }

        private void OpenSettings() => settingsPanel.SetActive(true);
        private void CloseSettings() => settingsPanel.SetActive(false);

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