
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    private const string VolumeKey = "MasterVolume";

    [Header("Scene Settings")]
    [SerializeField] private string gameplaySceneName = "GameplayScene";

    [Header("Settings Panel")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Slider masterVolumeSlider;

    [Header("Credits Panel")]
    [SerializeField] private GameObject creditPanel;

    private void Start()
    {
        // Hide panels when Main Menu starts
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        if (creditPanel != null)
        {
            creditPanel.SetActive(false);
        }

        // Load saved master volume
        float savedVolume = PlayerPrefs.GetFloat(
            VolumeKey, 1f
        );

        AudioListener.volume = savedVolume;

        // Initialize volume slider
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.minValue = 0f;
            masterVolumeSlider.maxValue = 1f;

            masterVolumeSlider.SetValueWithoutNotify(
                savedVolume
            );

            masterVolumeSlider.onValueChanged.AddListener(
                SetMasterVolume
            );
        }
    }

    // PLAY BUTTON
    public void PlayGame()
    {
        if (!Application.CanStreamedLevelBeLoaded(gameplaySceneName))
        {
            Debug.LogError(
                "Gameplay Scene tidak ditemukan: " +
                gameplaySceneName
            );

            return;
        }

        PlayerPrefs.Save();

        Debug.Log("Starting Game...");

        SceneManager.LoadScene(gameplaySceneName);
    }

    // SETTINGS BUTTON
    public void OpenSettings()
    {
        // Close Credits when opening Settings
        if (creditPanel != null)
        {
            creditPanel.SetActive(false);
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    // BACK BUTTON SETTINGS
    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    // CREDITS BUTTON
    public void OpenCredits()
    {
        // Close Settings when opening Credits
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        if (creditPanel != null)
        {
            creditPanel.SetActive(true);

            Debug.Log("[MainMenu] Credits berhasil dibuka.");
        }
        else
        {
            Debug.LogError(
                "[MainMenu] Credit Panel belum dihubungkan!"
            );
        }
    }

    // CLOSE CREDITS BUTTON
    public void CloseCredits()
    {
        if (creditPanel != null)
        {
            creditPanel.SetActive(false);

            Debug.Log("[MainMenu] Credits berhasil ditutup.");
        }
    }

    // MASTER VOLUME SLIDER
    public void SetMasterVolume(float value)
    {
        AudioListener.volume = Mathf.Clamp01(value);

        PlayerPrefs.SetFloat(
            VolumeKey,
            AudioListener.volume
        );

        PlayerPrefs.Save();
    }

    // QUIT BUTTON
    public void QuitGame()
    {
        Debug.Log("Closing Game...");

        PlayerPrefs.Save();

        Application.Quit();
    }

    private void OnDestroy()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.RemoveListener(
                SetMasterVolume
            );
        }
    }
}
