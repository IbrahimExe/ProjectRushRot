using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float accelerationForce = 30f;    // Force for accelerating forward
    public float maxSpeed = 20f;             // Speed cap (m/s)
    public float turnTorque = 300f;          // Torque for steering
    public float brakeForce = 50f;           // Force for braking
    [Range(0f, 1f)]
    public float driftFactor = 0.8f;         // 0 = full slide, 1 = no slide (high grip)
    public float jumpForce = 5f;             // Impulse force for jump
    public float coyoteTime = 0.2f;          // Allow jump shortly after leaving ground

    [Header("Ground Check")]
    public float groundCheckDistance = 0.5f; // Raycast length for ground
    public LayerMask groundLayers;           // Layers considered ground (e.g. "Ground")

    Rigidbody rb;
    PlayerInput playerInput;

    // Input state
    Vector2 moveInput = Vector2.zero; // X = steer, Y = accel/brake
    Vector2 lookInput = Vector2.zero;
    bool jumpPressed = false;
    bool braking = false;

    float lastGroundTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerInput = GetComponent<PlayerInput>();
    }

    void OnEnable()
    {
        if (playerInput != null)
            playerInput.onActionTriggered += OnActionTriggered;
    }

    void OnDisable()
    {
        if (playerInput != null)
            playerInput.onActionTriggered -= OnActionTriggered;
    }

    // This receives all action callbacks when PlayerInput.Behavior = Invoke C# Events
    void OnActionTriggered(InputAction.CallbackContext ctx)
    {
        // Match actions by name — these must match your InputAction asset exactly (case sensitive).
        var name = ctx.action.name;

        if (name == "Move")
        {
            // continuous vector2
            moveInput = ctx.ReadValue<Vector2>();
        }
        else if (name == "Look")
        {
            // camera look vector (mouse delta or right stick)
            lookInput = ctx.ReadValue<Vector2>();
        }
        else if (name == "Jump")
        {
            // typically a button; use performed to mark jump
            if (ctx.performed) jumpPressed = true;
        }
        else if (name == "Brake")
        {
            // could be button or axis; treat as pressed when value > deadzone
            if (ctx.control != null && ctx.action.expectedControlType == "Button")
            {
                braking = ctx.ReadValueAsButton();
            }
            else
            {
                // fallback: read as float and threshold
                float val = ctx.ReadValue<float>();
                braking = val > 0.1f;
            }
        }
    }

    void FixedUpdate()
    {
        // 1. Ground check (raycast down from center)
        bool grounded = Physics.Raycast(transform.position, -transform.up, out RaycastHit hit, groundCheckDistance, groundLayers);
        if (grounded)
        {
            lastGroundTime = Time.time;
            // Optionally align to normal:
            AlignToNormal(hit.normal);
        }

        // 2. Acceleration / Braking
        float accel = moveInput.y;  // Forward/back input from W/S or stick Y
        if (accel > 0f)
        {
            // Apply forward force if under max speed
            if (rb.linearVelocity.magnitude < maxSpeed)
                rb.AddForce(transform.forward * accel * accelerationForce);
        }
        else if (accel < 0f || braking)
        {
            // Brake by counter force
            float brakeAmount = brakeForce;
            // if using accel negative, scale by that
            if (accel < 0f) brakeAmount *= -accel;
            rb.AddForce(-transform.forward * brakeAmount);
        }

        // 3. Steering
        float steer = moveInput.x; // Left/right input from A/D or stick X
        if (Mathf.Abs(rb.linearVelocity.magnitude) > 0.1f)
        {
            // Apply torque around up axis for turning
            float speedFactor = Mathf.Clamp01(rb.linearVelocity.magnitude / Mathf.Max(0.0001f, maxSpeed));
            rb.AddTorque(transform.up * steer * turnTorque * speedFactor);
        }

        // 4. Drift / Lateral Friction
        // Separate velocity into forward vs lateral components
        Vector3 forwardVel = Vector3.Project(rb.linearVelocity, transform.forward);
        Vector3 lateralVel = rb.linearVelocity - forwardVel;
        // Reduce lateral velocity based on driftFactor and turning
        float grip = Mathf.Lerp(1f, driftFactor, rb.linearVelocity.magnitude / Mathf.Max(0.0001f, maxSpeed));
        rb.linearVelocity = forwardVel + lateralVel * grip;

        // 5. Jump
        if (jumpPressed && Time.time - lastGroundTime <= coyoteTime)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            jumpPressed = false;
        }
        else if (jumpPressed)
        {
            // consumed or timed out
            jumpPressed = false;
        }

        // 6. Limit speed (optional caps) — only horizontal speed clamped
        Vector3 horizVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (horizVel.magnitude > maxSpeed)
        {
            Vector3 newVel = horizVel.normalized * maxSpeed;
            rb.linearVelocity = new Vector3(newVel.x, rb.linearVelocity.y, newVel.z);
        }
    }

    // Smoothly align the board's up to the ground normal
    void AlignToNormal(Vector3 normal)
    {
        // Compute rotation needed to align 'up' with the ground normal
        Quaternion toSlope = Quaternion.FromToRotation(transform.up, normal);
        // Apply rotation with slerp for smoothness
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, toSlope * rb.rotation, 0.1f));
    }

    void OnDrawGizmosSelected()
    {
        // Draw 'forward' of the board
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
        Gizmos.DrawSphere(transform.position + transform.forward * 2f, 0.05f);

        // Draw velocity vector (red)
        if (rb != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + rb.linearVelocity.normalized * 2f);
            Gizmos.DrawSphere(transform.position + rb.linearVelocity.normalized * 2f, 0.05f);
        }
    }

    void UpdateDebug()
    {
        if (rb != null)
            Debug.Log($"transform.forward = {transform.forward}, velocity = {rb.linearVelocity}");
    }

}
