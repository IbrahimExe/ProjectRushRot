using UnityEngine;

public class Main : MonoBehaviour
{
    void Start()
    {
        SystemLoader.CallOnComplete(Initialize);
    }

    private void Initialize()
    {
        Debug.Log($"{nameof(Main)} Loading Main Scene");
        // Here is where you should initialize all of the Scene components/systems.
    }
}
