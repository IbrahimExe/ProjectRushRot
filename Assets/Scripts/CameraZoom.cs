using UnityEngine;
using Unity.Cinemachine;

public class CameraZoom : MonoBehaviour
{
    public CinemachineCamera vcam;
    public Rigidbody playerRb;

    public float baseFOV = 60f, maxFOV = 80f, speedForMax = 20f;
    void Update()
    {
        float speed = playerRb.linearVelocity.magnitude;
        // Lerp FOV between base and max
        vcam.Lens.FieldOfView = Mathf.Lerp(baseFOV, maxFOV, speed / speedForMax);
    }
}
