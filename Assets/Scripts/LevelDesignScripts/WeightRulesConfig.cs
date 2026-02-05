using System.Collections.Generic;
using UnityEngine;
using LevelGenerator.Data;
using System.Linq;

/// <summary>
/// Replaces the old WeightRulesConfig.
/// Holds the bidirectional constraints for the WFC solver using String IDs.
/// </summary>
[CreateAssetMenu(fileName = "NeighborRulesConfig", menuName = "Runner/Neighbor Rules Config")]
public class NeighborRulesConfig : ScriptableObject
{
    public PrefabCatalog catalog; // Needed to resolve IDs

    [System.Flags]
    public enum DirectionMask
    {
        None = 0,
        Forward = 1 << 0,
        Backward = 1 << 1,
        Left = 1 << 2,
        Right = 1 << 3,
        All = Forward | Backward | Left | Right
    }

    [System.Serializable]
    public class NeighborConstraint
    {
        public string neighborID; // ID of the allowed/denied neighbor
        public DirectionMask directions; // Relative to 'self' (Multi-select)
        [Tooltip("Relative selection bias (NOT probability). Normalized at runtime.")]
        public float weight = 1.0f;
    }

    [System.Serializable]
    public class NeighborEntry
    {
        public string selfID; // ID of the tile this rule applies to
        public List<NeighborConstraint> allowed = new List<NeighborConstraint>();
        public List<NeighborConstraint> denied = new List<NeighborConstraint>();
    }

    [Header("Constraint Database")]
    public List<NeighborEntry> entries = new List<NeighborEntry>();

    // Dictionary cache for fast runtime lookups
    private Dictionary<string, NeighborEntry> _entryCache;
    private bool _initialized;

    private void OnEnable()
    {
        _initialized = false;
    }

    private void BuildCache()
    {
        _entryCache = new Dictionary<string, NeighborEntry>();
        foreach (var entry in entries)
        {
            if (!string.IsNullOrEmpty(entry.selfID) && !_entryCache.ContainsKey(entry.selfID))
            {
                _entryCache[entry.selfID] = entry;
            }
        }
        _initialized = true;
    }

    /// <summary>
    /// Single Source of Truth for "What can spawn next to this tile?"
    /// </summary>
    public List<PrefabDef> GetAllowedNeighbors(
        PrefabDef self, 
        Direction direction, 
        List<PrefabDef> allSurfaces,
        out List<float> weights
    )
    {
        if (!_initialized) BuildCache();

        weights = new List<float>();

        // 1. If no entry for this tile (or self is null), return Unconstrained
        if (self == null || string.IsNullOrEmpty(self.ID) || !_entryCache.TryGetValue(self.ID, out NeighborEntry entry))
        {
            weights = Enumerable.Repeat(1f, allSurfaces.Count).ToList();
            return allSurfaces;
        }

        // Convert query direction to mask
        DirectionMask queryMask = DirectionToMask(direction);

        // 2. Filter allowed list by direction mask
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
                    resultCandidates.Add(neighborDef);
                    weights.Add(constraint.weight);
                }
            }
        }
        // CASE B: Implicit Allow All (minus denied)
        else
        {
            foreach (var potential in allSurfaces)
            {
                if (string.IsNullOrEmpty(potential.ID)) continue;
                if (deniedForDirIds.Contains(potential.ID)) continue;
                
                resultCandidates.Add(potential);
                weights.Add(1.0f); // Default weight
            }
        }

        // Normalize weights
        float sum = weights.Sum();
        if (sum > 0)
        {
            for (int i = 0; i < weights.Count; i++) weights[i] /= sum;
        }

        return resultCandidates;
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
