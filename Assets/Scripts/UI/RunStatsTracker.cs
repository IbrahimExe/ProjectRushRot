using UnityEngine;

[DisallowMultipleComponent]
public class RunStatsTracker : MonoBehaviour
{
    public static RunStatsTracker Instance { get; private set; }

    [Header("Distance")]
    [SerializeField] private Transform player;
    [SerializeField] private bool countHorizontalDistanceOnly = true;

    private Vector3 lastPosition;
    private bool hasStartingPosition;

    public float DistanceTravelled { get; private set; }
    public int DestroyedCount { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (player == null)
        {
            PlayerControllerBase controller =
                FindFirstObjectByType<PlayerControllerBase>();

            if (controller != null)
                player = controller.transform;
        }

        ResetRun();
    }

    private void Update()
    {
        if (player == null)
            return;

        Vector3 currentPosition = player.position;

        if (!hasStartingPosition)
        {
            lastPosition = currentPosition;
            hasStartingPosition = true;
            return;
        }

        Vector3 movement = currentPosition - lastPosition;

        if (countHorizontalDistanceOnly)
            movement.y = 0f;

        DistanceTravelled += movement.magnitude;
        lastPosition = currentPosition;
    }

    public void RegisterDestruction()
    {
        DestroyedCount++;
    }

    public void RegisterDestructions(int amount)
    {
        if (amount <= 0)
            return;

        DestroyedCount += amount;
    }

    public void ResetRun()
    {
        DistanceTravelled = 0f;
        DestroyedCount = 0;

        if (player != null)
        {
            lastPosition = player.position;
            hasStartingPosition = true;
        }
        else
        {
            hasStartingPosition = false;
        }
    }
}