using System;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolManager : MonoBehaviour
{
    [Serializable]
    public class Pool
    {
        public string Name;
        public GameObject Prefab;
        public int InitialSize;
    }

    [SerializeField] private List<Pool> _pools;

    private Dictionary<string, Queue<GameObject>> _poolDictionary = new Dictionary<string, Queue<GameObject>>();
    private bool _initialized = false;

    //private void Awake()
    //{
    //    SystemLoader.CallOnComplete(Initialize);
    //}

    public void Initialize()
    {
        Debug.Log("Initializing ObjectPoolManager...");
        InitializePools();

        _initialized = true;
        Debug.Log("ObjectPoolManager initialization complete.");
    }

    private void InitializePools()
    {
        foreach (var pool in _pools)
        {
            _poolDictionary[pool.Name] = new Queue<GameObject>();
            GameObject poolParent = new GameObject(pool.Name);
            poolParent.transform.SetParent(transform);
            for (int i = 0; i < pool.InitialSize; i++)
            {
                GameObject obj = Instantiate(pool.Prefab, poolParent.transform);
                obj.name = $"{pool.Prefab.name}_{i}";
                obj.SetActive(false);
                _poolDictionary[pool.Name].Enqueue(obj);
            }
        }
    }

    public bool TryFetch(string poolname, out GameObject pooledObj)
    {
        if (!_poolDictionary.ContainsKey(poolname))
        {
            pooledObj = null;
            return false;
        }

        Queue<GameObject> poolQueue = _poolDictionary[poolname];

        return poolQueue.TryDequeue(out pooledObj);
    }

    public void Recycle(string poolname, GameObject pooledObj)
    {
        if (!_poolDictionary.ContainsKey(poolname))
        {
            Debug.LogError($"Pool with name {poolname} does not exist. Cannot recycle object.");
            return;
        }

        pooledObj.SetActive(false);
        Queue<GameObject> poolQueue = _poolDictionary[poolname];
        poolQueue.Enqueue(pooledObj);
    }
}
