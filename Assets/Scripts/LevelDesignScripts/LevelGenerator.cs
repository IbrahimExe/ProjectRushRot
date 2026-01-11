// ------------------------------------------------------------
// Runner Level Generator 
// - Generates forward-only rows ahead of the player
// - Uses local neighborhood to weight placements
// - Guarantees: each Z row has >= 1 walkable lane
// - Prefabs handle their own behavior; generator only picks what/where
// ------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
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
public struct RunnerGenConfig : ScriptableObject 
{
    [Header("Lane Setup")]
    public int laneCount;
   public float laneWidth;
   public float cellLength;
    [Header("Generation Settings")]
    public int bufferRows; // extra rows to keep loaded beyond player view
   public int keepRowsBehind; // rows behind player to keep loaded
   public int neigbhorhoodZ; 
   public int neighborhoodX;
   public float obstacleChanceCenter; //Base chance of obstacle in center lane
    [Header("Spawn Chances")]
    public float wallChanceEdge; // Base chance of wall in edge lanes
    public float wallCanceFalloff; // Reduces wall chance further from edge
    public float holeChance; // Base chance of hole in any lane
   public float dentisyPenalty; // Reduces spawn if area is dense
    [Header("Options")]
    public bool allowHoles; 
   public bool allowBridges;
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
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private GameObject obstaclePrefab;
    [SerializeField] private GameObject holePrefab;
    [SerializeField] private GameObject bridgePrefab;

    private void Start()
    {
        grid = new Dictionary<(int, int), CellState>();
        rng = new System.Random();
    }

    private void Update()
    {
        //unly update when the player is at x distance?
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
        return (float)rng.NextDouble();
    }

    private float Clamp(float v)
    {
        return Mathf.Clamp(v, 0f, 1f);
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
        return (c.surface != Surface.Hole) && (c.occupant == Occupant.None);
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

        for (int zz = z; zz < zMax; zz++)
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

    //Preview methods

    private bool WouldBreakRowIfPlaced(int z, int lane, Surface surf)
    {
        CellState old = getCell(z, lane);

        //temporarily set
        SetCell(z, lane, new CellState(surf, old.occupant)); //this might be wrong
        bool ok = RowHasAnyWalkable(z);

        //revert
        SetCell(z, lane, old);

        return !ok;

    }

    private void ComitAndSpawn(int z, int lane, CellState newState)
    {
        SetCell(z, lane, newState);

        //world position
        Vector3 position = new Vector3(
            (lane * config.laneWidth),
            0f,
            z * config.cellLength
        );

        //spawn prefabs MEMMORY MANAGEMENT NEEDED

        if (newState.surface == Surface.Hole && holePrefab != null)
        {
            Instantiate(holePrefab, position, Quaternion.identity);
        }
        else if (newState.surface == Surface.Bridge && bridgePrefab != null)
        {
            Instantiate(bridgePrefab, position, Quaternion.identity);
        }
        if (newState.occupant == Occupant.Wall && wallPrefab != null)
        {
            Instantiate(wallPrefab, position, Quaternion.identity);
        }
        else if (newState.occupant == Occupant.Obstacle && obstaclePrefab != null)
        {
            Instantiate(obstaclePrefab, position, Quaternion.identity);
        }

    }

    //Row Generation

    private void GenerateRow(int z)
    {
        //border walls first
        for (int lane = 0; lane < config.laneCount; lane++)
        {
            if (LaneIsBorder(lane))
            {
                float wallScore = ScoreWall(z, lane);
                if (Rand() < wallScore)
                {
                    CellState candidate = getCell(z, lane);
                    candidate.occupant = Occupant.Wall;

                    if (!WouldBreakRowIfPlaced(z, lane, candidate.surface)) //this might be wrong
                    {
                        ComitAndSpawn(z, lane, candidate);
                    }
                }
            }
        }

        //place holes
        if (config.allowHoles)
        {
            for (int lane = 0; lane < config.laneCount; lane++)
            {
                float holeScore = ScoreHole(z, lane);
                if (Rand() < holeScore)
                {
                    CellState candidate = getCell(z, lane);
                    if (candidate.surface == Surface.Solid) //only place hole on solid
                    {
                        candidate.surface = Surface.Hole;
                        candidate.occupant = Occupant.None;
                        if (WouldBreakRowIfPlaced(z, lane, candidate.surface))
                        {
                            if (config.allowBridges)
                            {
                                candidate.surface = Surface.Bridge;
                                ComitAndSpawn(z, lane, candidate);
                            }

                        }
                        else
                        {
                            ComitAndSpawn(z, lane, candidate);
                        }

                    }
                }
            }
        }
        //place obstacles
        for (int lane = 0; lane < config.laneCount; lane++)
        {
            CellState candidate = getCell(z, lane);
            if (candidate.surface == Surface.Hole) continue; //skip holes
            if (candidate.occupant == Occupant.Wall) continue; //skip walls
            //as the checks get longer maybe refactor to own function

            float obsScore = ScoreObstacle(z, lane);
            if (Rand() < obsScore)
            {
                CellState candidate = c; // wadafuk im confused, too many candidate redeclarations
                candidate.occupant = Occupant.Obstacle;

                if (!WouldBreakRowIfPlaced(z, lane, candidate.surface)) //this might be wrong
                {
                    ComitAndSpawn(z, lane, candidate);
                }
            }
        }

        //check it works

        if (!RowHasAnyWalkable(z))
        {
           int prefferedLane = (config.laneCount - 1) / 2;
            int[] tryLane = {preferred, 0, config.laneCount - 1};

            foreach (int lane in tryLane)
            {
                CellState cc = new CellState(Surface.Solid, Occupant.None);
                ComitAndSpawn(z, tryLane, cc);
                if (RowHasAnyWalkable(z))
                    break;
            }
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
            // TODO: Despawn associated GameObjects
            grid.Remove(key);
        }
    }
}

