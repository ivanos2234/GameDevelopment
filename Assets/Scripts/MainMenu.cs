using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private string firstLevelScene = "Level_01";

    public void PlayGame()
    {
        Sfx.Play(SfxKind.Click);
        Time.timeScale = 1f;
        SceneManager.LoadScene(firstLevelScene);
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
