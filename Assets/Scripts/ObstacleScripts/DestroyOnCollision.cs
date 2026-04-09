using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DestroyOnCollision : MonoBehaviour
{
    [Header("Auto Destroy")]
    public float destroyAfterSeconds = 3f;

    private void OnCollisionEnter(Collision collision)
    {
        // Check if the collided object (or its parent) has the player component
        var player = collision.collider.GetComponentInParent<PlayerController2>();
        if (player != null)
        {
            Destroy(gameObject, destroyAfterSeconds);
        }
    }
}
