// ============================================================
// Level Generator Diagnostic Tool
// Add this as a new component to help visualize and verify
// that your level generator is working correctly
// ============================================================

using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using LevelGenerator.Data;

[RequireComponent(typeof(RunnerLevelGenerator))]
public class LevelGeneratorDiagnostics : MonoBehaviour
{
    [Header("Visualization")]
    [SerializeField] private bool showGoldenPath = true;
    [SerializeField] private bool showBiomes = true;
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
    private Dictionary<BiomeType, Color> biomeColors;
    private Queue<float> frameTimeHistory = new Queue<float>();

    // Stats tracking
    private int totalChunksGenerated = 0;
    private int totalCellsCollapsed = 0;
    private int totalWFCIterations = 0;
    private int totalConstraintViolations = 0;

    [ContextMenu("Debug Specific Violation")]
    public void DebugSpecificViolation()
    {
        var gridField = typeof(RunnerLevelGenerator).GetField("grid",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var grid = gridField?.GetValue(generator) as Dictionary<(int z, int lane), CellState>;

        var configField = typeof(RunnerLevelGenerator).GetField("config",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var config = configField?.GetValue(generator) as RunnerGenConfig;

        if (grid == null || config == null) return;

        Debug.Log("=== DETAILED CONSTRAINT ANALYSIS ===");

        // Find first violation
        foreach (var kvp in grid.OrderBy(k => k.Key.z).ThenBy(k => k.Key.lane))
        {
            (int z, int lane) = kvp.Key;
            CellState cell = kvp.Value;

            if (cell.surfaceDef == null || cell.isEdgeLane) continue;

            // Check forward
            if (grid.TryGetValue((z + 1, lane), out CellState forward))
            {
                if (forward.surfaceDef != null && !forward.isEdgeLane)
                {
                    bool allowed = config.weightRules.IsNeighborAllowed(
                        cell.surfaceDef, forward.surfaceDef, Direction.Forward);

                    if (!allowed)
                    {
                        Debug.LogError($"\n=== VIOLATION FOUND ===");
                        Debug.LogError($"Position: ({z}, {lane}) → ({z + 1}, {lane})");
                        Debug.LogError($"Tiles: {cell.surfaceDef.ID} → {forward.surfaceDef.ID}");
                        Debug.LogError($"Direction: Forward");

                        // Check if reverse is allowed
                        bool reverseAllowed = config.weightRules.IsNeighborAllowed(
                            forward.surfaceDef, cell.surfaceDef, Direction.Backward);
                        Debug.LogError($"Reverse check ({forward.surfaceDef.ID} ← {cell.surfaceDef.ID} Backward): {reverseAllowed}");

                        // Check what IS allowed from source
                        var allSurfaces = config.catalog.Definitions.Where(d => d.Layer == ObjectLayer.Surface).ToList();
                        List<float> weights;
                        var allowedForward = config.weightRules.GetAllowedNeighbors(
                            cell.surfaceDef, Direction.Forward, allSurfaces, out weights);

                        Debug.LogError($"{cell.surfaceDef.ID} CAN have these Forward neighbors:");
                        foreach (var def in allowedForward)
                        {
                            Debug.LogError($"  - {def.ID}");
                        }

                        // Check rules
                        Debug.LogError($"\nChecking rules for {cell.surfaceDef.ID}:");
                        var rules = config.weightRules.surfaceRules.FirstOrDefault(r => r.selfID == cell.surfaceDef.ID);
                        if (rules != null)
                        {
                            Debug.LogError($"  Allowed rules ({rules.allowed.Count}):");
                            foreach (var a in rules.allowed)
                            {
                                Debug.LogError($"    → {a.neighborID} (directions: {a.directions})");
                            }
                        }
                        else
                        {
                            Debug.LogError($"  NO RULES DEFINED for {cell.surfaceDef.ID}!");
                        }

                        return; // Stop at first violation for detailed analysis
                    }
                }
            }
        }

        Debug.Log("No violations found!");
    }

    void Awake()
    {
        generator = GetComponent<RunnerLevelGenerator>();
        InitializeBiomeColors();
    }

    void InitializeBiomeColors()
    {
        biomeColors = new Dictionary<BiomeType, Color>
        {
            { BiomeType.Default, Color.gray },
            { BiomeType.Grassy, Color.green },
            { BiomeType.Rocky, new Color(0.5f, 0.5f, 0.5f) },
            { BiomeType.Sandy, new Color(0.96f, 0.87f, 0.7f) },
            { BiomeType.Crystalline, new Color(0.5f, 0.9f, 1f) },
            { BiomeType.Swampy, new Color(0.2f, 0.4f, 0.3f) },
            { BiomeType.Volcanic, new Color(1f, 0.3f, 0f) }
        };
    }

    void Update()
    {
        if (trackPerformance)
        {
            float frameTime = Time.deltaTime * 1000f;
            frameTimeHistory.Enqueue(frameTime);
            if (frameTimeHistory.Count > 60) frameTimeHistory.Dequeue();

            if (frameTime > maxFrameTimeMs)
            {
                Debug.LogWarning($"[Performance] Frame time spike: {frameTime:F1}ms (target: {maxFrameTimeMs}ms)");
            }
        }
    }

    void OnDrawGizmos()
    {
        if (generator == null) return;
        if (!Application.isPlaying) return;

        // Access private grid via reflection (for diagnostic purposes)
        var gridField = typeof(RunnerLevelGenerator).GetField("grid",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (gridField == null) return;

        var grid = gridField.GetValue(generator) as Dictionary<(int z, int lane), CellState>;
        if (grid == null) return;

        var goldenPathField = typeof(RunnerLevelGenerator).GetField("goldenPathSet",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var goldenPath = goldenPathField?.GetValue(generator) as HashSet<(int z, int lane)>;

        var biomeMapField = typeof(RunnerLevelGenerator).GetField("biomeMap",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var biomeMap = biomeMapField?.GetValue(generator) as Dictionary<(int z, int lane), BiomeType>;

        // Get config for dimensions
        var configField = typeof(RunnerLevelGenerator).GetField("config",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var config = configField?.GetValue(generator) as RunnerGenConfig;

        if (config == null) return;

        float laneWidth = config.laneWidth;
        float cellLength = config.cellLength;
        int totalLanes = config.laneCount + 2; // Include edge lanes

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
                Gizmos.DrawWireCube(pos + Vector3.up * 0.5f, new Vector3(laneWidth * 0.9f, 1f, cellLength * 0.9f));
            }

            // Edge Lanes
            if (showEdgeLanes && cell.isEdgeLane)
            {
                Gizmos.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                Gizmos.DrawCube(pos, new Vector3(laneWidth * 0.95f, 0.1f, cellLength * 0.95f));
            }

            // Biomes
            if (showBiomes && biomeMap != null && biomeMap.TryGetValue((z, lane), out BiomeType biome))
            {
                if (biomeColors.TryGetValue(biome, out Color biomeColor))
                {
                    Gizmos.color = new Color(biomeColor.r, biomeColor.g, biomeColor.b, 0.9f);
                    Gizmos.DrawCube(pos + Vector3.down * 0.05f, new Vector3(laneWidth * 0.8f, 0.05f, cellLength * 0.8f));
                }
            }

            // Surface Types
            if (showSurfaceTypes)
            {
                Color surfaceColor = Color.white;
                switch (cell.surface)
                {
                    case SurfaceType.Solid: surfaceColor = Color.white; break;
                    case SurfaceType.Hole: surfaceColor = Color.black; break;
                    case SurfaceType.Bridge: surfaceColor = Color.cyan; break;
                    case SurfaceType.SafePath: surfaceColor = Color.green; break;
                    case SurfaceType.Edge: surfaceColor = Color.red; break;
                }

                Gizmos.color = new Color(surfaceColor.r, surfaceColor.g, surfaceColor.b, 0.5f);
                Gizmos.DrawCube(pos, new Vector3(laneWidth * 0.7f, 0.05f, cellLength * 0.7f));
            }

            // WFC State
            if (showWFCState && !cell.isCollapsed)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(pos + Vector3.up * 0.5f, 0.2f);
            }

            // Occupants
            if (showOccupants && cell.occupant != OccupantType.None)
            {
                Color occupantColor = Color.white;
                switch (cell.occupant)
                {
                    case OccupantType.Wall: occupantColor = Color.red; break;
                    case OccupantType.Obstacle: occupantColor = new Color(1f, 0.5f, 0f); break;
                    case OccupantType.Collectible: occupantColor = Color.yellow; break;
                    case OccupantType.Enemy: occupantColor = Color.magenta; break;
                    case OccupantType.EdgeWall: occupantColor = new Color(0.5f, 0f, 0.5f); break; // Purple
                }

                Gizmos.color = occupantColor;
                Gizmos.DrawSphere(pos + Vector3.up * 0.5f, 0.3f);
            }

            // Edge Wall Segments (show multi-row walls as connected boxes)
            if (showEdgeWallSegments && cell.occupant == OccupantType.EdgeWall && cell.occupantDef != null)
            {
                // Check if this is the origin cell (first cell of multi-row wall)
                bool isOrigin = true;
                if (z > 0 && grid.TryGetValue((z - 1, lane), out CellState prevCell))
                {
                    if (prevCell.occupant == OccupantType.EdgeWall && prevCell.occupantDef == cell.occupantDef)
                    {
                        isOrigin = false; // Previous cell has same wall, so this isn't the origin
                    }
                }

                // Only draw segment visualization from origin cell
                if (isOrigin)
                {
                    int sizeZ = cell.occupantDef.SizeZ;
                    float segmentLength = cellLength * sizeZ;
                    Vector3 segmentCenter = pos + Vector3.forward * (segmentLength - cellLength) * 0.5f;

                    // Draw wireframe box showing entire wall segment
                    Gizmos.color = new Color(0.8f, 0f, 0.8f, 0.8f); // Bright purple
                    Gizmos.DrawWireCube(
                        segmentCenter + Vector3.up * 0.5f,
                        new Vector3(laneWidth * 0.9f, 1f, segmentLength * 0.95f)
                    );

                    // Draw filled box at base
                    Gizmos.color = new Color(0.5f, 0f, 0.5f, 0.3f); // Translucent purple
                    Gizmos.DrawCube(
                        segmentCenter,
                        new Vector3(laneWidth * 0.8f, 0.1f, segmentLength * 0.9f)
                    );
                }
            }
        }
    }

    private float LaneToWorldX(int lane, int totalLanes, float laneWidth)
    {
        float centerLane = (totalLanes - 1) * 0.5f;
        return (lane - centerLane) * laneWidth;
    }

    // ============================================================
    // VERIFICATION METHODS - Call these to check system health
    // ============================================================

    [ContextMenu("Run Full System Verification")]
    public void RunFullVerification()
    {
        Debug.Log("=== LEVEL GENERATOR VERIFICATION ===");

        bool allPassed = true;

        allPassed &= VerifyConfiguration();
        allPassed &= VerifyGoldenPathIntegrity();
        allPassed &= VerifyWalkability();
        allPassed &= VerifyBiomeCoherence();
        allPassed &= VerifyConstraintCompliance();
        allPassed &= VerifyEdgeLaneIntegrity();
        allPassed &= VerifyEdgeWallCoverage();

        if (allPassed)
        {
            Debug.Log("<color=green>✓ ALL VERIFICATION CHECKS PASSED</color>");
        }
        else
        {
            Debug.LogError("<color=red>✗ SOME VERIFICATION CHECKS FAILED - See above for details</color>");
        }

        PrintStatistics();
    }

    bool VerifyConfiguration()
    {
        Debug.Log("\n--- Configuration Check ---");

        var configField = typeof(RunnerLevelGenerator).GetField("config",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var config = configField?.GetValue(generator) as RunnerGenConfig;

        if (config == null)
        {
            Debug.LogError("✗ Config is null!");
            return false;
        }

        if (config.catalog == null)
        {
            Debug.LogError("✗ Catalog is null!");
            return false;
        }

        if (config.weightRules == null)
        {
            Debug.LogError("✗ WeightRules is null!");
            return false;
        }

        if (config.laneCount < 3)
        {
            Debug.LogWarning($"⚠ Lane count is very low: {config.laneCount}");
        }

        Debug.Log($"✓ Config valid: {config.laneCount} lanes, buffer={config.bufferRows}, chunk={config.chunkSize}");
        return true;
    }

    bool VerifyGoldenPathIntegrity()
    {
        Debug.Log("\n--- Golden Path Integrity Check ---");

        var goldenPathField = typeof(RunnerLevelGenerator).GetField("goldenPathSet",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var goldenPath = goldenPathField?.GetValue(generator) as HashSet<(int z, int lane)>;

        if (goldenPath == null || goldenPath.Count == 0)
        {
            Debug.LogWarning("⚠ Golden path is empty - has generation started?");
            return true; // Not necessarily an error
        }

        // Check for gaps
        var sortedPath = goldenPath.OrderBy(p => p.z).ToList();
        int gaps = 0;

        for (int i = 1; i < sortedPath.Count; i++)
        {
            int zDiff = sortedPath[i].z - sortedPath[i - 1].z;
            if (zDiff > 1)
            {
                gaps++;
                Debug.LogWarning($"⚠ Golden path gap detected: Z {sortedPath[i - 1].z} → {sortedPath[i].z}");
            }
        }

        if (gaps == 0)
        {
            Debug.Log($"✓ Golden path continuous: {goldenPath.Count} cells");
            return true;
        }
        else
        {
            Debug.LogError($"✗ Golden path has {gaps} gaps!");
            return false;
        }
    }

    bool VerifyWalkability()
    {
        Debug.Log("\n--- Walkability Check ---");

        var gridField = typeof(RunnerLevelGenerator).GetField("grid",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var grid = gridField?.GetValue(generator) as Dictionary<(int z, int lane), CellState>;

        var configField = typeof(RunnerLevelGenerator).GetField("config",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var config = configField?.GetValue(generator) as RunnerGenConfig;

        if (grid == null || config == null)
        {
            Debug.LogWarning("⚠ Cannot verify walkability - grid or config null");
            return true;
        }

        // Group by Z row
        var rowGroups = grid.GroupBy(kvp => kvp.Key.z).OrderBy(g => g.Key);

        int blockedRows = 0;
        foreach (var row in rowGroups)
        {
            int z = row.Key;
            bool hasWalkable = false;

            foreach (var cell in row)
            {
                int lane = cell.Key.lane;
                CellState c = cell.Value;

                // Skip edge lanes
                if (c.isEdgeLane) continue;

                // Check if walkable
                bool isWalkable = c.surface != SurfaceType.Hole &&
                                 (c.occupant == OccupantType.None ||
                                  c.occupant == OccupantType.Collectible);

                if (isWalkable)
                {
                    hasWalkable = true;
                    break;
                }
            }

            if (!hasWalkable)
            {
                blockedRows++;
                Debug.LogError($"✗ Row {z} has NO walkable lanes!");
            }
        }

        if (blockedRows == 0)
        {
            Debug.Log($"✓ All {rowGroups.Count()} rows have at least one walkable lane");
            return true;
        }
        else
        {
            Debug.LogError($"✗ {blockedRows} rows are completely blocked!");
            return false;
        }
    }

    bool VerifyBiomeCoherence()
    {
        Debug.Log("\n--- Biome Coherence Check ---");

        var biomeMapField = typeof(RunnerLevelGenerator).GetField("biomeMap",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var biomeMap = biomeMapField?.GetValue(generator) as Dictionary<(int z, int lane), BiomeType>;

        var configField = typeof(RunnerLevelGenerator).GetField("config",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var config = configField?.GetValue(generator) as RunnerGenConfig;

        if (!config.useBiomeSystem)
        {
            Debug.Log("○ Biome system disabled - skipping");
            return true;
        }

        if (biomeMap == null || biomeMap.Count == 0)
        {
            Debug.LogWarning("⚠ Biome map is empty");
            return true;
        }

        // Count biome transitions
        var sortedCells = biomeMap.OrderBy(kvp => kvp.Key.z).ThenBy(kvp => kvp.Key.lane).ToList();
        int transitions = 0;

        for (int i = 1; i < sortedCells.Count; i++)
        {
            var prev = sortedCells[i - 1];
            var curr = sortedCells[i];

            if (prev.Value != curr.Value)
            {
                transitions++;
            }
        }

        float transitionRate = (float)transitions / biomeMap.Count;
        Debug.Log($"✓ Biome transitions: {transitions}/{biomeMap.Count} ({transitionRate:P1})");

        if (transitionRate > 0.5f)
        {
            Debug.LogWarning("⚠ High biome transition rate - may appear noisy");
        }

        return true;
    }

    bool VerifyConstraintCompliance()
    {
        Debug.Log("\n--- Constraint Compliance Check ---");

        var gridField = typeof(RunnerLevelGenerator).GetField("grid",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var grid = gridField?.GetValue(generator) as Dictionary<(int z, int lane), CellState>;

        var configField = typeof(RunnerLevelGenerator).GetField("config",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var config = configField?.GetValue(generator) as RunnerGenConfig;

        if (grid == null || config?.weightRules == null)
        {
            Debug.LogWarning("⚠ Cannot verify constraints - missing data");
            return true;
        }

        int violations = 0;

        foreach (var kvp in grid)
        {
            (int z, int lane) = kvp.Key;
            CellState cell = kvp.Value;

            if (cell.surfaceDef == null) continue;
            if (cell.isEdgeLane) continue; // Edge lanes don't follow neighbor rules

            // Check forward neighbor
            if (grid.TryGetValue((z + 1, lane), out CellState forward))
            {
                if (forward.surfaceDef != null && !forward.isEdgeLane)
                {
                    bool allowed = config.weightRules.IsNeighborAllowed(
                        cell.surfaceDef, forward.surfaceDef, Direction.Forward);

                    if (!allowed)
                    {
                        violations++;
                        if (logConstraintViolations)
                        {
                            Debug.LogWarning($"⚠ Constraint violation at ({z},{lane}): " +
                                $"{cell.surfaceDef.ID} → {forward.surfaceDef.ID} (Forward)");
                        }
                    }
                }
            }

            // Check right neighbor
            if (grid.TryGetValue((z, lane + 1), out CellState right))
            {
                if (right.surfaceDef != null && !right.isEdgeLane)
                {
                    bool allowed = config.weightRules.IsNeighborAllowed(
                        cell.surfaceDef, right.surfaceDef, Direction.Right);

                    if (!allowed)
                    {
                        violations++;
                        if (logConstraintViolations)
                        {
                            Debug.LogWarning($"⚠ Constraint violation at ({z},{lane}): " +
                                $"{cell.surfaceDef.ID} → {right.surfaceDef.ID} (Right)");
                        }
                    }
                }
            }
        }

        if (violations == 0)
        {
            Debug.Log("✓ No constraint violations detected");
            return true;
        }
        else
        {
            Debug.LogError($"✗ {violations} constraint violations found!");

            return false;
        }
    }

    bool VerifyEdgeLaneIntegrity()
    {
        Debug.Log("\n--- Edge Lane Integrity Check ---");

        var gridField = typeof(RunnerLevelGenerator).GetField("grid",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var grid = gridField?.GetValue(generator) as Dictionary<(int z, int lane), CellState>;

        var configField = typeof(RunnerLevelGenerator).GetField("config",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var config = configField?.GetValue(generator) as RunnerGenConfig;

        if (grid == null || config == null)
        {
            Debug.LogWarning("⚠ Cannot verify edge lanes");
            return true;
        }

        int totalLanes = config.laneCount + 2;
        int edgeViolations = 0;

        foreach (var kvp in grid)
        {
            int lane = kvp.Key.lane;
            CellState cell = kvp.Value;

            bool shouldBeEdge = (lane == 0 || lane == totalLanes - 1);

            if (shouldBeEdge && !cell.isEdgeLane)
            {
                edgeViolations++;
                Debug.LogError($"✗ Lane {lane} should be edge but isn't!");
            }

            if (!shouldBeEdge && cell.isEdgeLane)
            {
                edgeViolations++;
                Debug.LogError($"✗ Lane {lane} is edge but shouldn't be!");
            }

            if (cell.isEdgeLane && cell.surface != SurfaceType.Edge)
            {
                edgeViolations++;
                Debug.LogError($"✗ Edge lane has wrong surface type: {cell.surface}");
            }
        }

        if (edgeViolations == 0)
        {
            Debug.Log("✓ Edge lanes correctly configured");
            return true;
        }
        else
        {
            Debug.LogError($"✗ {edgeViolations} edge lane violations!");
            return false;
        }
    }

    bool VerifyEdgeWallCoverage()
    {
        if (!logEdgeWallGeneration) return true;

        Debug.Log("\n--- Edge Wall Coverage Check ---");

        var gridField = typeof(RunnerLevelGenerator).GetField("grid",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var grid = gridField?.GetValue(generator) as Dictionary<(int z, int lane), CellState>;

        var configField = typeof(RunnerLevelGenerator).GetField("config",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var config = configField?.GetValue(generator) as RunnerGenConfig;

        if (grid == null || config == null)
        {
            Debug.LogWarning("⚠ Cannot verify edge walls");
            return true;
        }

        int totalLanes = config.laneCount + 2;
        int leftLane = 0;
        int rightLane = totalLanes - 1;

        // Get all edge lane cells sorted by Z
        var leftCells = grid.Where(kvp => kvp.Key.lane == leftLane).OrderBy(kvp => kvp.Key.z).ToList();
        var rightCells = grid.Where(kvp => kvp.Key.lane == rightLane).OrderBy(kvp => kvp.Key.z).ToList();

        int leftWalls = 0, rightWalls = 0;
        int leftEmpty = 0, rightEmpty = 0;
        Dictionary<int, int> leftWallLengths = new Dictionary<int, int>();
        Dictionary<int, int> rightWallLengths = new Dictionary<int, int>();

        // Analyze left lane
        foreach (var kvp in leftCells)
        {
            if (kvp.Value.occupant == OccupantType.EdgeWall)
            {
                leftWalls++;
                int sizeZ = kvp.Value.occupantDef?.SizeZ ?? 1;
                if (!leftWallLengths.ContainsKey(sizeZ))
                    leftWallLengths[sizeZ] = 0;
                leftWallLengths[sizeZ]++;
            }
            else if (kvp.Value.occupant == OccupantType.None)
            {
                leftEmpty++;
            }
        }

        // Analyze right lane
        foreach (var kvp in rightCells)
        {
            if (kvp.Value.occupant == OccupantType.EdgeWall)
            {
                rightWalls++;
                int sizeZ = kvp.Value.occupantDef?.SizeZ ?? 1;
                if (!rightWallLengths.ContainsKey(sizeZ))
                    rightWallLengths[sizeZ] = 0;
                rightWallLengths[sizeZ]++;
            }
            else if (kvp.Value.occupant == OccupantType.None)
            {
                rightEmpty++;
            }
        }

        Debug.Log($"Left lane: {leftWalls} wall cells, {leftEmpty} empty cells (Total: {leftCells.Count})");
        Debug.Log($"Right lane: {rightWalls} wall cells, {rightEmpty} empty cells (Total: {rightCells.Count})");

        if (leftWallLengths.Count > 0)
        {
            Debug.Log("Left wall distribution:");
            foreach (var kvp in leftWallLengths.OrderBy(k => k.Key))
            {
                Debug.Log($"  SizeZ={kvp.Key}: {kvp.Value} cells");
            }
        }

        if (rightWallLengths.Count > 0)
        {
            Debug.Log("Right wall distribution:");
            foreach (var kvp in rightWallLengths.OrderBy(k => k.Key))
            {
                Debug.Log($"  SizeZ={kvp.Key}: {kvp.Value} cells");
            }
        }

        // Count unique wall segments (origin cells only)
        int leftSegments = 0, rightSegments = 0;

        for (int i = 0; i < leftCells.Count; i++)
        {
            var cell = leftCells[i].Value;
            if (cell.occupant == OccupantType.EdgeWall)
            {
                // Check if this is an origin cell (prev cell doesn't have same def)
                bool isOrigin = true;
                if (i > 0 && leftCells[i - 1].Value.occupant == OccupantType.EdgeWall
                    && leftCells[i - 1].Value.occupantDef == cell.occupantDef)
                {
                    isOrigin = false;
                }
                if (isOrigin) leftSegments++;
            }
        }

        for (int i = 0; i < rightCells.Count; i++)
        {
            var cell = rightCells[i].Value;
            if (cell.occupant == OccupantType.EdgeWall)
            {
                bool isOrigin = true;
                if (i > 0 && rightCells[i - 1].Value.occupant == OccupantType.EdgeWall
                    && rightCells[i - 1].Value.occupantDef == cell.occupantDef)
                {
                    isOrigin = false;
                }
                if (isOrigin) rightSegments++;
            }
        }

        Debug.Log($"Wall segments: Left={leftSegments}, Right={rightSegments}");

        if (leftSegments <= 1 || rightSegments <= 1)
        {
            Debug.LogError($"✗ Very few wall segments detected! Left={leftSegments}, Right={rightSegments}");
            Debug.LogError("   This suggests edge walls are only spawning once per chunk.");
            return false;
        }
        else
        {
            Debug.Log($"✓ Multiple wall segments detected in both lanes");
            return true;
        }
    }

    void PrintStatistics()
    {
        Debug.Log("\n--- Performance Statistics ---");

        if (frameTimeHistory.Count > 0)
        {
            float avgFrameTime = frameTimeHistory.Average();
            float maxFrameTime = frameTimeHistory.Max();
            Debug.Log($"Frame time: Avg={avgFrameTime:F1}ms, Max={maxFrameTime:F1}ms, Target={maxFrameTimeMs}ms");
        }

        Debug.Log($"Total chunks generated: {totalChunksGenerated}");
        Debug.Log($"Total cells collapsed: {totalCellsCollapsed}");

        if (totalWFCIterations > 0)
        {
            Debug.Log($"Total WFC iterations: {totalWFCIterations}");
            Debug.Log($"Avg iterations/chunk: {(float)totalWFCIterations / totalChunksGenerated:F1}");
        }
    }
}