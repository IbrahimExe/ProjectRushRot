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
    [SerializeField] private bool showSurfaceTypes = true;
    [SerializeField] private bool showWFCState = false;
    [SerializeField] private bool showOccupants = true;
    [SerializeField] private bool showEdgeLanes = true;
    [SerializeField] private bool showEdgeWallSegments = true;

    [Header("Debug Output")]
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
    
    // Stats tracking
    private int totalChunksGenerated = 0;

    void Awake()
    {
        generator = GetComponent<RunnerLevelGenerator>();

        // Cache all reflection handles up front
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        gridField = typeof(RunnerLevelGenerator).GetField("grid", flags);
        goldenPathField = typeof(RunnerLevelGenerator).GetField("goldenPathSet", flags);
        configField = typeof(RunnerLevelGenerator).GetField("config", flags);
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

    // ============================================================
    // Gizmo Rendering
    // ============================================================

    private float LaneToWorldX(int lane, int totalLanes, float laneWidth)
    {
        float centerLane = (totalLanes - 1) * 0.5f;
        return (lane - centerLane) * laneWidth;
    }

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

        bool hasNoiseCandidate = config.catalog.Definitions.Any(d => d.Layer == ObjectLayer.Surface && d.isNoiseCandidate && d.noiseChannel != null);
        if (!hasNoiseCandidate) Debug.LogError("✗ No Surface tile has isNoiseCandidate=true with a valid noiseChannel!");

        bool hasSafePath = config.catalog.Definitions.Any(d => d.Layer == ObjectLayer.Surface && d.SurfaceType == SurfaceType.SafePath);
        if (!hasSafePath) Debug.LogError("✗ No SafePath surface tile found in catalog!");

        // Print a best-effort snapshot without hard-binding to any one config version.
        var sb = new StringBuilder();
        sb.Append($"  Noise scale={config.worldNoiseScale}");

        Debug.Log(sb.ToString());
        return hasNoiseCandidate && hasSafePath;
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
            if (cell.occupant == OccupantType.None || cell.occupantDef == null || cell.isEdgeLane) continue;

            void Check(int tz, int tl, Direction dir)
            {
                if (!grid.TryGetValue((tz, tl), out CellState neighbor)) return;
                if (neighbor.occupant == OccupantType.None || neighbor.occupantDef == null || neighbor.isEdgeLane) return;

                if (!config.weightRules.IsNeighborAllowed(cell.occupantDef, neighbor.occupantDef, dir))
                {
                    violations++;
                    if (logConstraintViolations)
                        Debug.LogWarning($"⚠ Violation ({z},{lane})→({tz},{tl}) [{dir}]: " +
                            $"{cell.occupantDef.ID} → {neighbor.occupantDef.ID}");
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

        var allOccupants = config.catalog.Definitions
            .Where(d => d.Layer == ObjectLayer.Occupant && d.OccupantType != OccupantType.EdgeWall)
            .ToList();

        Debug.Log("=== DETAILED CONSTRAINT ANALYSIS ===");

        foreach (var kvp in grid.OrderBy(k => k.Key.z).ThenBy(k => k.Key.lane))
        {
            (int z, int lane) = kvp.Key;
            CellState cell = kvp.Value;
            if (cell.occupantDef == null || cell.isEdgeLane) continue;

            if (!grid.TryGetValue((z + 1, lane), out CellState forward)) continue;
            if (forward.occupantDef == null || forward.isEdgeLane) continue;

            if (config.weightRules.IsNeighborAllowed(cell.occupantDef, forward.occupantDef, Direction.Forward))
                continue;

            Debug.LogError($"\n=== VIOLATION ===");
            Debug.LogError($"Position: ({z},{lane}) → ({z + 1},{lane})");
            Debug.LogError($"Occupants: {cell.occupantDef.ID} → {forward.occupantDef.ID} (Forward)");
            Debug.LogError($"Reverse (Backward): " +
                $"{config.weightRules.IsNeighborAllowed(forward.occupantDef, cell.occupantDef, Direction.Backward)}");

            List<float> weights;
            var allowed = config.weightRules.GetAllowedNeighbors(
                cell.occupantDef, Direction.Forward, allOccupants, out weights);
            Debug.LogError($"{cell.occupantDef.ID} allowed Forward neighbors: " +
                string.Join(", ", allowed.Select(d => d.ID)));

            bool found = config.weightRules.TryGetEntry(
                cell.occupantDef.ID, cell.occupantDef.Layer,
                out NeighborRulesConfig.NeighborEntry rules);

            Debug.LogError(found
                ? $"Rules for '{cell.occupantDef.ID}': {rules.allowed.Count} allowed, {rules.denied.Count} denied"
                : $"NO CACHED RULES found for '{cell.occupantDef.ID}' — check ID mismatch or missing entry");

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
    }
}
