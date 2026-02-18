using System.Collections.Generic;
using UnityEngine;
using LevelGenerator.Data;
using System.Linq;


//Replaces the old WeightRulesConfig.
// Holds the bidirectional constraints for the WFC solver using String IDs.

[CreateAssetMenu(fileName = "NeighborRulesConfig", menuName = "Runner/Neighbor Rules Config")]
public class NeighborRulesConfig : ScriptableObject
{
    public PrefabCatalog catalog; // Needed to resolve IDs

    [System.Flags]
    public enum DirectionMask
    {
        None = 0,
        Forward = 1 << 0,        // Z + 1
        Backward = 1 << 1,       // Z - 1
        Left = 1 << 2,           // Lane - 1
        Right = 1 << 3,          // Lane + 1
        Corners = 1 << 4,        // All 4 diagonal neighbors
        All = Forward | Backward | Left | Right,
        AllWithCorners = Forward | Backward | Left | Right | Corners
    }

    [System.Serializable]
    public class NeighborConstraint
    {
        public string neighborID; // ID of the allowed neighbor
        public DirectionMask directions; // Relative to 'self' (Multi-select)
        [Tooltip("Relative selection bias (NOT probability). Normalized at runtime.")]
        public float weight = 1.0f;
    }

    [System.Serializable]
    public class NeighborDenial
    {
        public string neighborID; // ID of the denied neighbor
        public DirectionMask directions; // Relative to 'self' (Multi-select)
    }

    [System.Serializable]
    public class NeighborEntry
    {
        public string selfID; // ID of the tile this rule applies to
        public List<NeighborConstraint> allowed = new List<NeighborConstraint>();
        public List<NeighborDenial> denied = new List<NeighborDenial>();
    }

    [Header("Constraint Database")]
    public List<NeighborEntry> surfaceRules = new List<NeighborEntry>();
    public List<NeighborEntry> occupantRules = new List<NeighborEntry>();
    
    // Legacy support or just internal
    // public List<NeighborEntry> entries = new List<NeighborEntry>(); 

    // Dictionary cache for fast runtime lookups
    private Dictionary<string, NeighborEntry> _occupantCache;
    private Dictionary<string, NeighborEntry> _surfaceCache;
    private bool _initialized;

    private void OnEnable()
    {
        _initialized = false;
    }

    private static string Norm(string id) => string.IsNullOrEmpty(id) ? id : id.Trim();

    public void BuildCache()
    {
        _occupantCache = new Dictionary<string, NeighborEntry>();
        _surfaceCache = new Dictionary<string, NeighborEntry>();

        foreach (var e in surfaceRules)
        {
            var key = Norm(e.selfID);
            if (string.IsNullOrEmpty(key)) continue;

            if (_surfaceCache.ContainsKey(key))
                Debug.LogWarning($"NeighborRulesConfig: DUPLICATE surfaceRules entry for selfID '{key}'. Cache will use the LAST one.");

            e.selfID = key; // optional: normalize stored value too
            _surfaceCache[key] = e;
        }

        foreach (var e in occupantRules)
        {
            var key = Norm(e.selfID);
            if (string.IsNullOrEmpty(key)) continue;

            if (_occupantCache.ContainsKey(key))
                Debug.LogWarning($"NeighborRulesConfig: DUPLICATE occupantRules entry for selfID '{key}'. Cache will use the LAST one.");

            e.selfID = key;
            _occupantCache[key] = e;
        }

        _initialized = true;
    }

    /// Returns a list of Allowed Neighbors for a specific direction.
    /// If NO rules exist for that direction, it returns ALL candidates (implicit allow).
    /// If ANY rule exists for that direction, it becomes an EXCLUSIVE list.
    /// Denied neighbors are always removed.
    public List<PrefabDef> GetAllowedNeighbors(
        PrefabDef self, 
        Direction direction, 
        List<PrefabDef> allCandidates,
        out List<float> weights
    )
    {
        if (!_initialized) BuildCache();

        weights = new List<float>();
        
        // 1. Determine which cache to use
        Dictionary<string, NeighborEntry> cache = (self.Layer == ObjectLayer.Occupant) ? _occupantCache : _surfaceCache;

        // 2. If no entry for this tile (or self is null), return Unconstrained (Multipliers = 1.0)
        if (self == null || string.IsNullOrEmpty(self.ID) || !cache.TryGetValue(self.ID, out NeighborEntry entry))
        {
            // raw multiplier 1.0
            weights = Enumerable.Repeat(1.0f, allCandidates.Count).ToList();
            return new List<PrefabDef>(allCandidates); 
        }

        // Convert query direction to mask
        DirectionMask queryMask = DirectionToMask(direction);

        // 3. Filter allowed list by direction mask
        // We look for constraints that INCLUDE the query direction
        var allowedForDir = entry.allowed.Where(c => (c.directions & queryMask) != 0).ToList();
        var deniedForDirIds = entry.denied.Where(c => (c.directions & queryMask) != 0).Select(c => c.neighborID).ToHashSet();

        List<PrefabDef> resultCandidates = new List<PrefabDef>();
        
        // CASE A: Explicit Allow List exists
        if (allowedForDir.Count > 0)
        {
            foreach (var constraint in allowedForDir)
            {
                if (string.IsNullOrEmpty(constraint.neighborID)) continue;
                if (deniedForDirIds.Contains(constraint.neighborID)) continue;

                // Resolve ID to PrefabDef
                if (catalog == null) 
                {
                    Debug.LogError("NeighborRulesConfig: Catalog is null! Cannot resolve IDs.");
                    break;
                }

                PrefabDef neighborDef = catalog.GetByID(constraint.neighborID);
                if (neighborDef != null)
                {
                    // Check if candidate is actually in the passed list?
                    // Optimization: We could just return the definition, but caller expects filtered candidates from 'allCandidates'
                    // For now, simple check:
                    if(allCandidates.Contains(neighborDef)) // O(N) linear scan, but list is small
                    {
                        resultCandidates.Add(neighborDef);
                        weights.Add(constraint.weight); // Raw Multiplier
                        //OPTIMIZATION: If lists get larger (>20 items), convert allCandidates to HashSet before this loop for O(1) lookups.
                    }
                }
            }
        }
        // CASE B: Implicit Allow All (minus denied)
        else
        {
            foreach (var potential in allCandidates)
            {
                if (string.IsNullOrEmpty(potential.ID)) continue;
                if (deniedForDirIds.Contains(potential.ID)) continue;
                
                resultCandidates.Add(potential);
                weights.Add(1.0f); // Default Multiplier
            }
        }

        // NO NORMALIZATION - Return raw weights
        return resultCandidates;
    }

    public bool IsNeighborAllowed(PrefabDef self, PrefabDef neighbor, Direction direction)
    {
        if (!_initialized) BuildCache();

        string selfId = Norm(self?.ID);
        string neiId = Norm(neighbor?.ID);

        if (string.IsNullOrEmpty(selfId)) return true;
        if (string.IsNullOrEmpty(neiId)) return true;

        var cache = (self.Layer == ObjectLayer.Occupant) ? _occupantCache : _surfaceCache;
        if (!cache.TryGetValue(selfId, out NeighborEntry entry)) return true;

        DirectionMask queryMask = DirectionToMask(direction);

        foreach (var denial in entry.denied)
        {
            if (Norm(denial.neighborID) == neiId && (denial.directions & queryMask) != 0)
                return false;
        }

        bool hasExplicitAllow = false;
        bool isExplicitlyAllowed = false;

        foreach (var constraint in entry.allowed)
        {
            if ((constraint.directions & queryMask) != 0)
            {
                hasExplicitAllow = true;
                if (Norm(constraint.neighborID) == neiId)
                {
                    isExplicitlyAllowed = true;
                    break;
                }
            }
        }

        return hasExplicitAllow ? isExplicitlyAllowed : true;
    }

    public bool TryGetEntry(string selfId, ObjectLayer layer, out NeighborEntry entry)
    {
        if (!_initialized) BuildCache();
        var cache = (layer == ObjectLayer.Occupant) ? _occupantCache : _surfaceCache;
        return cache.TryGetValue(Norm(selfId), out entry);
    }

    private DirectionMask DirectionToMask(Direction d)
    {
        switch (d)
        {
            case Direction.Forward: return DirectionMask.Forward;
            case Direction.Backward: return DirectionMask.Backward;
            case Direction.Left: return DirectionMask.Left;
            case Direction.Right: return DirectionMask.Right;
            default: return DirectionMask.None;
        }
    }
}
