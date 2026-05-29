using UnityEngine;

public abstract class ProjectileBehavior : MonoBehaviour
{
    protected Projectile projectile;

    private void Awake()
    {
        projectile = GetComponent<Projectile>();
    }

    // Called the moment the projectile is launched
    public virtual void OnLaunched() { }

    // Called every frame while the projectile is active
    public virtual void OnTick() { }

    // Called on collision — return true if the projectile should be returned to pool
    public abstract bool OnHit(Collision collision);
}