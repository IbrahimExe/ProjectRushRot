using UnityEngine;

public class ArcLaunch : LaunchBehavior
{
    [SerializeField] private float arcHeight = 4f;
    [SerializeField] private float gravity = -Physics.gravity.y;

    public override void Launch(Rigidbody rb, Vector3 origin, Vector3 targetPosition)
    {
        rb.linearVelocity = CalculateArcVelocity(origin, targetPosition);
    }

    private Vector3 CalculateArcVelocity(Vector3 origin, Vector3 target)
    {
        float displacementY = target.y - origin.y;
        Vector3 displacementXZ = new Vector3(target.x - origin.x, 0, target.z - origin.z);

        float time = Mathf.Sqrt(-2 * arcHeight / -gravity) +
                     Mathf.Sqrt(2 * (displacementY - arcHeight) / -gravity);

        Vector3 velocityY = Vector3.up * Mathf.Sqrt(-2 * -gravity * arcHeight);
        Vector3 velocityXZ = displacementXZ / time;

        return velocityXZ + velocityY * Mathf.Sign(-gravity);
    }
}
