using UnityEngine;

public class Projectile : MonoBehaviour
{
    public Projectile SourcePrefab { get; private set; }
    public Rigidbody Rb { get; private set; }

    private ProjectileBehavior behavior;

    private void Awake()
    {
        Rb = GetComponent<Rigidbody>();
        behavior = GetComponent<ProjectileBehavior>();
    }

    public void Init(Projectile sourcePrefab)
    {
        SourcePrefab = sourcePrefab;
    }

    public void OnLaunched()
    {
        behavior?.OnLaunched();
    }

    private void Update()
    {
        behavior?.OnTick();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (behavior != null && behavior.OnHit(collision))
            ReturnToPool();
    }

    public void ReturnToPool()
    {
        ProjectilePoolManager.Instance.Release(SourcePrefab, this);
    }
}