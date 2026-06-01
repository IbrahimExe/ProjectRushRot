using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    [SerializeField] private List<ObjectSpawner> _spawners;

    public void Initialize()
    {
        Debug.Log("Initializing Spawn Manager...");
        foreach (var spawner in _spawners)
        {
            spawner.Initialize();
        }
        Debug.Log("Spawn Manager initialization complete.");
    }

    public void StartSpawners()
    {
        Debug.Log("Starting all spawners...");
        foreach (var spawner in _spawners)
        {
            spawner.StartSpawning();
        }
    }
}
