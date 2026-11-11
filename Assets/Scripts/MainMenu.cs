using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private string firstLevelScene = "Level_01";
    [SerializeField] private string tutorialScene = "Tutorial";

    /// <summary>Starts the first level, or the tutorial if the player has never finished it.</summary>
    public void PlayGame()
    {
        Sfx.Play(SfxKind.Click);
        Time.timeScale = 1f;
        bool tutorialDone = PlayerPrefs.GetInt(TutorialDirector.DoneKey, 0) == 1;
        SceneManager.LoadScene(tutorialDone || string.IsNullOrEmpty(tutorialScene) ? firstLevelScene : tutorialScene);
    }

    public void PlayTutorial()
    {
        Sfx.Play(SfxKind.Click);
        Time.timeScale = 1f;
        SceneManager.LoadScene(tutorialScene);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
