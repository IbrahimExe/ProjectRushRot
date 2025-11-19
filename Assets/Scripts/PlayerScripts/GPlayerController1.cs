using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class GPlayerController1 : MonoBehaviour
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

    void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
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
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // Use ground normal for proper slope handling
        Vector3 up = (lastGroundNormal == Vector3.zero) ? Vector3.up : lastGroundNormal;

        // Steer around ground up (decoupled from camera)
        float yawDelta = h * rotationSpeed * Time.fixedDeltaTime;
        transform.rotation = Quaternion.AngleAxis(yawDelta, up) * transform.rotation;

        // Planar basis relative to ground
        Vector3 forwardFlat = Vector3.ProjectOnPlane(transform.forward, up).normalized;
        Vector3 planarVel = Vector3.ProjectOnPlane(rb.linearVelocity, up);
        float planarSpeed = planarVel.magnitude;

        // Maintain speed floor at actual motion (preserves momentum)
        if (currentMoveSpeed < planarSpeed)
            currentMoveSpeed = planarSpeed;

        bool hasThrottle = Mathf.Abs(v) > 0.01f;

        if (hasThrottle)
        {
            // Kick off from rest first
            if (currentMoveSpeed < startMoveSpeed)
                currentMoveSpeed = startMoveSpeed;

            // Constant acceleration toward max when holding W/S
            // Using Time.deltaTime matches the old behavior
            currentMoveSpeed = Mathf.MoveTowards(
                currentMoveSpeed,
                maxMoveSpeed,
                acceleration * Time.deltaTime
            );
        }
        else
        {
            // Bleed speed with no throttle
            currentMoveSpeed = Mathf.MoveTowards(
                currentMoveSpeed,
                0f,
                deceleration * Time.deltaTime
            );
        }

        // Target planar velocity (forward or reverse)
        Vector3 moveDir = forwardFlat * Mathf.Sign(v); // Reverse if v < 0
        Vector3 targetVelocity = moveDir * currentMoveSpeed;

        // Change ONLY planar velocity (vertical stays fully physics-driven)
        Vector3 velocityChange = targetVelocity - planarVel;
        rb.AddForce(velocityChange, ForceMode.VelocityChange);
    }

    private void Jump()
    {
        // Do NOT zero Y; add impulse on top of current velocity
        // This preserves upward momentum from ramps
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    private bool CheckGrounded()
    {
        float rayLength = 1.0f;
        RaycastHit hit;

        if (Physics.Raycast(feetTransform.position, Vector3.down, out hit, rayLength))
        {
            Debug.DrawLine(feetTransform.position, hit.point, Color.green);
            lastGroundNormal = hit.normal;
            rb.linearDamping = linearDrag;
            return true;
        }

        rb.linearDamping = 0f;
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
            Quaternion groundTilt = Quaternion.FromToRotation(cartModel.up, lastGroundNormal) * cartModel.rotation;

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