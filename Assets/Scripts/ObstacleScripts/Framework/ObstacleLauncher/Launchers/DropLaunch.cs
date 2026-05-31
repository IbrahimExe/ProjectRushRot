using UnityEngine;

public class DropLaunch : LaunchBehavior
{
    [SerializeField] private float dropForce = 1f;

    public override void Launch(Rigidbody rb, Vector3 origin, Vector3 targetPosition)
    {
        // Just a small push; gravity pulls it down
        Vector3 direction = (targetPosition - origin).normalized;
        rb.linearVelocity = direction * dropForce;
    }
}
