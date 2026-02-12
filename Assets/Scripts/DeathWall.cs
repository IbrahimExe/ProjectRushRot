using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DeathWall : MonoBehaviour
{
    [Header("Movement")]
    public Vector3 MoveDirection = Vector3.forward;
    public float BaseSpeed = 5f;
    public float MaxSpeed = 20f;
    public float SpeedIncrease = 0.5f;

    [Header("Distance")]
    [Tooltip("Maximum distance the wall is allowed to be behind the player.")]
    public float MaxDistance = 15f;

    [Header("References")]
    [Tooltip("Drag your Player GameObject here.")]
    public Rigidbody playerRb;

    [Header("UI")]
    [Tooltip("Optional: assign your Death (Game Over) panel here.")]
    public GameObject deathScreen;

    private Transform playerTransform;
    private bool hasKilled = false;
    private float currentSpeed;

    void Start()
    {
        currentSpeed = BaseSpeed;

        if (playerRb != null)
            playerTransform = playerRb.transform;
        else
            Debug.LogWarning("DeathWall: No player Rigidbody assigned!", this);

        if (deathScreen != null)
            deathScreen.SetActive(false);
    }

    void Update()
    {
        if (playerTransform == null) return;

        // Ramp speed up over time
        currentSpeed = Mathf.Min(currentSpeed + SpeedIncrease * Time.deltaTime, MaxSpeed);

        Vector3 dir = MoveDirection.normalized;

        float wallProgress = Vector3.Dot(transform.position, dir);
        float playerProgress = Vector3.Dot(playerTransform.position, dir);
        float distance = playerProgress - wallProgress;

        float moveSpeed;

        if (distance >= MaxDistance)
        {
            // Cap to player's forward speed, but never go below BaseSpeed
            // so the wall doesn't stop when the player is standing still
            float playerSpeed = GetPlayerSpeed();
            moveSpeed = Mathf.Max(playerSpeed, BaseSpeed);
        }
        else
        {
            moveSpeed = currentSpeed;
        }

        transform.position += dir * moveSpeed * Time.deltaTime;

        Debug.Log($"DeathWall | distance: {distance:F2} | moveSpeed: {moveSpeed:F2} | currentSpeed: {currentSpeed:F2}");
    }

    private float GetPlayerSpeed()
    {
        if (playerRb == null) return 0f;
        // Only the component of velocity along the wall's move direction
        return Vector3.Dot(playerRb.linearVelocity, MoveDirection.normalized);
    }

    private void KillPlayer(GameObject obj)
    {
        if (hasKilled) return;
        if (!obj.CompareTag("Player")) return;
        hasKilled = true;

        if (deathScreen != null)
            deathScreen.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 0f;

        Destroy(obj);
        Debug.Log("DeathWall: Player killed.");
    }

    private void OnTriggerEnter(Collider other) => KillPlayer(other.gameObject);
    private void OnCollisionEnter(Collision collision) => KillPlayer(collision.gameObject);

    private void OnDrawGizmos()
    {
        // Draw the move direction as an arrow
        Gizmos.color = Color.red;
        Vector3 dir = MoveDirection.normalized;
        Gizmos.DrawLine(transform.position, transform.position + dir * 3f);
        Gizmos.DrawSphere(transform.position + dir * 3f, 0.2f);

        // Draw the max distance leash relative to player
        if (playerTransform != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 leashPoint = playerTransform.position - dir * MaxDistance;
            Gizmos.DrawWireSphere(leashPoint, 0.5f);
            Gizmos.DrawLine(transform.position, leashPoint);
        }
    }
}