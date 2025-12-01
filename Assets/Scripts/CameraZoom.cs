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
    public float lookBackFOV = 60f;

    private CinemachineOrbitalFollow orbitalFollow;
    private float originalRangeMin;
    private float originalRangeMax;
    private float lookBackTarget = 0f;
    private bool isLookingBack = false;

    void Start()
    {
        orbitalFollow = vcam.GetComponent<CinemachineOrbitalFollow>();
        // Store the original axis range limits
        originalRangeMin = orbitalFollow.HorizontalAxis.Range.x;
        originalRangeMax = orbitalFollow.HorizontalAxis.Range.y;
    }

    void LateUpdate()
    {

        // Look back control
        if (Input.GetKey(lookBackKey))
        {
            isLookingBack = true;
            // Expand the range to allow 180 degree rotation
            orbitalFollow.HorizontalAxis.Range = new Vector2(-180f, 180f);

            // Target is 180 degrees (straight back)
            lookBackTarget = Mathf.MoveTowards(lookBackTarget, 180f, rotationSpeed * 100f * Time.deltaTime);
            orbitalFollow.HorizontalAxis.Value = lookBackTarget;

            // Use fixed FOV when looking back
            vcam.Lens.FieldOfView = lookBackFOV;
        }
        else
        {
            // Return to center (0)
            lookBackTarget = Mathf.MoveTowards(lookBackTarget, 0f, rotationSpeed * 100f * Time.deltaTime);

            // Only override if we're still returning
            if (Mathf.Abs(lookBackTarget) > 0.1f)
            {
                orbitalFollow.HorizontalAxis.Value = lookBackTarget;
                // Keep fixed FOV while transitioning back
                vcam.Lens.FieldOfView = lookBackFOV;
            }
            else
            {
                isLookingBack = false;
                // Restore original range when back to normal
                orbitalFollow.HorizontalAxis.Range = new Vector2(originalRangeMin, originalRangeMax);
            }

            // Speed-based FOV
            if (!isLookingBack)
            {

                //float speed = playerRb.linearVelocity.magnitude;

                //horizontal only speed
                float speed = new Vector3(playerRb.linearVelocity.x, 0f, playerRb.linearVelocity.z).magnitude;


                vcam.Lens.FieldOfView = Mathf.Lerp(baseFOV, maxFOV, speed / speedForMax);
            }
        }
    }
}