using UnityEngine;
using UnityEngine.Pool;

public class ProjectilePool
{
    private ObjectPool<Projectile> pool;

    public ProjectilePool(Projectile prefab, int defaultCapacity, int maxSize)
    {
        pool = new ObjectPool<Projectile>(
            createFunc: () => { var p = Object.Instantiate(prefab); p.Init(prefab); return p; },
            actionOnGet: p => p.gameObject.SetActive(true),
            actionOnRelease: p => p.gameObject.SetActive(false),
            actionOnDestroy: p => Object.Destroy(p.gameObject),
            defaultCapacity: defaultCapacity,
            maxSize: maxSize
        );
    }

    public Projectile Get() => pool.Get();
    public void Release(Projectile p) => pool.Release(p);
}