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

[CreateAssetMenu(fileName = "RunnerConfig", menuName = "Runner/GeneratorConfig")]
public sealed class RunnerGenConfig : ScriptableObject 
{
    [Header("Catalog Reference")]
    public PrefabCatalog catalog;
    public WeightRulesConfig weightRules;

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
    [Range(0f, 1f)] public float wallChanceEdge = 0.7f; // Base chance of wall in edge lanes
    [Range(0f, 1f)] public float wallChanceFalloff = 0.15f; // Reduces wall chance further from edge
    [Range(0f, 1f)] public float holeChance = 0.2f; // Base chance of hole in any lane
    [Range(0f, 1f)] public float obstacleChanceCenter = 0.3f; // Base chance of obstacle in center lane
    [Range(0f, 1f)] public float densityPenalty = 0.1f; // Reduces spawn if area is dense

  

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
    [Range(0f, 15f)] public float waveAmplitudeLanes = 10f; // for 30 lanes, 8�12 feels good

    [Tooltip("How fast the wave changes per row. Smaller = longer turns.")]
    [Range(0.001f, 0.1f)] public float waveFrequency = 0.02f;
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
    private WeightSystem weightSystem;


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
        weightSystem = new WeightSystem(config.weightRules);
        spawnedObjects = new Dictionary<(int, int), GameObject>();
        spawnedOccupant = new Dictionary<(int, int), GameObject>();
        
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

            float n1 = Mathf.PerlinNoise(waveSeedOffset + 10.0f, z * config.waveFrequency);
            float n2 = Mathf.PerlinNoise(waveSeedOffset + 77.7f, z * (config.waveFrequency * 2.2f));

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

    //Neighbors check

    private int CountNearbyOcc(int z, int lane, OccupantType occType)
    {
        int count = 0;
        int zMax = z + config.neigbhorhoodZ - 1;
        int xMin = Mathf.Max(0, lane - config.neighborhoodX);
        int xMax = Mathf.Min(config.laneCount - 1, lane + config.neighborhoodX);

        for (int zz = z; zz <= zMax; zz++)
        {
            for (int ll = xMin; ll <= xMax; ll++)
            {
                CellState c = getCell(zz, ll);
                if (c.occupant == occType)
                {
                    count++;
                }
            }
        }

        return count;

        //debating if i put an exit if count > threshold, but that might make it so the function doesnt work on edge cases
        // i want to make it chunk of rows where it takes the last row as a context to connect and then generates a larger cohesive area. 
        //this way we can have themes or tempos the algorithm can follow
    }

    private int CountNearbySurf(int z, int lane, SurfaceType surfType) //how many surfaces of type X are near this cell?
    {

        int count = 0;
        int zMax = z + config.neigbhorhoodZ - 1;
        int xMin = Mathf.Max(0, config.neighborhoodX - 1);
        int xMax = Mathf.Min(config.laneCount - 1, lane + 1);

        for (int zz = z; zz <= zMax; zz++)
        {
            for (int ll = xMin; ll <= xMax; ll++)
            {
                CellState c = getCell(zz, ll);
                if (c.surface == surfType)
                    count++;
            }
        }
        return count;
    }

    private float DensityPenalty(int z, int lane)
    {
        int nearObs = CountNearbyOcc(z, lane, OccupantType.Obstacle);
        int nearWall = CountNearbyOcc(z, lane, OccupantType.Wall);
        int nearHole = CountNearbySurf(z, lane, SurfaceType.Hole);
        return (nearObs + nearWall + nearHole) * config.densityPenalty;

        //OPTIMIZATION: Cache calculations if checking same cell multiple times
        // OPTIMIZATION: Consider not counting Enemy/Collectible in density??
    }

    //Scoring

    private float ScoreWall(int z, int lane)
    {
        float baseChance = config.wallChanceEdge;
        if (!LaneIsBorder(lane))
            baseChance *= config.wallChanceFalloff;
        return Clamp(baseChance - DensityPenalty(z, lane));
    }

    private float ScoreObstacle(int z, int lane)
    {
        float baseChance = config.obstacleChanceCenter;
        if (LaneIsCenter(lane))
            baseChance *= 1.5f; //hard coded
        if (LaneIsBorder(lane))
            baseChance *= 0.5f; //hard coded
        return Clamp(baseChance - DensityPenalty(z, lane));
    }

    private float ScoreHole(int z, int lane)
    {
        if (!config.allowHoles)
            return 0f;

        float baseChance = config.holeChance;
        if (LaneIsBorder(lane))
            baseChance *= 0.5f; //hard coded
        if (LaneIsCenter(lane))
            baseChance *= 1.2f; //hard coded
        return Clamp(baseChance - DensityPenalty(z, lane));
    }

    //I did 3 functions that basically do the same thing, maybe refactor later
    // OPTIMIZATION: Move hardcoded multipliers to config as public fields

    private bool WouldBreakRowIfPlaced(int z, int lane, CellState newState)
    {
        CellState old = getCell(z, lane);

        // temporarily set
        SetCell(z, lane, newState);

        // simple invariant: row must have at least one walkable lane
        bool ok = RowHasAnyWalkable(z);

        // revert
        SetCell(z, lane, old);

        return !ok;
    }


    //Unused helper removed/refactored logic into main loop

    //Row Generation

    private void GenerateChunk(int startZ, int chunkSize)
    {
        int endZ = startZ + chunkSize;

        // PASS 1: Initialization & Safe Path Stamping
        for (int z = startZ; z < endZ; z++)
        {
            for (int lane = 0; lane < config.laneCount; lane++)
            {
                bool isSafe = goldenPathSet.Contains((z, lane));
                SurfaceType surf = isSafe ? SurfaceType.SafePath : SurfaceType.Solid;
                
                SetCell(z, lane, new CellState(surf, OccupantType.None));
            }
        }

        // PASS 2: Feature Placement
        // Now that the canvas is ready, we place objects.
        // We iterate row by row, but because the grid is initialized ahead of us, 
        // neighbor checks (looking forward) will see the SafePath/Solid state of future rows.
        for (int z = startZ; z < endZ; z++)
        {
            GenerateRowFeatures(z);
            SpawnRowVisuals(z);
        }
    }

    private void GenerateRowFeatures(int z)
    {
        // 1. Place Walls (Border & Weighted)
        var wallCandidates = config.catalog.GetCandidates(OccupantType.Wall);
        for (int lane = 0; lane < config.laneCount; lane++)
        {
            if (goldenPathSet.Contains((z, lane))) continue;

            PlacementContext ctx = CreateContext(z, lane);
            
            PrefabDef selectedWall = weightSystem.SelectWeighted(wallCandidates, ctx, rng);
            if (selectedWall != null)
            {
                if ((selectedWall.Attributes & ObjectAttributes.Walkable) == 0 && goldenPathSet.Contains((z, lane)))
                    continue; 

                CellState candidate = getCell(z, lane);
                candidate.occupant = OccupantType.Wall;
                candidate.occupantDef = selectedWall; // Assign to OCCUPANT
                
                if (!WouldBreakRowIfPlaced(z, lane, candidate))
                    SetCell(z, lane, candidate);
            }
        }

        // 2. Place Holes (Weighted)
        if (config.allowHoles)
        {
            var holeCandidates = config.catalog.GetCandidates(SurfaceType.Hole);
            for (int lane = 0; lane < config.laneCount; lane++)
            {
                if (goldenPathSet.Contains((z, lane))) continue;

                PlacementContext ctx = CreateContext(z, lane);
                PrefabDef selectedHole = weightSystem.SelectWeighted(holeCandidates, ctx, rng);
                
                if (selectedHole != null)
                {
                    CellState candidate = getCell(z, lane);
                    if (candidate.occupant != OccupantType.None) continue;

                    candidate.surface = SurfaceType.Hole;
                    candidate.surfaceDef = selectedHole; // Assign to SURFACE
                    
                    if (WouldBreakRowIfPlaced(z, lane, candidate))
                    {
                        if (config.allowBridges)
                        {
                            candidate.surface = SurfaceType.Bridge;
                            candidate.surfaceDef = null; // Reset def (unless we have a bridge def)
                            SetCell(z, lane, candidate);
                        }
                    }
                    else
                    {
                        SetCell(z, lane, candidate);
                    }
                }
            }
        }

        // 3. Place Obstacles (Weighted)
        var obstacleCandidates = config.catalog.GetCandidates(OccupantType.Obstacle);
        for (int lane = 0; lane < config.laneCount; lane++)
        {
            CellState c = getCell(z, lane);
            if (c.surface == SurfaceType.Hole) continue;
            
            bool isSafePath = goldenPathSet.Contains((z, lane));
            if (c.occupant != OccupantType.None) continue;

            PlacementContext ctx = CreateContext(z, lane);
            PrefabDef selectedObstacle = weightSystem.SelectWeighted(obstacleCandidates, ctx, rng);
            
            if (selectedObstacle != null)
            {
                bool isWalkable = (selectedObstacle.Attributes & ObjectAttributes.Walkable) != 0;
                if (isSafePath && !isWalkable) continue;

                CellState obstacleCandidate = c;
                obstacleCandidate.occupant = OccupantType.Obstacle;
                obstacleCandidate.occupantDef = selectedObstacle; // Assign to OCCUPANT
                
                if (!WouldBreakRowIfPlaced(z, lane, obstacleCandidate))
                    SetCell(z, lane, obstacleCandidate);
            }
        }
    }


    private void SpawnRowVisuals(int z)
    {
        for (int lane = 0; lane < config.laneCount; lane++)
        {
            var cell = getCell(z, lane);
            Vector3 worldPos = new Vector3(LaneToWorldX(lane), 0, z * config.cellLength);
            bool isSafePath = goldenPathSet.Contains((z, lane));

            // --- LAYER 0: SURFACE ---
            GameObject surfaceObj = null;

            // 1. Check for Holes
            if (cell.surface == SurfaceType.Hole)
            {
                if (cell.surfaceDef != null && cell.surfaceDef.Prefabs.Count > 0)
                    surfaceObj = Instantiate(cell.surfaceDef.Prefabs[rng.Next(cell.surfaceDef.Prefabs.Count)], worldPos, Quaternion.identity, transform);
            }
            // 2. Check for Specific Surface Def (Bridge, Special Floor)
            else if (cell.surfaceDef != null && cell.surfaceDef.Prefabs.Count > 0)
            {
                surfaceObj = Instantiate(cell.surfaceDef.Prefabs[rng.Next(cell.surfaceDef.Prefabs.Count)], worldPos, Quaternion.identity, transform);
            }
            // 3. Safe Path (Debug/Fallback)
            else if (isSafePath)
            {
                 // Priority: Config Debug -> Fallback Solid
                 if (config.catalog.debugSafePath != null)
                     surfaceObj = Instantiate(config.catalog.debugSafePath, worldPos, Quaternion.identity, transform);
                 // Else fall through to generic solid if you want, or leave null? 
                 // Usually SafePath should have a visual.
            }
            // 4. Default Solid
            else if (cell.surface == SurfaceType.Solid || cell.surface == SurfaceType.Bridge) // Treat generated bridge w/o def as solid? or debugSurface?
            {
                // Try random solid from catalog
                var candidates = config.catalog.GetCandidates(SurfaceType.Solid);
                if (candidates != null && candidates.Count > 0)
                {
                    var pick = candidates[rng.Next(candidates.Count)];
                    if(pick.Prefabs.Count > 0)
                        surfaceObj = Instantiate(pick.Prefabs[rng.Next(pick.Prefabs.Count)], worldPos, Quaternion.identity, transform);
                }
                
                // Fallback to debug
                if (surfaceObj == null && config.catalog.debugSurface != null)
                     surfaceObj = Instantiate(config.catalog.debugSurface, worldPos, Quaternion.identity, transform);
            }

            if (surfaceObj != null)
                spawnedObjects[(z, lane)] = surfaceObj;


            // --- LAYER 1: OCCUPANT ---
            GameObject occObj = null;

            if (cell.occupant != OccupantType.None)
            {
                // 1. Specific Def
                if (cell.occupantDef != null && cell.occupantDef.Prefabs.Count > 0)
                {
                    occObj = Instantiate(cell.occupantDef.Prefabs[rng.Next(cell.occupantDef.Prefabs.Count)], worldPos, Quaternion.identity, transform);
                }
                // 2. Fallback Debug
                else if (config.catalog.debugOccupant != null)
                {
                    occObj = Instantiate(config.catalog.debugOccupant, worldPos, Quaternion.identity, transform);
                }
            }

            if (occObj != null)
                spawnedOccupant[(z, lane)] = occObj;
        }
    }

    public void UpdateGeneration(float playerZWorld)
    {
        //
        int playerZIndex = Mathf.FloorToInt(playerZWorld / config.cellLength);
        int targetZ = playerZIndex + config.bufferRows;

        // Ensure we have a path planned up to this point
        UpdatePathBuffer(targetZ + 20);

        while (generatorZ < targetZ)
        {
            int chunkStart = generatorZ;
            int actualChunkSize = Mathf.Min(config.chunkSize, targetZ - generatorZ);
            GenerateChunk(chunkStart, actualChunkSize);
            generatorZ += actualChunkSize;
            //i do this 2 times, so maybe do a function in the future
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
            // Need to destroy the spawned object first
            if (spawnedObjects.TryGetValue(key, out GameObject obj))
            {
                Destroy(obj);
                // OPTIMIZATION: Return to object pool instead
                // ReturnToPool(obj);
                spawnedObjects.Remove(key);
            }

            grid.Remove(key);
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

