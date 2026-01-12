// ------------------------------------------------------------
// Runner Level Generator 
// - Generates forward-only rows ahead of the player
// - Uses local neighborhood to weight placements
// - Guarantees: each Z row has >= 1 walkable lane
// - Prefabs handle their own behavior; generator only picks what/where
// ------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public enum Surface
{
    Solid,
    Hole,
    Bridge,
}
public enum Occupant
    {
    None,
    Obstacle,
    Collectible,
    Enemy,
    Wall,
}

[System.Serializable]
public struct CellState{
    public Surface surface;
    public Occupant occupant;

    public CellState(Surface s, Occupant o){
        surface = s;
        occupant = o;
    }
}

[CreateAssetMenu(fileName = "RunnerConfig", menuName = "Runner/GeneratorConfig")]
public sealed class RunnerGenConfig : ScriptableObject 
{
    [Header("Lane Setup")]
    public int laneCount = 3;
    public float laneWidth = 2f;
    public float cellLength = 10f; // square cell size

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
public class LevelGenerator : MonoBehaviour
{
    [SerializeField] private RunnerGenConfig config;

    private Dictionary<(int z, int lane), CellState> grid;
    private int generatorZ = 0;
    private System.Random rng;

    //prefab references
    [Header("Prefabs")]
    [SerializeField] private GameObject floorPrefab;
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private GameObject obstaclePrefab;
    [SerializeField] private GameObject holePrefab;
    [SerializeField] private GameObject bridgePrefab;

    [Header("References")]
    [SerializeField] private Transform playerTransform;

    // OPTIMIZATION: Track spawned objects for cleanup
    // Currently missing - need this to pool/destroy spawned objects
    private Dictionary<(int z, int lane), GameObject> spawnedObjects = new Dictionary<(int, int), GameObject>();
    private Dictionary<(int z, int lane), GameObject> spawnedOccupant = new();

    void Start()
    {
        grid = new Dictionary<(int, int), CellState>();
        rng = new System.Random();
        spawnedObjects = new Dictionary<(int, int), GameObject>();
        spawnedOccupant = new Dictionary<(int, int), GameObject>();
        //Initialize object pools here

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
            UpdateGeneration(playerTransform.position.z);
        }
    }

    //FUNCTIONS

    private bool LaneIsBorder(int lane)
    {
        return lane == 0 || lane == config.laneCount - 1;
    }

    private bool LaneIsCenter(int lane)
    {
        int mid = (config.laneCount - 1) / 2;
        return lane == mid || (lane == mid + (config.laneCount % 2 == 0 ? 1 : 0));
    }

    float Rand()
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
        return new CellState(Surface.Solid, Occupant.None);
    }

    private void SetCell(int z, int lane, CellState state)
    {
        grid[(z, lane)] = state;
    }

    //Validate

    private bool IsWalkable(int z, int lane)
    {
        CellState c = getCell(z, lane);
        return (c.surface != Surface.Hole) && (c.occupant == Occupant.None || c.occupant == Occupant.Collectible);
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

    //Neighbors check

    private int CountNearbyOcc(int z, int lane, Occupant occType)
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

    }

    private int CountNearbySurf(int z, int lane, Surface surfType)
    {

        int count = 0;
        int zMax = z + config.neigbhorhoodZ - 1;
        int xMin = Mathf.Max(0, lane - 1);
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
        int nearObs = CountNearbyOcc(z, lane, Occupant.Obstacle);
        int nearWall = CountNearbyOcc(z, lane, Occupant.Wall);
        int nearHole = CountNearbySurf(z, lane, Surface.Hole);
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
        bool ok = RowHasAnyWalkable(z);

        //revert
        SetCell(z, lane, old);

        return !ok;

        //OPTIMIZATION: Consider caching results if checking same cell multiple times, also consider if this is called too often
    }

    private void SpawnFloorBelow(int z, int lane)
    {
        Vector3 position = new Vector3(
            (lane * config.laneWidth),
            0f,
            z * config.cellLength
        );
        if (floorPrefab != null)
        {
            GameObject floor = Instantiate(floorPrefab, position, Quaternion.identity);
            spawnedObjects[(z, lane)] = floor;
        }
    }

    private void ComitAndSpawn(int z, int lane, CellState newState) //Not Used but can be useful for future memmory management
    {
        SetCell(z, lane, newState);

        //world position
        Vector3 position = new Vector3(
            (lane * config.laneWidth),
            0f,
            z * config.cellLength
        );


        //spawn prefabs MEMMORY MANAGEMENT NEEDED
        //this needs to go, as is the floor spawns and then the wall/obstacle spawns on top of it. But holes need to remove the floor

        GameObject spawned = null;

        // Spawn occupants on top
        // Room for implementing anchors and seed to move objects inside the cell
        if (newState.surface == Surface.Hole && holePrefab != null)
        {
            Vector3 wallPos = position + Vector3.up * 0.5f;
            spawned = Instantiate(holePrefab, position, Quaternion.identity);
        }
        else if (newState.surface == Surface.Bridge && bridgePrefab != null)
        {
            Vector3 obsPos = position + Vector3.up * 0.5f;
            spawned = Instantiate(bridgePrefab, position, Quaternion.identity);
            
        }

        if (newState.occupant == Occupant.Wall && wallPrefab != null)
        {
            spawned = Instantiate(wallPrefab, position, Quaternion.identity);
            
        }
        else if (newState.occupant == Occupant.Obstacle && obstaclePrefab != null)
        {
            spawned = Instantiate(obstaclePrefab, position, Quaternion.identity);
           
        }


        if (spawned != null)
        {
            spawnedObjects[(z, lane)] = spawned;
            SpawnFloorBelow(z, lane);
        }

    }

    //Row Generation

    private void GenerateRow(int z)
    {
      

        for (int lane = 0; lane < config.laneCount; lane++)
        {
            SetCell(z, lane, new CellState(Surface.Solid, Occupant.None));
            
        }


        //border walls first
        for (int lane = 0; lane < config.laneCount; lane++)
        {
            if (!LaneIsBorder(lane)) continue;

            float wallScore = ScoreWall(z, lane);
            if (Rand() < wallScore)
            {
                CellState candidate = getCell(z, lane);
                candidate.occupant = Occupant.Wall;

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
                    if (candidate.surface != Surface.Solid) continue;

                    candidate.surface = Surface.Hole;
                    candidate.occupant = Occupant.None;

                    if (WouldBreakRowIfPlaced(z, lane, candidate))
                    {
                        if (config.allowBridges)
                        {
                            candidate.surface = Surface.Bridge;
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
            if (c.surface == Surface.Hole) continue;
            if (c.occupant != Occupant.None) continue;

            float obsScore = ScoreObstacle(z, lane);
            if (Rand() < obsScore)
            {
                CellState obstacleCandidate = c;
                obstacleCandidate.occupant = Occupant.Obstacle;

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
                CellState cc = new CellState(Surface.Solid, Occupant.None);
                SetCell(z, lane, cc);
                if (RowHasAnyWalkable(z))
                    break;
            }
        }

        for (int lane = 0; lane < config.laneCount; lane++)
        {
            CellState s = getCell(z, lane);

            Vector3 basePos = new Vector3(lane * config.laneWidth, 0f, z * config.cellLength);
            Vector3 occPos = basePos + Vector3.up * 0.5f;

            // ----- Surface -----
            GameObject surfaceObj = null;

            if (s.surface == Surface.Hole)
            {
                if (holePrefab != null) surfaceObj = Instantiate(holePrefab, basePos, Quaternion.identity);
            }
            else if (s.surface == Surface.Bridge)
            {
                if (bridgePrefab != null) surfaceObj = Instantiate(bridgePrefab, basePos, Quaternion.identity);
            }
            else
            {
                if (floorPrefab != null) surfaceObj = Instantiate(floorPrefab, basePos, Quaternion.identity);
            }

            if (surfaceObj != null)
                spawnedObjects[(z, lane)] = surfaceObj;

            // ----- Occupant -----
            GameObject occObj = null;

            if (s.occupant == Occupant.Wall)
            {
                if (wallPrefab != null) occObj = Instantiate(wallPrefab, occPos, Quaternion.identity);
            }
            else if (s.occupant == Occupant.Obstacle)
            {
                if (obstaclePrefab != null) occObj = Instantiate(obstaclePrefab, occPos, Quaternion.identity);
            }

            if (occObj != null)
                spawnedOccupant[(z, lane)] = occObj;
        }
    }

    public void UpdateGeneration(float playerZWorld)
    {
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

    }

    // MISSING FEATURE: No way to call UpdateGeneration from outside
    // Add a public reference to player transform or make this a service

    // OPTIMIZATION: Gizmos for debugging in Scene view


}

