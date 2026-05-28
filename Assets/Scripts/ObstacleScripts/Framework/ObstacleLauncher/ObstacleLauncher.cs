using UnityEngine;
using UnityEngine.ProBuilder;

[RequireComponent(typeof(LaunchBehavior))]
public class ObstacleLauncher : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ProjectilePool pool;
    [SerializeField] private Transform launchPoint;

    [Header("Detection")]
    [SerializeField] private float detectionRadius = 8f;
    [SerializeField] private LayerMask playerLayer;

    private LaunchBehavior launchBehavior;
    private bool playerInRange;

    private void Awake()
    {
        // Grab whichever LaunchBehavior component is on this GameObject
        launchBehavior = GetComponent<LaunchBehavior>();
    }

    private void Update()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, playerLayer);
        if (hits.Length > 0)
            TryLaunch(hits[0].transform.position);
    }

    private void TryLaunch(Vector3 targetPosition)
    {
        Projectile projectile = pool.Get();
        projectile.transform.position = launchPoint.position;
        projectile.transform.rotation = launchPoint.rotation;

        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        launchBehavior.Launch(rb, launchPoint.position, targetPosition);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}