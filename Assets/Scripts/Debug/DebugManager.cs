using UnityEngine;

public class DebugManager : MonoBehaviour
{
    [SerializeField] private bool _debugBuild = false;
    [SerializeField] private GameObject _graphyObject = null;

    private void Awake()
    {
        if (!_debugBuild)
        {
            Destroy(_graphyObject);
        }
    }
}
