using UnityEngine;

/// <summary>
/// AppLoader is responsible for loading all systems and managing the application lifecycle.
/// </summary>
public class AppLoader : SystemLoader
{
    /// <summary>
    /// The root transform for all MonoBehaviours that are registered in the system.
    /// </summary>
    public static Transform SystemRoot => _transform;
    private static Transform _transform;

    private static AppLoader _instance;

    private async void Awake()
    {
        // Singleton pattern to ensure only one instance of AppLoader exists
        // This is the only Singleton in the game. All other systems are registered in the ServiceLocator to provide inversion of control.
        if (_instance != null && _instance != this)
        {
            // Duplicate loader detected
            gameObject.SetActive(false);
            return;
        }
        _instance = this;
        _transform = transform;
        DontDestroyOnLoad(gameObject);

        // Clear any statics that may have been held between Playmode sessions.
        ClearStatics();

        // Register Systems
        RegisterSystems();

        // Register tasks to be run.
        AddStartupTasks();

        // Run tasks that have been registered.
        await RunTasks();

        Debug.Log($"[{nameof(AppLoader)}] - Loading Completed");
    }

    /// <summary>
    /// Clear static references that may be held between Playmode sessions
    /// </summary>
    private void ClearStatics()
    {
        Debug.Log($"{nameof(ClearStatics)}");
        ServiceLocator.Clear();
    }

    /// <summary>
    /// Register all systems in the game that are to be used Globally.
    /// </summary>
    private void RegisterSystems()
    {
        Debug.Log($"{nameof(RegisterSystems)}");
    }

    private T FindMonoSystem<T>() where T : MonoBehaviour
    {
        var system = GameObject.FindFirstObjectByType<T>();
        return system;
    }

    /// <summary>
    /// Register all tasks that need to be run at startup
    /// </summary>
    private void AddStartupTasks()
    {
        Debug.Log($"{nameof(AddStartupTasks)}");
    }
}
