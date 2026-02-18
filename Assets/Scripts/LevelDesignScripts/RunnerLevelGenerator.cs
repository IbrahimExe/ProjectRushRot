// ------------------------------------------------------------
// Runner Level Generator 
// - Generates forward-only rows ahead of the player
// - Uses local neighborhood to weight placements
// - Guarantees: each Z row has >= 1 walkable lane
// - Prefabs handle their own behavior; generator only picks what/where
// ------------------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;
using LevelGenerator.Data;
using System.Linq;

[CreateAssetMenu(fileName = "RunnerConfig", menuName = "Runner/GeneratorConfig")]
public sealed class RunnerGenConfig : ScriptableObject 
{
    [Header("Catalog Reference")]
    public PrefabCatalog catalog;
    public NeighborRulesConfig weightRules; // Kept name as requested, now NeighborRulesConfig type

    [Header("Lane Setup")]
    public int laneCount = 3;
    public float laneWidth = 2f;
    public float cellLength = 10f; 

    [Header("Generation Settings")]
    public int bufferRows = 20; // extra rows to keep loaded beyond player view
    public int keepRowsBehind = 5; // rows behind player to keep loaded
    public int chunkSize = 10; // number of rows to generate together as a chunk
    public int neigbhorhoodZ = 4;
    public int neighborhoodX = 1;

    [Header("Spawn Chances")]
    [Tooltip("Maximum 'Cost' of occupants per chunk.")]
    public int densityBudget = 50;

    [Tooltip("Chance to spawn a cell (0-1).")]
    [Range(0f, 1f)] public float globalSpawnChance = 0.15f; // Replaces legacy density/hole logic
    // [Range(0f, 1f)] public float holeChance = 0.2f; REMOVED
    // [Range(0f, 1f)] public float densityPenalty = 0.1f; REMOVED

    //[Header("WFC Settings")]
    //[Tooltip("Chance to seed a cell with a variety surface before solving.")]
    //[Range(0f, 1f)] public float surfaceVarietySeedChance = 0.1f;
   // public List<PrefabDef> varietySeedOptions;

    [Header("Biome Clustering")]
    [Tooltip("Enable biome system for coherent regions instead of noise.")]
    public bool useBiomeSystem = true;

    [Tooltip("Biome region size. Larger = bigger zones. (10-25=moderate, 30-50=huge)")]
    [Range(5f, 50f)] public float biomeNoiseScale = 15f;

    [Tooltip("Biome influence strength. 0=disabled, 1=maximum.")]
    [Range(0f, 1f)] public float biomeInfluence = 0.75f;

    [Tooltip("Use multi-octave noise for organic biome shapes.")]
    public bool useMultiOctaveBiomes = true;

    // [Header("Legacy - Removed")]
    // wallChanceEdge, obstacleChanceCenter etc removed.

    [Header("Golden Path Settings")]
    [Tooltip("Penalty for moving sideways if previous move was sideways (0-1). Higher = fewer repeated side moves.")]
    [Range(0f, 1f)] public float pathSidePenalty = 0.85f;
    [Tooltip("Penalty for Switchbacks (Left then Right immediatley). Higher = smoother curves.")]
    [Range(0f, 1f)] public float pathSwitchPenalty = 0.5f;
    [Tooltip("Chance to move forward regardless (0-1).")]
    [Range(0f, 1f)] public float pathForwardWeight = 0.6f;
    [Tooltip("Tendency to pull back to the center (0-1). Higher = stays near center.")]
    [Range(0f, 1f)] public float pathCenterBias = 0.2f;

    [Header("Golden Path Wave")]
    public bool useWavePath = true;

    [Range(0f, 1f)] public float waveStrength = 0.35f;
    // 0 = straight center, 1 = uses full amplitude

    [Tooltip("Max drift from center in lanes (before clamping).")]
    [Range(0f, 150f)] public float waveAmplitudeLanes = 10f; 

    [Tooltip("How fast the wave changes per row. Smaller = longer turns.")]
    [Range(0.001f, 0.05f)] public float waveFrequency = 0.02f;
    // 0.02 = ~50 rows per cycle
    
    [Tooltip("How quickly we chase the target (0..1).")]
    [Range(0.01f, 1f)] public float waveSmoothing = 0.12f;

    [Tooltip("Hard limit: max lanes we can shift per row (turn rate).")]
    [Range(0.05f, 2f)] public float maxLaneChangePerRow = 0.35f;
    // 0.2-0.5 feels like smooth steering

    [Tooltip("Keep path away from edges.")]
    [Range(0, 10)] public int edgePadding = 2;

    [Header("Golden Path Rest Straights")]
    [Tooltip("Max number of rows in a straight rest segment.")]
    [Range(2, 300)] public int restAreaMaxLength = 35;

    [Tooltip("Min number of rows in a straight rest segment.")]
    [Range(2, 300)] public int restAreaMinLength = 15;

    [Tooltip("Chance per row to START a rest (0..1). Example: 0.02 ~ roughly once every ~50 rows).")]
    [Range(0f, 1f)] public float restAreaFrequency = 0.02f;

    //[Header("A* Validation Settings")]
    //[Tooltip("Penalty added when the path changes lateral direction (discourages zig-zag).")]
    //[Range(0f, 10f)] public float aStarTurnCost = 0f;

    [Header("Options")]
    public bool allowHoles = true;
    public bool allowBridges = true;
}


//main class
public class RunnerLevelGenerator : MonoBehaviour
{
    [SerializeField] private RunnerGenConfig config;

    [Header("Seeding")]
    [Tooltip("Seed for deterministic generation")]
    public string seed = "hjgujklfu"; // Hardcoded default


    private Dictionary<(int z, int lane), CellState> grid;
    private int generatorZ = 0;
    private System.Random rng;
    // private WeightSystem weightSystem; // REMOVED

    private Queue<(int z, int lane)> propagationQueue = new Queue<(int z, int lane)>();
    // Rest-straight state
    private int restRowsRemaining = 0;

    // Wave phase counter (ADVANCES only when wave is active)
    // This is what "stretches" the wave during rests.
    private int wavePhaseZ = 0;

    // Optional tiny cooldown to avoid rest segments back-to-back
    private int restCooldown = 0;


    // Golden Path State
    private HashSet<(int z, int lane)> goldenPathSet = new HashSet<(int, int)>();
    private int pathTipZ = -1;
    private int pathTipLane = 1; 
    //private int lastPathMove = 0; // 0=Forward, -1=Left, 1=Right
    private float pathLaneF;          // continuous lane value we lerp
    //private int laneCooldownTimer = 0;
    private float waveSeedOffset;     // makes Perlin deterministic per seed

    [Header("References")]
    [SerializeField] private Transform playerTransform;

    // OPTIMIZATION: Track spawned objects for cleanup
    // Currently missing - need this to pool/destroy spawned objects
    private Dictionary<(int z, int lane), GameObject> spawnedObjects = new Dictionary<(int, int), GameObject>();
    private Dictionary<(int z, int lane), GameObject> spawnedOccupant = new();

    // Biome System
    private Dictionary<(int z, int lane), BiomeType> biomeMap = new Dictionary<(int, int), BiomeType>();
    private float biomeNoiseOffsetX;
    private float biomeNoiseOffsetZ;




    void Start()
    {
        if (config == null)
        {
            Debug.LogError("you forgot to assign the runner config dummy.");
           

            enabled = false;    
            return;
        }
        Debug.Log($"[Runner] laneCount={config.laneCount}, edgePadding={config.edgePadding}, waveAmp={config.waveAmplitudeLanes}, waveStrength={config.waveStrength}, waveFreq={config.waveFrequency}");

        grid = new Dictionary<(int, int), CellState>();
        if (!string.IsNullOrEmpty(seed))
        { 
            rng = new System.Random(seed.GetHashCode());
        }
        else
        {
            rng = new System.Random(); // fallback
        }

        // Initialize Biome System
        biomeNoiseOffsetX = (seed != null ? seed.GetHashCode() : 0) * 0.01f;
        biomeNoiseOffsetZ = (seed != null ? (seed.GetHashCode() * 7919) : 0) * 0.01f;

        // Initialize Golden Path
        pathTipLane = config.laneCount / 2;
        pathLaneF = pathTipLane;
        restRowsRemaining = 0;
        restCooldown = 0;
        wavePhaseZ = 0;

        // Deterministic perlin offset from seed
        waveSeedOffset = (seed != null ? seed.GetHashCode() : 0) * 0.001f;
        pathTipZ = -1;

        // Snap Player to Center/Start
        if (playerTransform != null)
        {
            float centerX = LaneToWorldX(pathTipLane);
            Vector3 pPos = playerTransform.position;
            pPos.x = centerX;
            
            // Handle CharacterController which overrides position assignments
            CharacterController cc = playerTransform.GetComponent<CharacterController>();
            if (cc != null) {
                cc.enabled = false;
                playerTransform.position = pPos;
                cc.enabled = true;
            }
            else {
                playerTransform.position = pPos;
            }
        }
        
        if(config.catalog != null) config.catalog.RebuildCache();

        // Generate initial buffer
        UpdatePathBuffer(config.bufferRows + 10); // Plan path ahead of generation

        // Generate initial chunks
        while (generatorZ < config.bufferRows)
        {
            int chunkStart = generatorZ;
            int actualChunkSize = Mathf.Min(config.chunkSize, config.bufferRows - generatorZ);
            GenerateChunk(chunkStart, actualChunkSize);
            generatorZ += actualChunkSize;
        }
    }


    
    void Update()
    {
        if (playerTransform != null)
        {
            //if player uis not onside update skip mathj
            UpdateGeneration(playerTransform.position.z);
        }
    }

    private PlacementContext CreateContext(int z, int lane)
    {
        CellState cell = getCell(z, lane);

        return new PlacementContext
        {
            position = (z, lane),
            currentSurface = cell.surface,
            currentOccupant = cell.occupant,
            GetCell = (checkZ, checkLane) => getCell(checkZ, checkLane),
            IsOnGoldenPath = (pos) => goldenPathSet.Contains(pos),
            laneCount = config.laneCount,
            playerZIndex = playerTransform != null ?
                Mathf.FloorToInt(playerTransform.position.z / config.cellLength) : 0
        };
    }

    private void UpdatePathBuffer(int targetZ)
    {
        while (pathTipZ < targetZ)
        {
            pathTipZ++;

            // If wave mode is off, just extend current lane
            if (!config.useWavePath)
            {
                goldenPathSet.Add((pathTipZ, pathTipLane));
                continue;
            }

            // --- Rest segment logic (straight line) ---
            // countdown cooldown so we don't start rests back-to-back
            if (restCooldown > 0) restCooldown--;

            // If we're currently resting: keep lane constant, DO NOT advance wavePhaseZ
            if (restRowsRemaining > 0)
            {
                restRowsRemaining--;
                goldenPathSet.Add((pathTipZ, pathTipLane));

                // after a rest ends, add a small cooldown
                if (restRowsRemaining == 0)
                    restCooldown = Mathf.Max(0, config.restAreaMinLength / 2);

                continue;
            }

            // Possibly start a new rest (only if not in cooldown)
            if (restCooldown == 0 && Rand() < config.restAreaFrequency)
            {
                int minLen = Mathf.Max(2, config.restAreaMinLength);
                int maxLen = Mathf.Max(minLen, config.restAreaMaxLength);

                restRowsRemaining = rng.Next(minLen, maxLen + 1);
                goldenPathSet.Add((pathTipZ, pathTipLane));
                continue;
            }

            // --- Wave path (active) ---
            // ADVANCE wave phase ONLY when wave is active
            wavePhaseZ++;

            float centerLane = (config.laneCount - 1) * 0.5f;

            // Multi-octave perlin
            float z = wavePhaseZ; // IMPORTANT: use wavePhaseZ, not pathTipZ

            float n1 = Mathf.PerlinNoise(z * config.waveFrequency, waveSeedOffset + 10.0f);
            float n2 = Mathf.PerlinNoise(z * (config.waveFrequency * 2.2f), waveSeedOffset + 77.7f);

            float w1 = (n1 * 2f) - 1f;
            float w2 = (n2 * 2f) - 1f;

            float wave = (w1 * 0.85f) + (w2 * 0.15f);

            float amplitude = config.waveAmplitudeLanes * config.waveStrength;
            float targetLaneF = centerLane + wave * amplitude;

            float minLaneF = config.edgePadding;
            float maxLaneF = (config.laneCount - 1) - config.edgePadding;

            // Safety: prevent collapsed lane range
            if (minLaneF >= maxLaneF)
            {
                minLaneF = 0f;
                maxLaneF = config.laneCount - 1;
            }

            targetLaneF = Mathf.Clamp(targetLaneF, minLaneF, maxLaneF);

            // Smoothly chase the target lane
            pathLaneF = Mathf.Lerp(pathLaneF, targetLaneF, config.waveSmoothing);

            // Turrn-rate limit
            float limitedNext = Mathf.MoveTowards(
                (float)pathTipLane,
                pathLaneF,
                config.maxLaneChangePerRow
            );

            int nextLane = Mathf.Clamp(Mathf.RoundToInt(limitedNext), (int)minLaneF, (int)maxLaneF);

            // CONNECTIVITY FIX:
            // If we jumped more than 1 lane (e.g. 2 -> 4), we must fill the gap (3) to ensure diagonal connectivity is not broken.
            // Although maxLaneChangePerRow < 1 prevents this, aggressive settings might break it.
            if (Mathf.Abs(nextLane - pathTipLane) > 1)
            {
                int direction = (int)Mathf.Sign(nextLane - pathTipLane);
                int fill = pathTipLane + direction;
                while (fill != nextLane)
                {
                    goldenPathSet.Add((pathTipZ, fill)); // Bridge the gap
                    fill += direction;
                }
            }

            pathTipLane = nextLane;

            goldenPathSet.Add((pathTipZ, pathTipLane));
        }
    }



private float LaneToWorldX(int lane)
    {
        float centerLane = (config.laneCount - 1) * 0.5f;
        return (lane - centerLane) * config.laneWidth;
    }


    //FUNCTIONS

    // ============================================
    // BIOME SYSTEM
    // ============================================

    /// <summary>
    /// Get biome type for a cell using Perlin noise
    /// </summary>
    private BiomeType GetBiomeForCell(int z, int lane)
    {
        if (!config.useBiomeSystem) return BiomeType.Default;

        // Check cache
        if (biomeMap.TryGetValue((z, lane), out BiomeType cached))
            return cached;

        // Sample Perlin noise
        float x = lane / config.biomeNoiseScale;
        float zCoord = z / config.biomeNoiseScale;

        float noise;
        if (config.useMultiOctaveBiomes)
        {
            // Multi-octave for organic shapes
            float n1 = Mathf.PerlinNoise(biomeNoiseOffsetX + x, biomeNoiseOffsetZ + zCoord);
            float n2 = Mathf.PerlinNoise(biomeNoiseOffsetX + 100 + x * 2.1f, biomeNoiseOffsetZ + 100 + zCoord * 2.1f);
            noise = n1 * 0.7f + n2 * 0.3f;
        }
        else
        {
            noise = Mathf.PerlinNoise(biomeNoiseOffsetX + x, biomeNoiseOffsetZ + zCoord);
        }

        // Map to biome types (customize these thresholds)
        BiomeType biome;
        if (noise < 0.25f)
            biome = BiomeType.Rocky;
        else if (noise < 0.5f)
            biome = BiomeType.Grassy;
        else if (noise < 0.75f)
            biome = BiomeType.Sandy;
        else
            biome = BiomeType.Crystalline;

        biomeMap[(z, lane)] = biome;
        return biome;
    }

    /// <summary>
    /// Get weight multiplier based on biome affinity
    /// </summary>
    private float GetBiomeAffinityWeight(PrefabDef surfaceDef, BiomeType biome)
    {
        if (!config.useBiomeSystem || surfaceDef == null)
            return 1.0f;

        float affinity = surfaceDef.GetBiomeAffinity(biome);

        // Lerp between neutral and affinity based on influence
        return Mathf.Lerp(1.0f, affinity, config.biomeInfluence);
    }



    private bool LaneIsBorder(int lane)
    {
        return lane == 0 || lane == config.laneCount - 1;
    }

    private bool LaneIsCenter(int lane) //NOTE: make the center the player at the start
    {
        int mid = (config.laneCount - 1) / 2;
        return lane == mid || (lane == mid + (config.laneCount % 2 == 0 ? 1 : 0));
    }

    float Rand()
    {
        return (float)rng.NextDouble();
    }

    private float Clamp(float v)
    {
        return Mathf.Clamp01(v);
    }

    //Grid

    private CellState getCell(int z, int lane)
    {
        var key = (z, lane);
        if (grid.TryGetValue(key, out CellState state))
        {
            return state;
        }
        return new CellState(SurfaceType.Solid, OccupantType.None);
    }

    private void SetCell(int z, int lane, CellState state)
    {
        grid[(z, lane)] = state;
    }

    //Validate

    private bool IsWalkable(int z, int lane)
    {
        CellState c = getCell(z, lane);
        return (c.surface != SurfaceType.Hole) && (c.occupant == OccupantType.None || c.occupant == OccupantType.Collectible);
        //IS ALSO CHECKING Collectible/Enemy as if they shouldn't block
    }

    private bool RowHasAnyWalkable(int z)
    {
        for (int lane = 0; lane < config.laneCount; lane++)
        {
            if (IsWalkable(z, lane))
            {
                return true;
            }
        }
        return false;
    }

    //Connectivity Check (A*) now useless 
    //// Checks if there is a valid path from the start of the current context window to the target z
    //private bool IsConnectedToStart(int targetZ)
    //{
    //    // 1. Identify start point: The player's current row or the beginning of the buffer?
    //    // For a continuous generator, we just need to ensure the NEW row connections to the EXISTING valid geometry.
    //    // We look back 'neighborhoodZ' rows. If we can path from (z - neighborhood) to (z), we are good.
        
    //    int startZ = Mathf.Max(0, targetZ - config.neigbhorhoodZ);
    //    if (startZ == targetZ) return true; // First row is always valid

    //    // Find a walkable start node in startZ
    //    (int z, int lane) startNode = (-1, -1);
    //    for(int l=0; l<config.laneCount; l++) {
    //        if (IsWalkable(startZ, l)) {
    //            startNode = (startZ, l);
    //            break; 
    //        }
    //    }
    //    if (startNode.z == -1) return false; // Previous area was fully blocked? Should not happen if we maintain invariant.

    //    // Find a walkable end node in targetZ
    //    (int z, int lane) endNode = (-1, -1);
    //    for(int l=0; l<config.laneCount; l++) {
    //        if (IsWalkable(targetZ, l)) {
    //            endNode = (targetZ, l);
    //            break;
    //        }
    //    }
    //    if (endNode.z == -1) return false; // Target row is fully blocked

    //    // Check path WIRE VALUES HERE
    //    var path = PathfindingHelper.FindPath(
    //      startNode,
    //      endNode,
    //      0, config.laneCount - 1,
    //      IsWalkable,
    //      turnCost: config.aStarTurnCost
    //  );


    //    return path != null && path.Count > 0;
    //}

    //Neighbors check - REMOVED (Legacy Density/Count methods)


    //Scoring

    // --- WFC & Generation Logic ---

    // Removed ScoreWall, ScoreObstacle, ScoreHole (Obsolete)

    private bool WouldBreakRowIfPlaced(int z, int lane, CellState newState)
    {
        CellState old = getCell(z, lane);
        SetCell(z, lane, newState);
        bool ok = RowHasAnyWalkable(z);
        SetCell(z, lane, old);
        return !ok;
    }

    private void GenerateChunk(int startZ, int chunkSize)
    {
        int endZ = startZ + chunkSize;

        // ===================================================
        // PASS 1: Initialize WFC State
        // ===================================================
        for (int z = startZ; z < endZ; z++)
        {
            InitializeRowForWFC(z);
        }

        // ===================================================
        // PASS 2: GOLDEN PATH (ALWAYS FIRST - HIGHEST PRIORITY)
        // ===================================================
        // This MUST happen before WFC solving so the safe path
        // acts as a constraint that WFC works around
        for (int z = startZ; z < endZ; z++)
        {
            PreCollapseSafePath(z);
        }

        // ===================================================
        // PASS 3: Solve Surfaces with WFC
        // ===================================================
        // Now WFC fills in the rest, respecting the golden path
        SolveChunkSurfaces(startZ, endZ);

        // ===================================================
        // PASS 4: Optional Variety Seeding
        // ===================================================
        // This can add specific tiles after WFC if desired
        // (Currently seeds BEFORE solving, so may want to remove this)
        // for (int z = startZ; z < endZ; z++)
        // {
        //     SeedSurfaceVariety(z);
        // }

        // ===================================================
        // PASS 5: Occupants
        // ===================================================
        for (int z = startZ; z < endZ; z++)
        {
            GenerateOccupants(z);
        }

        // ===================================================
        // PASS 6: Spawn Visuals
        // ===================================================
        for (int z = startZ; z < endZ; z++)
        {
            SpawnRowVisuals(z);
        }
    }

    private void InitializeRowForWFC(int z)
    {
        var allSurfaces = config.catalog.Definitions
            .Where(d => d.Layer == ObjectLayer.Surface)
            .ToList();

        for (int lane = 0; lane < config.laneCount; lane++)
        {
            CellState cell = new CellState(SurfaceType.Solid, OccupantType.None);
            cell.surfaceCandidates = new List<PrefabDef>(allSurfaces);
            cell.candidateWeights = new Dictionary<PrefabDef, float>();

            // Apply biome affinity to initial weights
            BiomeType biome = GetBiomeForCell(z, lane);

            foreach (var c in allSurfaces)
            {
                float baseWeight = 1.0f;
                float biomeWeight = GetBiomeAffinityWeight(c, biome);
                cell.candidateWeights[c] = baseWeight * biomeWeight;
            }

            cell.isCollapsed = false;
            cell.entropy = CalculateEntropy(cell);
            SetCell(z, lane, cell);
        }
    }

    private void PreCollapseSafePath(int z)
    {
        for (int lane = 0; lane < config.laneCount; lane++)
        {
            if (goldenPathSet.Contains((z, lane)))
            {
                CellState cell = getCell(z, lane);
                
                // Find safe path tile or specific debug tile
                PrefabDef safePathDef = config.catalog.debugSafePath != null ? 
                    config.catalog.Definitions.FirstOrDefault(d => d.Prefabs.Contains(config.catalog.debugSafePath)) : null;
                
                // Fallback: Filter for SafePath type
                if (safePathDef == null)
                    safePathDef = cell.surfaceCandidates.FirstOrDefault(d => d.SurfaceType == SurfaceType.SafePath);

                if (safePathDef != null)
                {
                    CollapseCell(z, lane, safePathDef);
                }
            }
        }
    }

    //private void SeedSurfaceVariety(int z)
    //{
    //    if (config.varietySeedOptions == null || config.varietySeedOptions.Count == 0) return;
        
    //    for (int lane = 0; lane < config.laneCount; lane++)
    //    {
    //        if (goldenPathSet.Contains((z, lane))) continue;
            
    //        // Randomly seed
    //        if (Rand() < config.surfaceVarietySeedChance)
    //        {
    //             PrefabDef seedDef = config.varietySeedOptions[rng.Next(config.varietySeedOptions.Count)];
    //             CollapseCell(z, lane, seedDef);
    //        }
    //    }
    //}

    private void CollapseCell(int z, int lane, PrefabDef forcedDef)
    {
        CellState cell = getCell(z, lane);
        if (cell.isCollapsed) return;

        cell.surfaceDef = forcedDef;
        cell.surface = forcedDef.SurfaceType;
        cell.isCollapsed = true;
        cell.surfaceCandidates.Clear();
        cell.surfaceCandidates.Add(forcedDef);
        // Clear weights for collapsed - or just ignored
        if(cell.candidateWeights == null) cell.candidateWeights = new Dictionary<PrefabDef, float>();
        cell.candidateWeights.Clear();
        cell.candidateWeights[forcedDef] = 1.0f;
        cell.entropy = 0f;
        
        SetCell(z, lane, cell);
        propagationQueue.Enqueue((z, lane));
    }

    // --- WFC Solver Core ---

    private void SolveChunkSurfaces(int startZ, int endZ)
    {
        propagationQueue.Clear();
        
        // Add initially collapsed cells to queue (already done in PreCollapse/Seed, but ensure we catch unprocessed ones if needed)
        // Note: PreCollapse adds to queue. 

        // Main Loop
        int maxIterations = (endZ - startZ) * config.laneCount * 10;
        int iter = 0;

        while (UncollapsedCellsExist(startZ, endZ) && iter < maxIterations)
        {
            iter++;

            // 1. Propagation Step (Drain Queue)
            while (propagationQueue.Count > 0)
            {
                (int pz, int plane) = propagationQueue.Dequeue();
                Propagate(pz, plane, startZ, endZ);
            }

            // 2. Collapse Step (Pick lowest entropy)
            if (UncollapsedCellsExist(startZ, endZ))
            {
                var target = GetLowestEntropyCell(startZ, endZ);
                if (target.z != -1)
                {
                    PerformWeightedCollapse(target.z, target.lane);
                }
            }
        }

        // FORCE COLLAPSE (Safety Net)
        // If loop finished but uncollapsed cells remain (max iterations hit), force them.
        ForceCollapseRemaining(startZ, endZ);
    }
    
    private void ForceCollapseRemaining(int startZ, int endZ)
    {
        int forcedCount = 0;
        for (int z = startZ; z < endZ; z++)
        {
            for (int l = 0; l < config.laneCount; l++)
            {
                CellState c = getCell(z, l);
                if (!c.isCollapsed)
                {
                    // Force pick ANY valid candidate (or fallback)
                    PerformWeightedCollapse(z, l);
                    forcedCount++;
                }
            }
        }
        
        if (forcedCount > 0)
        {
            Debug.LogWarning($"[WFC] Force-collapsed {forcedCount} cells in chunk {startZ}-{endZ}. Iteration limit reached?");
        }
    }

    private bool UncollapsedCellsExist(int startZ, int endZ)
    {
        // Optimization: Could maintain a count, but loop is okay for chunk size
        for (int z = startZ; z < endZ; z++)
        {
            for (int l = 0; l < config.laneCount; l++)
            {
                if (!getCell(z, l).isCollapsed) return true;
            }
        }
        return false;
    }

    private (int z, int lane) GetLowestEntropyCell(int startZ, int endZ)
    {
        float minEntropy = float.MaxValue;
        (int z, int lane) best = (-1, -1);

        for (int z = startZ; z < endZ; z++)
        {
            for (int l = 0; l < config.laneCount; l++)
            {
                CellState c = getCell(z, l);
                if (!c.isCollapsed && c.entropy < minEntropy)
                {
                    minEntropy = c.entropy;
                    best = (z, l);
                }
            }
        }
        return best;
    }

    private void PerformWeightedCollapse(int z, int lane)
    {
        CellState cell = getCell(z, lane);
        if (cell.surfaceCandidates == null || cell.surfaceCandidates.Count == 0)
        {
            // Contradiction: Fallback to Debug Surface
            Debug.LogWarning($"WFC Contradiction at ({z}, {lane}). Using fallback.");
            var debugDef = config.catalog.Definitions.FirstOrDefault(d => d.Prefabs.Contains(config.catalog.debugSurface));
            if (debugDef == null) debugDef = config.catalog.Definitions.FirstOrDefault(d => d.Layer == ObjectLayer.Surface);
            
            CollapseCell(z, lane, debugDef);
            return;
        }

        // Weighted Selection
        float totalWeight = 0f;
        foreach(var c in cell.surfaceCandidates)
        {
            if (cell.candidateWeights.ContainsKey(c))
                totalWeight += cell.candidateWeights[c];
            else
                totalWeight += 1f; // Should not happen if correctly inited
        }

        float r = (float)rng.NextDouble() * totalWeight;
        float current = 0f;
        PrefabDef selected = cell.surfaceCandidates[cell.surfaceCandidates.Count - 1]; // Default last

        foreach(var c in cell.surfaceCandidates)
        {
            float w = cell.candidateWeights.ContainsKey(c) ? cell.candidateWeights[c] : 1f;
            current += w;
            if (r <= current)
            {
                selected = c;
                break;
            }
        }

        CollapseCell(z, lane, selected);
    }

    private float CalculateEntropy(CellState cell)
    {
        var candidates = cell.surfaceCandidates;
        if (candidates == null || candidates.Count == 0) return 0f;

        float sumWeights = 0f;
        float sumWLogW = 0f;

        foreach (var cand in candidates)
        {
            float w = 1.0f;
            if (cell.candidateWeights != null && cell.candidateWeights.ContainsKey(cand))
            {
                w = cell.candidateWeights[cand];
            }

            if (w <= 1e-6f) continue;

            sumWeights += w;
            sumWLogW += w * Mathf.Log(w);
        }

        if (sumWeights <= 1e-6f) return 0f;

        // Weighted Shannon Entropy
        return Mathf.Log(sumWeights) - (sumWLogW / sumWeights) + (float)rng.NextDouble() * 0.001f;
    }

    private void Propagate(int z, int lane, int chunkStartZ, int chunkEndZ)
    {
        CellState collapsed = getCell(z, lane);
        PrefabDef sourceDef = collapsed.surfaceDef;
        if (sourceDef == null) return;

        // Directions: Forward (Z+1), Backward (Z-1), Left (L-1), Right (L+1)

        CheckNeighbor(z, lane, z + 1, lane, Direction.Forward, sourceDef, chunkStartZ, chunkEndZ);
        CheckNeighbor(z, lane, z - 1, lane, Direction.Backward, sourceDef, chunkStartZ, chunkEndZ);
        CheckNeighbor(z, lane, z, lane - 1, Direction.Left, sourceDef, chunkStartZ, chunkEndZ);
        CheckNeighbor(z, lane, z, lane + 1, Direction.Right, sourceDef, chunkStartZ, chunkEndZ);
    }

    private void CheckNeighbor(int sz, int sl, int tz, int tl, Direction dir, PrefabDef sourceDef, int minZ, int maxZ)
    {
        // Bounds check
        if (tz < minZ || tz >= maxZ || tl < 0 || tl >= config.laneCount) return;

        CellState target = getCell(tz, tl);
        if (target.isCollapsed) return;

        // Get allowed neighbors from config (RETURNS RELATIVE WEIGHTS)
        var allSurfaces = config.catalog.Definitions.Where(d => d.Layer == ObjectLayer.Surface).ToList();
        List<float> relativeWeights;
        List<PrefabDef> allowedBySource = config.weightRules.GetAllowedNeighbors(sourceDef, dir, allSurfaces, out relativeWeights);
        
        // Map weights for O(1)
        Dictionary<PrefabDef, float> incomingWeights = new Dictionary<PrefabDef, float>();
        for(int i=0; i<allowedBySource.Count; i++) incomingWeights[allowedBySource[i]] = relativeWeights[i];

        bool changed = false;
        
        for (int i = target.surfaceCandidates.Count - 1; i >= 0; i--)
        {
            PrefabDef cand = target.surfaceCandidates[i];
            
            if (!incomingWeights.TryGetValue(cand, out float w))
            {
                // Not allowed by neighbor -> Remove
                target.surfaceCandidates.RemoveAt(i);
                if(target.candidateWeights.ContainsKey(cand)) target.candidateWeights.Remove(cand);
                changed = true;
            }
            else
            {
                if (!target.candidateWeights.ContainsKey(cand)) target.candidateWeights[cand] = 1.0f;

                float oldW = target.candidateWeights[cand];

                // Apply biome affinity during propagation
                BiomeType targetBiome = GetBiomeForCell(tz, tl);
                float biomeAffinity = GetBiomeAffinityWeight(cand, targetBiome);
                target.candidateWeights[cand] = oldW * w * biomeAffinity;

                if (Mathf.Abs(oldW - target.candidateWeights[cand]) > 0.0001f) changed = true;
            }
        }

        if (changed)
        {
            if (target.surfaceCandidates.Count == 0)
            {
                // Contradiction
                // Debug.Log($"Contradiction found at {tz},{tl} from {sz},{sl} ({dir})");
            }
            
            target.entropy = CalculateEntropy(target); // Re-calc entropy
            SetCell(tz, tl, target);
            
            if (!propagationQueue.Contains((tz, tl)))
                propagationQueue.Enqueue((tz, tl));
        }
    }


    // --- Occupant Generation ---

    // --- Occupant Generation (Budget System) ---

    // Generate occupants based on Surface Compatibility and Density Budget
    private void GenerateOccupants(int z)
    {
        // 1. Setup Candidates
        var allOccupants = config.catalog.Definitions
            .Where(d => d.Layer == ObjectLayer.Occupant)
            .ToList();

        // 2. Budget Tracking
        // We track budget per ROW for simplicity in this function, 
        // but ideally it should be per chunk. since we call this row-by-row,
        // we might reset it? The user asked for "density budget" which implies a limit.
        // If we reset per row, it's consistent.
        // Let's assume the config.densityBudget is per CHUNK or per 10 rows?
        // Simpler: Per Row budget = Total / ChunkSize? 
        // Or just a probability check + Cost check.
        // Let's implement a "Current Row Cost" vs "Max Row Cost" derived from Density.
        // MaxCostPerRow = config.densityBudget / config.chunkSize? 
        // Let's use config.densityBudget as "Max Cost Per Row" for now to be safe, 
        // or just accumulate and stop if we hit a global limit? 
        // A "Global Spawn Chance" is already a regulator. 
        // Let's use Budget as a hard cap per row to prevent clumping.
        
        int currentCost = 0;
        int maxCost = config.densityBudget; // Treat as Per-Row or Per-Generation-Pass limit

        // 3. Iterate Lanes
        // Scramble lanes to avoid left-to-right bias in budget consumption
        var lanes = Enumerable.Range(0, config.laneCount).OrderBy(x => rng.Next()).ToList();

        foreach (int lane in lanes)
        {
            CellState c = getCell(z, lane);
            
            // A. Surface Check (Must be valid surface)
            if (c.surface == SurfaceType.Hole) continue;
            // Additional: Check 'AllowedSurfaces' implicitly later

            // B. Existing Occupant Check (Golden Path or Pre-placed)
            if (c.occupant != OccupantType.None) 
            {
                if (c.occupantDef != null) currentCost += c.occupantDef.Cost;
                continue;
            }

            // C. Global Spawn Chance (The "Attempt" roll)
            if (rng.NextDouble() > config.globalSpawnChance) continue;

            // D. Filter Candidates
            List<PrefabDef> candidates = new List<PrefabDef>();
            foreach(var def in allOccupants)
            {
                // 1. Budget Check
                if (currentCost + def.Cost > maxCost) continue;

                // 2. Allowed Surface Check
                // If list is empty -> Allowed on ALL (except Holes, handled above)
                if (def.AllowedSurfaceIDs != null && def.AllowedSurfaceIDs.Count > 0)
                {
                    // If surfaceDef is missing, we can't check ID.
                    if (c.surfaceDef == null) continue; 
                    if (!def.AllowedSurfaceIDs.Contains(c.surfaceDef.ID)) continue;
                }

                // 3. Walkability for Golden Path
                if (goldenPathSet.Contains((z, lane)) && !def.IsWalkable) continue;

                candidates.Add(def);
            }

            if (candidates.Count == 0) continue;

            // E. Weighted Selection
            // Use OccupantWeight from PrefabDef
            float totalWeight = 0f;
            foreach(var cand in candidates) totalWeight += cand.OccupantWeight;

            double r = rng.NextDouble() * totalWeight;
            float sum = 0f;
            PrefabDef selected = null;

            foreach(var cand in candidates)
            {
                sum += cand.OccupantWeight;
                if (r <= sum)
                {
                    selected = cand;
                    break;
                }
            }

            // F. Spawn
            if (selected != null)
            {
                CellState newState = c;
                newState.occupant = selected.OccupantType;
                newState.occupantDef = selected;
                SetCell(z, lane, newState);
                
                currentCost += selected.Cost;
            }
        }
    }

        




    private void SpawnRowVisuals(int z)
    {
        for (int lane = 0; lane < config.laneCount; lane++)
        {
            var cell = getCell(z, lane);
            Vector3 worldPos = new Vector3(LaneToWorldX(lane), 0, z * config.cellLength);
            
            // SURFACE
            GameObject surfaceObj = null;
            if (cell.surfaceDef != null && cell.surfaceDef.Prefabs.Count > 0)
            {
                surfaceObj = Instantiate(cell.surfaceDef.Prefabs[rng.Next(cell.surfaceDef.Prefabs.Count)], worldPos, Quaternion.identity, transform);
            }
            // Fallback for SafePath if WFC didn't find specific tile (should not happen if PreCollapse worked)
            else if (cell.surface == SurfaceType.SafePath && config.catalog.debugSafePath != null)
            {
                surfaceObj = Instantiate(config.catalog.debugSafePath, worldPos, Quaternion.identity, transform);
            }
            // Debug Fallback
            else if (config.catalog.debugSurface != null)
            {
                surfaceObj = Instantiate(config.catalog.debugSurface, worldPos, Quaternion.identity, transform);
            }

            if (surfaceObj != null) spawnedObjects[(z, lane)] = surfaceObj;

            // OCCUPANT
            GameObject occObj = null;
            if (cell.occupantDef != null && cell.occupantDef.Prefabs.Count > 0)
            {
                occObj = Instantiate(cell.occupantDef.Prefabs[rng.Next(cell.occupantDef.Prefabs.Count)], worldPos, Quaternion.identity, transform);
            }
            // Fallback
            else if (cell.occupant != OccupantType.None && config.catalog.debugOccupant != null)
            {
                occObj = Instantiate(config.catalog.debugOccupant, worldPos, Quaternion.identity, transform);
            }

            if (occObj != null) spawnedOccupant[(z, lane)] = occObj;
        }
    }

    public void UpdateGeneration(float playerZWorld)
    {
        //
        int playerZIndex = Mathf.FloorToInt(playerZWorld / config.cellLength);
        int targetZ = playerZIndex + config.bufferRows;

        // Ensure we have a path planned WAY ahead (target + chunk + extra)
        // because we might generate a full chunk that goes beyond targetZ
        UpdatePathBuffer(targetZ + config.chunkSize + 20);

        while (generatorZ < targetZ)
        {
            // FORCE FULL CHUNK
            // Instead of clamping to targetZ, we just generate a full chunk.
            // This means we might generate a bit ahead of the buffer, which is good for WFC coherence.
            int chunkStart = generatorZ;
            int actualChunkSize = config.chunkSize; 
            
            GenerateChunk(chunkStart, actualChunkSize);
            generatorZ += actualChunkSize;
        }

        // Cleanup old rows
        int minKeepZ = playerZIndex - config.keepRowsBehind;
        List<(int, int)> toRemove = new List<(int, int)>();

        foreach (var key in grid.Keys)
        {
            if (key.z < minKeepZ)
            {
                toRemove.Add(key);
            }
        }

        foreach (var key in toRemove)
        {
            if (spawnedObjects.TryGetValue(key, out GameObject surf) && surf != null)
            {
                Destroy(surf);
                spawnedObjects.Remove(key);
            }

            if (spawnedOccupant.TryGetValue(key, out GameObject occ) && occ != null)
            {
                Destroy(occ);
                spawnedOccupant.Remove(key);
            }

            grid.Remove(key);
        }
        //garbsage collection is gonna hitch when it needs to clean up the destroyed objects

    }

    // MISSING FEATURE: No way to call UpdateGeneration from outside
    // Add a public reference to player transform or make this a service

    // OPTIMIZATION: Gizmos for debugging in Scene view


}

