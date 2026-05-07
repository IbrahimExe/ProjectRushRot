using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PathMovingObstacle : MonoBehaviour
{
    // Movement
    [Header("Movement")]
    [Tooltip("Speed at which the obstacle travels between the two points.")]
    public float speed = 2f;

    [Tooltip("When true, the obstacle will rotate to face the direction it is moving.")]
    public bool rotateTowardDirection = false;

    [Tooltip("How fast the obstacle rotates toward its direction (degrees per second). 0 = instant snap.")]
    public float rotationSpeed = 360f;

    // Approach Lerp
    [Header("Approach Lerp")]
    [Tooltip("When true, the obstacle will smoothly decelerate as it nears each path point.")]
    public bool useLerp = false;

    [Tooltip("Distance from the target point at which the lerp deceleration begins.")]
    public float lerpStartDistance = 2f;

    [Tooltip("Lerp speed factor when approaching the target. Lower values = smoother / slower arrival.")]
    [Range(1f, 30f)]
    public float lerpSpeed = 8f;

    // Manual Points
    [Header("Path Points")]
    [Tooltip("First point")]
    public Vector3 localOffsetA = new Vector3(-5f, 0f, 0f);

    [Tooltip("Second point")]
    public Vector3 localOffsetB = new Vector3(5f, 0f, 0f);

    // Randomized Points
    [Header("Randomized Points")]
    [Tooltip("When true, it will generate two random points, one in the negative-X side and one in the positive-X side.")]
    public bool randomizePoints = false;

    [Tooltip("How far on the X axis each random point can be from the spawn position (min/max magnitude).")]
    public float randomXMin = 3f;
    public float randomXMax = 8f;

    [Tooltip("How far on the Z axis the random points can be offset from the spawn position.")]
    public float randomZRange = 3f;

    // Player Tag
    [Header("Collision")]
    [Tooltip("Tag used to identify the player. Collisions with this tag will not reverse the obstacle.")]
    public string playerTag = "Player";

    // Runtime State
    private Rigidbody rb;
    private Collider col;

    // World-space targets
    private Vector3 worldTargetA;
    private Vector3 worldTargetB;

    // Which target we are currently moving toward
    private bool movingToA = false;

    private Vector3 CurrentTarget => movingToA ? worldTargetA : worldTargetB;

    // -------------------------------------------------------------------------

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        col = GetComponent<Collider>();

        Vector3 spawnPos = transform.position;

        if (randomizePoints)
        {
            // Point A: negative X
            float xA = -Random.Range(randomXMin, randomXMax);
            float zA = Random.Range(-randomZRange, randomZRange);
            localOffsetA = new Vector3(xA, 0f, zA);

            // Point B: positive X
            float xB = Random.Range(randomXMin, randomXMax);
            float zB = Random.Range(-randomZRange, randomZRange);
            localOffsetB = new Vector3(xB, 0f, zB);
        }

        // Convert local offsets to world-space targets
        worldTargetA = spawnPos + transform.TransformDirection(localOffsetA);
        worldTargetB = spawnPos + transform.TransformDirection(localOffsetB);

        movingToA = false;
    }

    private void FixedUpdate()
    {
        MoveTowardTarget();
    }

    private void MoveTowardTarget()
    {
        Vector3 target = CurrentTarget;
        Vector3 currentPos = rb.position;

        Vector3 dir = (target - currentPos);
        float distToTarget = dir.magnitude;

        if (distToTarget <= 0.01f)
        {
            rb.MovePosition(target);
            FlipDirection();
            return;
        }

        float step = speed * Time.fixedDeltaTime;
        Vector3 moveDir = dir.normalized;

        // BoxCast ahead to detect colliders in the path
        //if (IsBlockedAhead(moveDir, step))
        //{
        //    FlipDirection();
        //    return;
        //}

        RotateToward(moveDir);

        // --- Approach Lerp ---
        // When within lerpStartDistance, blend position toward the target
        // instead of using a fixed step, creating a smooth deceleration.
        if (useLerp && distToTarget <= lerpStartDistance)
        {
            Vector3 lerpedPos = Vector3.Lerp(currentPos, target, lerpSpeed * Time.fixedDeltaTime);
            rb.MovePosition(lerpedPos);
            return;
        }

        if (step >= distToTarget)
        {
            rb.MovePosition(target);
            FlipDirection();
        }
        else
        {
            rb.MovePosition(currentPos + moveDir * step);
        }
    }

    // Returns true if a BoxCast in moveDir hits another obstacle, because on collision enter doesn't work on objects with no rigidbody
    private bool IsBlockedAhead(Vector3 moveDir, float step)
    {
        if (col == null) return false;

        Vector3 halfExtents = col.bounds.extents * 0.98f;
        Vector3 center      = col.bounds.center;
        float   castDist    = step + 0.05f;

        int hitCount = Physics.BoxCastNonAlloc(
            center, halfExtents, moveDir,
            _castHits, Quaternion.identity, castDist,
            ~0, QueryTriggerInteraction.Ignore); // ignore trigger colliders

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit h = _castHits[i];
            if (h.collider == null)                        continue; // cleared slot
            if (h.collider.gameObject == gameObject)       continue; // self
            if (h.collider.CompareTag(playerTag))          continue; // player
            // distance == 0 means the box overlaps this collider at the start of the cast
            if (h.distance <= 0f)                          continue;
            return true;
        }
        return false;
    }

    private readonly RaycastHit[] _castHits = new RaycastHit[8];

    private void RotateToward(Vector3 moveDir)
    {
        if (!rotateTowardDirection || moveDir == Vector3.zero) return;

        Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);

        if (rotationSpeed <= 0f)
        {
            rb.MoveRotation(targetRot);
        }
        else
        {
            Quaternion newRot = Quaternion.RotateTowards(rb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(newRot);
        }
    }

    private void FlipDirection()
    {
        movingToA = !movingToA;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag(playerTag)) return;
        //FlipDirection();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 origin = Application.isPlaying ? worldTargetA - transform.TransformDirection(localOffsetA) + transform.TransformDirection(localOffsetA)
                                               : transform.position;

        Vector3 a = Application.isPlaying ? worldTargetA : transform.position + transform.TransformDirection(localOffsetA);
        Vector3 b = Application.isPlaying ? worldTargetB : transform.position + transform.TransformDirection(localOffsetB);

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(a, 0.2f);
        Gizmos.DrawSphere(b, 0.2f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(a, b);
    }
#endif
}
