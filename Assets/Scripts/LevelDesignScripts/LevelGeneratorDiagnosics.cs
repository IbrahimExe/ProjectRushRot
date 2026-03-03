// ============================================================
// Level Generator Diagnostic Tool
// Add this as a component alongside RunnerLevelGenerator to
// visualize and verify that generation is working correctly.
// ============================================================

using LevelGenerator.Data;
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
    [SerializeField] private bool showBlendZone = true;   // WFC safe-path blend area
    [SerializeField] private bool showWFCState = false;  // uncollapsed cells
    [SerializeField] private bool showOccupants = true;
    [SerializeField] private bool showEdgeLanes = true;
    [SerializeField] private bool showEdgeWalls = true;   // L / R wall coverage

    [Header("Debug Output")]
    [SerializeField] private bool logConstraintViolations = true;
    [SerializeField] private bool logEdgeWallGeneration = true;

    [Header("Performance Monitoring")]
    [SerializeField] private bool trackPerformance = true;
    [SerializeField] private int maxFrameTimeMs = 16;  // 60 fps target

    private RunnerLevelGenerator generator;
    private Queue<float> frameTimeHistory = new Queue<float>();

    // Reflection handles — computed once in Awake
    private FieldInfo gridField;
    private FieldInfo goldenPathField;
    private FieldInfo configField;

    // Stats
    private int totalChunksGenerated = 0;

    // ============================================================
    // Lifecycle
    // ============================================================

    void Awake()
    {
        generator = GetComponent<RunnerLevelGenerator>();
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        gridField = typeof(RunnerLevelGenerator).GetField("grid", flags);
        goldenPathField = typeof(RunnerLevelGenerator).GetField("goldenPathSet", flags);
        configField = typeof(RunnerLevelGenerator).GetField("config", flags);
    }

    void Update()
    {
        if (!trackPerformance) return;
        float ft = Time.deltaTime * 1000f;
        frameTimeHistory.Enqueue(ft);
        if (frameTimeHistory.Count > 60) frameTimeHistory.Dequeue();
        if (ft > maxFrameTimeMs)
            Debug.LogWarning($"[Performance] Frame spike: {ft:F1}ms (target: {maxFrameTimeMs}ms)");
    }

    // ============================================================
    // Typed accessors
    // ============================================================

    private Dictionary<(int z, int lane), CellState> GetGrid() =>
        gridField?.GetValue(generator) as Dictionary<(int z, int lane), CellState>;

    private HashSet<(int z, int lane)> GetGoldenPath() =>
        goldenPathField?.GetValue(generator) as HashSet<(int z, int lane)>;

    private RunnerGenConfig GetConfig() =>
        configField?.GetValue(generator) as RunnerGenConfig;

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
    // Gizmo helpers
    // ============================================================

    private float LaneToWorldX(int lane, int totalLanes, float laneWidth)
    {
        float center = (totalLanes - 1) * 0.5f;
        return (lane - center) * laneWidth;
    }

    // ============================================================
    // OnDrawGizmos
    // ============================================================

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

        // Blend zone: lanes within safePathBlendRadius of a golden-path cell
        var blendZone = new HashSet<(int, int)>();
        if (showBlendZone && goldenPath != null && config.safePathBlendRadius > 0)
        {
            foreach (var gp in goldenPath)
            {
                for (int off = 1; off <= config.safePathBlendRadius; off++)
                {
                    blendZone.Add((gp.z, gp.lane - off));
                    blendZone.Add((gp.z, gp.lane + off));
                }
            }
        }

        foreach (var kvp in grid)
        {
            (int z, int lane) = kvp.Key;
            CellState cell = kvp.Value;

            Vector3 pos = new Vector3(
                LaneToWorldX(lane, totalLanes, laneWidth),
                0.1f,
                z * cellLength);

            // ── Golden path ───────────────────────────────────────────────────
            if (showGoldenPath && goldenPath != null && goldenPath.Contains((z, lane)))
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(pos + Vector3.up * 0.5f,
                    new Vector3(laneWidth * 0.9f, 1f, cellLength * 0.9f));
            }

            // ── Safe-path WFC blend zone ──────────────────────────────────────
            if (showBlendZone && blendZone.Contains((z, lane)) && !cell.isEdgeLane)
            {
                Gizmos.color = new Color(0f, 1f, 0.5f, 0.25f);
                Gizmos.DrawCube(pos, new Vector3(laneWidth * 0.85f, 0.08f, cellLength * 0.85f));
            }

            // ── Edge lanes ────────────────────────────────────────────────────
            if (showEdgeLanes && cell.isEdgeLane)
            {
                // Left = dark blue, Right = dark red
                bool isLeft = lane == 0;
                Gizmos.color = isLeft
                    ? new Color(0.1f, 0.1f, 0.6f, 0.5f)
                    : new Color(0.6f, 0.1f, 0.1f, 0.5f);
                Gizmos.DrawCube(pos, new Vector3(laneWidth * 0.95f, 0.1f, cellLength * 0.95f));
            }

            // ── Surface types ─────────────────────────────────────────────────
            if (showSurfaceTypes && !cell.isEdgeLane)
            {
                Color surfaceColor = cell.surface switch
                {
                    SurfaceType.Normal => Color.white,
                    SurfaceType.Hole => Color.black,
                    SurfaceType.SafePath => Color.green,
                    SurfaceType.EdgeL => new Color(0.2f, 0.2f, 0.8f),
                    SurfaceType.EdgeR => new Color(0.8f, 0.2f, 0.2f),
                    _ => Color.grey
                };

                // Tint by noise tier if available
                if (cell.surfaceDef != null && cell.surfaceDef.noiseTier > 0)
                {
                    float t = Mathf.Clamp01(cell.surfaceDef.noiseTier / 8f);
                    surfaceColor = Color.Lerp(surfaceColor, Color.cyan, t * 0.3f);
                }

                Gizmos.color = new Color(surfaceColor.r, surfaceColor.g, surfaceColor.b, 0.5f);
                Gizmos.DrawCube(pos, new Vector3(laneWidth * 0.7f, 0.05f, cellLength * 0.7f));
            }

            // ── WFC state — uncollapsed cells ─────────────────────────────────
            if (showWFCState && !cell.isCollapsed)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(pos + Vector3.up * 0.5f, 0.2f);
            }

            // ── Occupants ─────────────────────────────────────────────────────
            if (showOccupants && cell.hasOccupant && !cell.isEdgeLane)
            {
                // Color by tag if available, otherwise use a neutral orange
                Color occupantColor = new Color(1f, 0.5f, 0f); // default: orange
                if (cell.occupantDef != null)
                {
                    if (cell.occupantDef.HasTag("wall")) occupantColor = Color.red;
                    else if (cell.occupantDef.HasTag("collectible")) occupantColor = Color.yellow;
                    else if (cell.occupantDef.HasTag("enemy")) occupantColor = Color.magenta;
                    else if (cell.occupantDef.HasTag("walkable")) occupantColor = new Color(0.5f, 1f, 0.5f);
                }

                Gizmos.color = occupantColor;
                Gizmos.DrawSphere(pos + Vector3.up * 0.5f, 0.3f);
            }

            // ── Edge walls (L / R) ────────────────────────────────────────────
            if (showEdgeWalls && cell.isEdgeLane && cell.hasOccupant)
            {
                bool isLeft = lane == 0;
                Gizmos.color = isLeft
                    ? new Color(0.3f, 0.3f, 1f, 0.8f)
                    : new Color(1f, 0.3f, 0.3f, 0.8f);
                Gizmos.DrawWireCube(pos + Vector3.up * 0.5f,
                    new Vector3(laneWidth * 0.9f, 1f, cellLength * 0.95f));
                Gizmos.color = new Color(
                    isLeft ? 0.2f : 0.8f,
                    0.2f,
                    isLeft ? 0.8f : 0.2f,
                    0.2f);
                Gizmos.DrawCube(pos,
                    new Vector3(laneWidth * 0.8f, 0.1f, cellLength * 0.9f));
            }
        }
    }

    // ============================================================
    // Verification suite
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
        allPassed &= VerifySafePathDef();

        Debug.Log(allPassed
            ? "<color=green>✓ ALL VERIFICATION CHECKS PASSED</color>"
            : "<color=red>✗ SOME VERIFICATION CHECKS FAILED — see above for details</color>");

        PrintStatistics();
    }

    // ── Configuration ─────────────────────────────────────────────────────────

    bool VerifyConfiguration()
    {
        Debug.Log("\n--- Configuration Check ---");

        var config = GetConfig();
        if (config == null) { Debug.LogError("✗ Config is null!"); return false; }
        if (config.catalog == null) { Debug.LogError("✗ Catalog is null!"); return false; }
        if (config.weightRules == null) { Debug.LogError("✗ WeightRules is null!"); return false; }
        if (config.laneCount < 3) Debug.LogWarning($"⚠ Lane count is very low: {config.laneCount}");

        // noiseChannel is now on the catalog, not per-def
        if (config.catalog.noiseChannel == null)
            Debug.LogWarning("⚠ Catalog has no noiseChannel — surfaces will use fallback only.");

        bool hasNoiseCandidate = config.catalog.Definitions
            .Any(d => d.Layer == ObjectLayer.Surface && d.isNoiseCandidate);
        if (!hasNoiseCandidate)
            Debug.LogError("✗ No surface tile has isNoiseCandidate=true!");

        bool hasSafePath = config.catalog.Definitions
            .Any(d => d.Layer == ObjectLayer.Surface && d.SurfaceType == SurfaceType.SafePath);
        if (!hasSafePath)
            Debug.LogError("✗ No SafePath surface tile found in catalog!");

        bool hasLeftWalls = config.catalog.leftWallPrefabs != null && config.catalog.leftWallPrefabs.Count > 0;
        bool hasRightWalls = config.catalog.rightWallPrefabs != null && config.catalog.rightWallPrefabs.Count > 0;
        if (!hasLeftWalls) Debug.LogWarning("⚠ catalog.leftWallPrefabs is empty — left edge will have no walls.");
        if (!hasRightWalls) Debug.LogWarning("⚠ catalog.rightWallPrefabs is empty — right edge will have no walls.");

        var sb = new StringBuilder();
        sb.Append($"  noiseScale={config.worldNoiseScale}  ");
        sb.Append($"safePathBlendRadius={config.safePathBlendRadius}  ");
        sb.Append($"lanes={config.laneCount}  chunkSize={config.chunkSize}");
        Debug.Log(sb.ToString());

        return hasNoiseCandidate && hasSafePath;
    }

    // ── Safe-path def ─────────────────────────────────────────────────────────

    bool VerifySafePathDef()
    {
        Debug.Log("\n--- Safe Path Def Check ---");

        var config = GetConfig();
        if (config?.catalog == null) { Debug.LogWarning("⚠ Missing config/catalog"); return true; }

        var safePathDef = config.catalog.Definitions
            .FirstOrDefault(d => d.Layer == ObjectLayer.Surface && d.SurfaceType == SurfaceType.SafePath);

        if (safePathDef == null)
        {
            Debug.LogWarning("⚠ No SafePath PrefabDef in catalog — generator will use debugSafePath fallback.");
            if (config.catalog.debugSafePath == null)
                Debug.LogError("✗ debugSafePath fallback is also null — safe path will be invisible!");
            return false;
        }

        bool hasPrefabs = safePathDef.Prefabs != null && safePathDef.Prefabs.Count > 0;
        if (!hasPrefabs)
            Debug.LogWarning($"⚠ SafePath def '{safePathDef.ID}' has no prefabs assigned.");
        else
            Debug.Log($"✓ SafePath def '{safePathDef.ID}' with {safePathDef.Prefabs.Count} prefab variant(s).");

        return hasPrefabs;
    }

    // ── Golden path integrity ─────────────────────────────────────────────────

    bool VerifyGoldenPathIntegrity()
    {
        Debug.Log("\n--- Golden Path Integrity Check ---");

        var goldenPath = GetGoldenPath();
        if (goldenPath == null || goldenPath.Count == 0)
        {
            Debug.LogWarning("⚠ Golden path is empty — has generation started?");
            return true;
        }

        var sorted = goldenPath.OrderBy(p => p.z).ToList();
        int gaps = 0;

        for (int i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].z - sorted[i - 1].z > 1)
            {
                gaps++;
                Debug.LogWarning($"⚠ Path gap: Z {sorted[i - 1].z} → {sorted[i].z}");
            }
        }

        if (gaps == 0)
        {
            Debug.Log($"✓ Golden path continuous: {goldenPath.Count} cells");
            return true;
        }
        Debug.LogError($"✗ Golden path has {gaps} gap(s)!");
        return false;
    }

    // ── Walkability ───────────────────────────────────────────────────────────

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
            // A row is walkable if at least one non-edge, non-hole cell has no blocking occupant
            bool hasWalkable = row.Any(c =>
                !c.Value.isEdgeLane &&
                c.Value.surface != SurfaceType.Hole &&
                (!c.Value.hasOccupant ||
                 (c.Value.occupantDef != null && c.Value.occupantDef.HasTag("walkable"))));

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

    // ── Constraint compliance (occupants) ─────────────────────────────────────

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

            // Only check cells with real occupant defs
            if (!cell.hasOccupant || cell.occupantDef == null || cell.isEdgeLane) continue;

            void Check(int tz, int tl, Direction dir)
            {
                if (!grid.TryGetValue((tz, tl), out CellState neighbor)) return;
                if (!neighbor.hasOccupant || neighbor.occupantDef == null || neighbor.isEdgeLane) return;

                if (!config.weightRules.IsNeighborAllowed(cell.occupantDef, neighbor.occupantDef, dir))
                {
                    violations++;
                    if (logConstraintViolations)
                        Debug.LogWarning($"⚠ Occupant violation ({z},{lane})→({tz},{tl}) [{dir}]: " +
                            $"{cell.occupantDef.ID} → {neighbor.occupantDef.ID}");
                }
            }

            Check(z + 1, lane, Direction.Forward);
            Check(z, lane + 1, Direction.Right);
        }

        if (violations == 0) { Debug.Log("✓ No occupant constraint violations detected"); return true; }
        Debug.LogError($"✗ {violations} occupant constraint violation(s) found!");
        return false;
    }

    // ── Edge lane integrity ───────────────────────────────────────────────────

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
            { violations++; Debug.LogError($"✗ Lane {lane} should be edge but isEdgeLane=false!"); }

            if (!shouldBeEdge && cell.isEdgeLane)
            { violations++; Debug.LogError($"✗ Lane {lane} is marked edge but shouldn't be!"); }

            // Edge lanes must have EdgeL or EdgeR surface type
            if (cell.isEdgeLane && cell.surface != SurfaceType.EdgeL && cell.surface != SurfaceType.EdgeR)
            { violations++; Debug.LogError($"✗ Edge lane ({kvp.Key.z},{lane}) has unexpected surface: {cell.surface}"); }

            // Lane 0 must be EdgeL, last lane must be EdgeR
            if (lane == 0 && cell.isEdgeLane && cell.surface != SurfaceType.EdgeL)
                Debug.LogWarning($"⚠ Left edge lane has surface {cell.surface} instead of EdgeL");

            if (lane == totalLanes - 1 && cell.isEdgeLane && cell.surface != SurfaceType.EdgeR)
                Debug.LogWarning($"⚠ Right edge lane has surface {cell.surface} instead of EdgeR");
        }

        if (violations == 0) { Debug.Log("✓ Edge lanes correctly configured"); return true; }
        Debug.LogError($"✗ {violations} edge lane violation(s)!");
        return false;
    }

    // ── Edge wall coverage ────────────────────────────────────────────────────

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

        int leftWalls = AnalyzeEdgeLane(grid, leftLane, "Left");
        int rightWalls = AnalyzeEdgeLane(grid, rightLane, "Right");

        // Check catalog lists
        if (config.catalog.leftWallPrefabs == null || config.catalog.leftWallPrefabs.Count == 0)
            Debug.LogWarning("⚠ catalog.leftWallPrefabs is empty — left wall won't spawn anything.");
        if (config.catalog.rightWallPrefabs == null || config.catalog.rightWallPrefabs.Count == 0)
            Debug.LogWarning("⚠ catalog.rightWallPrefabs is empty — right wall won't spawn anything.");

        if (leftWalls == 0 && rightWalls == 0)
        {
            Debug.LogError("✗ No wall occupants found in either edge lane — edge walls may not be generating.");
            return false;
        }

        Debug.Log($"✓ Edge wall cells: Left={leftWalls}, Right={rightWalls}");
        return true;
    }

    /// Returns count of edge-lane cells that have hasOccupant=true.
    private int AnalyzeEdgeLane(Dictionary<(int z, int lane), CellState> grid, int lane, string label)
    {
        var cells = grid.Where(k => k.Key.lane == lane).OrderBy(k => k.Key.z).ToList();
        int walls = cells.Count(c => c.Value.hasOccupant);
        int empty = cells.Count(c => !c.Value.hasOccupant);

        Debug.Log($"{label} edge: {walls} wall cells, {empty} empty, {cells.Count} total");
        return walls;
    }

    // ============================================================
    // Detailed violation debugger
    // ============================================================

    [ContextMenu("Debug Specific Violation")]
    public void DebugSpecificViolation()
    {
        var grid = GetGrid();
        var config = GetConfig();
        if (grid == null || config == null) return;

        var allOccupants = config.catalog.GetAllOccupants();

        Debug.Log("=== DETAILED CONSTRAINT ANALYSIS ===");

        foreach (var kvp in grid.OrderBy(k => k.Key.z).ThenBy(k => k.Key.lane))
        {
            (int z, int lane) = kvp.Key;
            CellState cell = kvp.Value;
            if (!cell.hasOccupant || cell.occupantDef == null || cell.isEdgeLane) continue;

            if (!grid.TryGetValue((z + 1, lane), out CellState forward)) continue;
            if (!forward.hasOccupant || forward.occupantDef == null || forward.isEdgeLane) continue;

            if (config.weightRules.IsNeighborAllowed(cell.occupantDef, forward.occupantDef, Direction.Forward))
                continue;

            Debug.LogError($"\n=== VIOLATION ===");
            Debug.LogError($"Position: ({z},{lane}) → ({z + 1},{lane})");
            Debug.LogError($"Occupants: {cell.occupantDef.ID} → {forward.occupantDef.ID} (Forward)");
            Debug.LogError($"Reverse check (Backward allowed?): " +
                $"{config.weightRules.IsNeighborAllowed(forward.occupantDef, cell.occupantDef, Direction.Backward)}");

            List<float> weights;
            var allowed = config.weightRules.GetAllowedNeighbors(
                cell.occupantDef, Direction.Forward, allOccupants, out weights);
            Debug.LogError($"{cell.occupantDef.ID} allowed Forward neighbors: " +
                string.Join(", ", allowed.Select(d => d.ID)));

            // Inspect rules directly from the cache
            // (TryGetEntry removed — iterate occupantRules list instead)
            var rule = config.weightRules.occupantRules
                .FirstOrDefault(r => r.selfID == cell.occupantDef.ID);
            if (rule == null)
                Debug.LogError($"NO rule entry for '{cell.occupantDef.ID}' — check ID mismatch or missing entry");
            else
            {
                Debug.LogError($"Rules for '{cell.occupantDef.ID}': " +
                    $"{rule.allowed.Count} allowed, {rule.denied.Count} denied");
                foreach (var a in rule.allowed)
                    Debug.LogError($"  → allowed: {a.neighborID} ({a.directions})");
                foreach (var d in rule.denied)
                    Debug.LogError($"  ✗ denied: {d.neighborID} ({d.directions})");
            }

            return; // Stop at first violation
        }

        Debug.Log("No occupant violations found.");
    }

    // ============================================================
    // Statistics
    // ============================================================

    void PrintStatistics()
    {
        Debug.Log("\n--- Performance Statistics ---");
        if (frameTimeHistory.Count > 0)
            Debug.Log($"Frame time: Avg={frameTimeHistory.Average():F1}ms, " +
                $"Max={frameTimeHistory.Max():F1}ms, Target={maxFrameTimeMs}ms");
        Debug.Log($"Total chunks generated: {totalChunksGenerated}");
    }
}