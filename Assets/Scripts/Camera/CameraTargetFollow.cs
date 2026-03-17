using UnityEngine;

[DefaultExecutionOrder(-200)] 
public class CameraTargetFollow : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The player's root Transform.")]
    public Transform playerTransform;
    public Vector3 positionOffset = Vector3.zero;

    [Header("Rotation Smoothing")]
    [Tooltip(
        "How fast this target's yaw catches up to the player.\n" +
        "Low = camera arcs around player.\n" +
        "High = constraint fights fast turns.\n")]
    public float rotationSmoothSpeed = 25f;

    private void Start()
    {
        if (playerTransform == null)
        {
            Debug.LogError("CameraTargetFollow: Player Transform reference is not set.");
            return;
        }

        transform.position = playerTransform.position + positionOffset;
        transform.rotation = Quaternion.Euler(0f, playerTransform.eulerAngles.y, 0f);
    }

    void LateUpdate()
    {
        if (playerTransform == null) return;

        transform.position = playerTransform.position + positionOffset;

        float targetYaw  = playerTransform.eulerAngles.y;
        float currentYaw = transform.eulerAngles.y;
        float smoothYaw  = Mathf.LerpAngle(currentYaw, targetYaw, Time.deltaTime * rotationSmoothSpeed);

        transform.rotation = Quaternion.Euler(0f, smoothYaw, 0f);
    }
}
