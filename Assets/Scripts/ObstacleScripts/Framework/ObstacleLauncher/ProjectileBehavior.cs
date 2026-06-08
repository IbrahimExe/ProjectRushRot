using UnityEngine;

public abstract class ProjectileBehavior : MonoBehaviour
{
    [SerializeField] private float lifetime = 0f; // set 0 for infinite lifetime

    private float spawnTime;

    public virtual void OnLaunched()
    {
        spawnTime = Time.time;
    }

    public virtual void OnTick()
    {
        if (lifetime > 0 && Time.time - spawnTime >= lifetime)
            GetComponent<Projectile>().ReturnToPool();
    }

    public abstract bool OnHit(Collision collision);
}