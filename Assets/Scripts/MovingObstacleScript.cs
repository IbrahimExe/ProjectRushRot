using UnityEngine;

/// <summary>
/// A customizable obstacle script that handles rotation, side-to-side movement,
/// player collision bounce-back, and self-destruction.
/// </summary>
public class MovingObstacleScript : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Should this obstacle move from side to side?")]
    [SerializeField] private bool enableMovement = true;

    [Tooltip("The axis of movement (in local space if Use Local Space is checked).")]
    [SerializeField] private Vector3 moveDirection = Vector3.right;

    [Tooltip("How fast the obstacle moves side to side.")]
    [SerializeField] private float moveSpeed = 2f;

    [Tooltip("The maximum distance the obstacle travels from its starting position.")]
    [SerializeField] private float moveRange = 3f;

    [Tooltip("If true, the movement direction is relative to the obstacle's starting rotation.")]
    [SerializeField] private bool useLocalSpace = true;

    [Header("Rotation Settings")]
    [Tooltip("Should this obstacle rotate?")]
    [SerializeField] private bool enableRotation = true;

    [Tooltip("The local axis of rotation.")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;

    [Tooltip("Rotation speed in degrees per second.")]
    [SerializeField] private float rotationSpeed = 90f;

    [Header("Player Collision & Bounce")]
    [Tooltip("Force applied to bounce the player back.")]
    [SerializeField] private float bounceForce = 15f;

    [Tooltip("How much upward lift to add to the bounce back direction (0 = pure horizontal, 1 = equal upward lift).")]
    [Range(0f, 2f)]
    [SerializeField] private float bounceUpForceRatio = 0.5f;

    [Tooltip("Optional visual effect prefab to spawn when the obstacle destroys itself.")]
    [SerializeField] private GameObject destructionEffectPrefab;

    // Runtime state
    private Rigidbody rb;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private float timeCounter;
    private bool isDestroyed = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // If there's a Rigidbody, configure it for cinematic movement so physics collisions are smooth
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    private void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        
        // Seed time counter to random offset if you want obstacles to be out of sync,
        // or keep it at 0 to sync up.
        timeCounter = 0f;
    }

    private void FixedUpdate()
    {
        // Don't update position/rotation if we are in the process of being destroyed
        if (isDestroyed) return;

        timeCounter += Time.fixedDeltaTime;

        // 1. Calculate side-to-side translation using a smooth sine wave
        Vector3 targetPos = startPosition;
        if (enableMovement)
        {
            float offsetAmount = Mathf.Sin(timeCounter * moveSpeed) * moveRange;
            Vector3 worldDirection = useLocalSpace ? (startRotation * moveDirection.normalized) : moveDirection.normalized;
            targetPos = startPosition + worldDirection * offsetAmount;
        }

        // 2. Calculate continuous rotation
        Quaternion targetRot = transform.rotation;
        if (enableRotation)
        {
            float angle = rotationSpeed * timeCounter;
            Quaternion rotDelta = Quaternion.AngleAxis(angle, rotationAxis);
            targetRot = startRotation * rotDelta;
        }

        // 3. Apply changes via Rigidbody (best for physics & player interaction) or Transform fallback
        if (rb != null)
        {
            rb.MovePosition(targetPos);
            rb.MoveRotation(targetRot);
        }
        else
        {
            transform.position = targetPos;
            transform.rotation = targetRot;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isDestroyed) return;
        
        // Retrieve contact normal if available
        Vector3 impactNormal = collision.contacts.Length > 0 ? collision.contacts[0].normal : Vector3.zero;
        HandlePlayerImpact(collision.collider, impactNormal);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDestroyed) return;

        // For triggers, use the direction pointing from this obstacle to the player as the impact normal
        Vector3 impactNormal = (other.transform.position - transform.position).normalized;
        HandlePlayerImpact(other, impactNormal);
    }

    private void HandlePlayerImpact(Collider playerCollider, Vector3 impactNormal)
    {
        // Check if the collided object (or parent) is the player
        var player = playerCollider.GetComponentInParent<PlayerController2>();
        bool isPlayer = player != null || playerCollider.CompareTag("Player");

        if (!isPlayer) return;

        isDestroyed = true;

        // Apply bounce force to player's Rigidbody
        Rigidbody playerRb = playerCollider.attachedRigidbody;
        if (playerRb == null && player != null)
        {
            playerRb = player.GetComponent<Rigidbody>();
        }

        if (playerRb != null)
        {
            // Stop current player movement state to make the bounce feel punchy and responsive
            if (player != null)
            {
                player.BlockForwardMovement();
                player.BlockBackwardMovement();
            }

            // Compute bounce back direction (away from impact normal, with custom vertical tilt)
            Vector3 bounceDir = impactNormal;
            if (bounceDir == Vector3.zero || Mathf.Approximately(bounceDir.sqrMagnitude, 0f))
            {
                bounceDir = (playerCollider.transform.position - transform.position).normalized;
            }

            // Keep bounce mostly horizontal, then add custom vertical lift
            bounceDir.y = 0f;
            if (bounceDir.sqrMagnitude < 0.001f)
            {
                bounceDir = -transform.forward; // Fallback to backwards direction
            }
            bounceDir.Normalize();

            // Add upward lift to the bounce direction
            bounceDir += Vector3.up * bounceUpForceRatio;
            bounceDir.Normalize();

            // Apply direct velocity change for immediate game feel feedback (Unity 6 uses linearVelocity)
            playerRb.linearVelocity = bounceDir * bounceForce;
            
            Debug.Log($"[MovingObstacleScript] Player collided! Applied bounce force of {bounceForce} in direction {bounceDir}.", this);
        }

        // Spawn visual feedback particle effects if configured
        if (destructionEffectPrefab != null)
        {
            Instantiate(destructionEffectPrefab, transform.position, transform.rotation);
        }

        // Deactivate the obstacle instead of destroying it
        gameObject.SetActive(false);
    }
}
