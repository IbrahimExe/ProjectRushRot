using System.Collections.Generic;
using UnityEngine;

namespace LevelGenerator.Data
{
    // --- Layers ---------------------------------------------------------------

    public enum ObjectLayer
    {
        Surface,    // the floor / base tile
        Occupant    // objects that sit on the surface
    }

    // --- Surface types --------------------------------------------------------
    //
    // Solid and Bridge removed - normal floor tiles are identified by their
    // PrefabDef (noise-driven), not a hard enum value.
    // EdgeL / EdgeR replace the old single Edge: the generator uses them to
    // distinguish left-wall lanes from right-wall lanes so each side can have
    // its own prefab list in the catalog and its own surface rules.

    public enum SurfaceType
    {
        Normal,     // noise-placed floor tile (generic catch-all)
        Hole,       // gap - player falls through
        SafePath,   // guaranteed walkable golden path tile
        EdgeL,      // left boundary lane (lane 0) - never walkable
        EdgeR       // right boundary lane (lane TotalLaneCount-1) - never walkable
    }

    // --- Directions -----------------------------------------------------------

    public enum Direction
    {
        Forward,        // Z + 1
        Backward,       // Z - 1
        Left,           // Lane - 1
        Right,          // Lane + 1
        ForwardLeft,
        ForwardRight,
        BackwardLeft,
        BackwardRight
    }

    // --- Cell state -----------------------------------------------------------

    [System.Serializable]
    public class CellState
    {
        public SurfaceType surface;

        // hasOccupant replaces the old OccupantType enum on CellState.
        // The actual def is always in occupantDef.
        public bool hasOccupant;

        public PrefabDef surfaceDef;
        public PrefabDef occupantDef;

        // True for the two boundary lanes outside the playable area.
        // Set once at init, never changed.
        public bool isEdgeLane;

        // WFC fields (used for safe-path blend pass and future occupant WFC)
        public bool isCollapsed;
        public List<PrefabDef> surfaceCandidates;   // WFC candidate pool for surface
        public List<PrefabDef> occupantCandidates;
        public Dictionary<PrefabDef, float> candidateWeights;
        public float entropy;

        public CellState(SurfaceType s, bool occupied = false,
                         PrefabDef surfaceP = null, PrefabDef occupantP = null,
                         bool edgeLane = false)
        {
            surface = s;
            hasOccupant = occupied;
            surfaceDef = surfaceP;
            occupantDef = occupantP;
            isEdgeLane = edgeLane;
            isCollapsed = false;
            surfaceCandidates = null;
            occupantCandidates = null;
            candidateWeights = null;
            entropy = 0f;
        }

        public CellState()
        {
            surface = SurfaceType.Normal;
            hasOccupant = false;
            surfaceDef = null;
            occupantDef = null;
            isEdgeLane = false;
            isCollapsed = false;
            surfaceCandidates = null;
            occupantCandidates = null;
            candidateWeights = null;
            entropy = 0f;
        }
    }

    // --- Prefab definition ----------------------------------------------------

    [System.Serializable]
    public class OLDPrefabDef
    {
        [Tooltip("Stable Unique Identifier. Used for Save/Load and Logic.")]
        public string ID;

        [Tooltip("Display Name / Editor Name")]
        public string Name;

        [Tooltip("List of prefab variants. One will be picked randomly at spawn time.")]
        public List<GameObject> Prefabs = new List<GameObject>();

        // -- Classification ----------------------------------------------------
        [Header("Classification")]
        public ObjectLayer Layer = ObjectLayer.Occupant;

        [Tooltip("Only relevant for Surface layer entries.")]
        public SurfaceType SurfaceType = SurfaceType.Normal;

        // -- Budget system -----------------------------------------------------
        // OccupantWeight and Tags moved here from the old scattered locations.
        [Header("Budget System")]
        [Tooltip("Selection weight for occupant placement. Higher = more likely to be chosen.")]
        [Range(0f, 100f)] public float OccupantWeight = 10f;

        [Tooltip("Cost to spawn this occupant (consumed from the chunk density budget).")]
        public int Cost = 1;

        [Tooltip("Minimum number of rows between two spawns of this exact def in the same lane. 0 = no gap.")]
        [Range(0, 20)] public int MinRowGap = 2;

        [Tooltip("How many rows this occupant occupies in Z. Cells ahead are reserved to prevent overlap.")]
        [Range(1, 20)] public int SizeZ = 1;

        [Tooltip("Surface IDs this occupant is allowed to spawn on. Empty = allowed on any surface.")]
        public List<string> AllowedSurfaceIDs = new List<string>();

        public List<string> Tags = new List<string>();
        public bool HasTag(string tag) => Tags?.Contains(tag) ?? false;

        // -- Noise hierarchy ---------------------------------------------------
        // noiseChannel is now a single field on the PrefabCatalog, not per-def.
        // Each def just declares its tier (rank within the noise range).
        [Header("Noise Hierarchy")]
        [Tooltip("Rank within the noise channel's [0,1] range. " +
                 "0 = lowest value band, higher numbers = higher value bands. " +
                 "The range is divided equally among all noise candidates sorted by this tier.")]
        public int noiseTier = 0;

        [Tooltip("Whether this def participates in noise-driven surface placement.")]
        public bool isNoiseCandidate = true;
    }
}