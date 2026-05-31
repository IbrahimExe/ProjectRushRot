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

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
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
            Destroy(gameObject);
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
        PlayerDebuffReceiver receiver = hitObject.GetComponentInParent<PlayerDebuffReceiver>();

        if (receiver == null)
            return;

        receiver.ApplyDebuff(debuffType, debuffAmount, debuffDuration);

        Destroy(gameObject);
    }
}