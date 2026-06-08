using System;
using System.Collections.Concurrent;
using UnityEngine;

// Main-thread completion drainer for off-thread placement work.
// A worker computes plain SpawnedInstance data, then hands a main-thread action
// back through Enqueue. Update() runs those actions on the main thread, which is
// the only place UnityEngine / pool / catalog calls are legal.
//
// Thread rules:
//   Enqueue(...)  - safe to call from a worker thread (ConcurrentQueue handles it).
//   Instance      - MAIN THREAD ONLY (it can create a GameObject). Capture it on
//                   the main thread before starting a worker; never touch it from one.
public class PlacementDispatcher : MonoBehaviour
{
    static PlacementDispatcher _instance;

    readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

    // Main-thread only. Call from the main thread (e.g. ChunkSpawner.Initialise)
    // to guarantee the dispatcher exists before any worker is started.
    public static PlacementDispatcher Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[PlacementDispatcher]");
                _instance = go.AddComponent<PlacementDispatcher>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Thread-safe. Safe to call from a worker. The action runs later on the main
    // thread inside Update().
    public void Enqueue(Action action)
    {
        if (action != null) _queue.Enqueue(action);
    }

    void Update()
    {
        while (_queue.TryDequeue(out var action))
        {
            action.Invoke();
        }
    }
}