using System;
using System.Collections;
using UnityEngine;

public class WeightedObjectSpawner : BaseObjectSpawner
{
    [Serializable]
    public class SpawnEntry
    {
        public string PoolName;
        public float Weight;
    }

    [SerializeField] private float _spawnInterval = 1f;
    [SerializeField] private float _spawnRadius = 5f;
    [SerializeField] private float _objectLifetime = 5f;
    [SerializeField] private SpawnEntry[] _spawnEntries;

    private ObjectPoolManager _objectPoolManager = null;
    private bool _initialized = false;
    private WaitForSeconds _spawnWait = null;
    private WaitForSeconds _lifetimeWait = null;
    private Coroutine _spawnCoroutine = null;
    private bool _isSpawning = false;

    public override void Initialize()
    {
        _objectPoolManager = ServiceLocator.Get<ObjectPoolManager>();
        _spawnWait = new WaitForSeconds(_spawnInterval);
        _lifetimeWait = new WaitForSeconds(_objectLifetime);
        _initialized = true;
    }

    public override void StartSpawning()
    {
        if (_spawnCoroutine != null)
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
        int randomValue = UnityEngine.Random.Range(0, GetTotalWeight());
        for(int i = 0; i < _spawnEntries.Length; i++)
        {
            if (randomValue < (int)_spawnEntries[i].Weight)
            {
                TrySpawnFromPool(_spawnEntries[i].PoolName);
                return;
            }
            randomValue -= (int)_spawnEntries[i].Weight;
        }
    }

    private void TrySpawnFromPool(string poolName)
    {
        if(_objectPoolManager.TryFetch(poolName, out GameObject obj))
        {
            Vector2 random2DPoint = UnityEngine.Random.insideUnitCircle;
            Vector3 randomPos = new Vector3(random2DPoint.x, 0f, random2DPoint.y) * _spawnRadius;
            obj.transform.SetPositionAndRotation(randomPos, Quaternion.identity);
            obj.SetActive(true);
            StartCoroutine(RecycleAfterTime(obj, poolName));
        }
        else
        {
            Debug.LogWarning($"ObjectSpawner: No objects available in pool '{poolName}' to spawn.");
        }
    }

    private IEnumerator RecycleAfterTime(GameObject obj, string poolName)
    {
        yield return _lifetimeWait;
        _objectPoolManager.Recycle(poolName, obj);
    }

    private int GetTotalWeight()
    {
        int totalWeight = 0;
        foreach (var entry in _spawnEntries)
        {
            totalWeight += (int)entry.Weight;
        }
        return totalWeight;
    }

    public void StopSpawning()
    {
        _isSpawning = false;
        StopCoroutine(_spawnCoroutine);
        _spawnCoroutine = null;
    }
}
