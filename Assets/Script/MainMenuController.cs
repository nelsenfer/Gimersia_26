
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

    [Header("Font Utama Game (Otomatis Terisi Dimbo Regular)")]
    [SerializeField] private Font customGameFont;
    [SerializeField] private TMPro.TMP_FontAsset customGameTmpFont;

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (customGameFont == null)
        {
            customGameFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Asset fix/font/Dimbo Regular.ttf");
        }
        if (customGameTmpFont == null)
        {
            customGameTmpFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Asset fix/font/Dimbo Regular SDF.asset");
        }
#endif
        PhoneUIHelper.SetCustomDefaultFont(customGameFont, customGameTmpFont);
    }

    private void Awake()
    {
        PhoneUIHelper.ApplyProvidedFontToAllSceneTexts(customGameFont, customGameTmpFont);
    }

    private void Start()
    {
        // Hide settings when Main Menu starts
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
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
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    // BACK BUTTON
    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    // MASTER VOLUME SLIDER
    public void SetMasterVolume(float value)
    {
        AudioListener.volume = Mathf.Clamp01(value);

        PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume);
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
