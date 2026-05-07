using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CombatEntity : GameEntity, IDamageable
{
    [SerializeField] private float _maxHealth = 100f;

    private float _health;

    private void Awake()
    {
        SystemLoader.CallOnComplete(Initialize);
    }

    private void Initialize()
    {
        _health = _maxHealth;
    }

    public void TakeDamage(float damage)
    {
        _health -= damage;
        if (_health < 0)
        {
            Destroy(gameObject);
        }
    }
}
