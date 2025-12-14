using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyIdleChase : MonoBehaviour
{
    public enum State 
    { 
        Idle, 
        Chasing 
    }

    [Header("References")]
    public Transform Player;
    public Transform EyePoint;

    [Header("Detection")]
    public float DetectionRange = 100f;
    [Range(0f, 360f)] public float ViewAngle = 360f;
    public bool RequireLineOfSight = true;
    public float LoseSightDelay = 3f;
    public float CheckInterval = 0.24f;

    [Header("Movement / Climbing")]
    public float MoveSpeed = 18f;
    public float ClimbSpeed = 10f;
    public float ClimbCheckDistance = 1f;
    public float StoppingDistance = 8f;
    public bool FaceMovementDirection = true;

    [Header("Behavior")]
    public bool ReturnToStartWhenLost = true;
    public float IdleLookAmplitude = 10f;
    public float IdleLookSpeed = 1f;

    // Internal
    private State _state = State.Idle;
    private Vector3 _startPosition;
    private Quaternion _startRotation;
    private float _lastCheckTime;
    private float _lastSeenTime;
    private Vector3 _lastKnownPlayerPos;
    private Transform _eye;
    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    private void Start()
    {
        _startPosition = transform.position;
        _startRotation = transform.rotation;
        _eye = EyePoint ? EyePoint : transform;

        if (Player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p) Player = p.transform;
        }
    }

    // ---------------- UPDATE (LOGIC ONLY) ----------------
    private void Update()
    {
        if (Player == null) return;

        if (Time.time - _lastCheckTime >= CheckInterval)
        {
            _lastCheckTime = Time.time;

            if (CheckCanSeePlayer())
            {
                _lastSeenTime = Time.time;
                _lastKnownPlayerPos = Player.position;
                _state = State.Chasing;
            }
        }

        if (_state == State.Chasing && Time.time - _lastSeenTime > LoseSightDelay)
        {
            _state = State.Idle;
            if (ReturnToStartWhenLost)
                _lastKnownPlayerPos = _startPosition;
        }
    }

    // ---------------- FIXED UPDATE (PHYSICS) ----------------
    private void FixedUpdate()
    {
        if (_state == State.Idle)
            UpdateIdleBehaviour();
        else
            UpdateChaseBehaviour();
    }

    // ---------------- DETECTION ----------------
    private bool CheckCanSeePlayer()
    {
        Vector3 toPlayer = Player.position - _eye.position;
        float distSqr = toPlayer.sqrMagnitude;
        if (distSqr > DetectionRange * DetectionRange)
            return false;

        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        Vector3 flatToPlayer = Vector3.ProjectOnPlane(toPlayer, Vector3.up);

        if (Vector3.Angle(flatForward, flatToPlayer) > ViewAngle * 0.5f)
            return false;

        if (RequireLineOfSight)
        {
            if (Physics.Raycast(_eye.position, toPlayer.normalized, out RaycastHit hit, Mathf.Sqrt(distSqr)))
                return hit.transform == Player || hit.transform.IsChildOf(Player);

            return false;
        }

        return true;
    }

    // ---------------- IDLE ----------------
    private void UpdateIdleBehaviour()
    {
        float sway = Mathf.Sin(Time.time * IdleLookSpeed) * IdleLookAmplitude;
        Quaternion targetRot = _startRotation * Quaternion.Euler(0f, sway, 0f);
        _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, targetRot, 2f * Time.fixedDeltaTime));
    }

    // ---------------- CHASE ----------------
    private void UpdateChaseBehaviour()
    {
        Vector3 toTarget = _lastKnownPlayerPos - transform.position;
        float distance = toTarget.magnitude;

        // Wall check
        bool canClimb = false;
        Vector3 wallNormal = Vector3.zero;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;

        if (Physics.Raycast(rayOrigin, transform.forward, out RaycastHit hit, ClimbCheckDistance))
        {
            canClimb = true;
            wallNormal = hit.normal;
        }

        Vector3 movement = Vector3.zero;

        if (canClimb)
        {
            movement = Vector3.up * ClimbSpeed;

            if (wallNormal.sqrMagnitude > 0.01f)
            {
                Vector3 tangent = Vector3.Cross(Vector3.up, wallNormal).normalized;
                movement += tangent * 0.2f;
            }

            _rb.MovePosition(_rb.position + movement * Time.fixedDeltaTime);

            if (wallNormal.sqrMagnitude > 0.01f)
            {
                Quaternion look = Quaternion.LookRotation(-wallNormal);
                _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, look, 10f * Time.fixedDeltaTime));
            }
        }
        else
        {
            if (distance > StoppingDistance)
                movement = toTarget.normalized * MoveSpeed;

            _rb.MovePosition(_rb.position + movement * Time.fixedDeltaTime);

            if (FaceMovementDirection && movement.sqrMagnitude > 0.001f)
            {
                Vector3 lookDir = new Vector3(movement.x, 0f, movement.z);
                Quaternion look = Quaternion.LookRotation(lookDir);
                _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, look, 10f * Time.fixedDeltaTime));
            }
        }
    }

    // ---------------- DEBUG ----------------
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        Gizmos.DrawLine(rayOrigin, rayOrigin + transform.forward * ClimbCheckDistance);

        Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
        Gizmos.DrawSphere(transform.position, DetectionRange);

        Gizmos.color = Color.yellow;
        float half = ViewAngle * 0.5f;
        Gizmos.DrawLine(transform.position,
            transform.position + Quaternion.Euler(0f, -half, 0f) * transform.forward * DetectionRange);
        Gizmos.DrawLine(transform.position,
            transform.position + Quaternion.Euler(0f, half, 0f) * transform.forward * DetectionRange);
    }
}
