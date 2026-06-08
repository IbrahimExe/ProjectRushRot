using UnityEngine;

public class PlacementDemoHUD : MonoBehaviour
{
    float _smoothedDeltaTime;

    double _lastPlacementMs;   // main-thread placement ms in the last completed frame
    double _worstPlacementMs;  // peak since last reset

    void LateUpdate()
    {
        // Runs after every Update (including MapGenerator's, where synchronous placement
        // happens), so this captures a whole frame's main-thread placement cost, then
        // resets the accumulator for the next frame.
        _lastPlacementMs = PlacementMetrics.MainThreadMsThisFrame;
        if (_lastPlacementMs > _worstPlacementMs) _worstPlacementMs = _lastPlacementMs;
        PlacementMetrics.MainThreadMsThisFrame = 0;
    }

    void Update()
    {
        _smoothedDeltaTime += (Time.unscaledDeltaTime - _smoothedDeltaTime) * 0.1f;

        if (Input.GetKeyDown(KeyCode.T))
            ChunkSpawner.UseThreadedPlacement = !ChunkSpawner.UseThreadedPlacement;

        if (Input.GetKeyDown(KeyCode.R))
        {
            PlacementMetrics.ResetAll();
            _worstPlacementMs = 0;
        }
    }

    void OnGUI()
    {
        float fps = _smoothedDeltaTime > 0f ? 1f / _smoothedDeltaTime : 0f;
        var style = new GUIStyle(GUI.skin.label) { fontSize = 18 };

        GUI.Box(new Rect(10, 10, 440, 250), GUIContent.none);
        GUILayout.BeginArea(new Rect(20, 18, 420, 234));

        GUILayout.Label($"Placement: {(ChunkSpawner.UseThreadedPlacement ? "THREADED" : "SYNCHRONOUS")}", style);
        GUILayout.Label($"FPS: {fps:F0}", style);
        GUILayout.Space(6);
        GUILayout.Label($"Main-thread placement (this frame): {_lastPlacementMs:F2} ms", style);
        GUILayout.Label($"Main-thread placement (worst):      {_worstPlacementMs:F2} ms", style);
        GUILayout.Space(6);
        GUILayout.Label($"Chunks placed  sync: {PlacementMetrics.ChunksSync}   threaded: {PlacementMetrics.ChunksThreaded}", style);
        GUILayout.Label($"Last worker placement: {PlacementMetrics.LastWorkerMs:F2} ms", style);
        GUILayout.Space(6);

        if (GUILayout.Button(ChunkSpawner.UseThreadedPlacement ? "Switch to Synchronous (T)" : "Switch to Threaded (T)",
                             GUILayout.Height(30)))
            ChunkSpawner.UseThreadedPlacement = !ChunkSpawner.UseThreadedPlacement;

        if (GUILayout.Button("Reset metrics (R)", GUILayout.Height(26)))
        {
            PlacementMetrics.ResetAll();
            _worstPlacementMs = 0;
        }

        GUILayout.EndArea();
    }
}