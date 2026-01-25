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
        SafePath
    }

    // types of objects that occupy the surface
    public enum OccupantType
    {
        None,
        Wall,
        Obstacle,
        Collectible,
        Enemy
    }

    // attributes for objects, using flags so an object can have multiple traits
    [System.Flags]
    public enum ObjectAttributes
    {
        None        = 0,
        Physical    = 1 << 0,  // has a collider or blocks movement
        Moving      = 1 << 1,  // moves dynamically in the scene
        Hazardous   = 1 << 2,  // causes damage to the player
        Walkable    = 1 << 3,  // player can walk on top of this occupant
        Floating    = 1 << 4,  // can be placed over holes
        Collectible = 1 << 5   // can be collected by the player
    }

    /// <summary>
    /// Cell state in the grid
    /// </summary>
    [System.Serializable]
    public struct CellState
    {
        public SurfaceType surface;
        public OccupantType occupant;
        public PrefabDef surfaceDef;   // Specific prefab for the floor/surface
        public PrefabDef occupantDef;  // Specific prefab for the object on top

        public CellState(SurfaceType s, OccupantType o, PrefabDef surfaceP = null, PrefabDef occupantP = null)
        {
            surface = s;
            occupant = o;
            surfaceDef = surfaceP;
            occupantDef = occupantP;
        }
    }

    /// Context information for weight calculations
    /// Contains all data needed to make informed placement decisions
    public struct PlacementContext
    {
        public (int z, int lane) position;
        public SurfaceType currentSurface;
        public OccupantType currentOccupant;
        
        // References to grid data
        public System.Func<int, int, CellState> GetCell;
        public System.Func<(int, int), bool> IsOnGoldenPath;
        
        // Grid metadata
        public int laneCount;
        public int playerZIndex;
        
        // Helper methods
        public bool IsEdgeLane => position.lane == 0 || position.lane == laneCount - 1;
        public bool IsCenterLane => position.lane == (laneCount - 1) / 2;
        public float NormalizedLanePosition => (float)position.lane / (laneCount - 1);
        
        public int DistanceToGoldenPath
        {
            get
            {
                if (IsOnGoldenPath(position)) return 0;
                
                // Find nearest golden path cell in same row
                int minDist = int.MaxValue;
                for (int lane = 0; lane < laneCount; lane++)
                {
                    if (IsOnGoldenPath((position.z, lane)))
                    {
                        int dist = Mathf.Abs(position.lane - lane);
                        if (dist < minDist) minDist = dist;
                    }
                }
                return minDist;
            }
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

        [Header("Attributes")]
        public ObjectAttributes Attributes;

        [Header("Dimensions")]
        // size in grid cells (x, y, z), pivot is assumed to be bottom-left
        public Vector3Int Size = new Vector3Int(1, 1, 1);

        [Header("Generation Settings")]
        [Range(0f, 100f)] public float BaseWeight = 100f;
        
        // tags for specific filtering like 'forest_only' or 'rare'
        public List<string> Tags = new List<string>();

        // check if this definition has a specific tag
        public bool HasTag(string tag) => Tags.Contains(tag);
        
        // check if this definition has a specific attribute flag
        public bool HasAttribute(ObjectAttributes attr) => (Attributes & attr) == attr;
    }
}
