// CollisionBounce.cs
using UnityEngine;

[RequireComponent(typeof(PlayerControllerBase))]
public class CollisionBounce : MonoBehaviour
{
    [Header("Bounce Settings")]
    [Range(0f, 1f)]
    public float bouncePercent = 0.4f;   // e.g. 40% of impact speed
    public float minImpactSpeed = 5f;    // ignore tiny bumps

    private PlayerControllerBase player;
    private WallRunAbility wallRun;
    private Vector3 preCollisionVelocity;

    void Awake()
    {
        player = GetComponent<PlayerControllerBase>();
        wallRun = GetComponent<WallRunAbility>();
    }

    // Cache velocity BEFORE PhysX runs its collision resolution
    void FixedUpdate()
    {
        preCollisionVelocity = player.RB.linearVelocity;
    }

    void OnCollisionEnter(Collision col)
    {
        // Don't bounce off walls if we are actively wall running on them
        if (wallRun != null && wallRun.IsWallRunning) return;

        // Only bounce off walls/obstacles, not the ground
        Vector3 normal = col.GetContact(0).normal;
        if (Vector3.Dot(normal, Vector3.up) > 0.7f) return;

        // Use the pre-collision velocity - by the time OnCollisionEnter fires,
        // PhysX has already zeroed/modified the velocity along the normal
        float impactSpeed = Vector3.Dot(preCollisionVelocity, -normal);
        if (impactSpeed < minImpactSpeed) return;

        // Reflect the pre-collision velocity and scale by bounce percent
        Vector3 reflected = Vector3.Reflect(preCollisionVelocity, normal);
        player.RB.linearVelocity = reflected * bouncePercent;

        // Tell BaseMove not to overwrite this velocity for one frame
        player.SuppressVelocityOverride = true;
    }
}