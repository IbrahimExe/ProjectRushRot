using UnityEngine;

public class PlantShooter : MonoBehaviour
{
    public Transform firePoint;

    [Header("Pooling")]
    public string projectilePoolName = "ForestPlantProjectile";

    public float detectionRange = 25f;
    public float fireCooldown = 2f;
    public float rotationSpeed = 8f;

    private Transform player;
    private float fireTimer;

    private void OnEnable()
    {
        fireTimer = fireCooldown;

        PlayerControllerBase pc = FindFirstObjectByType<PlayerControllerBase>();
        if (pc != null)
            player = pc.transform;
    }

    private void Update()
    {
        if (player == null || firePoint == null)
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
        ObjectPoolManager pool = ServiceLocator.Get<ObjectPoolManager>();

        if (pool == null)
        {
            Debug.LogError("PlantShooter: ObjectPoolManager not found.");
            return;
        }

        GameObject obj = pool.Get(projectilePoolName, firePoint.position, firePoint.rotation);

        if (obj == null)
            return;

        PlantProjectile projectile = obj.GetComponent<PlantProjectile>();

        if (projectile != null)
            projectile.Initialize(player, projectilePoolName);
    }
}