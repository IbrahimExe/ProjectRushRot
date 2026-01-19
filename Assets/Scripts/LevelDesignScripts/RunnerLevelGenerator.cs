// ------------------------------------------------------------
// Runner Level Generator 
// - Generates forward-only rows ahead of the player
// - Uses local neighborhood to weight placements
// - Guarantees: each Z row has >= 1 walkable lane
// - Prefabs handle their own behavior; generator only picks what/where
// ------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LevelGenerator.Data;

[System.Serializable]
public struct CellState{
    public SurfaceType surface;
    public OccupantType occupant;

    public CellState(SurfaceType s, OccupantType o){
        surface = s;
        occupant = o;
    }
}

[CreateAssetMenu(fileName = "RunnerConfig", menuName = "Runner/GeneratorConfig")]
public sealed class RunnerGenConfig : ScriptableObject 
{
    [Header("Catalog Reference")]
    public PrefabCatalog catalog;

    [Header("Lane Setup")]
    public int laneCount = 3;
    public float laneWidth = 2f;
    public float cellLength = 10f; 

    [Header("Generation Settings")]
    public int bufferRows = 20; // extra rows to keep loaded beyond player view
    public int keepRowsBehind = 5; // rows behind player to keep loaded
    public int neigbhorhoodZ = 4;
    public int neighborhoodX = 1;

    [Header("Spawn Chances")]
    [Range(0f, 1f)] public float wallChanceEdge = 0.7f; // Base chance of wall in edge lanes
    [Range(0f, 1f)] public float wallCanceFalloff = 0.15f; // Reduces wall chance further from edge
    [Range(0f, 1f)] public float holeChance = 0.2f; // Base chance of hole in any lane
    [Range(0f, 1f)] public float obstacleChanceCenter = 0.3f; // Base chance of obstacle in center lane
    [Range(0f, 1f)] public float dentisyPenalty = 0.1f; // Reduces spawn if area is dense

    [Header("Options")]
    public bool allowHoles = true;
    public bool allowBridges = true;
}


//main class
public class RunnerLevelGenerator : MonoBehaviour
{
    [SerializeField] private RunnerGenConfig config;

    private Dictionary<(int z, int lane), CellState> grid;
    private int generatorZ = 0;
    private System.Random rng;

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
            Debug.LogError("RunnerLevelGenerator: No 'RunnerGenConfig' assigned! Please create a config asset and assign it.");
            enabled = false;
            return;
        }

        grid = new Dictionary<(int, int), CellState>();
        rng = new System.Random();
        spawnedObjects = new Dictionary<(int, int), GameObject>();
        spawnedOccupant = new Dictionary<(int, int), GameObject>();
        //Initialize object pools here
        
        if(config.catalog != null) config.catalog.RebuildCache();

        // Generate initial buffer

        for (int i = 0; i < config.bufferRows; i++)
        {
            GenerateRow(i);
            generatorZ++;
        }
    }
    

    void Update()
    {
        //unly update when the player is at x distance?
        if (playerTransform != null)
        {
            //if player uis not onside update skip mathj
            UpdateGeneration(playerTransform.position.z);
        }
    }

    private IEnumerator SpawnTileRoutine(List<GameObject> tiles)
    {
        foreach (var tile in tiles)
        {
            // Instantiate or activate tile

            yield return new WaitForSeconds(0.5f);
        }
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

    float Rand()// not very good, better create a seed at start then use all generation from that
    {
        return UnityEngine.Random.value;
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

    // --- Connectivity Check (A*) ---

    // Checks if there is a valid path from the start of the current context window to the target z
    private bool IsConnectedToStart(int targetZ)
    {
        // 1. Identify start point: The player's current row or the beginning of the buffer?
        // For a continuous generator, we just need to ensure the NEW row connections to the EXISTING valid geometry.
        // We look back 'neighborhoodZ' rows. If we can path from (z - neighborhood) to (z), we are good.
        
        int startZ = Mathf.Max(0, targetZ - config.neigbhorhoodZ);
        if (startZ == targetZ) return true; // First row is always valid

        // Find a walkable start node in startZ
        (int z, int lane) startNode = (-1, -1);
        for(int l=0; l<config.laneCount; l++) {
            if (IsWalkable(startZ, l)) {
                startNode = (startZ, l);
                break; 
            }
        }
        if (startNode.z == -1) return false; // Previous area was fully blocked? Should not happen if we maintain invariant.

        // Find a walkable end node in targetZ
        (int z, int lane) endNode = (-1, -1);
        for(int l=0; l<config.laneCount; l++) {
            if (IsWalkable(targetZ, l)) {
                endNode = (targetZ, l);
                break;
            }
        }
        if (endNode.z == -1) return false; // Target row is fully blocked

        // Check path
        var path = PathfindingHelper.FindPath(
            startNode, 
            endNode, 
            0, config.laneCount - 1, 
            IsWalkable
        );

        return path != null && path.Count > 0;
    }

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
        return (nearObs + nearWall + nearHole) * config.dentisyPenalty;

        //OPTIMIZATION: Cache calculations if checking same cell multiple times
        // OPTIMIZATION: Consider not counting Enemy/Collectible in density??
    }

    //Scoring

    private float ScoreWall(int z, int lane)
    {
        float baseChance = config.wallChanceEdge;
        if (!LaneIsBorder(lane))
            baseChance *= config.wallCanceFalloff;
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

    //Preview methods

    private bool WouldBreakRowIfPlaced(int z, int lane, CellState newState)
    {
        CellState old = getCell(z, lane);

        //temporarily set
        SetCell(z, lane, newState);
        
        // Check dynamic path connectivity
        bool ok = IsConnectedToStart(z);

        //revert
        SetCell(z, lane, old);

        return !ok;

        //OPTIMIZATION: IsConnectedToStart is expensive (A*). 
        // Calling this for every placement attempt might lag if 'neighborhoodZ' is large.
        // But since we are only generating 1 row at a time in Update, it might be acceptable.
    }

    //Unused helper removed/refactored logic into main loop

    //Row Generation

    private void GenerateRow(int z)
    {
      

        for (int lane = 0; lane < config.laneCount; lane++)
        {
            SetCell(z, lane, new CellState(SurfaceType.Solid, OccupantType.None));
            
        }


        //border walls first
        for (int lane = 0; lane < config.laneCount; lane++)
        {
            if (!LaneIsBorder(lane)) continue;

            float wallScore = ScoreWall(z, lane);
            if (Rand() < wallScore)
            {
                CellState candidate = getCell(z, lane);
                candidate.occupant = OccupantType.Wall;

                if (!WouldBreakRowIfPlaced(z, lane, candidate))
                {
                    SetCell(z, lane, candidate);
                }
            }
        }

        if (config.allowHoles)
        {
            for (int lane = 0; lane < config.laneCount; lane++)
            {
                float holeScore = ScoreHole(z, lane);
                if (Rand() < holeScore)
                {
                    CellState candidate = getCell(z, lane);

                    // Only carve holes from solid ground
                    if (candidate.surface != SurfaceType.Solid) continue;

                    candidate.surface = SurfaceType.Hole;
                    candidate.occupant = OccupantType.None;

                    if (WouldBreakRowIfPlaced(z, lane, candidate))
                    {
                        if (config.allowBridges)
                        {
                            candidate.surface = SurfaceType.Bridge;
                            SetCell(z, lane, candidate);
                        }
                        // else: do nothing, keep solid
                    }
                    else
                    {
                        SetCell(z, lane, candidate); // commit Hole
                    }
                }
            }
        }

        //place obstacles
        for (int lane = 0; lane < config.laneCount; lane++)
        {
            CellState c = getCell(z, lane);
            if (c.surface == SurfaceType.Hole) continue;
            if (c.occupant != OccupantType.None) continue;

            float obsScore = ScoreObstacle(z, lane);
            if (Rand() < obsScore)
            {
                CellState obstacleCandidate = c;
                obstacleCandidate.occupant = OccupantType.Obstacle;

                if (!WouldBreakRowIfPlaced(z, lane, obstacleCandidate))
                {
                    SetCell(z, lane, obstacleCandidate);
                }
            }
        }


        //check it works

        if (!RowHasAnyWalkable(z))
        {
           int prefferedLane = (config.laneCount - 1) / 2;
            int[] tryLanes = {prefferedLane, 0, config.laneCount - 1};

            foreach (int lane in tryLanes)
            {
                CellState cc = new CellState(SurfaceType.Solid, OccupantType.None);
                SetCell(z, lane, cc);
                if (RowHasAnyWalkable(z))
                    break;
            }
        }

        // --- Instantiation using Catalog ---
        if (config.catalog == null) return;

        for (int lane = 0; lane < config.laneCount; lane++)
        {
            CellState s = getCell(z, lane);

            Vector3 basePos = new Vector3(lane * config.laneWidth, 0f, z * config.cellLength);
            
            // ----- Surface -----
            GameObject surfaceObj = null;
            // Get candidates for the specific surface type
            var surfCandidates = config.catalog.GetCandidates(s.surface);
            // Pick one
            var surfDef = config.catalog.GetWeightedRandom(surfCandidates); 
            
            if (surfDef != null && surfDef.Prefabs != null && surfDef.Prefabs.Count > 0)
            {
                // Pick random variant
                GameObject chosenPrefab = surfDef.Prefabs[UnityEngine.Random.Range(0, surfDef.Prefabs.Count)];
                if (chosenPrefab != null)
                {
                    surfaceObj = Instantiate(chosenPrefab, basePos, Quaternion.identity);
                    spawnedObjects[(z, lane)] = surfaceObj;
                }
            }
            else
            {
                // Debug: Why no surface?
                if (surfCandidates.Count == 0) Debug.LogWarning($"No Surface candidates for type {s.surface}");
            }

            // ----- Occupant -----
            if (s.occupant != OccupantType.None)
            {
                // Get candidates
                var occCandidates = config.catalog.GetCandidates(s.occupant);
                // Pick one
                var occDef = config.catalog.GetWeightedRandom(occCandidates);

                if (occDef != null && occDef.Prefabs != null && occDef.Prefabs.Count > 0)
                {
                    // Basic positioning: center of tile + slight up? 
                    // Or rely on pivot. Assuming pivot is bottom-center or bottom-left.
                    Vector3 occPos = basePos; 
                    
                    // Pick random variant
                    GameObject chosenPrefab = occDef.Prefabs[UnityEngine.Random.Range(0, occDef.Prefabs.Count)];
                    
                    if (chosenPrefab != null)
                    {
                        GameObject occObj = Instantiate(chosenPrefab, occPos, Quaternion.identity);
                        spawnedOccupant[(z, lane)] = occObj;
                    }
                }
                else
                {
                     if (occCandidates.Count == 0) Debug.LogWarning($"No Occupant candidates for type {s.occupant}");
                }
            }
        }
    }

    public void UpdateGeneration(float playerZWorld)
    {
        //
        int playerZIndex = Mathf.FloorToInt(playerZWorld / config.cellLength);
        int targetZ = playerZIndex + config.bufferRows;

        while (generatorZ < targetZ)
        {
            GenerateRow(generatorZ);
            generatorZ++;
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

