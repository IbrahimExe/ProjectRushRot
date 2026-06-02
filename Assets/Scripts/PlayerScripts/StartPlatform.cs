using UnityEngine;
using LevelGenerator;

public class StartPlatform : MonoBehaviour
{
    [SerializeField] private Vector3 offset;
    private MapGenerator MapGenerator;
    [SerializeField] private GameObject player;
    [SerializeField] private Vector3 playerSpawnOffset;

    private void Start()
    {
        SystemLoader.CallOnComplete(Initialize);
    }

    private void Initialize()
    {
        MapGenerator = FindFirstObjectByType<MapGenerator>();

        if (MapGenerator == null)
        {
            Debug.LogError("StartPlatform: MapGenerator not found in the scene.");
        }

        float groundHeight = MapGenerator.GetHeightAtWorldPosition(transform.position);
        Vector3 newPosition = new Vector3(transform.position.x, groundHeight, transform.position.z) + offset;
        transform.position = newPosition;

        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }

        if (player == null)
        {
            Debug.LogError("StartPlatform: Player not found in the scene.");
        }
        else
        {
            Vector3 playerSpawnPosition = transform.position + playerSpawnOffset;
            player.transform.position = playerSpawnPosition;

            // Force the physics engine to acknowledge the teleport and reset any accumulated falling speeds
            if (player.TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                rb.position = playerSpawnPosition;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

    }
}
