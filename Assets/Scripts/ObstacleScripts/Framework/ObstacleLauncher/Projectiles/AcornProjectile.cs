using UnityEngine;

public class AcornProjectile : ProjectileBehavior
{
    [SerializeField] private float explosionRadius = 3f;
    [SerializeField] private float explosionForce = 800f;
    [SerializeField] private GameObject explosionVFX;

    public override bool OnHit(Collision collision)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (var hit in hits)
        {
            Rigidbody rb = hit.GetComponent<Rigidbody>();
            if (rb != null)
                rb.AddExplosionForce(explosionForce, transform.position, explosionRadius);
        }

        if (explosionVFX != null)
            Instantiate(explosionVFX, transform.position, Quaternion.identity);

        return true; // return to pool
    }
}