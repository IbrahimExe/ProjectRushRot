using UnityEngine;
using Unity.Cinemachine;

public class CameraZoom : MonoBehaviour
{
    public CinemachineCamera vcam;

    [Header("Speed Sources (pick one)")]
    public Rigidbody playerRb;                     
    public PlayerMotorAdvanced playerMotor;        

    [Header("FOV vs Speed")]
    public float baseFOV = 60f, maxFOV = 80f, speedForMax = 20f;

    [Header("Fallback (delta position)")]
    public Transform target;                       
    private Vector3 lastPos;
    private bool hasLast;

    void Update()
    {
        float speed = 0f;

        if (playerMotor != null)
        {
            speed = playerMotor.CurrentSpeed;      // Works with CharacterController player
        }
        else if (playerRb != null)
        {
            speed = playerRb.linearVelocity.magnitude; // Works with Rigidbody player
        }
        else if (target != null)
        {
            Vector3 p = target.position;
            if (hasLast) speed = (p - lastPos).magnitude / Mathf.Max(Time.deltaTime, 1e-6f);
            lastPos = p; hasLast = true;
        }

        float t = Mathf.Clamp01(speed / Mathf.Max(0.001f, speedForMax));
        vcam.Lens.FieldOfView = Mathf.Lerp(baseFOV, maxFOV, t);
    }
}
