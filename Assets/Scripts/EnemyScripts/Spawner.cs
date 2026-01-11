using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class Spawner : MonoBehaviour
{
    public GameObject Character;
    public int maxSpawns = 10;
    public int currentSpawns = 0;
    public float delay = 1f;
    private float delayPreUpdate;
    public Transform maxX;
    public Transform maxZ;

    public Transform spawnPosition;
    public quaternion Q;

    void Start()
    {
        delayPreUpdate = delay;
    }

    void Update()
    {
        if (currentSpawns < maxSpawns)
        {
            if (delay > 0)
            {
                delay -= Time.deltaTime;
            }
            else
            {
                currentSpawns++;

                GameObject enemy = Instantiate(Character, spawnPosition.position, Q);

                // Assign reference so the enemy knows its spawner
                enemy.GetComponent<EnemySpawnTracker>().spawner = this;

                delay = delayPreUpdate;
            }
        }
    }

    public void EnemyDied()
    {
        currentSpawns--;
        if (currentSpawns < 0)
            currentSpawns = 0;
    }
}