using UnityEngine;
using System.Collections.Generic;

public class ProjectilePoolManager : MonoBehaviour
{
    public static ProjectilePoolManager Instance {  get; private set; }

    [SerializeField] private List<ProjectilePoolEntry> poolEntries;

    private Dictionary<Projectile, ProjectilePool> pools;

    //private void Awake()
    //{
        
    //}

    public void Initialize()
    {
        if (Instance != null) { Destroy(gameObject); return; }

        Instance = this;

        DontDestroyOnLoad(gameObject);
        BuildPools();

    }

    private void BuildPools()
    {
        pools = new Dictionary<Projectile, ProjectilePool>();
        foreach (var entry in poolEntries)
        {
            if (entry.prefab == null)
            {
                Debug.LogWarning($"ProjectilePoolManager: Missing prefab for pool entry {entry}");
                continue;
            }
            if (pools.ContainsKey(entry.prefab))
            {
                Debug.LogWarning($"ProjectilePoolManager: Duplicate prefab {entry.prefab} in pool entries");
                continue;
            }
            pools[entry.prefab] = new ProjectilePool(entry.prefab, entry.defaultCapacity, entry.maxSize);
        }
    }

    public Projectile Get(Projectile prefab)
    {
        if ( !pools.TryGetValue(prefab, out var pool))
        {
            Debug.LogError($"[ProjectileManager] No pool registered for prefab {prefab.name}.");
            return null;
        }
        return pool.Get();
    }

    public void Release(Projectile prefab, Projectile instance)
    {
        if ( pools.TryGetValue(prefab, out var pool))
        {
            pool.Release(instance);
        }
    }
    
}
