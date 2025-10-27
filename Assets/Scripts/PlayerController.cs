using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float accelerationForce = 30f;    // Force for accelerating forward
    public float maxSpeed = 20f;             // Speed cap (m/s)
    public float turnTorque = 300f;          // Torque for steering
    public float brakeForce = 50f;           // Force for braking
    public float driftFactor = 0.8f;         // 0 = full slide, 1 = no slide (high grip)
    public float jumpForce = 5f;             // Impulse force for jump
    public float coyoteTime = 0.2f;          // Allow jump shortly after leaving ground

    [Header("Ground Check")]
    public float groundCheckDistance = 0.5f; // Raycast length for ground
    public LayerMask groundLayers;           // Layers considered ground (e.g. "Ground")

    Rigidbody rb;
    Vector2 moveInput = Vector2.zero;
    bool jumpPressed = false;
    float lastGroundTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Called by Input System
    public void OnMove(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>(); // X = steer, Y = accel/brake
    }
    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (ctx.performed) jumpPressed = true;
    }
    public void OnBrake(InputAction.CallbackContext ctx)
    {
        // e.g. set a flag or use moveInput.y negative
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
        if (accel > 0)
        {
            // Apply forward force
            if (rb.linearVelocity.magnitude < maxSpeed)
                rb.AddForce(transform.forward * accel * accelerationForce);
        }
        else if (accel < 0)
        {
            // Brake by counter force
            rb.AddForce(-transform.forward * (-accel) * brakeForce);
        }

        // 3. Steering
        float steer = moveInput.x; // Left/right input from A/D or stick X
        if (Mathf.Abs(rb.linearVelocity.magnitude) > 0.1f)
        {
            // Apply torque around up axis for turning
            rb.AddTorque(transform.up * steer * turnTorque * rb.linearVelocity.magnitude / maxSpeed);
        }

        // 4. Drift / Lateral Friction
        // Separate velocity into forward vs lateral components
        Vector3 forwardVel = Vector3.Project(rb.linearVelocity, transform.forward);
        Vector3 lateralVel = rb.linearVelocity - forwardVel;
        // Reduce lateral velocity based on driftFactor and turning
        float grip = Mathf.Lerp(1f, driftFactor, rb.linearVelocity.magnitude / maxSpeed);
        rb.linearVelocity = forwardVel + lateralVel * grip;

        // 5. Jump
        if (jumpPressed && Time.time - lastGroundTime <= coyoteTime)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            jumpPressed = false;
        }
        else if (jumpPressed)
        {
            jumpPressed = false;
        }

        // 6. Limit speed (optional caps)
        Vector3 horizVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        if (horizVel.magnitude > maxSpeed)
        {
            rb.linearVelocity = horizVel.normalized * maxSpeed + Vector3.up * rb.linearVelocity.y;
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
}