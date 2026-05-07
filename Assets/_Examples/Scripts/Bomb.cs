using UnityEngine;

public class Bomb : GameEntity
{
    [SerializeField] private float _blastRadius = 2.5f;
    [SerializeField] private float _damage = 50f;
    [SerializeField] private float _blastForce = 500f;
    [SerializeField] private float _upwardModifier = 1.0f;

    public void OnMouseDown()
    {
        Debug.Log("Boom!");
        Collider[] colliders = Physics.OverlapSphere(transform.position, _blastRadius);
        foreach (Collider collider in colliders)
        {
            IDamageable damageable = collider.GetComponent<IDamageable>();
            if (damageable != null)
            {
                Debug.Log($"{collider.gameObject.name} is in blast radius");
                collider.attachedRigidbody.AddExplosionForce(_blastForce, transform.position, _blastRadius, _upwardModifier, ForceMode.Impulse);
                damageable.TakeDamage(50f);
            }
        }
        Destroy(gameObject);
    }
}
