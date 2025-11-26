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

    private float randomX;
    private float randomZ;


    public Transform spawnPosition;

    public quaternion Q;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        delayPreUpdate = delay;
    }

    // Update is called once per frame
    void Update()
    {
        float delayUpdate=delay;
        
        if (currentSpawns <  maxSpawns)
        {
            if (delay > 0)
            {
                delay = delay - Time.deltaTime;
            }
            else
            {
                currentSpawns = currentSpawns + 1;
                Instantiate(Character, spawnPosition.position,Q);
                delay = delayPreUpdate;
            }
        }
        
    }
    private void selectRandomSpawnPoint ()
    {
        float distanceX = transform.position.x - maxX.position.x;
        float distanceZ = transform.position.z - maxX.position.z;

        randomX = Random.Range((transform.position.x+distanceX),maxX.position.x);
        randomX = Random.Range((transform.position.z + distanceZ), maxZ.position.z);
        spawnPosition.position = new Vector3(randomX, spawnPosition.position.y,randomZ);

        Q = quaternion.Euler(new Vector3(0, Random.Range(0, 361),0));
    }
}
