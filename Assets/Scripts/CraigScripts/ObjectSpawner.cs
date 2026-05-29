using System.Collections;
using UnityEngine;

public class ObjectSpawner : MonoBehaviour
{
    [SerializeField] private float _spawnInterval = 1f;
    [SerializeField] private float _spawnRadius = 5f;
    [SerializeField] private float _objectLifetime = 5f;
    [SerializeField] private string _poolName;

    private ObjectPoolManager _objectPoolManager = null;
    private bool _initialized = false;
    private WaitForSeconds _spawnWait = null;
    private WaitForSeconds _lifetimeWait = null;
    private Coroutine _spawnCoroutine = null;
    private bool _isSpawning = false;

    public void Initialize()
    {
        _objectPoolManager = ServiceLocator.Get<ObjectPoolManager>();
        _spawnWait = new WaitForSeconds(_spawnInterval);
        _lifetimeWait = new WaitForSeconds(_objectLifetime);
        _initialized = true;
    }

    public void StartSpawning()
    {
        if(_spawnCoroutine != null)
        {
            Debug.LogWarning("ObjectSpawner: Already spawning.");
            return;
        }
        _isSpawning = true;
        _spawnCoroutine = StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        while (_isSpawning)
        {
            SpawnObject();
            yield return _spawnWait;
        }
    }

    private void SpawnObject()
    {
        if(_objectPoolManager.TryFetch(_poolName, out GameObject obj))
        {
            obj.transform.position = transform.position + Random.insideUnitSphere * _spawnRadius;
            obj.SetActive(true);
            StartCoroutine(RecycleAfterTime(obj));
        }
        else
        {
            Debug.LogWarning($"ObjectSpawner: No objects available in pool '{_poolName}' to spawn.");
        }
    }

    private IEnumerator RecycleAfterTime(GameObject obj)
    {
        yield return _lifetimeWait;
        _objectPoolManager.Recycle(_poolName, obj);
    }

    public void StopSpawning()
    {
        _isSpawning = false;
        StopCoroutine(_spawnCoroutine);
        _spawnCoroutine = null;
    }
}
