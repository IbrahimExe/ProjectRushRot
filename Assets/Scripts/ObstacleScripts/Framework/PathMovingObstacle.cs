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

    [Tooltip("Check if you want the obstacle to flip direction when it collides with something except for the player.")]
    public bool reverseOnCollision = false;

    [Tooltip("Random delay before the obstacle starts moving. Set both to 0 to disable.")]
    public float startDelayMin = 0f;
    public float startDelayMax = 2f;

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
    [Tooltip("World-space offset from the spawn position to the first path point. E.g. (0, 20, 0) moves 20 units straight up in world space.")]
    public Vector3 localOffsetA = new Vector3(-5f, 0f, 0f);

    [Tooltip("World-space offset from the spawn position to the second path point. E.g. (0, -15, 0) moves 15 units straight down in world space.")]
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

    // Ground Floor Clamp
    [Header("Ground Floor Clamp")]
    [Tooltip("When enabled, neither path point will ever go below the obstacle's spawn Y plus MinHeightAboveGround. "
           + "Use this to prevent the lower path point from clipping into the terrain.")]
    public bool clampToGround = false;

    [Tooltip("Minimum world-Y clearance above the spawn point that the lower path point is allowed to reach. "
           + "0 = flush with spawn surface, positive values keep the obstacle above the ground.")]
    public float minHeightAboveGround = 0f;

    // Player Tag
    [Header("Collision")]
    [Tooltip("Tag used to identify the player. Collisions with this tag will not reverse the obstacle.")]
    public string playerTag = "Player";

    // Runtime State
    private Rigidbody rb;
    private Collider col;

    // Y of the terrain surface at the moment this obstacle was spawned / recycled.
    // Used to clamp path points so they never go underground.
    private float _spawnFloorY;

    // World-space targets
    private Vector3 worldTargetA;
    private Vector3 worldTargetB;

    // Which target we are currently moving toward
    private bool movingToA = false;

    private Vector3 CurrentTarget => movingToA ? worldTargetA : worldTargetB;

    // Start delay
    private float _startDelayTimer = 0f;
    private bool _moving = false;

    // -------------------------------------------------------------------------

    private void Start()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        col = GetComponent<Collider>();

        // The spawn origin is always the parent's world position — that is the root GameObject
        // that ChunkSpawner places precisely at the terrain surface.
        // We must NOT read transform.position here: the Rigidbody on this child object
        // physically moves it during gameplay (e.g. bouncing up to Y+20). When the pool
        // recycles and re-enables this object, the child's world position is still wherever
        // physics left it, NOT at the new spawn point. The parent (root) is always correct
        // because ChunkSpawner calls SetPositionAndRotation on it before SetActive(true).
        // Falls back to transform.position if this script happens to be on the root itself.
        Vector3 origin = transform.parent != null
            ? transform.parent.position
            : transform.position;

        // Snap the Rigidbody back to the origin so the fish always starts
        // from the root position on each activation, not from a stale physics position.
        rb.position = origin;
        transform.position = origin;

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

        // Path targets are simply the origin plus the configured offsets.
        // (0, 20, 0) → 20 units straight up from the terrain floor.
        // (0, -20, 0) → 20 units straight down, going through the floor.
        worldTargetA = origin + localOffsetA;
        worldTargetB = origin + localOffsetB;

        // Store origin Y for the gizmo and optional ground clamp.
        _spawnFloorY = origin.y;

        if (clampToGround)
        {
            float floorY = _spawnFloorY + minHeightAboveGround;
            worldTargetA.y = Mathf.Max(worldTargetA.y, floorY);
            worldTargetB.y = Mathf.Max(worldTargetB.y, floorY);
        }

        movingToA = false;

        // Pick a random start delay for this instance
        _startDelayTimer = Random.Range(startDelayMin, startDelayMax);
        _moving = (_startDelayTimer <= 0f);
    }

    private void FixedUpdate()
    {
        if (!_moving)
        {
            _startDelayTimer -= Time.fixedDeltaTime;
            if (_startDelayTimer <= 0f)
                _moving = true;
            return;
        }

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
        if (!reverseOnCollision) return;
        FlipDirection();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // In edit mode preview the path using the same world-space offset math as Initialize().
        Vector3 origin = Application.isPlaying ? new Vector3(transform.position.x, _spawnFloorY, transform.position.z)
                                               : transform.position;

        Vector3 a = Application.isPlaying ? worldTargetA : origin + localOffsetA;
        Vector3 b = Application.isPlaying ? worldTargetB : origin + localOffsetB;

        if (clampToGround)
        {
            float floorY = origin.y + minHeightAboveGround;
            a.y = Mathf.Max(a.y, floorY);
            b.y = Mathf.Max(b.y, floorY);

            // Draw the ground floor as a red horizontal line so you can see the clamp boundary
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
            Vector3 floorCenter = new Vector3(origin.x, floorY, origin.z);
            Gizmos.DrawLine(floorCenter + Vector3.left * 1.5f, floorCenter + Vector3.right * 1.5f);
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(a, 0.2f);
        Gizmos.DrawSphere(b, 0.2f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(a, b);
    }
#endif
}
