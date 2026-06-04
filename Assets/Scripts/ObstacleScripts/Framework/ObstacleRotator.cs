using UnityEngine;

public class ObstacleRotator : MonoBehaviour
{
    [SerializeField] private ObstacleLauncher obstacleLauncher;
    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 30f;

    private void Start()
    {
        if (obstacleLauncher == null)
        {
            obstacleLauncher = GetComponent<ObstacleLauncher>();
        }
    }

    private void Update()
    {
        if (obstacleLauncher.playerInRange)
        {
            Transform playerTransform = obstacleLauncher.PlayerTransform;
            // rotate to face the player on the horizontal plane only
            Vector3 directionToPlayer = playerTransform.position - transform.position;
            directionToPlayer.y = 0; // ignore vertical difference
            Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

        }
    }
}
