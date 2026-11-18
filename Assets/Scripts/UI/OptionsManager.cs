
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class OptionsManager : MonoBehaviour
{
    public static OptionsManager Instance;
    public static bool savedFullscreen = true;

    [Tooltip("Drag your Settings Panel here in the Inspector")]
    public GameObject settingsPanel;

    private bool isSettingsActive = false;

    public TMP_Dropdown resolutionDropdown;
    Resolution[] resolutions;

    public Slider masterVolumeSlider;
    public Toggle fullscreenToggle;

    public GameObject winPanel;
    public TextMeshProUGUI winText;

    private void Awake()
    {
Instance = this;

    // Apply the saved fullscreen setting when entering a scene.
    Screen.fullScreen = savedFullscreen;    }

    void Start()
    {
        // Resolution Setup
        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();
        int currentResolutionIndex = 0;

        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = resolutions[i].width + "x" + resolutions[i].height;
            options.Add(option);

            if (resolutions[i].width == Screen.currentResolution.width &&
                resolutions[i].height == Screen.currentResolution.height)
            {
                currentResolutionIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();

        InitializeVolumeSlider();
        if (fullscreenToggle != null)
{
    fullscreenToggle.SetIsOnWithoutNotify(savedFullscreen);
}
    }

private void OnDisable()
{
    if (masterVolumeSlider != null)
    {
        masterVolumeSlider.onValueChanged.RemoveListener(SetVolume);
    }
}

private void InitializeVolumeSlider()
{
   if (masterVolumeSlider == null)
    {
        Debug.LogError("Master Volume Slider is not assigned!");
        return;
    }

    if (SoundManager.Instance == null)
    {
        Debug.LogError("SoundManager does not exist!");
        return;
    }

    masterVolumeSlider.onValueChanged.RemoveListener(SetVolume);

    masterVolumeSlider.SetValueWithoutNotify(
        SoundManager.Instance.GetMasterVolume()
    );

    masterVolumeSlider.onValueChanged.AddListener(SetVolume);

    Debug.Log("Loaded volume: " + SoundManager.Instance.GetMasterVolume());
}

    void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ToggleSettings();
            Debug.Log("Escape button was pressed");
        }
    }

    public void ToggleSettings()
    {
        isSettingsActive = !isSettingsActive;
    settingsPanel.SetActive(isSettingsActive);

    if (isSettingsActive)
    {
        InitializeVolumeSlider();
    }

    Time.timeScale = isSettingsActive ? 0f : 1f;
    }

    public void CloseSettings()
    {
        if (isSettingsActive)
            ToggleSettings();
    }

    public void SetFullscreen(bool isFullscreen)
    {
        savedFullscreen = isFullscreen;

    Screen.fullScreen = savedFullscreen;

    Debug.Log("Fullscreen triggered. State: " + savedFullscreen);
    }

    public void SetVolume(float volume)
    {
        if (SoundManager.Instance != null)
    {
        SoundManager.Instance.SetMasterVolume(volume);
    }
    }

    public void SetResolution(int resolutionIndex)
    {
        Resolution resolution = resolutions[resolutionIndex];

        Screen.SetResolution(
            resolution.width,
            resolution.height,
            Screen.fullScreen
        );
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
        Debug.Log("Quit button pressed!");
        Application.Quit();
    }

    public void ShowWinPanel(int winner)
    {
        if (winPanel == null || winText == null)
            return;

        winText.text = $"Player {TurnManager.Winner} Wins!";
        winPanel.SetActive(true);
    }
}
