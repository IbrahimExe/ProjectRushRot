using UnityEngine;

public class PlayerProtector : MonoBehaviour
{
    [Tooltip("Select all the layers that should NOT be deactivated.")]
    [SerializeField] private LayerMask protectedLayers;
    
    private CapsuleCollider[] capsuleColliders;

    void Start()
    {
        // Get all capsule colliders on this object
        capsuleColliders = GetComponents<CapsuleCollider>();
    }

    void Update()
    {
        CheckAndDeactivate();
    }

    private void CheckAndDeactivate()
    {
        if (capsuleColliders == null || capsuleColliders.Length == 0) return;

        // Use the radius of the first capsule collider (assuming they are the same size)
        float radius = capsuleColliders[0].radius;
        
        // Account for any scaling on the GameObject
        float scaledRadius = radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        
        // OverlapSphere detects ALL colliders in the area, even if they don't have a Rigidbody
        Collider[] colliders = Physics.OverlapSphere(transform.position, scaledRadius);

        foreach (Collider col in colliders)
        {
            // Ignore ourselves just in case
            if (col.gameObject == gameObject) continue;

            // Check if the collider's layer is NOT in the protectedLayers mask
            if ((protectedLayers.value & (1 << col.gameObject.layer)) == 0)
            {
                col.gameObject.SetActive(false);
            }
        }
    }

    public void OnCollisionEnter(Collision collision)
    {
        if ((protectedLayers.value & (1 << collision.gameObject.layer)) == 0)
        {
            collision.gameObject.SetActive(false);
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        if ((protectedLayers.value & (1 << other.gameObject.layer)) == 0)
        {
            other.gameObject.SetActive(false);
        }
    }
}
