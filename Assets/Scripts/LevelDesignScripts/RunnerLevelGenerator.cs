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

    [Header("Biome Clustering")]
    [Tooltip("Turns biome regions on/off. When OFF, tiles ignore biome affinities and only neighbor rules affect surfaces.")]
    [SerializeField] public bool useBiomeSystem = true;

    [Tooltip("Controls the SIZE of biome regions in grid cells. Higher = larger, smoother zones. Lower = smaller, noisier patches.")]
    [Range(5f, 50f)] public float biomeNoiseScale = 15f;

    [Tooltip("How strongly the chosen biome influences tile selection. 0 = biome has no effect, 1 = biome fully applies tile affinities.")]
    [Range(0f, 1f)] public float biomeInfluence = 0.75f;

    [Tooltip("When ON, combines multiple layers of noise (octaves) for more natural, less 'blocky' biome shapes.")]
    public bool useMultiOctaveBiomes = true;

    [Header("Biome Noise Quality")]
    [Tooltip("Number of noise layers combined. More = richer detail, but too high can add speckle. Typical: 3–5.")]
    [Range(1, 8)] public int biomeOctaves = 4;

    [Tooltip("Frequency multiplier per octave. Higher = more detail each octave. Typical: ~2.0. Too high can cause busy patterns.")]
    [Range(1.2f, 3.5f)] public float biomeLacunarity = 2.0f;

    [Tooltip("Amplitude multiplier per octave. Lower = smoother (less high-frequency influence). Higher = more detail/speckle. Typical: 0.45–0.6.")]
    [Range(0.2f, 0.8f)] public float biomeGain = 0.5f;

    [Tooltip("How much we 'warp' the noise coordinates to create rounder, more organic regions. 0 = no warp. Too high = swirly/distorted.")]
    [Range(0f, 3f)] public float biomeWarpStrength = 0.9f;

    [Tooltip("Scale of the warp field. Lower = broad bends. Higher = tighter twisting. Typical: 1.5–3.0.")]
    [Range(0.5f, 6f)] public float biomeWarpScale = 2.0f;

    [Tooltip("Extra smoothing by averaging nearby noise samples. 0 = none. Higher = softer boundaries, fewer tiny islands. Too high = muddy transitions.")]
    [Range(0f, 1f)] public float biomeBlur = 0.35f;
    
    [Tooltip("temperature > 1 flattens differences -> more variety.")]
    [SerializeField]public float temperature = 1.9f; // try 1.4–2.6 (higher = more variety)

    [Tooltip("floor prevents any biome from becoming impossible")]
    [SerializeField]public float floor = 0.04f;      // try 0.02–0.10 (higher = rarer biomes show up more)

    [Header("Biome - WFC Coupling")]
    [Tooltip("Extra biome bias when WFC collapses a cell. 1 = no extra. Higher = biomes win more often.")]
    [Range(1f, 8f)] public float biomeCollapseBias = 2.5f;

    [Tooltip("Optional: remove candidates that are very off-biome. 0 keeps all. 0.1–0.25 makes biomes much more consistent.")]
    [Range(0f, 0.6f)] public float biomeMinAffinity = 0.12f;





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

    [Header("Options")]
    public bool allowHoles = true;
    public bool allowBridges = true;

    [Header("Edge Lanes")]
    [Tooltip("Prefab spawned on the two boundary lanes every row to form the side walls. " +
             "If null, falls back to catalog.debugEdgeWall.")]
    public GameObject edgeWallPrefab;

}


//main class
public class RunnerLevelGenerator : MonoBehaviour
{
    [SerializeField] private RunnerGenConfig config;

    // CONSTANTS - Centralized configuration values

    // Path generation
    private const int PATH_BUFFER_PADDING = 10;         // Extra rows to plan golden path ahead

    // WFC solver
    private const int WFC_MAX_ITERATIONS_PER_CELL = 10; // Max iterations per cell before force-collapse
    private const float ENTROPY_EPSILON = 1e-6f;        // Minimum weight to consider valid
    private const float ENTROPY_TIEBREAKER = 0.001f;    // Random noise added to entropy for tie-breaking

    // Weight calculations
    private const float WEIGHT_EPSILON = 0.0001f;       // Threshold for detecting weight changes

    // Perlin noise offsets (for wave and biome)
    private const float PERLIN_SEED_SCALE = 0.001f;     // Scale factor for converting seed to Perlin offset
    private const float PERLIN_WAVE_OFFSET = 10.0f;     // Base offset for wave Perlin noise
    private const float PERLIN_BIOME_OFFSET = 100f;     // Base offset for biome Perlin noise


    [Header("Seeding")]
    [Tooltip("Seed for deterministic generation")]
    public string seed = "hjgujklfu"; // Hardcoded default
    
    private Dictionary<(int z, int lane), CellState> grid;
    private int generatorZ = 0;
    private System.Random rng;

    private Queue<(int z, int lane)> propagationQueue = new Queue<(int z, int lane)>();
    private HashSet<(int z, int lane)> queuedCells = new HashSet<(int, int)>();

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
    // Tracks the wall GameObjects on the two edge lanes for cleanup alongside surface/occupant objects.
    private Dictionary<(int z, int lane), GameObject> spawnedEdgeWalls = new Dictionary<(int, int), GameObject>();

    // Total physical lane count = playable lanes + 2 edge lanes (one on each side).
    // Lane index 0 and (TotalLaneCount - 1) are always edge lanes.
    // Playable lanes run from index 1 to (TotalLaneCount - 2) inclusive.
    private int TotalLaneCount => config.laneCount + 2;
    private bool IsEdgeLaneIndex(int lane) => lane == 0 || lane == TotalLaneCount - 1;

    // Biome System
    private Dictionary<(int z, int lane), BiomeType> biomeMap = new Dictionary<(int, int), BiomeType>();
    private float biomeNoiseOffsetX;
    private float biomeNoiseOffsetZ;




    void Start()
    {
        if (!ValidateConfiguration())
        {
            Debug.LogError("[RunnerLevelGenerator] Configuration validation failed. Generator not enabled.");
            enabled = false;
            return;
        }

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
        waveSeedOffset = (seed != null ? seed.GetHashCode() : 0) * PERLIN_SEED_SCALE;
        pathTipZ = -1;

        // Snap Player to Center/Start
        if (playerTransform != null)
        {
            float centerX = LaneToWorldX(pathTipLane);
            Vector3 pPos = playerTransform.position;
            pPos.x = centerX;

            // Handle CharacterController which overrides position assignments
            CharacterController cc = playerTransform.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                playerTransform.position = pPos;
                cc.enabled = true;
            }
            else
            {
                playerTransform.position = pPos;
            }
        }

        if (config.catalog != null) config.catalog.RebuildCache();

        // Generate initial buffer
        UpdatePathBuffer(config.bufferRows + PATH_BUFFER_PADDING);

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

            float n1 = Mathf.PerlinNoise(z * config.waveFrequency, waveSeedOffset + PERLIN_WAVE_OFFSET);
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
        // Center is computed over ALL physical lanes (playable + 2 edge lanes)
        // so that the playable corridor sits symmetrically between the walls.
        float centerLane = (TotalLaneCount - 1) * 0.5f;
        return (lane - centerLane) * config.laneWidth;
    }

    //FUNCTIONS
    // BIOME SYSTEM
    // Get biome type for a cell using Perlin noise

    private BiomeType GetBiomeForCell(int z, int lane)
    {
        if (!config.useBiomeSystem) return BiomeType.Default;

        if (biomeMap.TryGetValue((z, lane), out BiomeType cached))
            return cached;

        // --- WORLD-SPACE sampling ---
        float worldX = LaneToWorldX(lane);
        float worldZ = z * config.cellLength;

        float x = worldX / (config.biomeNoiseScale * config.laneWidth);
        float y = worldZ / (config.biomeNoiseScale * config.cellLength);

        float noise = DomainWarpedFbm01(x, y); // 0..1

        // --- Turn 1 noise value into 4 overlapping biome "scores" (g/r/s/c) ---
        // Centers evenly spaced through 0..1. These define where each biome peaks.
        const float cg = 0.125f;
        const float cr = 0.375f;
        const float cs = 0.625f;
        const float cc = 0.875f;

        // halfWidth controls how wide each biome is before it fades.
        // feather controls overlap/softness. More feather = more mixing.
        float halfWidth = 0.11f;                 // try 0.08–0.14
        float feather = Mathf.Clamp(config.biomeBlur, 0.02f, 0.18f); // re-use your blur as a softness knob

        float g = BandScore(noise, cg, halfWidth, feather);
        float r = BandScore(noise, cr, halfWidth, feather);
        float s = BandScore(noise, cs, halfWidth, feather);
        float c = BandScore(noise, cc, halfWidth, feather);

        // --- Make rarer biomes more common ---

        float wg = Mathf.Pow(Mathf.Max(config.floor, g), 1f / config.temperature);
        float wr = Mathf.Pow(Mathf.Max(config.floor, r), 1f / config.temperature);
        float ws = Mathf.Pow(Mathf.Max(config.floor, s), 1f / config.temperature);
        float wc = Mathf.Pow(Mathf.Max(config.floor, c), 1f / config.temperature);

        float sum = wg + wr + ws + wc;
        float roll = Rand() * sum;

        BiomeType biome;
        if ((roll -= wg) <= 0f) biome = BiomeType.Grassy;
        else if ((roll -= wr) <= 0f) biome = BiomeType.Rocky;
        else if ((roll -= ws) <= 0f) biome = BiomeType.Sandy;
        else biome = BiomeType.Crystalline;

        biomeMap[(z, lane)] = biome;
        return biome;
    }




    // Get weight multiplier based on biome affinity

    private float GetBiomeAffinityWeight(PrefabDef surfaceDef, BiomeType biome)
    {
        if (!config.useBiomeSystem || surfaceDef == null)
            return 1.0f;

        float affinity = surfaceDef.GetBiomeAffinity(biome);

        // Lerp between neutral and affinity based on influence
        return Mathf.Lerp(1.0f, affinity, config.biomeInfluence);
    }



    // Returns true for the outermost PLAYABLE lanes (index 1 and laneCount).
    // These are distinct from edge lanes (index 0 and TotalLaneCount-1).
    private bool LaneIsBorder(int lane)
    {
        return lane == 1 || lane == config.laneCount;
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
        // Edge lanes and holes are never walkable.
        if (c.isEdgeLane || c.surface == SurfaceType.Hole || c.surface == SurfaceType.Edge) return false;
        return c.occupant == OccupantType.None || c.occupant == OccupantType.Collectible;
    }

    private bool RowHasAnyWalkable(int z)
    {
        for (int lane = 0; lane < TotalLaneCount; lane++)
        {
            if (IsEdgeLaneIndex(lane)) continue; // Edge lanes are never walkable
            if (IsWalkable(z, lane)) return true;
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
        // Budget is shared across the whole chunk — not reset per row.
        // Edge lanes are skipped inside the method.
        GenerateOccupantsForChunk(startZ, endZ);

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

        for (int lane = 0; lane < TotalLaneCount; lane++)
        {
            // Edge lanes are structural boundaries — pre-collapse immediately as Edge
            // so WFC never touches them and they never appear as candidates for neighbors.
            if (IsEdgeLaneIndex(lane))
            {
                CellState edgeCell = new CellState(SurfaceType.Edge, OccupantType.None, edgeLane: true);
                edgeCell.isCollapsed = true;
                edgeCell.entropy = 0f;
                SetCell(z, lane, edgeCell);
                continue;
            }

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
        for (int lane = 0; lane < TotalLaneCount; lane++)
        {
            // Edge lanes are pre-collapsed as Edge surface — golden path never touches them.
            if (IsEdgeLaneIndex(lane)) continue;

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
        if (cell.candidateWeights == null) cell.candidateWeights = new Dictionary<PrefabDef, float>();
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
        queuedCells.Clear();

        // Re-seed propagation from any pre-collapsed cells (Safe Path, etc.)
        for (int z = startZ; z < endZ; z++)
        {
            for (int l = 0; l < TotalLaneCount; l++)
            {
                if (IsEdgeLaneIndex(l)) continue;

                var c = getCell(z, l);
                if (c.isCollapsed)
                {
                    if (queuedCells.Add((z, l)))
                        propagationQueue.Enqueue((z, l));
                }
            }
        }

        // Main Loop
        int maxIterations = (endZ - startZ) * config.laneCount * WFC_MAX_ITERATIONS_PER_CELL;
        int iter = 0;

        while (UncollapsedCellsExist(startZ, endZ) && iter < maxIterations)
        {
            iter++;

            // 1. Propagation Step (Drain Queue)
            while (propagationQueue.Count > 0)
            {
                (int pz, int plane) = propagationQueue.Dequeue();
                queuedCells.Remove((pz, plane));
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

    private bool ValidateConfiguration()
    {
        bool valid = true;

        if (config == null)
        {
            Debug.LogError("[RunnerLevelGenerator] Config is not assigned!", this);
            valid = false;
        }
        else
        {
            if (config.catalog == null)
            {
                Debug.LogError("[RunnerLevelGenerator] Config.catalog is not assigned!", this);
                valid = false;
            }

            if (config.weightRules == null)
            {
                Debug.LogError("[RunnerLevelGenerator] Config.weightRules is not assigned!", this);
                valid = false;
            }

            if (config.catalog != null)
            {
                var surfaces = config.catalog.Definitions?.Where(d => d.Layer == ObjectLayer.Surface).ToList();
                if (surfaces == null || surfaces.Count == 0)
                {
                    Debug.LogError("[RunnerLevelGenerator] Catalog has no Surface definitions!", this);
                    valid = false;
                }

                if (config.catalog.debugSafePath == null)
                {
                    Debug.LogWarning("[RunnerLevelGenerator] Config.catalog.debugSafePath is not assigned. Golden path will use fallback.", this);
                }

                if (config.catalog.debugSurface == null)
                {
                    Debug.LogWarning("[RunnerLevelGenerator] Config.catalog.debugSurface is not assigned. WFC contradictions may fail.", this);
                }

                if (config.catalog.debugEdgeWall == null && config.edgeWallPrefab == null)
                {
                    Debug.LogError("[RunnerLevelGenerator] Neither edgeWallPrefab nor debugEdgeWall is assigned. Edge lanes will be invisible!", this);
                    valid = false;
                }
            }

            if (config.weightRules != null && config.catalog != null)
            {
                if (config.weightRules.catalog != config.catalog)
                {
                    Debug.LogWarning("[RunnerLevelGenerator] weightRules.catalog doesn't match config.catalog. This may cause ID resolution issues.", this);
                }

                if (config.weightRules.surfaceRules == null || config.weightRules.surfaceRules.Count == 0)
                {
                    Debug.LogWarning("[RunnerLevelGenerator] weightRules has no surfaceRules. WFC may produce random results.", this);
                }
            }

            if (config.laneCount < 1)
            {
                Debug.LogError("[RunnerLevelGenerator] laneCount must be at least 1!", this);
                valid = false;
            }

            if (config.chunkSize < 1)
            {
                Debug.LogError("[RunnerLevelGenerator] chunkSize must be at least 1!", this);
                valid = false;
            }
        }

        if (playerTransform == null)
        {
            Debug.LogWarning("[RunnerLevelGenerator] playerTransform is not assigned. Level will generate but won't update dynamically.", this);
        }

        return valid;
    }

    private void ForceCollapseRemaining(int startZ, int endZ)
    {
        int forcedCount = 0;
        for (int z = startZ; z < endZ; z++)
        {
            for (int l = 0; l < TotalLaneCount; l++)
            {
                if (IsEdgeLaneIndex(l)) continue; // Edge lanes are pre-collapsed
                CellState c = getCell(z, l);
                if (!c.isCollapsed)
                {
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
        for (int z = startZ; z < endZ; z++)
        {
            for (int l = 0; l < TotalLaneCount; l++)
            {
                if (IsEdgeLaneIndex(l)) continue; // Edge lanes are pre-collapsed
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
            for (int l = 0; l < TotalLaneCount; l++)
            {
                if (IsEdgeLaneIndex(l)) continue; // Edge lanes are pre-collapsed
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
            Debug.LogWarning($"[WFC] Contradiction at ({z}, {lane}): No valid candidates remaining. " + $"Check that neighbor rules allow at least one tile combination. Using fallback.", this);
            var debugDef = config.catalog.Definitions.FirstOrDefault(d => d.Prefabs.Contains(config.catalog.debugSurface));
            if (debugDef == null) debugDef = config.catalog.Definitions.FirstOrDefault(d => d.Layer == ObjectLayer.Surface);

            CollapseCell(z, lane, debugDef);
            return;
        }

        float totalWeight = 0f;

        for (int i = cell.surfaceCandidates.Count - 1; i >= 0; i--)
        {
            var c = cell.surfaceCandidates[i];

            float w = (cell.candidateWeights != null && cell.candidateWeights.TryGetValue(c, out var cw)) ? cw : 1f;

            // --- biome coupling (the important part) ---
            float affinity = BiomeAffinity01(c, z, lane);

            // Prune: remove very off-biome candidates (but don't delete everything)
            if (config.biomeMinAffinity > 0f && affinity < config.biomeMinAffinity && cell.surfaceCandidates.Count > 1)
            {
                cell.surfaceCandidates.RemoveAt(i);
                if (cell.candidateWeights.ContainsKey(c)) cell.candidateWeights.Remove(c);
                continue;
            }

            // Boost affinity using exponent; 1 = no change, >1 strengthens biomes
            float bias = Mathf.Pow(Mathf.Max(0.0001f, affinity), config.biomeCollapseBias);

            totalWeight += w * bias;
        }

        // (If pruning happened, write back the modified cell)
        SetCell(z, lane, cell);

        float r = (float)rng.NextDouble() * totalWeight;
        float current = 0f;

        PrefabDef selected = cell.surfaceCandidates[cell.surfaceCandidates.Count - 1];
        foreach (var c in cell.surfaceCandidates)
        {
            float w = cell.candidateWeights.TryGetValue(c, out var cw) ? cw : 1f;
            float affinity = BiomeAffinity01(c, z, lane);
            float bias = Mathf.Pow(Mathf.Max(0.0001f, affinity), config.biomeCollapseBias);

            current += w * bias;
            if (r <= current) { selected = c; break; }
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

            if (w <= ENTROPY_EPSILON) continue;

            sumWeights += w;
            sumWLogW += w * Mathf.Log(w);
        }

        if (sumWeights <= ENTROPY_EPSILON) return 0f;

        // Weighted Shannon Entropy
        return Mathf.Log(sumWeights) - (sumWLogW / sumWeights) + (float)rng.NextDouble() * ENTROPY_TIEBREAKER;
    }

    private void Propagate(int z, int lane, int chunkStartZ, int chunkEndZ)
    {
        CellState collapsed = getCell(z, lane);
        PrefabDef sourceDef = collapsed.surfaceDef;
        if (sourceDef == null) return;

        // Directions: Forward (Z+1), Backward (Z-1), Left (L-1), Right (L+1)
        // Edge lanes are already collapsed so CheckNeighbor will bail out on them immediately.
        CheckNeighbor(z, lane, z + 1, lane, Direction.Forward, sourceDef, chunkStartZ, chunkEndZ);
        CheckNeighbor(z, lane, z - 1, lane, Direction.Backward, sourceDef, chunkStartZ, chunkEndZ);
        CheckNeighbor(z, lane, z, lane - 1, Direction.Left, sourceDef, chunkStartZ, chunkEndZ);
        CheckNeighbor(z, lane, z, lane + 1, Direction.Right, sourceDef, chunkStartZ, chunkEndZ);
        CheckNeighbor(z, lane, z + 1, lane - 1, Direction.ForwardLeft, sourceDef, chunkStartZ, chunkEndZ);
        CheckNeighbor(z, lane, z + 1, lane + 1, Direction.ForwardRight, sourceDef, chunkStartZ, chunkEndZ);
        CheckNeighbor(z, lane, z - 1, lane - 1, Direction.BackwardLeft, sourceDef, chunkStartZ, chunkEndZ);
        CheckNeighbor(z, lane, z - 1, lane + 1, Direction.BackwardRight, sourceDef, chunkStartZ, chunkEndZ);
    }

    private void CheckNeighbor(int sz, int sl, int tz, int tl, Direction dir, PrefabDef sourceDef, int minZ, int maxZ)
    {
        // Bounds check — use TotalLaneCount so edge lanes are included in the grid
        if (tz < minZ || tz >= maxZ || tl < 0 || tl >= TotalLaneCount) return;

        CellState target = getCell(tz, tl);
        if (target.isCollapsed) return;

        // Get allowed neighbors from config (RETURNS RELATIVE WEIGHTS)
        var allSurfaces = config.catalog.Definitions.Where(d => d.Layer == ObjectLayer.Surface).ToList();
        List<float> relativeWeights;
        List<PrefabDef> allowedBySource = config.weightRules.GetAllowedNeighbors(sourceDef, dir, allSurfaces, out relativeWeights);

        // Map weights for O(1)
        Dictionary<PrefabDef, float> incomingWeights = new Dictionary<PrefabDef, float>();
        for (int i = 0; i < allowedBySource.Count; i++) incomingWeights[allowedBySource[i]] = relativeWeights[i];

        bool changed = false;

        for (int i = target.surfaceCandidates.Count - 1; i >= 0; i--)
        {
            PrefabDef cand = target.surfaceCandidates[i];

            if (!incomingWeights.TryGetValue(cand, out float w))
            {
                // Not allowed by neighbor -> Remove
                target.surfaceCandidates.RemoveAt(i);
                if (target.candidateWeights.ContainsKey(cand)) target.candidateWeights.Remove(cand);
                changed = true;
            }
            else
            {
                if (!target.candidateWeights.ContainsKey(cand)) target.candidateWeights[cand] = 1.0f;

                float oldW = target.candidateWeights[cand];

                // Apply biome affinity during propagation
                BiomeType targetBiome = GetBiomeForCell(tz, tl);
                float biomeAffinity = GetBiomeAffinityWeight(cand, targetBiome);
                target.candidateWeights[cand] = oldW * w;

                if (Mathf.Abs(oldW - target.candidateWeights[cand]) > WEIGHT_EPSILON) changed = true;
            }
        }

        if (changed)
        {
            if (target.surfaceCandidates.Count == 0)
            {
                // Contradiction
                Debug.Log($"Contradiction found at {tz},{tl} from {sz},{sl} ({dir})");
            }

            target.entropy = CalculateEntropy(target); // Re-calc entropy
            SetCell(tz, tl, target);

            if (queuedCells.Add((tz, tl))) // Returns false if already present
            {
                propagationQueue.Enqueue((tz, tl));
            }

            if (!propagationQueue.Contains((tz, tl)))
                propagationQueue.Enqueue((tz, tl));
        }
    }


    // --- Occupant Generation ---
    private void GenerateOccupantsForChunk(int startZ, int endZ)
    {
        var allOccupants = config.catalog.Definitions
            .Where(d => d.Layer == ObjectLayer.Occupant)
            .ToList();

        if (allOccupants.Count == 0) return;

        int remainingBudget = config.densityBudget;

        // Tracks the last Z a given (lane, OccupantType) was placed to enforce MinRowGap.
        var lastSpawnedZ = new Dictionary<(int lane, OccupantType type), int>();

        // Cells reserved by multi-row occupants (SizeZ > 1).
        var reservedCells = new HashSet<(int z, int lane)>();

        for (int z = startZ; z < endZ; z++)
        {
            // Count walkable playable lanes before placing anything.
            int walkableLanes = 0;
            for (int l = 0; l < TotalLaneCount; l++)
            {
                if (IsEdgeLaneIndex(l)) continue;
                if (IsWalkable(z, l)) walkableLanes++;
            }

            var lanes = Enumerable.Range(0, TotalLaneCount).OrderBy(_ => rng.Next()).ToList();

            foreach (int lane in lanes)
            {
                // Hard skip: edge lanes never receive occupants.
                if (IsEdgeLaneIndex(lane)) continue;

                CellState c = getCell(z, lane);

                if (c.surface == SurfaceType.Hole) continue;
                if (c.occupant != OccupantType.None) continue;
                if (reservedCells.Contains((z, lane))) continue;

                if (rng.NextDouble() > config.globalSpawnChance) continue;

                var candidates = new List<PrefabDef>();
                foreach (var def in allOccupants)
                {
                    if (remainingBudget - def.Cost < 0) continue;

                    if (def.AllowedSurfaceIDs != null && def.AllowedSurfaceIDs.Count > 0)
                    {
                        if (c.surfaceDef == null) continue;
                        if (!def.AllowedSurfaceIDs.Contains(c.surfaceDef.ID)) continue;
                    }

                    if (goldenPathSet.Contains((z, lane)) && !def.IsWalkable) continue;
                    if (!def.IsWalkable && walkableLanes <= 1) continue;

                    var gapKey = (lane, def.OccupantType);
                    if (lastSpawnedZ.TryGetValue(gapKey, out int lastZ))
                        if (z - lastZ < def.MinRowGap) continue;

                    candidates.Add(def);
                }

                if (candidates.Count == 0) continue;

                float totalWeight = candidates.Sum(d => d.OccupantWeight);
                float roll = (float)(rng.NextDouble() * totalWeight);
                float acc = 0f;
                PrefabDef selected = candidates[candidates.Count - 1];

                foreach (var cand in candidates)
                {
                    acc += cand.OccupantWeight;
                    if (roll <= acc) { selected = cand; break; }
                }

                c.occupant = selected.OccupantType;
                c.occupantDef = selected;
                SetCell(z, lane, c);

                remainingBudget -= selected.Cost;
                lastSpawnedZ[(lane, selected.OccupantType)] = z;
                if (!selected.IsWalkable) walkableLanes--;

                for (int extraZ = 1; extraZ < selected.SizeZ; extraZ++)
                    reservedCells.Add((z + extraZ, lane));

                if (remainingBudget <= 0) break;
            }

            if (remainingBudget <= 0) break;
        }
    }

    private void SpawnRowVisuals(int z)
    {
        // Resolve which wall prefab to use for edge lanes (config field takes priority).
        GameObject wallPrefab = config.edgeWallPrefab != null
            ? config.edgeWallPrefab
            : config.catalog.debugEdgeWall;

        for (int lane = 0; lane < TotalLaneCount; lane++)
        {
            var cell = getCell(z, lane);
            Vector3 worldPos = new Vector3(LaneToWorldX(lane), 0, z * config.cellLength);

            // --- EDGE LANE: spawn wall prefab, nothing else ---
            if (IsEdgeLaneIndex(lane))
            {
                if (wallPrefab != null)
                {
                    GameObject wallObj = Instantiate(wallPrefab, worldPos, Quaternion.identity, transform);
                    spawnedEdgeWalls[(z, lane)] = wallObj;
                }
                continue;
            }

            // --- PLAYABLE LANE: surface then occupant (unchanged logic) ---

            // SURFACE
            GameObject surfaceObj = null;
            if (cell.surfaceDef != null && cell.surfaceDef.Prefabs.Count > 0)
            {
                surfaceObj = Instantiate(cell.surfaceDef.Prefabs[rng.Next(cell.surfaceDef.Prefabs.Count)], worldPos, Quaternion.identity, transform);
            }
            else if (cell.surface == SurfaceType.SafePath && config.catalog.debugSafePath != null)
            {
                surfaceObj = Instantiate(config.catalog.debugSafePath, worldPos, Quaternion.identity, transform);
            }
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

            // Edge wall cleanup — same lifetime as the row it belongs to.
            if (spawnedEdgeWalls.TryGetValue(key, out GameObject wall) && wall != null)
            {
                Destroy(wall);
                spawnedEdgeWalls.Remove(key);
            }

            grid.Remove(key);
        }
        //garbsage collection is gonna hitch when it needs to clean up the destroyed objects

    }

    // MISSING FEATURE: No way to call UpdateGeneration from outside
    // Add a public reference to player transform or make this a service

    // OPTIMIZATION: Gizmos for debugging in Scene view

    // --- Noise helpers -------------------------------------------------

    private float BiomeAffinity01(PrefabDef def, int z, int lane)
    {
        var biome = GetBiomeForCell(z, lane);
        // affinity comes from your PrefabDef biomeAffinities list
        float a = def.GetBiomeAffinity(biome); // usually 0..1
                                               // blend based on influence (0 => always 1, 1 => use raw affinity)
        return Mathf.Lerp(1f, a, config.biomeInfluence);
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    // Returns a soft "bump" centered at `center`.
    // halfWidth = flat-ish strong region, feather = soft falloff band.
    private static float BandScore(float x01, float center, float halfWidth, float feather)
    {
        float d = Mathf.Abs(x01 - center);

        // 1 when d <= halfWidth, 0 when d >= halfWidth + feather
        float t = Mathf.InverseLerp(halfWidth + feather, halfWidth, d);
        return Smooth01(t);
    }

    private float Perlin01(float x, float y)
    {
        return Mathf.PerlinNoise(x, y); // 0..1
    }

    // Fractal Brownian Motion (FBM): layered Perlin = smoother large blobs + detail
    private float Fbm01(float x, float y, int octaves, float lacunarity, float gain, float seedX, float seedY)
    {
        float amp = 1f;
        float freq = 1f;
        float sum = 0f;
        float norm = 0f;

        // Small rotation reduces axis-aligned artifacts
        const float rot = 0.5f;
        float rx = Mathf.Cos(rot);
        float ry = Mathf.Sin(rot);

        for (int i = 0; i < octaves; i++)
        {
            float nx = (x * freq);
            float ny = (y * freq);

            // rotate the domain a bit each octave
            float rnx = nx * rx - ny * ry;
            float rny = nx * ry + ny * rx;

            float n = Perlin01(rnx + seedX + i * 17.13f, rny + seedY + i * 31.77f);
            sum += n * amp;

            norm += amp;
            amp *= gain;
            freq *= lacunarity;
        }

        return (norm > 0f) ? (sum / norm) : 0.5f;
    }

    // Domain warp makes biomes "rounder" and less grid-like.
    // We compute an offset vector from noise, then sample FBM in that warped position.
    private float DomainWarpedFbm01(float x, float y)
    {
        // You can also just use constants if you don't want new config fields yet.
        int oct = Mathf.Max(1, config.biomeOctaves);
        float lac = Mathf.Max(1.01f, config.biomeLacunarity);
        float gain = Mathf.Clamp(config.biomeGain, 0.05f, 0.95f);

        float warpStrength = Mathf.Max(0f, config.biomeWarpStrength);
        float warpScale = Mathf.Max(0.001f, config.biomeWarpScale);

        // seed offsets already computed from your seed
        float sx = biomeNoiseOffsetX;
        float sy = biomeNoiseOffsetZ;

        // warp vector from low-frequency noise
        float wx = (Perlin01(sx + x * warpScale, sy + y * warpScale) * 2f - 1f);
        float wy = (Perlin01(sx + 100.0f + x * warpScale, sy + 100.0f + y * warpScale) * 2f - 1f);

        float warpedX = x + wx * warpStrength;
        float warpedY = y + wy * warpStrength;

        float n = Fbm01(warpedX, warpedY, oct, lac, gain, sx, sy);

        // Optional blur: sample a few nearby points in noise space and average.
        float blur = Mathf.Clamp01(config.biomeBlur);
        if (blur > 0f)
        {
            float r = 0.6f; // blur radius in noise-space
            float nN = Fbm01(warpedX, warpedY + r, oct, lac, gain, sx, sy);
            float nS = Fbm01(warpedX, warpedY - r, oct, lac, gain, sx, sy);
            float nE = Fbm01(warpedX + r, warpedY, oct, lac, gain, sx, sy);
            float nW = Fbm01(warpedX - r, warpedY, oct, lac, gain, sx, sy);

            float avg = (n * 4f + nN + nS + nE + nW) / 8f;
            n = Mathf.Lerp(n, avg, blur);
        }

        return n; // 0..1
    }


}