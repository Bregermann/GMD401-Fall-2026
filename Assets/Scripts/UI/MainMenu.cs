using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void PlayGame()
    {
        SceneTransition.TargetScene = "GameScene"; // replace with your actual gameplay scene name
        SceneManager.LoadScene("LoadingScene");
    }

    public void GoToOptions()
    {
        SceneTransition.TargetScene = "OptionsScene"; // must match your options scene's exact name
        SceneManager.LoadScene("LoadingScene");
    }
    public void GoToMainMenu()
    {
        SceneTransition.TargetScene = "MainMenuScene"; // must match your options scene's exact name
        SceneManager.LoadScene("LoadingScene");
    }
}
