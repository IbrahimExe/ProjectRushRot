using System.Collections.Generic;
using UnityEngine;

namespace LevelGenerator.Data
{
    // distinct layers to separate the ground from what sits on top of it
    public enum ObjectLayer
    {
        Surface,    // the floor or base
        Occupant    // objects that sit on the surface
    }

    // types of surface elements
    public enum SurfaceType
    {
        Solid,
        Hole,
        Bridge,
        SafePath,
        Edge        // The two boundary lanes that flank the playable area.
                    // Always non-walkable. WFC and occupant system never touch these.
    }

    // types of objects that occupy the surface
    public enum OccupantType
    {
        None,
        Wall,
        Obstacle,
        Collectible,
        Enemy,
        EdgeWall      // Walls that keep player in bounds on edge lanes
    }


    // Directions for neighbor lookup
    public enum Direction
    {
        Forward,          // Z + 1, Lane
        Backward,         // Z - 1, Lane
        Left,             // Z, Lane - 1
        Right,            // Z, Lane + 1
        ForwardLeft,      // Z + 1, Lane - 1
        ForwardRight,     // Z + 1, Lane + 1
        BackwardLeft,     // Z - 1, Lane - 1
        BackwardRight     // Z - 1, Lane + 1
    }


    //Cell state in the grid

    [System.Serializable]
    public class CellState
    {
        public SurfaceType surface;
        public OccupantType occupant;
        public PrefabDef surfaceDef;
        public PrefabDef occupantDef;

        // True for the two boundary lanes outside the playable area.
        // Set once at initialization, never changed.
        public bool isEdgeLane;

        // WFC Fields
        public bool isCollapsed;
        public List<PrefabDef> occupantCandidates;
        public Dictionary<PrefabDef, float> candidateWeights;
        public float entropy;

        // Constructor
        public CellState(SurfaceType s, OccupantType o, PrefabDef surfaceP = null, PrefabDef occupantP = null, bool edgeLane = false)
        {
            surface = s;
            occupant = o;
            surfaceDef = surfaceP;
            occupantDef = occupantP;
            isEdgeLane = edgeLane;
            isCollapsed = false;
            occupantCandidates = null;
            candidateWeights = null;
            entropy = 0f;
        }

        // Default constructor for class
        public CellState()
        {
            surface = SurfaceType.Solid;
            occupant = OccupantType.None;
            surfaceDef = null;
            occupantDef = null;
            isEdgeLane = false;
            isCollapsed = false;
            occupantCandidates = null;
            candidateWeights = null;
            entropy = 0f;
        }
    }



    [System.Serializable]
    public class PrefabDef
    {
        [Tooltip("Stable Unique Identifier. Used for Save/Load and Logic.")]
        public string ID;

        [Tooltip("Display Name / Editor Name")]
        public string Name;

        [Tooltip("List of prefab variants. One will be picked randomly.")]
        public List<GameObject> Prefabs = new List<GameObject>();

        [Header("Classification")]
        public ObjectLayer Layer = ObjectLayer.Occupant;
        public SurfaceType SurfaceType = SurfaceType.Solid;
        public OccupantType OccupantType = OccupantType.None;

        [Header("Dimensions")]
        public Vector3Int Size = new Vector3Int(1, 1, 1);

        [Tooltip("Selection weight for occupant WFC placement.")]
        [Range(0f, 100f)] public float OccupantWeight = 10f;

        public List<string> Tags = new List<string>();

        public bool HasTag(string tag) => Tags?.Contains(tag) ?? false;

        [Header("Budget System")]
        [Tooltip("Cost to spawn this occupant (for density budget).")]
        public int Cost = 1;

        [Tooltip("Minimum number of rows between two spawns of this type in the same lane. 0 = no restriction.")]
        [Range(0, 20)] public int MinRowGap = 2;

        [Tooltip("How many rows this occupant occupies in Z. Cells ahead are reserved to prevent overlap. 1 = single cell.")]
        [Range(1, 5)] public int SizeZ = 1;

        [Tooltip("List of Surface IDs this occupant is allowed to spawn on. If empty, allowed on any.")]
        public List<string> AllowedSurfaceIDs = new List<string>();

        [Tooltip("Can the player walk through this object?")]
        public bool IsWalkable = false;

        [Header("Noise Hierarchy")]
        [Tooltip("Position in the sorted hierarchy (0 = lowest/darkest, N-1 = highest/brightest).")]
        public int noiseTier = 0;

        [Tooltip("Which noise asset this tile responds to (allows multi-layer setups).")]
        public NoiseConfig noiseChannel;

        [Tooltip("Whether this def participates in noise-driven placement at all.")]
        public bool isNoiseCandidate = true;

        [Tooltip("Expands the tile's range at both edges, creating an overlap zone where it can appear instead of its neighbors.")]
        [Range(0f, 1f)] public float noiseBlend = 0f;
    }

}