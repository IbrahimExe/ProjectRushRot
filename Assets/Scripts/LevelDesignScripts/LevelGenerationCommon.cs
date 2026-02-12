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
        Enemy
    }

    // NEW: Biome types for regional coherence
    public enum BiomeType
    {
        Default,      // Neutral/mixed (no preference)
        Grassy,       // Green, natural, soft surfaces
        Rocky,        // Stone, hard, grey surfaces
        Sandy,        // Desert, warm, beige surfaces
        Crystalline,  // Magical, glowing, fantasy surfaces
        Swampy,       // Water, murky, dark green surfaces
        Volcanic      // Fire, lava, red/orange surfaces
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
    public struct CellState
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
        public List<PrefabDef> surfaceCandidates;
        public Dictionary<PrefabDef, float> candidateWeights;
        public float entropy;

        public CellState(SurfaceType s, OccupantType o, PrefabDef surfaceP = null, PrefabDef occupantP = null, bool edgeLane = false)
        {
            surface = s;
            occupant = o;
            surfaceDef = surfaceP;
            occupantDef = occupantP;
            isEdgeLane = edgeLane;
            isCollapsed = false;
            surfaceCandidates = null;
            candidateWeights = null;
            entropy = 0f;
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
        public int laneCount;       // playable lane count (does NOT include the 2 edge lanes)
        public int playerZIndex;

        // True when this cell is one of the two structural boundary lanes.
        public bool isEdgeLane;

        // Helper methods
        public bool IsEdgeLane => isEdgeLane;
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

        [Header("Dimensions")]
        public Vector3Int Size = new Vector3Int(1, 1, 1);

        [Tooltip("Used ONLY if Layer == Occupant. Ignored for Surfaces (WFC uses constraints).")]
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

        [Header("Biome System")]
        [Tooltip("How strongly this tile prefers each biome. Higher = more likely. Leave empty for neutral (1.0).")]
        public Dictionary<BiomeType, float> BiomeAffinities = new Dictionary<BiomeType, float>();

        /// Gets the biome affinity weight for a specific biome.
        /// Returns 1.0 (neutral) if no affinity is defined.
        [System.Serializable]
        public struct BiomeAffinity
        {
            public BiomeType biome;
            public float weight; // multiplier (0..whatever)
        }

        [Header("Biome System")]
        [Tooltip("Biome weight multipliers. 1 = neutral, >1 prefers, <1 avoids.")]
        public List<BiomeAffinity> biomeAffinities = new List<BiomeAffinity>();

        public float GetBiomeAffinity(BiomeType biome)
        {
            if (biomeAffinities == null) return 1f;

            for (int i = 0; i < biomeAffinities.Count; i++)
            {
                if (biomeAffinities[i].biome == biome)
                    return Mathf.Max(0f, biomeAffinities[i].weight);
            }
            return 1f;
        }
    }

}