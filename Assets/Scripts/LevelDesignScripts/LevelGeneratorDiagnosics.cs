// ============================================================
// Level Generator Diagnostic Tool
// Add this as a new component to help visualize and verify
// that your level generator is working correctly
// ============================================================

using LevelGenerator.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

[RequireComponent(typeof(RunnerLevelGenerator))]
public class LevelGeneratorDiagnostics : MonoBehaviour
{
    [Header("Visualization")]
    [SerializeField] private bool showGoldenPath = true;
    [SerializeField] private bool showNoiseWeights = true;   // Replaces showBiomes — shows raw noise value as a heat map
    [SerializeField] private bool showTerrainZones = true;   // Zone-first biome system overlay (Forest/Grass/Sandy/Water)
    [SerializeField] private bool showSurfaceTypes = true;
    [SerializeField] private bool showWFCState = false;
    [SerializeField] private bool showOccupants = true;
    [SerializeField] private bool showEdgeLanes = true;
    [SerializeField] private bool showEdgeWallSegments = true;

    [Header("Debug Output")]
    [SerializeField] private bool logChunkGeneration = true;
    [SerializeField] private bool logWFCSteps = false;
    [SerializeField] private bool logConstraintViolations = true;
    [SerializeField] private bool logEdgeWallGeneration = true;

    [Header("Performance Monitoring")]
    [SerializeField] private bool trackPerformance = true;
    [SerializeField] private int maxFrameTimeMs = 16; // 60fps target

    private RunnerLevelGenerator generator;
    private Queue<float> frameTimeHistory = new Queue<float>();

    // Cached reflection handles — computed once in Awake to avoid per-frame overhead
    private FieldInfo gridField;
    private FieldInfo goldenPathField;
    private FieldInfo configField;
    private MethodInfo sampleBiomeNoiseMethod;

    // New generator fields (multi-biome configs + zone-first biome system)
    private FieldInfo biomeConfigsField;
    private FieldInfo currentBiomeIndexField;

    // Static zone configuration on RunnerLevelGenerator (private nested enum + static readonly fields)
    private FieldInfo zoneThresholdsField;
    private FieldInfo zoneOrderField;
    private FieldInfo zoneDominantIdField;
    private FieldInfo zoneDepthCheckField;

    // Stats tracking
    private int totalChunksGenerated = 0;
    private int totalCellsCollapsed = 0;
    private int totalWFCIterations = 0;

    void Awake()
    {
        generator = GetComponent<RunnerLevelGenerator>();

        // Cache all reflection handles up front
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        gridField = typeof(RunnerLevelGenerator).GetField("grid", flags);
        goldenPathField = typeof(RunnerLevelGenerator).GetField("goldenPathSet", flags);
        configField = typeof(RunnerLevelGenerator).GetField("config", flags);
        sampleBiomeNoiseMethod = typeof(RunnerLevelGenerator).GetMethod("SampleBiomeNoise", flags);

        biomeConfigsField = typeof(RunnerLevelGenerator).GetField("biomeConfigs", flags);
        currentBiomeIndexField = typeof(RunnerLevelGenerator).GetField("currentBiomeIndex", flags);

        // Zone system lives on the generator as private static fields.
        // We reflect them so diagnostics stays aligned even if you tweak thresholds / IDs.
        var sFlags = BindingFlags.NonPublic | BindingFlags.Static;
        zoneThresholdsField = typeof(RunnerLevelGenerator).GetField("ZoneThresholds", sFlags);
        zoneOrderField = typeof(RunnerLevelGenerator).GetField("ZoneOrder", sFlags);
        zoneDominantIdField = typeof(RunnerLevelGenerator).GetField("ZoneDominantID", sFlags);
        zoneDepthCheckField = typeof(RunnerLevelGenerator).GetField("ZONE_DEPTH_CHECK", sFlags);
    }

    // ============================================================
    // Helpers — typed accessors over the cached reflection handles
    // ============================================================

    private Dictionary<(int z, int lane), CellState> GetGrid() =>
        gridField?.GetValue(generator) as Dictionary<(int z, int lane), CellState>;

    private HashSet<(int z, int lane)> GetGoldenPath() =>
        goldenPathField?.GetValue(generator) as HashSet<(int z, int lane)>;

    private RunnerGenConfig GetConfig() =>
        configField?.GetValue(generator) as RunnerGenConfig;

    // Safe optional reads from RunnerGenConfig so diagnostics doesn't break when configs evolve.
    private bool TryGetConfigValue<T>(string memberName, out T value)
    {
        value = default;
        var cfg = GetConfig();
        if (cfg == null) return false;

        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        try
        {
            var f = typeof(RunnerGenConfig).GetField(memberName, flags);
            if (f != null && f.GetValue(cfg) is T fv) { value = fv; return true; }

            var p = typeof(RunnerGenConfig).GetProperty(memberName, flags);
            if (p != null && p.GetValue(cfg) is T pv) { value = pv; return true; }
        }
        catch { }

        return false;
    }

    private int GetCurrentBiomeIndex()
    {
        if (currentBiomeIndexField == null) return -1;
        try { return (int)currentBiomeIndexField.GetValue(generator); }
        catch { return -1; }
    }

    private List<RunnerGenConfig> GetBiomeConfigs()
    {
        if (biomeConfigsField == null) return null;
        try { return biomeConfigsField.GetValue(generator) as List<RunnerGenConfig>; }
        catch { return null; }
    }

    // Returns the raw 0..1 noise value for a cell, or -1 if unavailable.
    private float SampleNoise(int z, int lane)
    {
        if (sampleBiomeNoiseMethod == null) return -1f;
        try { return (float)sampleBiomeNoiseMethod.Invoke(generator, new object[] { z, lane }); }
        catch { return -1f; }
    }

    // ============================================================
    // Zone-first biome system helpers (mirrors RunnerLevelGenerator)
    // ============================================================

    private float[] GetZoneThresholds()
    {
        try { return zoneThresholdsField?.GetValue(null) as float[]; }
        catch { return null; }
    }

    private object[] GetZoneOrder()
    {
        // Array of the generator's private nested enum values.
        try
        {
            var arr = zoneOrderField?.GetValue(null) as Array;
            if (arr == null) return null;
            var outArr = new object[arr.Length];
            for (int i = 0; i < arr.Length; i++) outArr[i] = arr.GetValue(i);
            return outArr;
        }
        catch { return null; }
    }

    private int GetZoneDepthCheck()
    {
        // const int compiles to a literal static field, reflection still works in editor.
        try
        {
            if (zoneDepthCheckField == null) return 2;
            return (int)zoneDepthCheckField.GetValue(null);
        }
        catch { return 2; }
    }

    private System.Collections.IDictionary GetZoneDominantIdMap()
    {
        // Dictionary<TerrainZone, string>
        try { return zoneDominantIdField?.GetValue(null) as System.Collections.IDictionary; }
        catch { return null; }
    }

    private object GetZoneFromNoise(float noise01)
    {
        var thresholds = GetZoneThresholds();
        var order = GetZoneOrder();
        if (thresholds == null || order == null || thresholds.Length == 0 || order.Length == 0)
        {
            // Fallback to the current generator defaults (Forest/Grass/Sandy/Water @ 0.25 steps)
            float[] t = { 0.25f, 0.50f, 0.75f, 1.0f };
            string[] z = { "Forest", "Grass", "Sandy", "Water" };
            for (int i = 0; i < t.Length; i++)
                if (noise01 < t[i]) return z[i];
            return z[z.Length - 1];
        }

        for (int i = 0; i < thresholds.Length && i < order.Length; i++)
            if (noise01 < thresholds[i]) return order[i];
        return order[order.Length - 1];
    }

    private bool IsDeeplyInsideZone(int z, int lane, object zone)
    {
        // Mirrors RunnerLevelGenerator.IsDeeplyInsideZone:
        //  - samples noise only
        //  - ignores edge lanes
        var config = GetConfig();
        if (config == null) return false;

        int depth = Mathf.Max(1, GetZoneDepthCheck());
        int totalLanes = config.laneCount + 2;
        for (int dz = -depth; dz <= depth; dz++)
        {
            for (int dl = -depth; dl <= depth; dl++)
            {
                if (dz == 0 && dl == 0) continue;
                int nl = lane + dl;
                if (nl <= 0 || nl >= totalLanes - 1) continue; // ignore edge lanes
                float n = SampleNoise(z + dz, nl);
                if (n < 0f) return false;
                object otherZone = GetZoneFromNoise(n);
                if (!Equals(zone, otherZone)) return false;
            }
        }
        return true;
    }

    private string GetDominantSurfaceIdForZone(object zone)
    {
        var map = GetZoneDominantIdMap();
        if (map == null || zone == null) return null;
        try
        {
            // IDictionary uses object keys; the zone object must be the same enum type.
            if (map.Contains(zone)) return map[zone] as string;
        }
        catch { }

        // If we couldn't read the map (or we're in fallback string-zone mode)
        // use the generator's current defaults.
        string zn = zone.ToString();
        return zn switch
        {
            "Forest" => "Forest_SUR",
            "Grass" => "GRASS_SUR",
            "Sandy" => "Sand_SUR",
            "Water" => "Water_SUR",
            _ => null
        };
    }

    private float LaneToWorldX(int lane, int totalLanes, float laneWidth)
    {
        float centerLane = (totalLanes - 1) * 0.5f;
        return (lane - centerLane) * laneWidth;
    }

    // ============================================================
    // Gizmo Rendering
    // ============================================================

    void Update()
    {
        if (trackPerformance)
        {
            float frameTime = Time.deltaTime * 1000f;
            frameTimeHistory.Enqueue(frameTime);
            if (frameTimeHistory.Count > 60) frameTimeHistory.Dequeue();

            if (frameTime > maxFrameTimeMs)
                Debug.LogWarning($"[Performance] Frame time spike: {frameTime:F1}ms (target: {maxFrameTimeMs}ms)");
        }
    }

    void OnDrawGizmos()
    {
        if (generator == null || !Application.isPlaying) return;

        var grid = GetGrid();
        var goldenPath = GetGoldenPath();
        var config = GetConfig();

        if (grid == null || config == null) return;

        float laneWidth = config.laneWidth;
        float cellLength = config.cellLength;
        int totalLanes = config.laneCount + 2;

        foreach (var kvp in grid)
        {
            (int z, int lane) = kvp.Key;
            CellState cell = kvp.Value;

            Vector3 pos = new Vector3(
                LaneToWorldX(lane, totalLanes, laneWidth),
                0.1f,
                z * cellLength
            );

            // Golden Path
            if (showGoldenPath && goldenPath != null && goldenPath.Contains((z, lane)))
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(pos + Vector3.up * 0.5f,
                    new Vector3(laneWidth * 0.9f, 1f, cellLength * 0.9f));
            }

            // Edge Lanes
            if (showEdgeLanes && cell.isEdgeLane)
            {
                Gizmos.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                Gizmos.DrawCube(pos, new Vector3(laneWidth * 0.95f, 0.1f, cellLength * 0.95f));
            }

            // Noise heat map — replaces old per-cell biome colour.
            // Blue (0) → Red (1) showing raw DomainWarpedFbm01 value.
            // Smooth spatial gradients confirm the noise is working correctly.
            // Zone bands (default): 0-0.25=Forest, 0.25-0.5=Grass, 0.5-0.75=Sandy, 0.75-1=Water
            if (showNoiseWeights && !cell.isEdgeLane && config.useBiomeSystem)
            {
                float noise = SampleNoise(z, lane);
                if (noise >= 0f)
                {
                    // Heat map: blue→cyan→green→yellow→red
                    Color heatColor = Color.HSVToRGB((1f - noise) * 0.66f, 1f, 1f);
                    Gizmos.color = new Color(heatColor.r, heatColor.g, heatColor.b, 0.8f);
                    Gizmos.DrawCube(pos + Vector3.down * 0.05f,
                        new Vector3(laneWidth * 0.5f, 0.05f, cellLength * 0.5f));
                }
            }

            // Terrain Zones overlay — shows the discrete zone assignment derived from noise.
            if (showTerrainZones && !cell.isEdgeLane && config.useBiomeSystem)
            {
                float noise = SampleNoise(z, lane);
                if (noise >= 0f)
                {
                    object zone = GetZoneFromNoise(noise);
                    // Colours are intentionally distinct and semi-transparent
                    Color zoneColor = zone.ToString() switch
                    {
                        "Forest" => new Color(0.10f, 0.35f, 0.10f, 0.25f),
                        "Grass" => new Color(0.20f, 0.70f, 0.20f, 0.25f),
                        "Sandy" => new Color(0.90f, 0.85f, 0.35f, 0.25f),
                        "Water" => new Color(0.20f, 0.50f, 0.90f, 0.25f),
                        _ => new Color(0.70f, 0.70f, 0.70f, 0.25f)
                    };

                    Gizmos.color = zoneColor;
                    Gizmos.DrawCube(pos + Vector3.up * 0.02f,
                        new Vector3(laneWidth * 0.92f, 0.02f, cellLength * 0.92f));
                }
            }

            // Surface Types
            if (showSurfaceTypes)
            {
                Color surfaceColor = cell.surface switch
                {
                    SurfaceType.Solid => Color.white,
                    SurfaceType.Hole => Color.black,
                    SurfaceType.Bridge => Color.cyan,
                    SurfaceType.SafePath => Color.green,
                    SurfaceType.Edge => Color.red,
                    _ => Color.white
                };

                Gizmos.color = new Color(surfaceColor.r, surfaceColor.g, surfaceColor.b, 0.5f);
                Gizmos.DrawCube(pos, new Vector3(laneWidth * 0.7f, 0.05f, cellLength * 0.7f));
            }

            // WFC State — uncollapsed cells shown as magenta spheres
            if (showWFCState && !cell.isCollapsed)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(pos + Vector3.up * 0.5f, 0.2f);
            }

            // Occupants
            if (showOccupants && cell.occupant != OccupantType.None)
            {
                Color occupantColor = cell.occupant switch
                {
                    OccupantType.Wall => Color.red,
                    OccupantType.Obstacle => new Color(1f, 0.5f, 0f),
                    OccupantType.Collectible => Color.yellow,
                    OccupantType.Enemy => Color.magenta,
                    OccupantType.EdgeWall => new Color(0.5f, 0f, 0.5f),
                    _ => Color.white
                };

                Gizmos.color = occupantColor;
                Gizmos.DrawSphere(pos + Vector3.up * 0.5f, 0.3f);
            }

            // Edge Wall Segments — draw entire multi-row extents from origin cell only
            if (showEdgeWallSegments && cell.occupant == OccupantType.EdgeWall && cell.occupantDef != null)
            {
                bool isOrigin = true;
                if (grid.TryGetValue((z - 1, lane), out CellState prevCell))
                {
                    if (prevCell.occupant == OccupantType.EdgeWall &&
                        prevCell.occupantDef == cell.occupantDef)
                        isOrigin = false;
                }

                if (isOrigin)
                {
                    int sizeZ = cell.occupantDef.SizeZ;
                    float segmentLength = cellLength * sizeZ;
                    Vector3 segmentCenter = pos + Vector3.forward * (segmentLength - cellLength) * 0.5f;

                    Gizmos.color = new Color(0.8f, 0f, 0.8f, 0.8f);
                    Gizmos.DrawWireCube(segmentCenter + Vector3.up * 0.5f,
                        new Vector3(laneWidth * 0.9f, 1f, segmentLength * 0.95f));

                    Gizmos.color = new Color(0.5f, 0f, 0.5f, 0.3f);
                    Gizmos.DrawCube(segmentCenter,
                        new Vector3(laneWidth * 0.8f, 0.1f, segmentLength * 0.9f));
                }
            }
        }
    }

    // ============================================================
    // Verification Suite
    // ============================================================

    [ContextMenu("Run Full System Verification")]
    public void RunFullVerification()
    {
        Debug.Log("=== LEVEL GENERATOR VERIFICATION ===");

        bool allPassed = true;
        allPassed &= VerifyConfiguration();
        allPassed &= VerifyGoldenPathIntegrity();
        allPassed &= VerifyWalkability();
        allPassed &= VerifyZoneSystemCoherence();
        allPassed &= VerifyConstraintCompliance();
        allPassed &= VerifyEdgeLaneIntegrity();
        allPassed &= VerifyEdgeWallCoverage();

        Debug.Log(allPassed
            ? "<color=green>✓ ALL VERIFICATION CHECKS PASSED</color>"
            : "<color=red>✗ SOME VERIFICATION CHECKS FAILED - See above for details</color>");

        PrintStatistics();
    }

    bool VerifyConfiguration()
    {
        Debug.Log("\n--- Configuration Check ---");

        var config = GetConfig();
        if (config == null) { Debug.LogError("✗ Config is null!"); return false; }
        if (config.catalog == null) { Debug.LogError("✗ Catalog is null!"); return false; }
        if (config.weightRules == null) { Debug.LogError("✗ WeightRules is null!"); return false; }
        if (config.laneCount < 3) Debug.LogWarning($"⚠ Lane count is very low: {config.laneCount}");

        int biomeIndex = GetCurrentBiomeIndex();
        var biomes = GetBiomeConfigs();
        if (biomes != null && biomes.Count > 0)
        {
            string activeName = biomeIndex >= 0 && biomeIndex < biomes.Count && biomes[biomeIndex] != null
                ? biomes[biomeIndex].name
                : "(unknown)";
            Debug.Log($"✓ Config valid: {config.laneCount} lanes, buffer={config.bufferRows}, chunk={config.chunkSize} | " +
                      $"ActiveBiome={biomeIndex}/{biomes.Count - 1} '{activeName}'");
        }
        else
        {
            Debug.Log($"✓ Config valid: {config.laneCount} lanes, buffer={config.bufferRows}, chunk={config.chunkSize}");
        }
        // Print a best-effort snapshot of biome-related fields without hard-binding to any one config version.
        var sb = new StringBuilder();
        sb.Append($"  Biome system: {(config.useBiomeSystem ? "ON" : "OFF")}");

        if (TryGetConfigValue<float>("biomeNoiseScale", out var noiseScale)) sb.Append($", noiseScale={noiseScale}");
        if (TryGetConfigValue<int>("biomeOctaves", out var oct)) sb.Append($", octaves={oct}");
        if (TryGetConfigValue<float>("biomeLacunarity", out var lac)) sb.Append($", lacunarity={lac}");
        if (TryGetConfigValue<float>("biomeGain", out var gain)) sb.Append($", gain={gain}");
        if (TryGetConfigValue<float>("biomeWarpStrength", out var warp)) sb.Append($", warpStrength={warp}");
        if (TryGetConfigValue<float>("biomeWarpScale", out var warpScale)) sb.Append($", warpScale={warpScale}");
        if (TryGetConfigValue<float>("biomeBlur", out var blur)) sb.Append($", blur={blur}");
        if (TryGetConfigValue<float>("biomeCollapseBias", out var collapseBias)) sb.Append($", collapseBias={collapseBias}");

        Debug.Log(sb.ToString());
        return true;
    }

    bool VerifyGoldenPathIntegrity()
    {
        Debug.Log("\n--- Golden Path Integrity Check ---");

        var goldenPath = GetGoldenPath();
        if (goldenPath == null || goldenPath.Count == 0)
        {
            Debug.LogWarning("⚠ Golden path is empty - has generation started?");
            return true;
        }

        var sortedPath = goldenPath.OrderBy(p => p.z).ToList();
        int gaps = 0;

        for (int i = 1; i < sortedPath.Count; i++)
        {
            if (sortedPath[i].z - sortedPath[i - 1].z > 1)
            {
                gaps++;
                Debug.LogWarning($"⚠ Gap: Z {sortedPath[i - 1].z} → {sortedPath[i].z}");
            }
        }

        if (gaps == 0)
        {
            Debug.Log($"✓ Golden path continuous: {goldenPath.Count} cells");
            return true;
        }

        Debug.LogError($"✗ Golden path has {gaps} gaps!");
        return false;
    }

    bool VerifyWalkability()
    {
        Debug.Log("\n--- Walkability Check ---");

        var grid = GetGrid();
        var config = GetConfig();
        if (grid == null || config == null) { Debug.LogWarning("⚠ Missing data"); return true; }

        var rows = grid.GroupBy(kvp => kvp.Key.z).OrderBy(g => g.Key);
        int blockedRows = 0;

        foreach (var row in rows)
        {
            bool hasWalkable = row.Any(c =>
                !c.Value.isEdgeLane &&
                c.Value.surface != SurfaceType.Hole &&
                (c.Value.occupant == OccupantType.None || c.Value.occupant == OccupantType.Collectible));

            if (!hasWalkable)
            {
                blockedRows++;
                Debug.LogError($"✗ Row {row.Key} has NO walkable lanes!");
            }
        }

        if (blockedRows == 0)
        {
            Debug.Log($"✓ All {rows.Count()} rows have at least one walkable lane");
            return true;
        }

        Debug.LogError($"✗ {blockedRows} rows are completely blocked!");
        return false;
    }

    // Zone-first biome system (RunnerLevelGenerator):
    //   - Noise defines a discrete zone (Forest/Grass/Sandy/Water) via thresholds.
    //   - "Deep" cells (zone-consistent neighbourhood) are pre-collapsed to the zone's dominant tile.
    //   - Boundary cells keep many candidates with noise-biased weights, and WFC handles transitions.
    // We verify:
    //   1) SampleBiomeNoise returns 0..1
    //   2) Noise field is reasonably smooth (avg neighbour delta)
    //   3) Deep-zone cells are mostly stamped with the dominant zone tile
    bool VerifyZoneSystemCoherence()
    {
        Debug.Log("\n--- Zone System Coherence Check ---");

        var config = GetConfig();
        var grid = GetGrid();
        if (config == null || grid == null) { Debug.LogWarning("⚠ Missing data"); return true; }

        if (!config.useBiomeSystem)
        {
            Debug.Log("○ Biome system disabled — skipping noise check");
            return true;
        }

        if (sampleBiomeNoiseMethod == null)
        {
            Debug.LogWarning("⚠ SampleBiomeNoise method not found via reflection — " +
                "ensure it is not renamed and is non-public instance.");
            return true;
        }

        // 1. Spot-check noise values are in range
        int outOfRange = 0;
        var sampleCells = grid.Where(k => !k.Value.isEdgeLane).Take(200).ToList();

        foreach (var kvp in sampleCells)
        {
            float n = SampleNoise(kvp.Key.z, kvp.Key.lane);
            if (n < 0f || n > 1f) outOfRange++;
        }

        if (outOfRange > 0)
            Debug.LogError($"✗ {outOfRange} cells returned noise outside 0..1!");
        else
            Debug.Log($"✓ Noise values in valid range across {sampleCells.Count} sampled cells");

        // 2. Check gradient smoothness — adjacent cells should have similar noise values.
        // High delta variance indicates the noise scale is too small (too noisy/grainy).
        var playableCells = grid
            .Where(k => !k.Value.isEdgeLane && k.Value.isCollapsed)
            .ToDictionary(k => k.Key, k => k.Value);

        float totalDelta = 0f;
        int comparisons = 0;
        float maxDelta = 0f;

        foreach (var kvp in playableCells)
        {
            (int z, int lane) = kvp.Key;
            float n0 = SampleNoise(z, lane);

            if (playableCells.ContainsKey((z, lane + 1)))
            {
                float delta = Mathf.Abs(n0 - SampleNoise(z, lane + 1));
                totalDelta += delta;
                maxDelta = Mathf.Max(maxDelta, delta);
                comparisons++;
            }
            if (playableCells.ContainsKey((z + 1, lane)))
            {
                float delta = Mathf.Abs(n0 - SampleNoise(z + 1, lane));
                totalDelta += delta;
                maxDelta = Mathf.Max(maxDelta, delta);
                comparisons++;
            }
        }

        if (comparisons > 0)
        {
            float avgDelta = totalDelta / comparisons;
            Debug.Log($"  Noise gradient: avg delta={avgDelta:F4}, max delta={maxDelta:F4}");

            if (avgDelta > 0.15f)
                Debug.LogWarning($"⚠ High average noise delta ({avgDelta:F4}) — biomeNoiseScale may be too small, " +
                    "causing noisy terrain patches. Try increasing it.");
            else
                Debug.Log("✓ Noise gradients look smooth");

            if (maxDelta > 0.5f)
                Debug.LogWarning($"⚠ Max noise delta {maxDelta:F4} is very high — " +
                    "possible warp overshoot. Try lowering biomeWarpStrength.");
        }

        // 3. Deep-zone stamping check: cells that are deeply inside a zone should
        // be pre-collapsed to the dominant tile for that zone.
        int deepChecked = 0;
        int deepMismatches = 0;
        int deepNoMapping = 0;

        foreach (var kvp in playableCells.Take(600))
        {
            (int z, int lane) = kvp.Key;
            CellState cell = kvp.Value;
            if (cell.surfaceDef == null) continue;
            if (cell.surfaceDef.SurfaceType == SurfaceType.SafePath) continue; // forced, skip
            if (GetGoldenPath() != null && GetGoldenPath().Contains((z, lane))) continue;

            float noise = SampleNoise(z, lane);
            if (noise < 0f) continue;

            object zone = GetZoneFromNoise(noise);
            if (!IsDeeplyInsideZone(z, lane, zone)) continue;

            string dominantId = GetDominantSurfaceIdForZone(zone);
            if (string.IsNullOrEmpty(dominantId)) { deepNoMapping++; continue; }

            deepChecked++;
            if (!string.Equals(cell.surfaceDef.ID, dominantId, System.StringComparison.Ordinal))
            {
                deepMismatches++;
                Debug.LogWarning($"⚠ Deep-zone mismatch at ({z},{lane}) zone={zone} noise={noise:F3}: " +
                    $"expected '{dominantId}', got '{cell.surfaceDef.ID}'");
            }
        }

        if (deepChecked > 0)
        {
            float mismatchRate = (float)deepMismatches / deepChecked;
            if (deepMismatches == 0)
                Debug.Log($"✓ Deep-zone stamping OK ({deepChecked} deep cells checked)");
            else if (mismatchRate < 0.10f)
                Debug.LogWarning($"⚠ Deep-zone stamping had {deepMismatches}/{deepChecked} mismatches ({mismatchRate:P0}). " +
                    "Small rates can happen if WFC forced a legal fix; large rates usually mean ZoneDominantID is out of sync with the catalog.");
            else
                Debug.LogError($"✗ Deep-zone stamping had {deepMismatches}/{deepChecked} mismatches ({mismatchRate:P0}). " +
                    "Update ZoneDominantID in RunnerLevelGenerator or your catalog IDs.");
        }
        else
        {
            Debug.Log("○ No deep-zone cells detected in sample (this can happen with small chunks, very noisy settings, or biome system off)." +
                      (deepNoMapping > 0 ? " Dominant ID map missing/unreadable." : string.Empty));
        }

        return outOfRange == 0 && deepMismatches == 0;
    }

    bool VerifyConstraintCompliance()
    {
        Debug.Log("\n--- Constraint Compliance Check ---");

        var grid = GetGrid();
        var config = GetConfig();
        if (grid == null || config?.weightRules == null) { Debug.LogWarning("⚠ Missing data"); return true; }

        int violations = 0;

        foreach (var kvp in grid)
        {
            (int z, int lane) = kvp.Key;
            CellState cell = kvp.Value;
            if (cell.surfaceDef == null || cell.isEdgeLane) continue;

            void Check(int tz, int tl, Direction dir)
            {
                if (!grid.TryGetValue((tz, tl), out CellState neighbor)) return;
                if (neighbor.surfaceDef == null || neighbor.isEdgeLane) return;

                if (!config.weightRules.IsNeighborAllowed(cell.surfaceDef, neighbor.surfaceDef, dir))
                {
                    violations++;
                    if (logConstraintViolations)
                        Debug.LogWarning($"⚠ Violation ({z},{lane})→({tz},{tl}) [{dir}]: " +
                            $"{cell.surfaceDef.ID} → {neighbor.surfaceDef.ID}");
                }
            }

            Check(z + 1, lane, Direction.Forward);
            Check(z, lane + 1, Direction.Right);
        }

        if (violations == 0)
        {
            Debug.Log("✓ No constraint violations detected");
            return true;
        }

        Debug.LogError($"✗ {violations} constraint violations found!");
        return false;
    }

    bool VerifyEdgeLaneIntegrity()
    {
        Debug.Log("\n--- Edge Lane Integrity Check ---");

        var grid = GetGrid();
        var config = GetConfig();
        if (grid == null || config == null) { Debug.LogWarning("⚠ Missing data"); return true; }

        int totalLanes = config.laneCount + 2;
        int violations = 0;

        foreach (var kvp in grid)
        {
            int lane = kvp.Key.lane;
            CellState cell = kvp.Value;
            bool shouldBeEdge = lane == 0 || lane == totalLanes - 1;

            if (shouldBeEdge && !cell.isEdgeLane)
            { violations++; Debug.LogError($"✗ Lane {lane} should be edge but isn't!"); }

            if (!shouldBeEdge && cell.isEdgeLane)
            { violations++; Debug.LogError($"✗ Lane {lane} is marked edge but shouldn't be!"); }

            if (cell.isEdgeLane && cell.surface != SurfaceType.Edge)
            { violations++; Debug.LogError($"✗ Edge lane at ({kvp.Key.z},{lane}) has wrong surface: {cell.surface}"); }
        }

        if (violations == 0) { Debug.Log("✓ Edge lanes correctly configured"); return true; }
        Debug.LogError($"✗ {violations} edge lane violations!"); return false;
    }

    bool VerifyEdgeWallCoverage()
    {
        if (!logEdgeWallGeneration) return true;
        Debug.Log("\n--- Edge Wall Coverage Check ---");

        var grid = GetGrid();
        var config = GetConfig();
        if (grid == null || config == null) { Debug.LogWarning("⚠ Missing data"); return true; }

        int totalLanes = config.laneCount + 2;
        int leftLane = 0;
        int rightLane = totalLanes - 1;

        AnalyzeEdgeLane(grid, leftLane, config.cellLength, "Left");
        AnalyzeEdgeLane(grid, rightLane, config.cellLength, "Right");

        int leftSegments = CountWallSegments(grid, leftLane);
        int rightSegments = CountWallSegments(grid, rightLane);

        Debug.Log($"Wall segments: Left={leftSegments}, Right={rightSegments}");

        if (leftSegments <= 1 || rightSegments <= 1)
        {
            Debug.LogError("✗ Very few wall segments — edge walls may only be spawning once per chunk.");
            return false;
        }

        Debug.Log("✓ Multiple wall segments in both lanes"); return true;
    }

    private void AnalyzeEdgeLane(Dictionary<(int z, int lane), CellState> grid, int lane,
        float cellLength, string label)
    {
        var cells = grid.Where(k => k.Key.lane == lane).OrderBy(k => k.Key.z).ToList();
        int walls = cells.Count(c => c.Value.occupant == OccupantType.EdgeWall);
        int empty = cells.Count(c => c.Value.occupant == OccupantType.None);

        Debug.Log($"{label} lane: {walls} wall cells, {empty} empty, {cells.Count} total");

        var bySize = cells
            .Where(c => c.Value.occupant == OccupantType.EdgeWall && c.Value.occupantDef != null)
            .GroupBy(c => c.Value.occupantDef.SizeZ)
            .OrderBy(g => g.Key);

        foreach (var g in bySize)
            Debug.Log($"  SizeZ={g.Key}: {g.Count()} cells");
    }

    private int CountWallSegments(Dictionary<(int z, int lane), CellState> grid, int lane)
    {
        var cells = grid.Where(k => k.Key.lane == lane).OrderBy(k => k.Key.z).ToList();
        int segments = 0;

        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i].Value.occupant != OccupantType.EdgeWall) continue;
            bool isOrigin = i == 0 ||
                cells[i - 1].Value.occupant != OccupantType.EdgeWall ||
                cells[i - 1].Value.occupantDef != cells[i].Value.occupantDef;
            if (isOrigin) segments++;
        }

        return segments;
    }

    // ============================================================
    // Detailed Violation Debugger
    // ============================================================

    [ContextMenu("Debug Specific Violation")]
    public void DebugSpecificViolation()
    {
        var grid = GetGrid();
        var config = GetConfig();
        if (grid == null || config == null) return;

        var allSurfaces = config.catalog.Definitions
            .Where(d => d.Layer == ObjectLayer.Surface).ToList();

        Debug.Log("=== DETAILED CONSTRAINT ANALYSIS ===");

        foreach (var kvp in grid.OrderBy(k => k.Key.z).ThenBy(k => k.Key.lane))
        {
            (int z, int lane) = kvp.Key;
            CellState cell = kvp.Value;
            if (cell.surfaceDef == null || cell.isEdgeLane) continue;

            if (!grid.TryGetValue((z + 1, lane), out CellState forward)) continue;
            if (forward.surfaceDef == null || forward.isEdgeLane) continue;

            if (config.weightRules.IsNeighborAllowed(cell.surfaceDef, forward.surfaceDef, Direction.Forward))
                continue;

            Debug.LogError($"\n=== VIOLATION ===");
            Debug.LogError($"Position: ({z},{lane}) → ({z + 1},{lane})");
            Debug.LogError($"Tiles: {cell.surfaceDef.ID} → {forward.surfaceDef.ID} (Forward)");
            Debug.LogError($"Reverse (Backward): " +
                $"{config.weightRules.IsNeighborAllowed(forward.surfaceDef, cell.surfaceDef, Direction.Backward)}");

            List<float> weights;
            var allowed = config.weightRules.GetAllowedNeighbors(
                cell.surfaceDef, Direction.Forward, allSurfaces, out weights);
            Debug.LogError($"{cell.surfaceDef.ID} allowed Forward neighbors: " +
                string.Join(", ", allowed.Select(d => d.ID)));

            // Show noise context for both cells
            if (config.useBiomeSystem)
            {
                Debug.LogError($"Noise at ({z},{lane}): {SampleNoise(z, lane):F4} | " +
                    $"at ({z + 1},{lane}): {SampleNoise(z + 1, lane):F4}");
            }

            bool found = config.weightRules.TryGetEntry(
                cell.surfaceDef.ID, cell.surfaceDef.Layer,
                out NeighborRulesConfig.NeighborEntry rules);

            Debug.LogError(found
                ? $"Rules for '{cell.surfaceDef.ID}': {rules.allowed.Count} allowed, {rules.denied.Count} denied"
                : $"NO CACHED RULES found for '{cell.surfaceDef.ID}' — check ID mismatch or missing entry");

            if (rules != null)
                foreach (var a in rules.allowed)
                    Debug.LogError($"  → {a.neighborID} (directions: {a.directions})");

            return; // Stop at first violation
        }

        Debug.Log("No violations found!");
    }

    void PrintStatistics()
    {
        Debug.Log("\n--- Performance Statistics ---");

        if (frameTimeHistory.Count > 0)
        {
            Debug.Log($"Frame time: Avg={frameTimeHistory.Average():F1}ms, " +
                $"Max={frameTimeHistory.Max():F1}ms, Target={maxFrameTimeMs}ms");
        }

        Debug.Log($"Total chunks generated: {totalChunksGenerated}");
        Debug.Log($"Total cells collapsed: {totalCellsCollapsed}");

        if (totalWFCIterations > 0)
        {
            Debug.Log($"Total WFC iterations: {totalWFCIterations}");
            Debug.Log($"Avg iterations/chunk: {(float)totalWFCIterations / Mathf.Max(1, totalChunksGenerated):F1}");
        }
    }
}
