using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerControllerBase : MonoBehaviour
{
    [Header("Character")]
    public PlayerCharacterData characterData;
    private GameObject currentModel;

    [Header("Movement Settings")]
    public float baseMaxMoveSpeed = 50f;
    public float baseAcceleration = 25f;
    public float baseDeceleration = 25f;

    private float maxMoveSpeed;
    private float acceleration;

    public float manualDeceleration = 30f;
    public float backwardMaxMoveSpeed = 10f;
    public float backwardAcceleration = 15f;
    public float backwardDeceleration = 20f;

    public float rotationSpeed = 75f;
    public float linearDrag = 0.2f;

    [Header("Drift Settings")]
    public float baseGrip = 8f;
    public float turnGrip = 2f;
    public float gripLerpSpeed = 5f;

    [Header("Landing Grip")]
    public float landingGripDelay = 0.35f;
    private float lastAirTime;

    [Header("Jump and Gravity Settings")]
    public float baseJumpForce = 125f;
    private float jumpForce;

    public float fallMultiplier = 3f;
    public float lowJumpMultiplier = 1f;
    public float maxFallSpeed = -150f;

    [Header("Jump Timing")]
    public float coyoteTime = 0.12f;
    public float jumpBuffer = 0.12f;

    [Header("Ground Check Settings")]
    public Transform feetTransform;

    [Header("Visual Tilt (Ground Only)")]
    public Transform cartModel;
    public Transform rayOrigin;
    public float rayLength = 1.5f;
    public float tiltForwardAmount = 10f;
    public float tiltSideAmount = 10f;
    public float tiltSpeed = 5f;
    public float groundAlignSpeed = 8f;

    [Header("Air Upright (Visual)")]
    public float airUprightSpeed = 8f;
    public bool keepModelUprightInAir = true;
    public float uprightLockoutAfterWallJump = 0.15f;

    [Header("Air Movement Tilt (Visual)")]
    public bool enableAirMovementTilt = true;
    [Tooltip("Max tilt angle when moving forward/backward in air.")]
    public float airTiltAngle = 15f;
    [Tooltip("How quickly the tilt responds to velocity changes.")]
    public float airTiltSpeed = 3f;

    private float lastWallJumpTime = -999f;

    [Header("Abilities (assign these components)")]
    public DashAbility dash;
    public WallRunAbility wallRun;
    public WallJumpAbility wallJump;

    public Rigidbody RB { get; private set; }
    public bool IsGrounded { get; private set; }
    public Vector3 GroundNormal { get; private set; } = Vector3.up;

    // Ability-driven flags
    public bool SuppressMoveInput { get; set; } = false;   // dash uses this
    public bool SuppressJumpBuffer { get; set; } = false;  // dash flip uses this
    public float MaxSpeedMultiplier { get; set; } = 1f;    // dash boost uses this

    private float lastGroundedTime;
    private float lastJumpPressedTime = -999f;
    public float baseDashKillWindow = 0.25f;
    public float baseJumpAwayImpulse = 50f;
    public float baseJumpUpImpulse = 25f;
    public float baseWallRunDuration = 4f;
    public float baseWallRunSpeed = 45f;

    [Header("Air Jumps")]
    public int currentJumps = 0;
    void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        RB = GetComponent<Rigidbody>();

        if (characterData != null) ApplyCharacter(characterData);
        else SetBaseStats();
    }

    public void ApplyCharacter(PlayerCharacterData data)
    {
        characterData = data;

        baseMaxMoveSpeed = data.maxMoveSpeed;
        baseAcceleration = data.acceleration;
        baseDeceleration = data.deceleration;
        baseJumpForce = data.jumpForce;

        SetBaseStats();

        if (currentModel != null) Destroy(currentModel);

        currentModel = Instantiate(data.modelPrefab, cartModel);

        Transform t = currentModel.transform;
        t.localPosition = data.modelOffset;
        t.localRotation = Quaternion.Euler(data.modelRotation);
        t.localScale = data.modelScale;

        if (dash != null) dash.cartModel = cartModel;
        if (wallRun != null) wallRun.cartModel = cartModel;
    }

    public void SetBaseStats()
    {
        maxMoveSpeed = baseMaxMoveSpeed;
        acceleration = baseAcceleration;
        jumpForce = baseJumpForce;
    }

    public void NotifyWallJump()
    {
        lastWallJumpTime = Time.time;
    }

    void Update()
    {
        IsGrounded = CheckGrounded();
        if (IsGrounded) lastGroundedTime = Time.time;

        if (!SuppressJumpBuffer)
        {
            if (Input.GetButtonDown("Jump"))
                lastJumpPressedTime = Time.time;
        }

        if (dash != null) dash.TickUpdate();
    }

    void FixedUpdate()
    {
        if (dash != null) dash.TickFixed();
        if (wallRun != null) wallRun.TickFixed();
        if (wallJump != null) wallJump.TickFixed();

        BaseMove();
        ApplyCustomGravity();
        AlignModelToGroundAndTilt_GroundOnly();
        UprightModelInAir();
        ApplyAirMovementTilt();

        if (!IsGrounded)
        {
            Quaternion rot = RB.rotation;
            rot.eulerAngles = new Vector3(0f, rot.eulerAngles.y, 0f);
            RB.MoveRotation(rot);
        }

        if (!IsGrounded)
            lastAirTime = Time.time;
    }

    private void BaseMove()
    {
        if (wallRun != null && wallRun.IsWallRunning) return;
        if (dash != null && (dash.IsDashing || dash.IsSideDashing)) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        if (SuppressMoveInput) { h = 0f; v = 0f; }

        Vector3 up = IsGrounded ? GroundNormal : Vector3.up;

        float yaw = h * rotationSpeed * Time.fixedDeltaTime;
        RB.MoveRotation(Quaternion.AngleAxis(yaw, up) * RB.rotation);

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, up).normalized;
        Vector3 planarVel = Vector3.ProjectOnPlane(RB.linearVelocity, up);

        float maxForward = maxMoveSpeed * MaxSpeedMultiplier;

        if (v > 0f)
            RB.AddForce(forward * acceleration * v, ForceMode.Acceleration);
        else if (v < 0f)
            RB.AddForce(-forward * backwardAcceleration * -v, ForceMode.Acceleration);

        Vector3 newPlanar = Vector3.ClampMagnitude(planarVel, maxForward);
        RB.linearVelocity = newPlanar + Vector3.Project(RB.linearVelocity, up);

        if (IsGrounded)
        {
            Vector3 sideways = Vector3.Project(planarVel, transform.right);
            float speedFactor = planarVel.magnitude / maxMoveSpeed;
            float turnAmount = Mathf.Abs(h) * speedFactor;
            float targetGrip = Mathf.Lerp(baseGrip, turnGrip, turnAmount);

            float timeSinceAir = Time.time - lastAirTime;
            float landingGripPercent = Mathf.Clamp01(timeSinceAir / landingGripDelay);
            float grip = Mathf.Lerp(0f, targetGrip, landingGripPercent);

            RB.AddForce(-sideways * grip, ForceMode.Acceleration);
        }
    }

    private void ApplyCustomGravity()
    {
        if (wallRun != null && wallRun.IsWallRunning) return;

        RB.AddForce(Physics.gravity * fallMultiplier, ForceMode.Acceleration);

        if (RB.linearVelocity.y < 0)
            RB.AddForce(Physics.gravity * (fallMultiplier - 1f), ForceMode.Acceleration);
        else if (RB.linearVelocity.y > 0 && !Input.GetButton("Jump"))
            RB.AddForce(Physics.gravity * (lowJumpMultiplier - 1f), ForceMode.Acceleration);

        if (RB.linearVelocity.y < maxFallSpeed)
            RB.linearVelocity = new Vector3(RB.linearVelocity.x, maxFallSpeed, RB.linearVelocity.z);
    }

    public bool WantsJumpBuffered() => (Time.time - lastJumpPressedTime) <= jumpBuffer;
    public bool CanCoyoteJump() => (Time.time - lastGroundedTime) <= coyoteTime;
    public void ConsumeJumpBuffer() => lastJumpPressedTime = -999f;

    public void DoNormalJump()
    {
        RB.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    private bool CheckGrounded()
    {
        if (feetTransform == null) return false;

        if (Physics.Raycast(feetTransform.position, Vector3.down, out RaycastHit hit, 1.0f))
        {
            RB.linearDamping = linearDrag;
            GroundNormal = hit.normal;
            return true;
        }

        RB.linearDamping = 0f;
        return false;
    }

    private void AlignModelToGroundAndTilt_GroundOnly()
    {
        if (cartModel == null || rayOrigin == null) return;
        if (dash != null && dash.IsDashFlipping) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        if (SuppressMoveInput) { h = 0f; v = 0f; }

        bool isMoving = Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f;
        float targetForwardTilt = isMoving ? -v * tiltForwardAmount : 0f;
        float targetSideTilt = isMoving ? -h * tiltSideAmount : 0f;

        Quaternion moveTilt = Quaternion.Euler(targetForwardTilt, 0f, targetSideTilt);

        if (Physics.Raycast(rayOrigin.position, Vector3.down, out RaycastHit hit, rayLength))
        {
            GroundNormal = hit.normal;
            Quaternion groundTilt = Quaternion.FromToRotation(cartModel.up, GroundNormal) * cartModel.rotation;
            cartModel.rotation = Quaternion.Slerp(cartModel.rotation, groundTilt, Time.deltaTime * groundAlignSpeed);
            cartModel.rotation = Quaternion.Euler(
                cartModel.rotation.eulerAngles.x,
                RB.rotation.eulerAngles.y,
                cartModel.rotation.eulerAngles.z
            );
        }

        cartModel.localRotation *= Quaternion.Lerp(Quaternion.identity, moveTilt, Time.deltaTime * tiltSpeed);
    }

    private void UprightModelInAir()
    {
        if (!keepModelUprightInAir) return;
        if (cartModel == null) return;
        if (wallRun != null && wallRun.IsWallRunning) return;
        if (dash != null && dash.IsDashFlipping) return;
        if (IsGrounded) return;
        if (Time.time - lastWallJumpTime < uprightLockoutAfterWallJump) return;

        if (enableAirMovementTilt)
        {
            float currentY = cartModel.rotation.eulerAngles.y;
            float targetY = RB.rotation.eulerAngles.y;
            float smoothY = Mathf.LerpAngle(currentY, targetY, Time.deltaTime * airUprightSpeed);
            Vector3 currentEuler = cartModel.rotation.eulerAngles;
            cartModel.rotation = Quaternion.Euler(currentEuler.x, smoothY, currentEuler.z);
        }
        else
        {
            Quaternion target = Quaternion.Euler(0f, RB.rotation.eulerAngles.y, 0f);
            cartModel.rotation = Quaternion.Slerp(cartModel.rotation, target, Time.deltaTime * airUprightSpeed);
        }
    }

    private void ApplyAirMovementTilt()
    {
        if (!enableAirMovementTilt) return;
        if (cartModel == null) return;
        if (IsGrounded) return;
        if (wallRun != null && wallRun.IsWallRunning) return;
        if (dash != null && dash.IsDashFlipping) return;
        if (dash != null && (dash.IsDashing || dash.IsSideDashing)) return;
        if (Time.time - lastWallJumpTime < uprightLockoutAfterWallJump) return;

        Vector3 planarVel = Vector3.ProjectOnPlane(RB.linearVelocity, Vector3.up);
        float forwardSpeed = Vector3.Dot(planarVel, transform.forward);
        float speedFactor = Mathf.Clamp(forwardSpeed / baseMaxMoveSpeed, -1f, 1f);
        float targetTiltX = -speedFactor * airTiltAngle;

        Vector3 currentEuler = cartModel.rotation.eulerAngles;
        Quaternion targetRotation = Quaternion.Euler(targetTiltX, currentEuler.y, 0f);
        cartModel.rotation = Quaternion.Slerp(cartModel.rotation, targetRotation, Time.deltaTime * airTiltSpeed);
    }

    // ─────────────────────────────────────────────
    //  UPGRADE HELPERS
    // ─────────────────────────────────────────────

    // — Movement —
    public void addMaxSpeed(float amount) => maxMoveSpeed += amount;
    public void addAcceleration(float amount) => acceleration += amount;
    public void addJumpForce(float amount) => jumpForce += amount;

    // — Wall Run (delegates to WallRunAbility) —
    public void addWallRunDuration(float amount)
    {
        if (wallRun != null)
            wallRun.wallRunDuration += amount;
        else
            Debug.LogWarning("PlayerControllerBase: No WallRunAbility assigned — cannot upgrade Wall Run Duration.");
    }

    public void addWallRunSpeed(float amount)
    {
        if (wallRun != null)
            wallRun.wallRunSpeedMultiplier += amount;   // scales tangent acceleration
        else
            Debug.LogWarning("PlayerControllerBase: No WallRunAbility assigned — cannot upgrade Wall Run Speed.");
    }

    // — Wall Jump (delegates to WallJumpAbility) —
    public void addWallJumpUpImpulse(float amount)
    {
        if (wallJump != null)
            wallJump.wallJumpUpImpulse += amount;
        else
            Debug.LogWarning("PlayerControllerBase: No WallJumpAbility assigned — cannot upgrade Wall Jump Up Impulse.");
    }

    public void addWallJumpAwayImpulse(float amount)
    {
        if (wallJump != null)
            wallJump.wallJumpAwayImpulse += amount;
        else
            Debug.LogWarning("PlayerControllerBase: No WallJumpAbility assigned — cannot upgrade Wall Jump Away Impulse.");
    }

    // — Dash Kill (delegates to DashAbility) —
    public void addDashKillWindow(float amount)
    {
        if (dash != null)
            dash.hitWindowAfterDash += amount;
        else
            Debug.LogWarning("PlayerControllerBase: No DashAbility assigned — cannot upgrade Dash Kill Window.");
    }

    public void addDashKillCount(int amount)
    {
        if (dash != null)
            dash.dashKillCap += amount;
        else
            Debug.LogWarning("PlayerControllerBase: No DashAbility assigned — cannot upgrade Dash Kill Count.");
    }

    // ─────────────────────────────────────────────

    public void Respawn()
    {
        RB.linearVelocity = Vector3.zero;
        RB.angularVelocity = Vector3.zero;
    }

    public void ChangeCharacter(PlayerCharacterData newData)
    {
        ApplyCharacter(newData);
    }
}