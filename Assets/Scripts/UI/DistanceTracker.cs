using UnityEngine;
using TMPro;

public class DistanceTracker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private TextMeshProUGUI distanceText;

    [Header("Settings")]
    [SerializeField] private float minimumMovement = 0.001f;

    private Vector3 lastPosition;
    private float totalDistance;
    private bool isTracking;

    private void Start()
    {
        SystemLoader.CallOnComplete(Initialize);
    }

    private void Initialize()
    {
        if (player == null)
        {
            Debug.LogError("DistanceTracker: Player reference is missing.");
            return;
        }

        ResetTracker();
        isTracking = true;
    }

    private void Update()
    {
        if (!isTracking || player == null)
            return;

        Vector3 currentPosition = player.position;
        Vector3 movement = currentPosition - lastPosition;

        // Only count movement toward world +Z.
        float forwardDistance = movement.z;

        if (forwardDistance >= minimumMovement)
        {
            totalDistance += forwardDistance;
        }

        lastPosition = currentPosition;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (distanceText != null)
        {
            distanceText.SetText("{0:0.0} m", totalDistance);
        }
    }

    public void StopTracking()
    {
        isTracking = false;
    }

    public void StartTracking()
    {
        if (player == null)
            return;

        lastPosition = player.position;
        isTracking = true;
    }

    public void ResetTracker()
    {
        totalDistance = 0f;

        if (player != null)
        {
            lastPosition = player.position;
        }

        UpdateDisplay();
    }

    public float GetDistance()
    {
        return totalDistance;
    }
}