using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.SceneManagement;

public class OptionsManager : MonoBehaviour
{
    public static OptionsManager Instance;
    [Tooltip("Drag your Settings Panel here in the Inspector")]
    public GameObject settingsPanel;
    private bool isSettingsActive = false;
    public TMP_Dropdown resolutionDropdown;
    Resolution[] resolutions;
    public GameObject winPanel;
    public TextMeshProUGUI winText;

    void Start()
    {
        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();
        List<string> options = new List<string>();
        int currentResolutionIndex = 0;
        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = resolutions[i].width + "x" + resolutions[i].height;
            options.Add(option);
            if (resolutions[i].width == Screen.currentResolution.width && resolutions[i].height == Screen.currentResolution.height)
            {
                currentResolutionIndex = i;
            }
        }
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();
    }
    void Update()
    {
       if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
    {
        ToggleSettings();
        Debug.Log("escape button was pressed");
    }
    }

    public void ToggleSettings()
    {
        isSettingsActive = !isSettingsActive;
        settingsPanel.SetActive(isSettingsActive);
        Time.timeScale = isSettingsActive ? 0f : 1f; 
    }

    // --- New UI Methods Below ---

    public void CloseSettings()
    {
        if (isSettingsActive) ToggleSettings();
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        Debug.Log("Fullscreen triggered. State: " + isFullscreen);
    }

    public void SetVolume(float volume)
    {
        AudioListener.volume = volume; 
    }

    public void SetResolution(int resolutionIndex)
    {
        
       Resolution resolution = resolutions[resolutionIndex];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f; // Ensure time unpauses before reloading
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
        Debug.Log("Quit button pressed!"); 
        Application.Quit(); 
    }
    public void ShowWinPanel(int winner)
    {
        if (winPanel == null || winText == null) return;

        winText.text = $"Player {TurnManager.Winner} Wins!";
        winPanel.SetActive(true);
    }
}