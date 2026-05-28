using UnityEngine;

public class PlantShooter : MonoBehaviour
{
    public Transform firePoint;
    public GameObject projectilePrefab;

    public float detectionRange = 25f;
    public float fireCooldown = 2f;
    public float rotationSpeed = 8f;

    private Transform player;
    private float fireTimer;

    private void Start()
    {
        PlayerControllerBase playerController = FindFirstObjectByType<PlayerControllerBase>();

        if (playerController != null)
            player = playerController.transform;
    }

    private void Update()
    {
        if (player == null || projectilePrefab == null || firePoint == null)
            return;

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance > detectionRange)
            return;

        RotateTowardPlayer();

        fireTimer -= Time.deltaTime;

        if (fireTimer <= 0f)
        {
            Fire();
            fireTimer = fireCooldown;
        }
    }

    private void RotateTowardPlayer()
    {
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude <= 0.01f)
            return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
    }

    private void Fire()
    {
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);

        PlantProjectile proj = projectile.GetComponent<PlantProjectile>();

        if (proj != null)
            proj.SetTarget(player);
    }
}