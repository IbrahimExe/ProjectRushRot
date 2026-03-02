using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _quitButton;

    private const string GameplaySceneName = "IbrahimScene";

    private void Start()
    {
        SystemLoader.CallOnComplete(Initialize);
    }

    private void Initialize()
    {
        _startButton.onClick.AddListener(OnStartClicked);
        _quitButton.onClick.AddListener(OnQuitClicked);
    }

    public void OnStartClicked()
    {
        SceneManager.LoadScene(GameplaySceneName);
    }

    public void OnQuitClicked()
    {
        Application.Quit();
    }
}
