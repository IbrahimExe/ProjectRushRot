using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerControllerBase : MonoBehaviour
{
    [Header("Character")]
    public PlayerCharacterData characterData;
    private GameObject currentModel;

    [Header("Movement Settings")]
    public float baseStartMoveSpeed = 5f;
    public float baseMaxMoveSpeed = 50f;
    public float baseAcceleration = 25f;
    public float baseDeceleration = 25f;

    public float currentMoveSpeed;
    private float startMoveSpeed;
    private float maxMoveSpeed;
    private float acceleration;
    private float deceleration;

    private float currentReverseSpeed = 0f;

    public float manualDeceleration = 30f;
    public float backwardMaxMoveSpeed = 10f;
    public float backwardAcceleration = 15f;
    public float backwardDeceleration = 20f;

    public float rotationSpeed = 75f;
    public float linearDrag = 5f;

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
    public Transform cartModel; //----------------------------------------
    public Transform rayOrigin;
    public float rayLength = 1.5f;
    public float tiltForwardAmount = 10f;
    public float tiltSideAmount = 10f;
    public float tiltSpeed = 5f;
    public float groundAlignSpeed = 8f;

    // Ability references (to keep current FixedUpdate ordering)
    [Header("Abilities (assign these components)")]
    public DashAbility dash;
    public WallRunAbility wallRun;
    public WallJumpAbility wallJump;

    public Rigidbody RB { get; private set; }
    public bool IsGrounded { get; private set; }
    public Vector3 GroundNormal { get; private set; } = Vector3.up;

    // Ability-driven flags
    public bool SuppressMoveInput { get; set; } = false;      // dash uses this
    public bool SuppressJumpBuffer { get; set; } = false;     // dash flip uses this
    public float MaxSpeedMultiplier { get; set; } = 1f;       // dash boost uses this

    private float lastGroundedTime;
    private float lastJumpPressedTime = -999f;
    private float lastWallJumpTime = -999f;

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

        // ---------------- STATS ----------------
        baseStartMoveSpeed = data.startMoveSpeed;
        baseMaxMoveSpeed = data.maxMoveSpeed;
        baseAcceleration = data.acceleration;
        baseDeceleration = data.deceleration;
        baseJumpForce = data.jumpForce;

        SetBaseStats();

        // ---------------- MODEL ----------------
        if (currentModel != null)
            Destroy(currentModel);

        // Instantiate as child of PlayerModel
        currentModel = Instantiate(data.modelPrefab, cartModel);

        // Apply local transform offsets
        Transform t = currentModel.transform;
        t.localPosition = data.modelOffset;
        t.localRotation = Quaternion.Euler(data.modelRotation);
        t.localScale = data.modelScale;


    }

    public void SetBaseStats()
    {
        startMoveSpeed = baseStartMoveSpeed;
        maxMoveSpeed = baseMaxMoveSpeed;
        acceleration = baseAcceleration;
        deceleration = baseDeceleration;

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
        // Preserve original ordering 
        if (dash != null) dash.TickFixed();
        if (wallRun != null) wallRun.TickFixed();
        if (wallJump != null) wallJump.TickFixed(); // wall jump only

        BaseMove();
        ApplyCustomGravity();
        AlignModelToGroundAndTilt_GroundOnly();

        // keep upright when airborne
        if (!IsGrounded)
        {
            Quaternion rot = RB.rotation;
            rot.eulerAngles = new Vector3(0f, rot.eulerAngles.y, 0f);
            RB.MoveRotation(rot);
        }
    }

    private void BaseMove()
    {
        // If wall-running, wall-run script owns movement this frame
        if (wallRun != null && wallRun.IsWallRunning)
            return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        if (SuppressMoveInput)
        {
            h = 0f;
            v = 0f;
        }

        Vector3 up = IsGrounded ? GroundNormal : Vector3.up;

        float yawDelta = h * rotationSpeed * Time.fixedDeltaTime;
        transform.rotation = Quaternion.AngleAxis(yawDelta, up) * transform.rotation;

        Vector3 forwardFlat = Vector3.ProjectOnPlane(transform.forward, up).normalized;
        Vector3 planarVel = Vector3.ProjectOnPlane(RB.linearVelocity, up);
        float planarSpeed = planarVel.magnitude;

        float motionSign = (planarVel.sqrMagnitude > 0.001f)
            ? Mathf.Sign(Vector3.Dot(planarVel, forwardFlat))
            : 0f;

        if (motionSign >= 0f && currentMoveSpeed < planarSpeed)
            currentMoveSpeed = planarSpeed;

        if (motionSign < 0f && currentReverseSpeed < planarSpeed)
            currentReverseSpeed = planarSpeed;

        bool pressingForward = v > 0.01f;
        bool pressingBackward = v < -0.01f;

        if (pressingForward)
        {
            if (currentMoveSpeed < startMoveSpeed)
                currentMoveSpeed = startMoveSpeed;

            float maxFwd = maxMoveSpeed * Mathf.Max(0.01f, MaxSpeedMultiplier);

            currentMoveSpeed = Mathf.MoveTowards(
                currentMoveSpeed,
                maxFwd,
                acceleration * Time.deltaTime
            );
            currentReverseSpeed = 0f;
        }
        else if (pressingBackward)
        {
            if (currentMoveSpeed > 0.001f)
            {
                currentMoveSpeed = Mathf.MoveTowards(
                    currentMoveSpeed,
                    0f,
                    manualDeceleration * Time.deltaTime
                );
                currentReverseSpeed = 0f;
            }
            else
            {
                currentReverseSpeed = Mathf.MoveTowards(
                    currentReverseSpeed,
                    backwardMaxMoveSpeed,
                    backwardAcceleration * Time.deltaTime
                );
                currentMoveSpeed = 0f;
            }
        }
        else
        {
            currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, 0f, deceleration * Time.deltaTime);
            currentReverseSpeed = Mathf.MoveTowards(currentReverseSpeed, 0f, backwardDeceleration * Time.deltaTime);
        }

        Vector3 targetVelocity;
        if (currentMoveSpeed > 0.001f) targetVelocity = forwardFlat * currentMoveSpeed;
        else if (currentReverseSpeed > 0.001f) targetVelocity = -forwardFlat * currentReverseSpeed;
        else targetVelocity = Vector3.zero;

        Vector3 velocityChange = targetVelocity - planarVel;
        RB.AddForce(velocityChange, ForceMode.VelocityChange);
    }

    private void ApplyCustomGravity()
    {
        // WallRunAbility will override vertical when wall-running
        if (wallRun != null && wallRun.IsWallRunning)
            return;

        RB.AddForce(Physics.gravity * fallMultiplier, ForceMode.Acceleration);

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

    private void AlignModelToGroundAndTilt_GroundOnly()
    {
        if (cartModel == null || rayOrigin == null) return;

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

            cartModel.rotation = Quaternion.Euler(
                cartModel.rotation.eulerAngles.x,
                RB.rotation.eulerAngles.y,
                cartModel.rotation.eulerAngles.z
            );
        }

        cartModel.localRotation *= Quaternion.Lerp(Quaternion.identity, moveTilt, Time.deltaTime * tiltSpeed);
    }

    // Upgrade helpers 
    public void addMaxSpeed(float amount) => maxMoveSpeed += amount;
    public void addAcceleration(float amount) => acceleration += amount;
    public void addJumpForce(float amount) => jumpForce += amount;

    public void BlockForwardMovement() => currentMoveSpeed = 0f;
    public void BlockBackwardMovement() => currentReverseSpeed = 0f;
}
