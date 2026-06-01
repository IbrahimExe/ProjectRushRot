using UnityEngine;

public enum DebuffType
{
    Slow,
    ReducedJump,
    DisableDash,
    Damage
}

public class PlantProjectile : MonoBehaviour
{
    public float speed = 25f;
    public float lifetime = 5f;

    public DebuffType debuffType;
    public float debuffAmount = 0.5f;
    public float debuffDuration = 3f;

    private Transform target;
    private float timer;
    private string poolName;

    public void Initialize(Transform newTarget, string newPoolName)
    {
        target = newTarget;
        poolName = newPoolName;
        timer = lifetime;
    }

    private void OnEnable()
    {
        timer = lifetime;
    }

    private void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            ReturnToPool();
            return;
        }

        if (target == null)
        {
            transform.position += transform.forward * speed * Time.deltaTime;
            return;
        }

        Vector3 dir = (target.position - transform.position).normalized;

        transform.position += dir * speed * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHit(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryHit(collision.gameObject);
    }

    private void TryHit(GameObject hitObject)
    {
        PlayerControllerBase player = hitObject.GetComponentInParent<PlayerControllerBase>();

        if (player == null)
            return;

        PlayerDebuffReceiver receiver = player.GetComponent<PlayerDebuffReceiver>();

        if (receiver != null)
            receiver.ApplyDebuff(debuffType, debuffAmount, debuffDuration);

        ReturnToPool();
    }

    private void ReturnToPool()
    {
        ObjectPoolManager pool = ServiceLocator.Get<ObjectPoolManager>();

        if (pool != null && !string.IsNullOrEmpty(poolName))
            pool.Return(poolName, gameObject);
        else
            gameObject.SetActive(false);
    }
}