using UnityEngine;
using LevelGenerator;

public class DeathWallZombie : MonoBehaviour
{
    [SerializeField] private GameObject zombiePrefab;
    [SerializeField] private Animator animator;
    private bool isRunning = false;

    public MapGenerator MapGenerator;

    void Start()
    {
        if (animator != null)
        {
            animator.SetBool("Running", isRunning);
        }
        if (MapGenerator == null)
        {
            MapGenerator = FindFirstObjectByType<MapGenerator>();
            if (MapGenerator == null)
            {
                Debug.LogError("DeathWallZombie: No MapGenerator found in the scene!", this);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (MapGenerator == null)
        {
            return;
        }
        float groundHeight = MapGenerator.GetHeightAtWorldPosition(transform.position);
        transform.position = new Vector3(transform.position.x, groundHeight, transform.position.z);

        if (!isRunning && GameState.IsStarted)
        {
            // Set isRunning immediately so Update doesn't keep queuing Invoke calls every frame
            isRunning = true;
            float randomDelay = Random.Range(0f, 1.5f);
            Invoke(nameof(StartRunning), randomDelay);
        }
    }

    private void StartRunning()
    {
        if (animator != null)
        {
            animator.SetBool("Running", true);
            // Start the animation at a random point in the cycle so the horde looks desynced
            animator.Play(0, -1, Random.Range(0f, 3f));
        }
    }
}
