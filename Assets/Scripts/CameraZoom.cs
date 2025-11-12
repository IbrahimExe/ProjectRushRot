using UnityEngine;
using Unity.Cinemachine;

public class CameraZoom : MonoBehaviour
{
    public CinemachineCamera vcam;
    public Rigidbody playerRb;
    public float baseFOV = 60f, maxFOV = 80f, speedForMax = 20f;

    [Header("Look Back Settings")]
    public KeyCode lookBackKey = KeyCode.N;
    public float rotationSpeed = 5f;

    private CinemachineOrbitalFollow orbitalFollow;
    private float originalRangeMin;
    private float originalRangeMax;
    private float lookBackTarget = 0f;

    void Start()
    {
        orbitalFollow = vcam.GetComponent<CinemachineOrbitalFollow>();
        // Store the original axis range limits
        originalRangeMin = orbitalFollow.HorizontalAxis.Range.x;
        originalRangeMax = orbitalFollow.HorizontalAxis.Range.y;
    }

    void LateUpdate()
    {
        // Speed-based FOV
        float speed = playerRb.linearVelocity.magnitude;
        vcam.Lens.FieldOfView = Mathf.Lerp(baseFOV, maxFOV, speed / speedForMax);

        // Look back control
        if (Input.GetKey(lookBackKey))
        {
            // Expand the range to allow 180 degree rotation
            orbitalFollow.HorizontalAxis.Range = new Vector2(-180f, 180f);

            // Target is 180 degrees (straight back)
            lookBackTarget = Mathf.MoveTowards(lookBackTarget, 180f, rotationSpeed * 100f * Time.deltaTime);
            orbitalFollow.HorizontalAxis.Value = lookBackTarget;
        }
        else
        {
            // Return to center (0)
            lookBackTarget = Mathf.MoveTowards(lookBackTarget, 0f, rotationSpeed * 100f * Time.deltaTime);

            // Only override if we're still returning
            if (Mathf.Abs(lookBackTarget) > 0.1f)
            {
                orbitalFollow.HorizontalAxis.Value = lookBackTarget;
            }
            else
            {
                // Restore original range when back to normal
                orbitalFollow.HorizontalAxis.Range = new Vector2(originalRangeMin, originalRangeMax);
            }
        }
    }
}