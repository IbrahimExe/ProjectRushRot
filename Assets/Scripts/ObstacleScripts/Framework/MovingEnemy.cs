using UnityEngine;

public class MovingEnemy : MonoBehaviour
{
    // State

    private enum EnemyState { Wandering, Chasing }
    private EnemyState _state = EnemyState.Wandering;

    // General

    [Header("Movement")]
    public float moveSpeed = 4f;
    public float rotationSpeed = 5f;

    // Wander

    [Header("Wander")]
    public float wanderSpeed = 2f;
    public float wanderRadius = 6f;        // How far from spawn it can roam
    public float waypointReachedDist = 0.5f;
    public float wanderPauseDuration = 1.5f; // How long to idle at each waypoint

    private Vector3 _wanderTarget;
    private Vector3 _spawnPosition;
    private float _pauseTimer = 0f;
    private bool _isPaused = false;

    // Chase

    [Header("Chase")]
    public float losePlayerRange = 20f;    // Gives up chase beyond this distance
    public float losePlayerDelay = 3f;     // Seconds before giving up after losing sight

    private Transform _player;
    private float _losePlayerTimer = 0f;

    // Obstacle Avoidance

    [Header("Obstacle Avoidance")]
    public float detectionRange = 3f;
    public float avoidanceStrength = 2f;
    public int rayCount = 5;
    public float raySpreadAngle = 60f;
    public LayerMask obstacleLayer;

    // Ground Following

    [Header("Ground Following")]
    public float groundCheckDistance = 5f;
    public float groundOffset = 0.5f;
    public float groundFollowSpeed = 10f;
    public float maxClimbAngle = 45f;
    public LayerMask groundLayer;

    // Unity Events

    void Start()
    {
        _spawnPosition = transform.position;
        SnapToGround();
        PickNewWanderTarget();

        // Ensure a trigger sphere is present - can also be set up in the prefab
        SetupDetectionSphere();

        // Register after all Awakes have run - safe for runtime-spawned enemies
        if (EnemyManager.Instance != null)
            EnemyManager.Instance.Register(this);
        else
            Debug.LogWarning($"{name}: EnemyManager.Instance is null in Start! " +
                "Make sure an EnemyManager is in the scene.");
    }

    // Called when the spawner destroys this enemy (e.g. it falls behind the runner)
    void OnDestroy() => EnemyManager.Instance?.Unregister(this);

    // Called by the detection sphere child object
    public void OnPlayerEnterRange(Transform player)
    {
        _player = player;
        _state = EnemyState.Chasing;
        _losePlayerTimer = 0f;

        Debug.Log($"{name} spotted the player - chasing!");
    }

    // Main Update (called by EnemyManager)

    public void ManualUpdate()
    {
        UpdateGrounding();

        switch (_state)
        {
            case EnemyState.Wandering: UpdateWander(); break;
            case EnemyState.Chasing: UpdateChase(); break;
        }
    }

    // Wander

    void UpdateWander()
    {
        if (_isPaused)
        {
            _pauseTimer -= Time.deltaTime;
            if (_pauseTimer <= 0f)
            {
                _isPaused = false;
                PickNewWanderTarget();
            }
            return;
        }

        Vector3 direction = GetSteeringDirection(_wanderTarget);
        Move(direction, wanderSpeed);

        // Reached waypoint - pause then pick a new one
        float distToTarget = Vector3.Distance(
            new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(_wanderTarget.x, 0, _wanderTarget.z)
        );

        if (distToTarget < waypointReachedDist)
        {
            _isPaused = true;
            _pauseTimer = wanderPauseDuration;
        }
    }

    void PickNewWanderTarget()
    {
        // Pick a random point within wanderRadius of spawn position
        Vector2 randomCircle = Random.insideUnitCircle * wanderRadius;
        Vector3 candidate = _spawnPosition + new Vector3(randomCircle.x, 0, randomCircle.y);

        // Snap candidate Y to ground
        if (Physics.Raycast(candidate + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f, groundLayer))
            candidate.y = hit.point.y + groundOffset;

        _wanderTarget = candidate;
    }

    // Chase

    void UpdateChase()
    {
        if (_player == null) { ReturnToWander(); return; }

        float distToPlayer = Vector3.Distance(transform.position, _player.position);

        if (distToPlayer > losePlayerRange)
        {
            _losePlayerTimer += Time.deltaTime;
            if (_losePlayerTimer >= losePlayerDelay)
            {
                ReturnToWander();
                return;
            }
        }
        else
        {
            _losePlayerTimer = 0f; // reset timer if player comes back in range
        }

        Vector3 direction = GetSteeringDirection(_player.position);
        Move(direction, moveSpeed);
    }

    void ReturnToWander()
    {
        _state = EnemyState.Wandering;
        _player = null;
        _losePlayerTimer = 0f;
        _isPaused = false;
        PickNewWanderTarget();

        Debug.Log($"{name} lost the player - wandering.");
    }

    // Shared Steering

    Vector3 GetSteeringDirection(Vector3 target)
    {
        Vector3 toTarget = (target - transform.position);
        toTarget.y *= 0.3f;
        toTarget.Normalize();

        Vector3 avoidance = Vector3.zero;

        for (int i = 0; i < rayCount; i++)
        {
            float t = rayCount == 1 ? 0.5f : (float)i / (rayCount - 1);
            float angle = Mathf.Lerp(-raySpreadAngle / 2f, raySpreadAngle / 2f, t);
            Vector3 rayDir = Quaternion.Euler(0, angle, 0) * toTarget;

            if (Physics.Raycast(transform.position, rayDir, out RaycastHit hit, detectionRange, obstacleLayer))
            {
                float proximity = 1f - (hit.distance / detectionRange);
                float slopePenalty = Vector3.Angle(hit.normal, Vector3.up) > maxClimbAngle ? 2f : 1f;
                avoidance -= rayDir * proximity * slopePenalty;
            }

            Debug.DrawRay(transform.position, rayDir * detectionRange, Color.red);
        }

        Vector3 final = (toTarget + avoidance * avoidanceStrength).normalized;
        final.y = 0f;
        return final;
    }

    // Ground Following

    void SnapToGround()
    {
        if (Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down,
            out RaycastHit hit, groundCheckDistance + 2f, groundLayer))
        {
            Vector3 pos = transform.position;
            pos.y = hit.point.y + groundOffset;
            transform.position = pos;
        }
    }

    void UpdateGrounding()
    {
        if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down,
            out RaycastHit hit, groundCheckDistance, groundLayer))
        {
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            if (slopeAngle <= maxClimbAngle)
            {
                Vector3 pos = transform.position;
                pos.y = Mathf.Lerp(pos.y, hit.point.y + groundOffset, groundFollowSpeed * Time.deltaTime);
                transform.position = pos;
            }
        }
        else
        {
            Vector3 pos = transform.position;
            pos.y -= 9.8f * Time.deltaTime;
            transform.position = pos;
        }
    }

    // Movement

    void Move(Vector3 direction, float speed)
    {
        if (direction != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        transform.position += direction * speed * Time.deltaTime;
    }

    // Setup

    void SetupDetectionSphere()
    {
        // Only auto-create if no PlayerDetector child exists yet
        if (GetComponentInChildren<PlayerDetector>() != null) return;

        GameObject detector = new GameObject("PlayerDetector");
        detector.transform.SetParent(transform);
        detector.transform.localPosition = Vector3.zero;
        detector.layer = gameObject.layer;

        SphereCollider col = detector.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 8f; // detection radius - tune this in Inspector via PlayerDetector

        detector.AddComponent<PlayerDetector>();
    }
}
