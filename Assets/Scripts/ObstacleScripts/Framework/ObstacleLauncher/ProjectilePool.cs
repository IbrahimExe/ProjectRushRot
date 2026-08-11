using UnityEngine;
using UnityEngine.Pool;

public class ProjectilePool
{
    private ObjectPool<Projectile> pool;
    private Projectile prefab;

    public ProjectilePool(Projectile prefab, int defaultCapacity, int maxSize)
    {
        this.prefab = prefab;
        pool = new ObjectPool<Projectile>(
            createFunc: () => { var p = Object.Instantiate(prefab); p.Init(prefab); return p; },
            actionOnGet: p => p.gameObject.SetActive(true),
            actionOnRelease: p => p.gameObject.SetActive(false),
            actionOnDestroy: p => { if (p != null) Object.Destroy(p.gameObject); },
            collectionCheck: true,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize
        );
    }

    public Projectile Get()
    {
        // If the pool hands us a destroyed instance, discard it and get a fresh one.
        Projectile p = pool.Get();
        while (p == null)
        {
            p = pool.Get();
        }
        return p;
    }

    public void Release(Projectile p) => pool.Release(p);
}