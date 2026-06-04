using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private float gravityScale = 1f;

    public Projectile SourcePrefab { get; private set; }
    public Rigidbody Rb { get; private set; }
    public float GravityScale => gravityScale;

    private ProjectileBehavior behavior;

    private void Awake()
    {
        Rb = GetComponent<Rigidbody>();
        behavior = GetComponent<ProjectileBehavior>();

        Rb.useGravity = false;
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

    private void FixedUpdate()
    {
        // Apply custom gravity
        Rb.AddForce(Physics.gravity * gravityScale, ForceMode.Acceleration);
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