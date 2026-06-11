using UnityEngine;

public class ArcLaunch : LaunchBehavior
{
    [SerializeField] private float arcHeightRatio = 0.3f;
    [SerializeField] private float minArcHeight = 2f;
    [SerializeField] private float maxArcHeight = 10f;

    public override void Launch(Rigidbody rb, Vector3 origin, Vector3 targetPosition)
    {
        Projectile projectile = rb.GetComponent<Projectile>();

        // Read gravityScale from the projectile to keep math in sync
        float gravityScale = projectile != null ? projectile.GravityScale : 1f;
        float effectiveGravity = Mathf.Abs(Physics.gravity.y) * gravityScale;

        float distance = Vector3.Distance(origin, targetPosition);
        float arcHeight = Mathf.Clamp(distance * arcHeightRatio, minArcHeight, maxArcHeight);

        rb.linearVelocity = CalculateArcVelocity(origin, targetPosition, arcHeight, effectiveGravity);
    }

    private Vector3 CalculateArcVelocity(Vector3 origin, Vector3 target, float arcHeight, float g)
    {
        float displacementY = target.y - origin.y;
        Vector3 displacementXZ = new Vector3(target.x - origin.x, 0, target.z - origin.z);

        float time = Mathf.Sqrt(2 * arcHeight / g) +
                     Mathf.Sqrt(2 * Mathf.Max(0, arcHeight - displacementY) / g);

        Vector3 velocityY = Vector3.up * Mathf.Sqrt(2 * g * arcHeight);
        Vector3 velocityXZ = displacementXZ / time;

        return velocityXZ + velocityY;
    }
}