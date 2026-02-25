using LevelGenerator.Data;
using UnityEngine;

[CreateAssetMenu(fileName = "RunnerConfig", menuName = "Runner/GeneratorConfig")]
public class RunnerGenConfig : ScriptableObject
{
    [Header("Catalog Reference")]
    [Tooltip("Prefab catalog used for surfaces/occupants lookup.")]
    public PrefabCatalog catalog;

    [Tooltip("Neighbor rules used for WFC + adjacency constraints.")]
    public NeighborRulesConfig weightRules;

    [Header("Track Dimensions")]
    [Tooltip("How many playable lanes exist across the track (X). (Generator adds 2 edge lanes internally.)")]
    public int laneCount = 30;

    [Tooltip("Width of each lane in world units.")]
    public float laneWidth = 10f;

    [Tooltip("Length of a single row/cell in world units (Z).")]
    public float cellLength = 10f;

    [Header("Streaming & Chunking")]
    [Tooltip("Extra rows to keep generated ahead/beyond the active play space.")]
    public int bufferRows = 30;

    [Tooltip("How many rows behind the player we keep alive (cleanup threshold).")]
    public int keepRowsBehind = 20;

    [Tooltip("Number of rows to generate together as a chunk.")]
    public int chunkSize = 10;

    // (kept your tooltip verbatim)
    [Tooltip("Number of rows of existing context to keep, VERY HEAVY.")]
    public int contextRows = 1;

    [Header("Occupant Spawning")]
    [Tooltip("Maximum 'Cost' of occupants per chunk.")]
    public int densityBudget = 60;

    [Tooltip("Chance to attempt occupant spawning per cell (0-1).")]
    [Range(0f, 1f)] public float globalSpawnChance = 0.5f;

    [Header("Biome Noise (Zone System)")]
    [Tooltip("Turns the zone/biome noise system on/off. When OFF, tiles ignore biome affinities and only neighbor rules affect surfaces.")]
    public bool useBiomeSystem = true;

    [Tooltip("Controls the SIZE of terrain zones in grid cells. Higher = larger, smoother zones. Lower = smaller, noisier patches.")]
    [Range(5f, 50f)] public float biomeNoiseScale = 18f;

    [Header("Biome Noise Quality")]
    [Tooltip("Number of noise layers combined. More = richer detail, but too high can add speckle.")]
    [Range(1, 8)] public int biomeOctaves = 8;

    [Tooltip("Frequency multiplier per octave. Higher = more detail each octave.")]
    [Range(1.2f, 3.5f)] public float biomeLacunarity = 1.2f;

    [Tooltip("Amplitude multiplier per octave. Lower = smoother. Higher = more detail/speckle.")]
    [Range(0.2f, 0.8f)] public float biomeGain = 0.2f;

    [Tooltip("How much we 'warp' the noise coordinates to create rounder, more organic regions. 0 = no warp.")]
    [Range(0f, 3f)] public float biomeWarpStrength = 1f;

    [Tooltip("Scale of the warp field. Lower = broad bends. Higher = tighter twisting.")]
    [Range(0.5f, 6f)] public float biomeWarpScale = 0.5f;

    [Tooltip("Extra smoothing by averaging nearby noise samples. 0 = none. Higher = softer boundaries, fewer tiny islands.")]
    [Range(0f, 1f)] public float biomeBlur = 0.5f;

    [Header("Golden Path Wave")]
    public bool useWavePath = true;

    [Tooltip("How strongly the wave influences the golden path (0..1).")]
    [Range(0f, 1f)] public float waveStrength = 1f;

    [Tooltip("Max drift from center in lanes (before clamping).")]
    [Range(0f, 150f)] public float waveAmplitudeLanes = 23f;

    [Tooltip("How fast the wave changes per row. Smaller = longer turns.")]
    [Range(0.001f, 0.05f)] public float waveFrequency = 0.0216f;

    [Tooltip("How quickly we chase the target (0..1).")]
    [Range(0.01f, 1f)] public float waveSmoothing = 0.713f;

    [Tooltip("Hard limit: max lanes we can shift per row (turn rate).")]
    [Range(0.05f, 2f)] public float maxLaneChangePerRow = 2f;

    [Tooltip("Keep path away from edges.")]
    [Range(0, 10)] public int edgePadding = 2;

    [Header("Golden Path Rest Straights")]
    [Tooltip("Max number of rows in a straight rest segment.")]
    [Range(2, 300)] public int restAreaMaxLength = 35;

    [Tooltip("Min number of rows in a straight rest segment.")]
    [Range(2, 300)] public int restAreaMinLength = 15;

    [Tooltip("Chance per row to START a rest (0..1). Example: 0.02 ~ roughly once every ~50 rows).")]
    [Range(0f, 1f)] public float restAreaFrequency = 0.05f;

    public void EnsureReady()
    {
        if (catalog != null) catalog.RebuildCache();

        if (weightRules != null)
        {
            weightRules.catalog = catalog;
            weightRules.BuildCache();
        }
    }

    private void OnValidate() => EnsureReady();
}
