using UnityEngine;
using Unity.Cinemachine;

public class CameraZoom : MonoBehaviour
{
    public CinemachineCamera vcam;
    public Rigidbody playerRb;

    public float baseFOV = 60f, maxFOV = 80f, speedForMax = 20f;

    [Header("Look Back Settings")]
    public KeyCode lookBackKey = KeyCode.N;
    public float rotationSpeed = 10f;

    private CinemachineOrbitalFollow orbitalFollow;
    private float targetYaw = 0f;
    private float currentYaw = 0f;

    void Start()
    {
        orbitalFollow = vcam.GetComponent<CinemachineOrbitalFollow>();
    }

    void Update()
    {
        // Speed-based FOV
        float speed = playerRb.linearVelocity.magnitude;
        // Lerp FOV between base and max
        vcam.Lens.FieldOfView = Mathf.Lerp(baseFOV, maxFOV, speed / speedForMax);

        // Look back control
        if (Input.GetKey(lookBackKey))
        {
            targetYaw = 180f;
        }
        else
        {
            //reset the horisontal axis to 0,0
            orbitalFollow.HorizontalAxis.Value = 0f;

        }

        // Smooth orbit rotation
        currentYaw = Mathf.Lerp(currentYaw, targetYaw, Time.deltaTime * rotationSpeed);
        orbitalFollow.HorizontalAxis.Value = currentYaw;
    }
}