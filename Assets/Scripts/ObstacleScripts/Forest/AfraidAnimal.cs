using UnityEngine;

public class AfraidAnimal : MonoBehaviour
{
    [Header("Editor Field (requested)")]
    [Tooltip("Cone angle in degrees centered on direction toward the player. 180 = any direction.")]
    [Range(0f, 180f)]
    [SerializeField] private float maxConeAngleTowardPlayer = 90f;

    [Header("References")]
    [Tooltip("Big trigger collider used to detect player presence.")]
    [SerializeField] private Collider presenceTrigger;

    [Header("Movement (kept simple)")]
    [SerializeField] private float runSpeed = 6f;

    private Transform player;
    private bool playerInRange;
    private bool startled;
    private Vector3 runDir;

    private void Reset()
    {
        presenceTrigger = GetComponent<Collider>();
    }

    private void Update()
    {
        if (!playerInRange || startled == true) { MoveIfStartled(); return; }

        // "Listening": any of these keys pressed startles it
        if (Input.GetKeyDown(KeyCode.W) ||
            Input.GetKeyDown(KeyCode.A) ||
            Input.GetKeyDown(KeyCode.S) ||
            Input.GetKeyDown(KeyCode.D) ||
            Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.LeftShift) ||
            Input.GetKeyDown(KeyCode.RightShift))
        {
            StartledRun();
        }
    }

    private void MoveIfStartled()
    {
        if (!startled) return;

        // Move on XZ plane
        Vector3 step = runDir * (runSpeed * Time.deltaTime);
        transform.position += step;

        // Optional: face movement direction
        if (runDir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(runDir, Vector3.up);
    }

    private void StartledRun()
    {
        startled = true;

        if (player == null)
        {
            // If no player found, just run random
            runDir = RandomXZDirection();
            return;
        }

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;

        Vector3 centerDir = (toPlayer.sqrMagnitude < 0.0001f) ? RandomXZDirection() : toPlayer.normalized;

        // Pick a random direction within the cone centered at centerDir
        runDir = RandomDirectionInConeXZ(centerDir, maxConeAngleTowardPlayer);
    }

    private Vector3 RandomXZDirection()
    {
        float a = Random.Range(0f, 360f);
        Quaternion rot = Quaternion.Euler(0f, a, 0f);
        return (rot * Vector3.forward).normalized;
    }

    private Vector3 RandomDirectionInConeXZ(Vector3 centerDir, float coneAngleDeg)
    {
        // coneAngleDeg is total cone angle. We'll use half-angle to rotate left/right from center.
        float half = Mathf.Clamp(coneAngleDeg * 0.5f, 0f, 180f);

        // If 180, any direction
        if (half >= 179.999f)
            return RandomXZDirection();

        // Random yaw offset in [-half, +half]
        float yaw = Random.Range(-half, half);
        Quaternion rot = Quaternion.AngleAxis(yaw, Vector3.up);

        Vector3 d = rot * centerDir;
        d.y = 0f;
        return d.normalized;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        player = other.transform;
        playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
        if (!startled) player = null;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Destroy if it collides with anything at all
        Destroy(gameObject);
    }
}