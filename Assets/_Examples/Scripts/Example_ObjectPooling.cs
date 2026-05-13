using UnityEngine;

public class Example_ObjectPooling : MonoBehaviour
{
    [SerializeField] private ObjectPoolManager _poolManager;
    [SerializeField] private SpawnManager _spawnManager;

    private void Start()
    {
        SystemLoader.CallOnComplete(Initialize);
    }

    private void Initialize()
    {
        _poolManager.Initialize();
        ServiceLocator.Register<ObjectPoolManager>(_poolManager);

        _spawnManager.Initialize();
        ServiceLocator.Register<SpawnManager>(_spawnManager);

        _spawnManager.StartSpawners();
    }
}
