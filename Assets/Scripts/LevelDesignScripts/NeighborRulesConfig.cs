using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using LevelGenerator.Data;

[CreateAssetMenu(fileName = "NeighborRulesConfig", menuName = "Runner/Neighbor Rules Config")]
public class NeighborRulesConfig : ScriptableObject
{
    public PrefabCatalog catalog;

    // ─── Direction mask ───────────────────────────────────────────────────────

    [System.Flags]
    public enum DirectionMask
    {
        None = 0,
        Forward = 1 << 0,   // Z + 1
        Backward = 1 << 1,   // Z - 1
        Left = 1 << 2,   // Lane - 1
        Right = 1 << 3,   // Lane + 1
        Corners = 1 << 4,   // All 4 diagonals
        All = Forward | Backward | Left | Right,
        AllWithCorners = Forward | Backward | Left | Right | Corners
    }

    // ─── Rule types ───────────────────────────────────────────────────────────
    // Class names and field names match the old WeightRulesConfig exactly so
    // existing .asset files deserialize without data loss.
    // The weight field on NeighborConstraint is kept for serialization compat
    // but is IGNORED at runtime — all rules are pure allow/deny.

    [System.Serializable]
    public class NeighborConstraint
    {
        [Tooltip("ID of the allowed neighbor.")]
        public string neighborID;
        public DirectionMask directions;
        // Weight retained for serialization compatibility — not used at runtime.
        // Surface and occupant rules are allow/deny only.
        [HideInInspector] public float weight = 1f;
    }

    [System.Serializable]
    public class NeighborDenial
    {
        [Tooltip("ID of the denied neighbor.")]
        public string neighborID;
        public DirectionMask directions;
    }

    [System.Serializable]
    public class NeighborEntry
    {
        [Tooltip("ID of the tile/def this rule applies to.")]
        public string selfID;
        public List<NeighborConstraint> allowed = new List<NeighborConstraint>();
        public List<NeighborDenial> denied = new List<NeighborDenial>();
    }

    // ─── Inspector lists ──────────────────────────────────────────────────────
    // surfaceRules: used by the safe-path WFC blend pass.
    //   Allow/deny controls which surface tiles may sit next to each other in
    //   the blend zone. Symmetry is expected — if A allows B, add B allows A.
    // occupantRules: used by occupant constraint checks.
    // Both use NeighborEntry so existing serialized assets are not affected.

    [Header("Surface Adjacency Rules")]
    [Tooltip("Controls surface tile adjacency in the safe-path WFC blend zone. " +
             "Pure allow/deny — weight field is ignored. " +
             "Rules should be symmetric: if A allows B, also add B allows A.")]
    public List<NeighborEntry> surfaceRules = new List<NeighborEntry>();

    [Header("Occupant Constraint Database")]
    public List<NeighborEntry> occupantRules = new List<NeighborEntry>();

    // ─── Caches ───────────────────────────────────────────────────────────────

    private Dictionary<string, NeighborEntry> _surfaceCache;
    private Dictionary<string, NeighborEntry> _occupantCache;
    private bool _initialized;

    private void OnEnable() => _initialized = false;

    private static string Norm(string id) =>
        string.IsNullOrEmpty(id) ? id : id.Trim();

    public void BuildCache()
    {
        _surfaceCache = new Dictionary<string, NeighborEntry>();
        _occupantCache = new Dictionary<string, NeighborEntry>();

        foreach (var e in surfaceRules)
        {
            var key = Norm(e.selfID);
            if (string.IsNullOrEmpty(key)) continue;
            if (_surfaceCache.ContainsKey(key))
                Debug.LogWarning($"[NeighborRules] Duplicate surfaceRules entry for '{key}'. Last one wins.");
            e.selfID = key;
            _surfaceCache[key] = e;
        }

        foreach (var e in occupantRules)
        {
            var key = Norm(e.selfID);
            if (string.IsNullOrEmpty(key)) continue;
            if (_occupantCache.ContainsKey(key))
                Debug.LogWarning($"[NeighborRules] Duplicate occupantRules entry for '{key}'. Last one wins.");
            e.selfID = key;
            _occupantCache[key] = e;
        }

        _initialized = true;
    }

    private void EnsureBuilt() { if (!_initialized) BuildCache(); }

    // ─── Surface adjacency API ────────────────────────────────────────────────
    // Used exclusively by the safe-path WFC blend pass.
    // Logic: deny takes priority. If any allow rules exist for the direction,
    // the neighbor must be in the allow list (exclusive). If none exist,
    // all non-denied neighbors are allowed (open world default).

   
    // Returns true if <paramref name="neighbor"/> surface is allowed adjacent
    // to <paramref name="self"/> surface in <paramref name="direction"/>.
    // No rule for self = everything allowed.
    
    public bool IsSurfaceNeighborAllowed(PrefabDef self, PrefabDef neighbor, Direction direction)
    {
        EnsureBuilt();

        string selfId = Norm(self?.ID);
        string neiId = Norm(neighbor?.ID);

        if (string.IsNullOrEmpty(selfId) || string.IsNullOrEmpty(neiId)) return true;
        if (!_surfaceCache.TryGetValue(selfId, out var entry)) return true;

        DirectionMask mask = DirectionToMask(direction);

        foreach (var d in entry.denied)
            if (Norm(d.neighborID) == neiId && (d.directions & mask) != 0)
                return false;

        bool hasExplicit = entry.allowed.Any(a => (a.directions & mask) != 0);
        if (!hasExplicit) return true;

        return entry.allowed.Any(a =>
            Norm(a.neighborID) == neiId && (a.directions & mask) != 0);
    }

   
    // Filters <paramref name="candidates"/> to those whose surface ID is
    // allowed next to <paramref name="self"/> in <paramref name="direction"/>.
    // Returns all candidates if no rule is defined for self.
    
    public List<PrefabDef> GetAllowedSurfaceNeighbors(
        PrefabDef self, Direction direction, List<PrefabDef> candidates)
    {
        EnsureBuilt();

        string selfId = Norm(self?.ID);
        if (string.IsNullOrEmpty(selfId) ||
            !_surfaceCache.TryGetValue(selfId, out var entry))
            return new List<PrefabDef>(candidates);

        DirectionMask mask = DirectionToMask(direction);

        var denied = entry.denied
            .Where(d => (d.directions & mask) != 0)
            .Select(d => Norm(d.neighborID))
            .ToHashSet();

        var allowed = entry.allowed
            .Where(a => (a.directions & mask) != 0)
            .Select(a => Norm(a.neighborID))
            .ToHashSet();

        var result = new List<PrefabDef>();
        foreach (var c in candidates)
        {
            string cid = Norm(c.ID);
            if (denied.Contains(cid)) continue;
            if (allowed.Count > 0 && !allowed.Contains(cid)) continue;
            result.Add(c);
        }
        return result;
    }

    // ─── Occupant adjacency API (original signatures preserved) ──────────────

   
    // Returns allowed occupant neighbors in <paramref name="direction"/>.
    // Weights output is always 1.0 — weight field on NeighborConstraint is
    // no longer used but kept for serialization compatibility.
    
    public List<PrefabDef> GetAllowedNeighbors(
        PrefabDef self,
        Direction direction,
        List<PrefabDef> allCandidates,
        out List<float> weights)
    {
        EnsureBuilt();
        weights = new List<float>();

        if (self == null || string.IsNullOrEmpty(self.ID) ||
            !_occupantCache.TryGetValue(Norm(self.ID), out var entry))
        {
            weights = Enumerable.Repeat(1f, allCandidates.Count).ToList();
            return new List<PrefabDef>(allCandidates);
        }

        DirectionMask mask = DirectionToMask(direction);

        var deniedIds = entry.denied
            .Where(d => (d.directions & mask) != 0)
            .Select(d => Norm(d.neighborID))
            .ToHashSet();

        var allowedForDir = entry.allowed
            .Where(a => (a.directions & mask) != 0)
            .ToList();

        var result = new List<PrefabDef>();

        if (allowedForDir.Count > 0)
        {
            foreach (var constraint in allowedForDir)
            {
                if (deniedIds.Contains(Norm(constraint.neighborID))) continue;
                if (catalog == null) { Debug.LogError("[NeighborRules] Catalog is null."); break; }

                var def = catalog.GetByID(constraint.neighborID);
                if (def != null && allCandidates.Contains(def))
                {
                    result.Add(def);
                    weights.Add(1f); // weight field ignored at runtime
                }
            }
        }
        else
        {
            foreach (var c in allCandidates)
            {
                if (string.IsNullOrEmpty(c.ID)) continue;
                if (deniedIds.Contains(Norm(c.ID))) continue;
                result.Add(c);
                weights.Add(1f);
            }
        }

        return result;
    }

   
    // Returns true if <paramref name="neighbor"/> is an allowed occupant
    // neighbor of <paramref name="self"/> in <paramref name="direction"/>.
    
    public bool IsNeighborAllowed(PrefabDef self, PrefabDef neighbor, Direction direction)
    {
        EnsureBuilt();

        string selfId = Norm(self?.ID);
        string neiId = Norm(neighbor?.ID);

        if (string.IsNullOrEmpty(selfId) || string.IsNullOrEmpty(neiId)) return true;
        if (!_occupantCache.TryGetValue(selfId, out var entry)) return true;

        DirectionMask mask = DirectionToMask(direction);

        foreach (var d in entry.denied)
            if (Norm(d.neighborID) == neiId && (d.directions & mask) != 0)
                return false;

        bool hasExplicit = entry.allowed.Any(a => (a.directions & mask) != 0);
        if (!hasExplicit) return true;

        return entry.allowed.Any(a =>
            Norm(a.neighborID) == neiId && (a.directions & mask) != 0);
    }

   
    // Direct cache lookup — works for both layers.
    // Layer.Surface routes to _surfaceCache, Layer.Occupant to _occupantCache.
    
    public bool TryGetEntry(string selfId, ObjectLayer layer, out NeighborEntry entry)
    {
        EnsureBuilt();
        var cache = (layer == ObjectLayer.Surface) ? _surfaceCache : _occupantCache;
        return cache.TryGetValue(Norm(selfId), out entry);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private DirectionMask DirectionToMask(Direction d)
    {
        switch (d)
        {
            case Direction.Forward: return DirectionMask.Forward;
            case Direction.Backward: return DirectionMask.Backward;
            case Direction.Left: return DirectionMask.Left;
            case Direction.Right: return DirectionMask.Right;
            default: return DirectionMask.Corners;
        }
    }
}