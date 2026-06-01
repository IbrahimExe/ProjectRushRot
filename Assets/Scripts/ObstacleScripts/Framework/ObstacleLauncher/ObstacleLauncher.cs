using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LaunchBehavior))]
public class ObstacleLauncher : MonoBehaviour
{
    [Header("Projectiles")]
    [SerializeField] private List<Projectile> projectilePrefabs;
    [SerializeField] private Transform launchPoint;

    [Header("Detection")]
    [SerializeField] private float detectionRadius = 8f;
    [SerializeField] private LayerMask playerLayer;

    public bool playerInRange;
    public Transform PlayerTransform { get; private set; }

    [Header("Timing")]
    [SerializeField] private float cooldown = 2f;

    private LaunchBehavior launchBehavior;
    private float lastLaunchTime = float.NegativeInfinity;

    private void Awake()
    {
        launchBehavior = GetComponent<LaunchBehavior>();
    }

    private void Update()
    {
        if (Time.time - lastLaunchTime < cooldown) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, playerLayer);
        if (hits.Length > 0)
        {
            TryLaunch(hits[0].transform.position);
            playerInRange = true;
            PlayerTransform = hits[0].transform;
        }
        else if (playerInRange)
        {
            playerInRange = false;
        }
    }

    private void TryLaunch(Vector3 targetPosition)
    {
        if (projectilePrefabs.Count == 0) return;

        Projectile prefab = projectilePrefabs[Random.Range(0, projectilePrefabs.Count)];
        Projectile projectile = ProjectilePoolManager.Instance.Get(prefab);
        if (projectile == null) return;

        projectile.transform.position = launchPoint.position;
        projectile.transform.rotation = launchPoint.rotation;

        launchBehavior.Launch(projectile.Rb, launchPoint.position, targetPosition);
        projectile.OnLaunched();

        lastLaunchTime = Time.time;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}