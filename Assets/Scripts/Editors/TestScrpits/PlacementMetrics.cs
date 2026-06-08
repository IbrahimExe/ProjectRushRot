// Instrumentation for the sync-vs-threaded placement comparison.
// MainThreadMsThisFrame is the headline: time the placement computation costs ON THE
// MAIN THREAD this frame. Synchronous placement adds to it; threaded placement does not
// (that work runs on a worker). This is the metric that actually separates the modes —
// unlike whole-frame FPS, which is dominated by texture/mesh/instantiation costs that
// stay on the main thread in both modes.
public static class PlacementMetrics
{
    public static double MainThreadMsThisFrame; // reset each frame by the HUD
    public static int ChunksSync;            // chunks whose placement ran on the main thread
    public static int ChunksThreaded;        // chunks whose placement ran on a worker
    public static double LastWorkerMs;          // wall-clock of the most recent worker placement

    public static void ResetAll()
    {
        MainThreadMsThisFrame = 0;
        ChunksSync = 0;
        ChunksThreaded = 0;
        LastWorkerMs = 0;
    }
}