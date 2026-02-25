using System.Collections.Generic;
using UnityEngine;

namespace LevelGenerator.Data
{
    [CreateAssetMenu(fileName = "PrefabCatalog", menuName = "Runner/Prefab Catalog")]
    public class PrefabCatalog : ScriptableObject
    {
        [Header("Registered Prefabs")]
        public List<PrefabDef> Definitions = new List<PrefabDef>();

        [Header("Debug / Fallbacks")]
        [Tooltip("Used for the Golden Path/Safe Path if no specific tile is defined.")]
        public GameObject debugSafePath;

        [Tooltip("Used for generic solid surface if WFC hits a contradiction. Assign a real Surface PrefabDef from your catalog.")]
        public PrefabDef debugSurfaceDef;

        [Tooltip("Used if an occupant is required but no candidate found.")]
        public GameObject debugOccupant;

        [Tooltip("Used for edge lane walls if no EdgeWall PrefabDef is defined in the catalog.")]
        public GameObject debugEdgeWall;

        // Fast lookup caches
        private Dictionary<OccupantType, List<PrefabDef>> _occupantCache;
        private Dictionary<SurfaceType, List<PrefabDef>> _surfaceCache;
        private Dictionary<string, PrefabDef> _idIndex;
        private bool _initialized;

        private void OnEnable() => RebuildCache();

        private void OnValidate()
        {
            // Ensure stable IDs + validate occupant AllowedSurfaceIDs references
            if (Definitions != null)
            {
                var validSurfaceIDs = new HashSet<string>();

                // First pass: generate IDs (if needed) + gather surface IDs
                foreach (var def in Definitions)
                {
                    if (def == null) continue;

                    if (string.IsNullOrEmpty(def.ID) && !string.IsNullOrEmpty(def.Name))
                        def.ID = def.Name.ToUpperInvariant().Replace(" ", "_");

                    if (def.Layer == ObjectLayer.Surface && !string.IsNullOrEmpty(def.ID))
                        validSurfaceIDs.Add(def.ID);
                }

                // Second pass: validate occupant allowed surfaces
                foreach (var def in Definitions)
                {
                    if (def == null) continue;
                    if (def.Layer != ObjectLayer.Occupant) continue;
                    if (def.AllowedSurfaceIDs == null || def.AllowedSurfaceIDs.Count == 0) continue;

                    for (int i = def.AllowedSurfaceIDs.Count - 1; i >= 0; i--)
                    {
                        string surfaceID = def.AllowedSurfaceIDs[i];

                        // Strip empty entries
                        if (string.IsNullOrEmpty(surfaceID))
                        {
                            def.AllowedSurfaceIDs.RemoveAt(i);
                            continue;
                        }

                        // Warn on unknown IDs (don’t auto-remove: you may be mid-edit)
                        if (!validSurfaceIDs.Contains(surfaceID))
                        {
                            Debug.LogWarning(
                                $"[PrefabCatalog] Occupant '{def.Name}' references unknown surface ID '{surfaceID}'.",
                                this
                            );
                        }
                    }
                }
            }

            _initialized = false;
        }

        // Rebuilds lookup dictionaries
        public void RebuildCache()
        {
            _occupantCache = new Dictionary<OccupantType, List<PrefabDef>>();
            _surfaceCache = new Dictionary<SurfaceType, List<PrefabDef>>();
            _idIndex = new Dictionary<string, PrefabDef>();
            _initialized = false;

            // Initialize enum buckets so GetCandidates never needs allocations for known types
            foreach (OccupantType t in System.Enum.GetValues(typeof(OccupantType)))
                _occupantCache[t] = new List<PrefabDef>();

            foreach (SurfaceType t in System.Enum.GetValues(typeof(SurfaceType)))
                _surfaceCache[t] = new List<PrefabDef>();

            if (Definitions == null)
            {
                _initialized = true;
                return;
            }

            foreach (var def in Definitions)
            {
                if (def == null) continue;
                if (string.IsNullOrEmpty(def.ID)) continue;

                // Index by ID (first wins to avoid silent overwrites)
                if (!_idIndex.ContainsKey(def.ID))
                    _idIndex[def.ID] = def;

                // Bucket by layer/type
                if (def.Layer == ObjectLayer.Surface)
                {
                    _surfaceCache[def.SurfaceType].Add(def);
                }
                else if (def.Layer == ObjectLayer.Occupant)
                {
                    _occupantCache[def.OccupantType].Add(def);
                }
            }

            _initialized = true;
        }

        private void EnsureCache()
        {
            if (!_initialized || _occupantCache == null || _surfaceCache == null || _idIndex == null)
                RebuildCache();
        }

        // --- Retrieval Methods (used by RunnerLevelGenerator) ---

        public PrefabDef GetByID(string id)
        {
            EnsureCache();
            return (!string.IsNullOrEmpty(id) && _idIndex.TryGetValue(id, out var def)) ? def : null;
        }

        // Use OccupantType.EdgeWall to get edge wall variants
        public List<PrefabDef> GetCandidates(OccupantType type)
        {
            EnsureCache();
            return _occupantCache.TryGetValue(type, out var list) ? list : new List<PrefabDef>();
        }

        public List<PrefabDef> GetCandidates(SurfaceType type)
        {
            EnsureCache();
            return _surfaceCache.TryGetValue(type, out var list) ? list : new List<PrefabDef>();
        }
    }
}
