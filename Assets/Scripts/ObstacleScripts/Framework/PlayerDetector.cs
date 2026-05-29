using UnityEngine;

// Attach to a child GameObject with a SphereCollider (isTrigger = true)
public class PlayerDetector : MonoBehaviour
{
    [Tooltip("Detection radius in world units")]
    public float radius = 8f;

    private MovingEnemy _enemy;
    private SphereCollider _sphere;

    void Awake()
    {
        _enemy = GetComponentInParent<MovingEnemy>();

        _sphere = GetComponent<SphereCollider>();
        if (_sphere == null)
        {
            _sphere = gameObject.AddComponent<SphereCollider>();
            _sphere.isTrigger = true;
        }

        _sphere.radius = radius;
    }

    void OnValidate()
    {
        // Lets you tweak radius live in the Inspector
        if (_sphere != null) _sphere.radius = radius;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log($"{name} detected player entering range!");
            _enemy.OnPlayerEnterRange(other.transform);
        }
    }
}