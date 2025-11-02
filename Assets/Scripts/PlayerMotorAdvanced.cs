using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotorAdvanced : MonoBehaviour
{
    [Header("Camera Rig (Cinemachine)")]
    [SerializeField] private Transform cameraYawPivot;    
    [SerializeField] private Transform cameraPitchPivot;  
    [SerializeField] private Camera playerCamera;         

    [Header("Look")]
    [SerializeField] private float lookSensitivity = 120f;
    [SerializeField] private float minPitch = -75f;
    [SerializeField] private float maxPitch = 75f;
    [SerializeField] private float maxYawOffset = 90f;            
    [SerializeField] private KeyCode rearviewKey = KeyCode.LeftAlt;

    [Header("Ground Move")]
    [SerializeField] private float maxSpeed = 8f;
    [SerializeField] private float acceleration = 30f;
    [SerializeField] private float groundFriction = 12f;

    [Header("Air Move")]
    [SerializeField] private float airMaxSpeed = 12f;
    [SerializeField] private float airAcceleration = 9f;
    [SerializeField] private float airFriction = 0.35f;

    [Header("Jumping")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBuffer = 0.12f;

    [Header("Wall Jump")]
    [SerializeField] private LayerMask wallLayers;
    [SerializeField] private float wallCheckRadius = 0.45f;
    [SerializeField] private float wallCheckDistance = 0.6f;
    [SerializeField] private float wallJumpUpImpulse = 6.5f;
    [SerializeField] private float wallJumpAwayImpulse = 7.5f;
    [SerializeField] private float wallStickMaxSpeed = 3.5f;
    [SerializeField] private float wallJumpCooldown = 0.15f;

    [Header("Wall Run")]
    [SerializeField] private bool wallRunEnabled = true;
    [SerializeField] private float wallRunSpeed = 10f;
    [SerializeField] private float wallRunDuration = 1.4f;        
    [SerializeField] private float wallRunGravityScale = 0.2f;    
    [SerializeField] private float wallRunMinHeight = 1.1f;       
    [SerializeField] private float wallRunMinForwardDot = 0.2f;   
    [SerializeField] private float wallRunCooldown = 0.35f;       
    [SerializeField] private float wallRunStick = 2.0f;           
    [SerializeField] private float cameraTiltOnRun = 8f;          
    [SerializeField] private float cameraTiltLerp = 10f;

    [Header("Gravity / Slope")]
    [SerializeField] private float gravity = -24f;
    [SerializeField] private float terminalVelocity = -60f;
    [SerializeField] private float slopeStickForce = 8f;

    [Header("Misc")]
    [SerializeField] private bool faceMoveDirection = true;
    [SerializeField] private float controllerSkinExtra = 0.05f;

    public float CurrentSpeed { get; private set; }

    // Internals
    private CharacterController controller;
    private PlayerControls controls;
    private Vector2 moveInput, lookInput;
    private Vector3 velocity;
    private float yawOffset, pitch;
    private float lastGroundedTime, lastJumpPressedTime, wallJumpLockUntil;
    private bool usesCinemachine;

    // Wall run state
    private bool isWallRunning;
    private float wallRunEndTime;
    private float nextWallRunReadyTime;
    private Vector3 wallRunNormal;
    private Vector3 wallRunTangent;
    private float cameraRoll; // cosmetic

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (!playerCamera) playerCamera = Camera.main;
        usesCinemachine = playerCamera && playerCamera.GetComponent<CinemachineBrain>() != null;

        controls = new PlayerControls();
        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += ctx => moveInput = Vector2.zero;
        controls.Player.Jump.performed += ctx => lastJumpPressedTime = Time.time;
        controls.Camera.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        controls.Camera.Look.canceled += ctx => lookInput = Vector2.zero;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnEnable() { controls.Enable(); }
    void OnDisable() { controls.Disable(); }

    void Update()
    {
        // --- LOOK (yaw/pitch pivots) ---
        Vector2 mouse = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        Vector2 delta = (lookInput + mouse) * (lookSensitivity * Time.deltaTime);
        yawOffset = Mathf.Clamp(yawOffset + delta.x, -maxYawOffset, maxYawOffset);
        pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);

        bool rearview = Input.GetKey(rearviewKey);
        float yawWithRear = yawOffset + (rearview ? 180f : 0f);
        if (cameraYawPivot) cameraYawPivot.localRotation = Quaternion.Euler(0f, yawWithRear, 0f);
        if (cameraPitchPivot) cameraPitchPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        if (usesCinemachine && playerCamera && cameraPitchPivot)
            playerCamera.transform.rotation = cameraPitchPivot.rotation;

        // --- Wish direction (camera-yaw-relative) ---
        Vector3 moveForward = cameraYawPivot ? cameraYawPivot.forward : transform.forward;
        Vector3 moveRight = Vector3.Cross(Vector3.up, moveForward).normalized;
        Vector3 wishDir = (moveForward * moveInput.y + moveRight * moveInput.x);
        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

        // --- Grounding ---
        bool grounded = controller.isGrounded;
        if (grounded) lastGroundedTime = Time.time;

        // Split velocity
        Vector3 horizVel = new Vector3(velocity.x, 0f, velocity.z);

        // ===== WALL RUN LOGIC =====
        bool wantsJump = (Time.time - lastJumpPressedTime) <= jumpBuffer;

        // Try start wall run
        if (wallRunEnabled && !grounded && !isWallRunning && Time.time >= nextWallRunReadyTime && Time.time >= wallJumpLockUntil)
        {
            // Must be near a viable wall and at least some input along wall
            if (TryGetWall(out Vector3 n))
            {
                // Tangent along wall surface (choose side that aligns with wishDir)
                Vector3 t = Vector3.Cross(Vector3.up, n).normalized;
                if (Vector3.Dot(t, wishDir) < 0f) t = -t;

                // Require some forward intent along tangent and a little height from ground
                bool hasIntent = Vector3.Dot(t, wishDir) > wallRunMinForwardDot || Vector3.Dot(t, horizVel.normalized) > wallRunMinForwardDot;
                if (hasIntent && HighEnoughForWallRun())
                {
                    isWallRunning = true;
                    wallRunNormal = n;
                    wallRunTangent = t;
                    wallRunEndTime = Time.time + wallRunDuration;
                }
            }
        }

        // While wall running
        if (isWallRunning)
        {
            // Cancel conditions
            bool timeUp = Time.time >= wallRunEndTime;
            bool lostWall = !TryGetWall(out Vector3 n2) || Vector3.Dot(n2, wallRunNormal) < 0.7f; // deviated
            if (grounded || timeUp || lostWall)
            {
                ExitWallRun();
            }
            else
            {
                // Move along tangent at target speed, lightly stick to wall
                float target = wallRunSpeed;
                Vector3 tangential = Vector3.Project(horizVel, wallRunTangent);
                float add = target - tangential.magnitude;
                if (add > 0f)
                    tangential += wallRunTangent * Mathf.Min(acceleration * Time.deltaTime, add);

                // small inward push to cling
                Vector3 stick = -wallRunNormal * wallRunStick;

                horizVel = tangential + stick * Time.deltaTime;

                // Damp gravity while running
                float g = gravity * wallRunGravityScale;
                velocity.y = Mathf.Max(terminalVelocity, velocity.y + g * Time.deltaTime);

                // Camera cosmetic tilt
                float side = Mathf.Sign(Vector3.Dot(wallRunTangent, moveRight));
                float targetRoll = cameraTiltOnRun * side;
                cameraRoll = Mathf.Lerp(cameraRoll, targetRoll, cameraTiltLerp * Time.deltaTime);
                if (usesCinemachine && playerCamera)
                    playerCamera.transform.rotation = Quaternion.AngleAxis(cameraRoll, cameraPitchPivot ? cameraPitchPivot.forward : transform.forward)
                                                     * (cameraPitchPivot ? cameraPitchPivot.rotation : playerCamera.transform.rotation);
            }
        }

        // ===== NORMAL MOVE / JUMP / GRAVITY (when NOT wall running) =====
        if (!isWallRunning)
        {
            float currMax = grounded ? maxSpeed : airMaxSpeed;
            float accel = grounded ? acceleration : airAcceleration;
            float friction = grounded ? groundFriction : airFriction;

            if (wishDir.sqrMagnitude < 0.0001f)
                horizVel *= Mathf.Clamp01(1f - friction * Time.deltaTime);

            if (wishDir.sqrMagnitude > 0f)
            {
                float proj = Vector3.Dot(horizVel, wishDir);
                float add = currMax - proj;
                if (add > 0f)
                {
                    float step = Mathf.Min(accel * Time.deltaTime, add);
                    horizVel += wishDir * step;
                }
            }

            // Gravity & coyote jump
            if (grounded && velocity.y < 0f) velocity.y = -slopeStickForce;
            else velocity.y = Mathf.Max(terminalVelocity, velocity.y + gravity * Time.deltaTime);
        }

        // ===== JUMPING =====
        bool canCoyote = (Time.time - lastGroundedTime) <= coyoteTime;
        if (wantsJump)
        {
            if (isWallRunning)
            {
                // Wall jump cancels wall run
                ExitWallRun();
                velocity.y = 0f;
                velocity += wallRunNormal.normalized * wallJumpAwayImpulse + Vector3.up * wallJumpUpImpulse;
                wallJumpLockUntil = Time.time + wallJumpCooldown;
                lastJumpPressedTime = -999f;
            }
            else if (grounded || canCoyote)
            {
                velocity.y = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
                lastJumpPressedTime = -999f;
            }
            // else airborne normal jump is ignored (
        }

        // Reassemble and move
        velocity = new Vector3(horizVel.x, velocity.y, horizVel.z);
        Vector3 deltaMove = velocity * Time.deltaTime;
        controller.Move(deltaMove + Vector3.down * controllerSkinExtra * Time.deltaTime);

        // Reset cosmetic tilt when not wallrunning
        if (!isWallRunning && Mathf.Abs(cameraRoll) > 0.01f && usesCinemachine && playerCamera)
        {
            cameraRoll = Mathf.Lerp(cameraRoll, 0f, cameraTiltLerp * Time.deltaTime);
            playerCamera.transform.rotation = Quaternion.AngleAxis(cameraRoll, cameraPitchPivot ? cameraPitchPivot.forward : transform.forward)
                                             * (cameraPitchPivot ? cameraPitchPivot.rotation : playerCamera.transform.rotation);
        }

        // Optional: rotate player to movement
        if (faceMoveDirection)
        {
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            if (flat.sqrMagnitude > 0.0001f)
            {
                Quaternion to = Quaternion.LookRotation(flat.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, to, 10f * Time.deltaTime);
            }
        }

        CurrentSpeed = new Vector2(velocity.x, velocity.z).magnitude;
    }

    void ExitWallRun()
    {
        isWallRunning = false;
        nextWallRunReadyTime = Time.time + wallRunCooldown;
        cameraRoll = 0f;
    }

    bool HighEnoughForWallRun()
    {
        // quick check: cast down to see if we're high enough
        Vector3 origin = transform.position + (controller ? controller.center : Vector3.up);
        float downDist = (controller ? controller.height * 0.5f : 1f) + wallRunMinHeight;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, downDist, ~0, QueryTriggerInteraction.Ignore))
            return hit.distance > wallRunMinHeight;
        return true;
    }

    bool TryGetWall(out Vector3 wallNormal)
    {
        wallNormal = Vector3.zero;
        Vector3 origin = transform.position + (controller ? controller.center : Vector3.up);
        float radius = controller ? Mathf.Max(controller.radius, wallCheckRadius) : wallCheckRadius;
        float dist = controller ? Mathf.Max(controller.radius + 0.05f, wallCheckDistance) : wallCheckDistance;

        const int rays = 12;
        for (int i = 0; i < rays; i++)
        {
            float ang = (Mathf.PI * 2f / rays) * i;
            Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            if (Physics.SphereCast(origin, radius * 0.5f, dir, out RaycastHit hit, dist, wallLayers, QueryTriggerInteraction.Ignore))
            {
                // near-vertical surface
                if (Vector3.Dot(hit.normal, Vector3.up) < 0.5f)
                {
                    wallNormal = hit.normal;
                    return true;
                }
            }
        }
        return false;
    }
}
