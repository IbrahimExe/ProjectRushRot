using UnityEngine;
using Unity.Cinemachine;

public class CameraZoom : MonoBehaviour
{
    [Header("References")]
    public CinemachineCamera vcam;
    public Rigidbody playerRb;
    public DashAbility dashAbility; // Reference to detect dashing

    [Header("FOV Settings")]
    public float baseFOV = 60f;
    public float maxFOV = 100f;
    public float speedForMax = 60f;

    [Header("Smoothing")]
    [Tooltip("How quickly FOV changes. Lower = smoother but slower response.")]
    public float fovSmoothTime = 0.15f;

    [Tooltip("How quickly speed is smoothed before calculating FOV. Lower = less jitter.")]
    public float speedSmoothTime = 0.2f;

    [Tooltip("Faster FOV response during dash for more punch.")]
    public float dashFovSmoothTime = 0.08f;

    [Header("Look Back Settings")]
    public KeyCode lookBackKey = KeyCode.F;
    public float rotationSpeed = 5f;
    public float lookBackFOV = 60f;

    // Smoothing variables
    private float currentFOV;
    private float fovVelocity;
    private float smoothedSpeed;
    private float speedVelocity;

    private CinemachineOrbitalFollow orbitalFollow;
    private float originalRangeMin;
    private float originalRangeMax;
    private float lookBackTarget = 0f;
    private bool isLookingBack = false;

    void Start()
    {
        orbitalFollow = vcam.GetComponent<CinemachineOrbitalFollow>();
        originalRangeMin = orbitalFollow.HorizontalAxis.Range.x;
        originalRangeMax = orbitalFollow.HorizontalAxis.Range.y;

        // Initialize smoothed values
        currentFOV = baseFOV;
        vcam.Lens.FieldOfView = currentFOV;
    }

    void LateUpdate()
    {
        HandleLookBack();

        if (!isLookingBack)
        {
            HandleSpeedBasedFOV();
        }
    }

    void HandleLookBack()
    {
        if (Input.GetKey(lookBackKey))
        {
            isLookingBack = true;
            orbitalFollow.HorizontalAxis.Range = new Vector2(-180f, 180f);
            lookBackTarget = Mathf.MoveTowards(lookBackTarget, 180f, rotationSpeed * 100f * Time.deltaTime);
            orbitalFollow.HorizontalAxis.Value = lookBackTarget;

            // Smoothly transition to look back FOV
            currentFOV = Mathf.SmoothDamp(currentFOV, lookBackFOV, ref fovVelocity, fovSmoothTime);
            vcam.Lens.FieldOfView = currentFOV;
        }
        else
        {
            lookBackTarget = Mathf.MoveTowards(lookBackTarget, 0f, rotationSpeed * 100f * Time.deltaTime);

            if (Mathf.Abs(lookBackTarget) > 0.1f)
            {
                orbitalFollow.HorizontalAxis.Value = lookBackTarget;

                // Keep transitioning FOV smoothly
                currentFOV = Mathf.SmoothDamp(currentFOV, lookBackFOV, ref fovVelocity, fovSmoothTime);
                vcam.Lens.FieldOfView = currentFOV;
            }
            else
            {
                isLookingBack = false;
                orbitalFollow.HorizontalAxis.Range = new Vector2(originalRangeMin, originalRangeMax);
            }
        }
    }

    void HandleSpeedBasedFOV()
    {
        // Calculate current horizontal speed
        float currentSpeed = new Vector3(playerRb.linearVelocity.x, 0f, playerRb.linearVelocity.z).magnitude;

        // Smooth the speed reading to prevent sudden jumps
        smoothedSpeed = Mathf.SmoothDamp(smoothedSpeed, currentSpeed, ref speedVelocity, speedSmoothTime);

        // Calculate target FOV based on smoothed speed
        float targetFOV = Mathf.Lerp(baseFOV, maxFOV, smoothedSpeed / speedForMax);

        // Use faster smoothing during dash for more responsive feel
        float activeSmoothTime = (dashAbility != null && (dashAbility.IsDashing || dashAbility.IsSideDashing))
            ? dashFovSmoothTime
            : fovSmoothTime;

        // Smooth the FOV change
        currentFOV = Mathf.SmoothDamp(currentFOV, targetFOV, ref fovVelocity, activeSmoothTime);

        vcam.Lens.FieldOfView = currentFOV;
    }
}