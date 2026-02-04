using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// AppStart is responsible for loading the initial scene in a build
/// </summary>
public class AppStart : MonoBehaviour
{
    [Tooltip("Build index of the next scene to load")]
    [SerializeField] private int _sceneToLoad;
    [SerializeField] private float _minDisplayTimeSeconds;

    private void Start()
    {
        Debug.Log("AppStart -> Start");
        SystemLoader.CallOnComplete(Initialize);
    }

    /// <summary>
    /// Initialize the AppStart class asynchronously
    /// </summary>
    private async void Initialize()
    {
        Debug.Log($"[{nameof(AppStart)}] - Initializing");

        Debug.Log($"Loading Scene: {_sceneToLoad}");
        await ShowSplashScreenAsync();
        var loadTask = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(_sceneToLoad);

        // Load the scene asynchronously
        while (loadTask.isDone == false)
        {
            Debug.Log($"Loading Scene: {_sceneToLoad} - Progress: {loadTask.progress * 100}%");
            await Task.Delay(100);
        }
    }

    /// <summary>
    /// Show the splash screen for a minimum amount of time
    /// </summary>
    /// <returns></returns>
    private async Task ShowSplashScreenAsync()
    {
        await Task.Delay((int)(_minDisplayTimeSeconds * 1000));
    }
}
