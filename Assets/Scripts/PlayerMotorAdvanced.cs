using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine; // Unity 6 / Cinemachine 3

[RequireComponent(typeof(CharacterController))]
public class PlayerMotorAdvanced : MonoBehaviour
{
    [Header("Camera Rig (Cinemachine)")]
    [SerializeField] private Transform cameraYawPivot;    
    [SerializeField] private Transform cameraPitchPivot;  
    [SerializeField] private Camera playerCamera;         // Main Camera (with CinemachineBrain)

    [Header("Look")]
    [SerializeField] private float lookSensitivity = 120f;
    [SerializeField] private float minPitch = -75f;
    [SerializeField] private float maxPitch = 75f;
    [SerializeField] private float maxYawOffset = 90f;    // ±90°
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
    private float yawOffset;   // relative to player forward (clamped)
    private float pitch;
    private float lastGroundedTime, lastJumpPressedTime, wallJumpLockUntil;
    private bool usesCinemachine;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (!playerCamera) playerCamera = Camera.main;
        usesCinemachine = playerCamera && playerCamera.GetComponent<CinemachineBrain>() != null;

        // Input
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
        // ----- LOOK: update yaw/pitch pivots -----
        Vector2 mouse = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        Vector2 delta = (lookInput + mouse) * (lookSensitivity * Time.deltaTime);

        yawOffset = Mathf.Clamp(yawOffset + delta.x, -maxYawOffset, maxYawOffset);
        pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);

        bool rearview = Input.GetKey(rearviewKey);
        float yawWithRear = yawOffset + (rearview ? 180f : 0f);

        if (cameraYawPivot) cameraYawPivot.localRotation = Quaternion.Euler(0f, yawWithRear, 0f);
        if (cameraPitchPivot) cameraPitchPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // If Cinemachine Brain is active, set Main Camera's rotation to our pitch pivot
        if (usesCinemachine && playerCamera && cameraPitchPivot)
            playerCamera.transform.rotation = cameraPitchPivot.rotation;

        // ----- MOVE: camera-yaw-relative WASD -----
        Vector3 moveForward = cameraYawPivot ? cameraYawPivot.forward : transform.forward;
        Vector3 moveRight = Vector3.Cross(Vector3.up, moveForward).normalized;

        Vector3 wishDir = (moveForward * moveInput.y + moveRight * moveInput.x);
        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

        bool grounded = controller.isGrounded;
        if (grounded) lastGroundedTime = Time.time;

        Vector3 horizVel = new Vector3(velocity.x, 0f, velocity.z);

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

        velocity = horizVel + Vector3.up * velocity.y;

        // Gravity + jump (with coyote and buffer)
        bool wantsJump = (Time.time - lastJumpPressedTime) <= jumpBuffer;
        if (grounded && velocity.y < 0f) velocity.y = -slopeStickForce;
        else velocity.y = Mathf.Max(terminalVelocity, velocity.y + gravity * Time.deltaTime);

        bool canCoyote = (Time.time - lastGroundedTime) <= coyoteTime;
        if (wantsJump && (grounded || canCoyote))
        {
            velocity.y = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
            lastJumpPressedTime = -999f;
        }

        // Wall jump
        if (!grounded && Time.time >= wallJumpLockUntil && wantsJump && TryGetWall(out Vector3 wallNormal))
        {
            Vector3 flat = new Vector3(horizVel.x, 0f, horizVel.z);
            float towardWall = flat.sqrMagnitude > 0.0001f ? Vector3.Dot(flat.normalized, -wallNormal) : 0f;
            if (towardWall > 0.15f || flat.magnitude < wallStickMaxSpeed)
            {
                velocity.y = 0f;
                velocity += wallNormal.normalized * wallJumpAwayImpulse + Vector3.up * wallJumpUpImpulse;
                wallJumpLockUntil = Time.time + wallJumpCooldown;
                lastJumpPressedTime = -999f;
            }
        }

        // Move + optional face-turn
        Vector3 deltaMove = velocity * Time.deltaTime;
        controller.Move(deltaMove + Vector3.down * controllerSkinExtra * Time.deltaTime);

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

    bool TryGetWall(out Vector3 wallNormal)
    {
        wallNormal = Vector3.zero;
        Vector3 origin = transform.position + (controller ? controller.center : Vector3.up);
        float radius = controller ? Mathf.Max(controller.radius, wallCheckRadius) : wallCheckRadius;
        float dist = controller ? Mathf.Max(controller.radius + 0.05f, wallCheckDistance) : wallCheckDistance;

        const int rays = 10;
        for (int i = 0; i < rays; i++)
        {
            float ang = (Mathf.PI * 2f / rays) * i;
            Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            if (Physics.SphereCast(origin, radius * 0.5f, dir, out RaycastHit hit, dist, wallLayers, QueryTriggerInteraction.Ignore))
            {
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
