using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void StartGame()
    {
        SceneManager.LoadScene(2);
    }

    public void StartProcedural()
    {
        // Load the intermediate scene instead
        SceneManager.LoadScene("ProceduralLoading");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}