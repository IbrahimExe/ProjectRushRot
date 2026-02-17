// PlayerControllerBase.cs
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
    public float linearDrag = 5f;

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

        //baseStartMoveSpeed = data.startMoveSpeed;
        baseMaxMoveSpeed = data.maxMoveSpeed;
        baseAcceleration = data.acceleration;
        baseDeceleration = data.deceleration;
        baseJumpForce = data.jumpForce;

        SetBaseStats();

        // model swap
        if (currentModel != null) Destroy(currentModel);


        if (currentModel != null)
            Destroy(currentModel);

        // Instantiate as child of PlayerModel
        currentModel = Instantiate(data.modelPrefab, cartModel);

        // Apply local transform offsets
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

        // Dash input in Update
        if (dash != null) dash.TickUpdate();
    }
    void FixedUpdate()
    {
        // Preserve ordering: abilities first, then base motor, then visuals
        if (dash != null) dash.TickFixed();
        if (wallRun != null) wallRun.TickFixed();
        if (wallJump != null) wallJump.TickFixed();

        BaseMove();
        ApplyCustomGravity();
        AlignModelToGroundAndTilt_GroundOnly();
        UprightModelInAir();
        ApplyAirMovementTilt();


        // Keep upright when airborne (physics body)
        if (!IsGrounded)
        {
            Quaternion rot = RB.rotation;
            rot.eulerAngles = new Vector3(0f, rot.eulerAngles.y, 0f);
            RB.MoveRotation(rot);
        }

        if (!IsGrounded)
            lastAirTime = Time.time;
    }

    // BASE MOVEMENT MOTOR
    // Handles standard grounded/air movement when no abilities override control.
    // - Rotation
    // - Acceleration
    // - Speed limiting
    // - Drift grip
    // - Landing traction smoothing
    private void BaseMove()
    {
        // Don't apply base movement forces if we're doing a special movement that should override it
        if (wallRun != null && wallRun.IsWallRunning) return;
        if (dash != null && (dash.IsDashing || dash.IsSideDashing)) return;

        // Read input
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        if (SuppressMoveInput)
        {
            h = 0f;
            v = 0f;
        }

        // Determine the up direction for movement and rotation
        Vector3 up = IsGrounded ? GroundNormal : Vector3.up;

        // Rotation
        float yaw = h * rotationSpeed * Time.fixedDeltaTime;
        RB.MoveRotation(Quaternion.AngleAxis(yaw, up) * RB.rotation);

        // Determine the forward direction for movement
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, up).normalized;
        Vector3 planarVel = Vector3.ProjectOnPlane(RB.linearVelocity, up); // current horizontal velocity

        // Calculate max forward speed with multiplier
        float maxForward = maxMoveSpeed * MaxSpeedMultiplier;

        // Engine force (only when input is pressed)
        if (v > 0f)
        {
            // Forward acceleration
            RB.AddForce(forward * acceleration * v, ForceMode.Acceleration);
        }
        else if (v < 0f)
        {
            // Backward acceleration
            RB.AddForce(-forward * backwardAcceleration * -v, ForceMode.Acceleration);
        }

        // SPEED LIMIT
        Vector3 newPlanar = Vector3.ClampMagnitude(planarVel, maxForward);

        RB.linearVelocity =
            newPlanar +
            Vector3.Project(RB.linearVelocity, up);

        // Grip / Drift (only when grounded, and only sideways)
        if (IsGrounded)
        {
            // sideways velocity relative to player orientation
            Vector3 sideways = Vector3.Project(planarVel, transform.right);

            // How fast palyer is moving relative to max speed
            float speedFactor = planarVel.magnitude / maxMoveSpeed;
            // turning harder at higher speeds = less grip
            float turnAmount = Mathf.Abs(h) * speedFactor;
            // interpolate between base grip and turn grip based on how much we're turning
            float targetGrip = Mathf.Lerp(baseGrip, turnGrip, turnAmount);

            // Landing grip fade
            // prevent instant traction when landing from a jump
            float timeSinceAir = Time.time - lastAirTime;
            float landingGripPercent = Mathf.Clamp01(timeSinceAir / landingGripDelay);

            float grip = Mathf.Lerp(0f, targetGrip, landingGripPercent);

            // apply sideways force to simulate grip/drift
            RB.AddForce(-sideways * grip, ForceMode.Acceleration);
        }

    }
    private void ApplyCustomGravity()
    {
        // WallRunAbility will override vertical when wall-running
        if (wallRun != null && wallRun.IsWallRunning)
            return;

        // Optional: if you want dash hop to feel consistent, let dash own vertical while active
        // (Side dash sets vertical velocity directly at start, then you can still let gravity act.)
        // Keeping gravity active during dash is usually fine.

        // Apply base "heavier gravity" feel
        RB.AddForce(Physics.gravity * fallMultiplier, ForceMode.Acceleration);

        // Extra gravity shaping (kept close to your original intent)
        if (RB.linearVelocity.y < 0)
            RB.AddForce(Physics.gravity * (fallMultiplier - 1f), ForceMode.Acceleration);
        else if (RB.linearVelocity.y > 0 && !Input.GetButton("Jump"))
            RB.AddForce(Physics.gravity * (lowJumpMultiplier - 1f), ForceMode.Acceleration);

        if (RB.linearVelocity.y < maxFallSpeed)
            RB.linearVelocity = new Vector3(RB.linearVelocity.x, maxFallSpeed, RB.linearVelocity.z);
    }

    public bool WantsJumpBuffered()
    {
        return (Time.time - lastJumpPressedTime) <= jumpBuffer;
    }

    public bool CanCoyoteJump()
    {
        return (Time.time - lastGroundedTime) <= coyoteTime;
    }

    public void ConsumeJumpBuffer()
    {
        lastJumpPressedTime = -999f;
    }

    public void DoNormalJump()
    {
        RB.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    private bool CheckGrounded()
    {
        if (feetTransform == null)
            return false;

        float rayLen = 1.0f;

        if (Physics.Raycast(feetTransform.position, Vector3.down, out RaycastHit hit, rayLen))
        {
            RB.linearDamping = linearDrag;

            GroundNormal = hit.normal;
            return true;
        }

        RB.linearDamping = 0f;
        return false;
    }

    // Visual model alignment and tilt (ground only)
    private void AlignModelToGroundAndTilt_GroundOnly()
    {
        if (cartModel == null || rayOrigin == null)
            return;

        // Avoid ground-tilt fighting the dash flip visuals
        if (dash != null && dash.IsDashFlipping)
            return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        if (SuppressMoveInput)
        {
            h = 0f;
            v = 0f;
        }

        bool isMoving = Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f;
        float targetForwardTilt = isMoving ? -v * tiltForwardAmount : 0f;
        float targetSideTilt = isMoving ? -h * tiltSideAmount : 0f;

        Quaternion moveTilt = Quaternion.Euler(targetForwardTilt, 0f, targetSideTilt);

        if (Physics.Raycast(rayOrigin.position, Vector3.down, out RaycastHit hit, rayLength))
        {
            GroundNormal = hit.normal;

            Quaternion groundTilt =
                Quaternion.FromToRotation(cartModel.up, GroundNormal) * cartModel.rotation;

            cartModel.rotation = Quaternion.Slerp(cartModel.rotation, groundTilt, Time.deltaTime * groundAlignSpeed);

            // Snap Y to match forward (no snapping issue since Y is pre-aligned in air)
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

        //  don't upright right after a wall jump
        if (Time.time - lastWallJumpTime < uprightLockoutAfterWallJump)
            return;

        // When air tilt is enabled, only handle Y rotation here
        // ApplyAirMovementTilt handles X tilt
        if (enableAirMovementTilt)
        {
            // Smoothly rotate Y to match forward direction
            float currentY = cartModel.rotation.eulerAngles.y;
            float targetY = RB.rotation.eulerAngles.y;
            float smoothY = Mathf.LerpAngle(currentY, targetY, Time.deltaTime * airUprightSpeed);

            // Preserve X and Z, only update Y
            Vector3 currentEuler = cartModel.rotation.eulerAngles;
            cartModel.rotation = Quaternion.Euler(currentEuler.x, smoothY, currentEuler.z);
        }
        else
        {
            // Normal upright behavior when tilt is disabled
            Quaternion target = Quaternion.Euler(0f, RB.rotation.eulerAngles.y, 0f);
            cartModel.rotation = Quaternion.Slerp(cartModel.rotation, target, Time.deltaTime * airUprightSpeed);
        }
    }
    private void ApplyAirMovementTilt()
    {
        if (!enableAirMovementTilt) return;
        if (cartModel == null) return;
        if (IsGrounded) return;

        // Don't apply air tilt during special abilities
        if (wallRun != null && wallRun.IsWallRunning) return;
        if (dash != null && dash.IsDashFlipping) return;
        if (dash != null && (dash.IsDashing || dash.IsSideDashing)) return;

        // Don't tilt right after a wall jump
        if (Time.time - lastWallJumpTime < uprightLockoutAfterWallJump)
            return;

        // Calculate forward velocity relative to player orientation
        Vector3 planarVel = Vector3.ProjectOnPlane(RB.linearVelocity, Vector3.up);
        float forwardSpeed = Vector3.Dot(planarVel, transform.forward);

        // Normalize by max speed to get a -1 to 1 range
        float speedFactor = Mathf.Clamp(forwardSpeed / baseMaxMoveSpeed, -1f, 1f);

        // Moving forward = negative tilt (lean back)
        // Moving backward = positive tilt (lean forward)
        float targetTiltX = -speedFactor * airTiltAngle;

        // Get current rotation and smoothly lerp to target tilt
        Vector3 currentEuler = cartModel.rotation.eulerAngles;
        float currentY = currentEuler.y;

        Quaternion targetRotation = Quaternion.Euler(targetTiltX, currentY, 0f);
        cartModel.rotation = Quaternion.Slerp(cartModel.rotation, targetRotation, Time.deltaTime * airTiltSpeed);
    }

    // Upgrade helpers
    public void addMaxSpeed(float amount) => maxMoveSpeed += amount;
    public void addAcceleration(float amount) => acceleration += amount;
    public void addJumpForce(float amount) => jumpForce += amount;

    // After respawn logic
    public void Respawn()
    {
        // Reset velocities
        RB.linearVelocity = Vector3.zero;
        RB.angularVelocity = Vector3.zero;
        // Clear movement speeds
        RB.angularVelocity = Vector3.zero;
    }

    // Character switching logic
    public void ChangeCharacter(PlayerCharacterData newData)
    {
        ApplyCharacter(newData);
    }

}
