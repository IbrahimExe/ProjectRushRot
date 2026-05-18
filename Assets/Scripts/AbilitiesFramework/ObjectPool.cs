using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private int startingAmount = 20;
    [SerializeField] private string poolId;

    private readonly Queue<GameObject> pool = new();

    private void Awake()
    {
        PoolRegistry.Register(poolId, this);

        for (int i = 0; i < startingAmount; i++)
        {
            GameObject obj = CreateNewObject();
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
    }

    private GameObject CreateNewObject()
    {
        GameObject obj = Instantiate(prefab, transform);
        obj.SetActive(false);

        PooledObject pooled = obj.GetComponent<PooledObject>();
        if (pooled == null)
            pooled = obj.AddComponent<PooledObject>();

        pooled.SetPool(this);

        return obj;
    }

    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        GameObject obj = pool.Count > 0 ? pool.Dequeue() : CreateNewObject();

        obj.transform.SetParent(null);
        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);

        Debug.Log($"POOL GET: {gameObject.name} | Remaining: {pool.Count}");

        return obj;
    }

    public void Return(GameObject obj)
    {
        obj.SetActive(false);
        obj.transform.SetParent(transform);
        pool.Enqueue(obj);

        Debug.Log($"POOL RETURN: {obj.name} | Available: {pool.Count}");
    }
}