using UnityEngine;

//attempt at a physics based player controller

[RequireComponent(typeof(Rigidbody))]
public class GPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float startMoveSpeed = 5f;
    public float maxMoveSpeed = 15f;
    public float acceleration = 20f;
    public float deceleration = 25f;
    public float rotationSpeed = 100f;
    public float linearDrag = 5f;
    public float jumpForce = 5f;

    public float currentMoveSpeed = 1f;

    [Header("Custom Gravity Settings")]
    public float fallMultiplier = 2f;
    public float lowJumpMultiplier = 1.5f;
    public float maxFallSpeed = -20f;

    [Header("Ground Check Settings")]
    public Transform feetTransform;

    [Header("Visual Tilt Settings")]
    public Transform cartModel;
    public Transform rayOrigin;
    public float rayLength = 1.5f;
    public float tiltForwardAmount = 10f;
    public float tiltSideAmount = 10f;
    public float tiltSpeed = 5f;
    public float groundAlignSpeed = 8f;

    private Rigidbody rb;
    private Transform cam;
    private bool isGrounded;
    private Vector3 lastGroundNormal = Vector3.up;
    private float _turnSmoothVelocity;

    void Start()
    {
        Cursor.visible = false; // Hides the cursor
        Cursor.lockState = CursorLockMode.Locked; // Locks it to the center
        rb = GetComponent<Rigidbody>();
        cam = Camera.main.transform;
    }


    void Update()
    {
        isGrounded = CheckGrounded();

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            Jump();
        }
    }

    private void FixedUpdate()
    {
        Move();
        ApplyCustomGravity();
        AlignModelToGroundAndTilt();
    }

    private void Move()
    {
        // Input
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 inputDir = new Vector3(h, 0f, v).normalized;

        bool isMoving = inputDir.magnitude >= 0.1f;

        // Horizontal velocity (ignore vertical)
        Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float currentVelocityMag = horizontalVel.magnitude;

        if (isMoving)
        {
            // Camera-relative movement
            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + cam.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _turnSmoothVelocity, 1f / rotationSpeed);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            // Keep current speed from dropping below the actual physical velocity
            if (currentMoveSpeed < currentVelocityMag)
                currentMoveSpeed = currentVelocityMag;

            // Set initial move speed if starting from rest
            if (currentMoveSpeed < startMoveSpeed)
                currentMoveSpeed = startMoveSpeed;
            

            // Gradually accelerate toward max speed
            currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, maxMoveSpeed, acceleration * Time.deltaTime);

            // Apply movement
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            Vector3 targetVelocity = moveDir.normalized * currentMoveSpeed;
            Vector3 velocityChange = targetVelocity - horizontalVel;

            rb.AddForce(velocityChange, ForceMode.VelocityChange);
        }
        else
        {
            // Gradually decelerate when no input
            currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, 0f, deceleration * Time.deltaTime);
        }
    }

    private void Jump()
    {
        // Reset Y velocity before jump for consistent height
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    private bool CheckGrounded()
    {
        float rayLength = 1.0f; // Adjust to match character height
        RaycastHit hit;

        // Cast a ray straight down from player position
        if (Physics.Raycast(feetTransform.transform.position, Vector3.down, out hit, rayLength))
        {
            Debug.DrawLine(transform.position, hit.point, Color.green);

            rb.linearDamping = linearDrag; // Apply drag when grounded
            return true;
        }

        // No ground hit
        rb.linearDamping = 0f; // No drag when in air
        return false;
    }

    private void ApplyCustomGravity()
    {
        // Only modify gravity when falling or going up
        if (rb.linearVelocity.y < 0)
        {
            // Falling — add extra gravity
            rb.AddForce(Physics.gravity * (fallMultiplier - 1f), ForceMode.Acceleration);
        }
        else if (rb.linearVelocity.y > 0 && !Input.GetButton("Jump"))
        {
            // If jump released early, apply low jump gravity for shorter hops
            rb.AddForce(Physics.gravity * (lowJumpMultiplier - 1f), ForceMode.Acceleration);
        }

        // Clamp fall speed
        if (rb.linearVelocity.y < maxFallSpeed)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, maxFallSpeed, rb.linearVelocity.z);
        }
    }

    private void AlignModelToGroundAndTilt()
    {
        //  Get movement input
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        bool isMoving = Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f;

        //  Calculate tilt rotation
        float targetForwardTilt = isMoving ? -v * tiltForwardAmount : 0f;
        float targetSideTilt = isMoving ? -h * tiltSideAmount : 0f;
        Quaternion moveTilt = Quaternion.Euler(targetForwardTilt, 0f, targetSideTilt);

        //  Raycast to detect ground
        if (Physics.Raycast(rayOrigin.position, Vector3.down, out RaycastHit hit, rayLength))
        {
            lastGroundNormal = hit.normal;

            // Calculate rotation that makes model's up match ground normal
            Quaternion groundTilt = Quaternion.FromToRotation(cartModel.up,lastGroundNormal) * cartModel.rotation;

            // Smoothly interpolate to match the ground
            cartModel.rotation = Quaternion.Slerp(cartModel.rotation, groundTilt, Time.deltaTime * groundAlignSpeed);
        }
        else
        {
            // No ground detected — return to upright
            cartModel.rotation = Quaternion.Slerp(cartModel.rotation, rb.rotation, Time.deltaTime * 2f);
        }

        //  Apply visual tilt relative to ground alignment
        cartModel.localRotation *= Quaternion.Lerp(Quaternion.identity, moveTilt, Time.deltaTime * tiltSpeed);
    }

}
