using UnityEngine;

// Demo HUD for the concurrency assignment. Shows frame timing and toggles between
// synchronous and threaded chunk placement so the difference is visible and recordable.
//
// The toggle only affects chunks initialised AFTER you flip it (each chunk reads
// ChunkSpawner.UseThreadedPlacement once, at Initialise). So: flip the mode, then
// fly into fresh terrain to see that mode's behaviour.
public class PlacementDemoHUD : MonoBehaviour
{
    [SerializeField] float _worstFrameWindow = 2f; // seconds before the worst-frame resets

    float _smoothedDeltaTime;
    float _worstFrameMs;
    float _windowTimer;

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // Smoothed for a stable FPS number.
        _smoothedDeltaTime += (dt - _smoothedDeltaTime) * 0.1f;

        // Rolling worst frame — this is what makes a placement hitch visible. A spike
        // shows up here even when the smoothed average barely moves.
        float ms = dt * 1000f;
        if (ms > _worstFrameMs) _worstFrameMs = ms;

        _windowTimer += dt;
        if (_windowTimer >= _worstFrameWindow)
        {
            _windowTimer = 0f;
            _worstFrameMs = ms;
        }

        if (Input.GetKeyDown(KeyCode.T))
            ChunkSpawner.UseThreadedPlacement = !ChunkSpawner.UseThreadedPlacement;
    }

    void OnGUI()
    {
        float fps = _smoothedDeltaTime > 0f ? 1f / _smoothedDeltaTime : 0f;
        float frameMs = _smoothedDeltaTime * 1000f;

        var style = new GUIStyle(GUI.skin.label) { fontSize = 20 };

        GUI.Box(new Rect(10, 10, 360, 150), GUIContent.none);
        GUILayout.BeginArea(new Rect(20, 18, 340, 134));
        GUILayout.Label($"Placement: {(ChunkSpawner.UseThreadedPlacement ? "THREADED" : "SYNCHRONOUS")}", style);
        GUILayout.Label($"FPS: {fps:F0}   ({frameMs:F1} ms)", style);
        GUILayout.Label($"Worst frame ({_worstFrameWindow:F0}s): {_worstFrameMs:F1} ms", style);

        if (GUILayout.Button(ChunkSpawner.UseThreadedPlacement ? "Switch to Synchronous" : "Switch to Threaded",
                             GUILayout.Height(34)))
            ChunkSpawner.UseThreadedPlacement = !ChunkSpawner.UseThreadedPlacement;
        GUILayout.EndArea();
    }
}