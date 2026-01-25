using System.Collections.Generic;
using UnityEngine;
using System.Linq;

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
        [Tooltip("Used for generic solid surface if random selection fails.")]
        public GameObject debugSurface;
        [Tooltip("Used if an occupant is required but no candidate found.")]
        public GameObject debugOccupant;

        // cache dictionaries for fast lookup by type or id
        private Dictionary<OccupantType, List<PrefabDef>> _occupantCache;
        private Dictionary<SurfaceType, List<PrefabDef>> _surfaceCache;
        private Dictionary<string, PrefabDef> _idIndex;
        private bool _initialized = false;

        private void OnEnable() => RebuildCache();
        
        // ensures cache is rebuilt in the editor if values change
        private void OnValidate()
        {
            // generate ids if missing
            if (Definitions != null)
            {
                foreach (var def in Definitions)
                {
                    if (string.IsNullOrEmpty(def.ID) && !string.IsNullOrEmpty(def.Name))
                        def.ID = def.Name.ToUpper().Replace(" ", "_");
                }
            }
            _initialized = false; 
        }

        // rebuilds the lookup dictionaries
        public void RebuildCache()
        {
            _occupantCache = new Dictionary<OccupantType, List<PrefabDef>>();
            _surfaceCache = new Dictionary<SurfaceType, List<PrefabDef>>();
            _idIndex = new Dictionary<string, PrefabDef>();

            // initialize lists for each enum type
            foreach (OccupantType t in System.Enum.GetValues(typeof(OccupantType)))
                _occupantCache[t] = new List<PrefabDef>();
            
            foreach (SurfaceType t in System.Enum.GetValues(typeof(SurfaceType)))
                _surfaceCache[t] = new List<PrefabDef>();

            if (Definitions == null) return;

            foreach (var def in Definitions)
            {
                if (string.IsNullOrEmpty(def.ID)) continue;

                // index by id
                if (!_idIndex.ContainsKey(def.ID)) _idIndex[def.ID] = def;

                // index by type based on layer
                if (def.Layer == ObjectLayer.Surface)
                {
                    if (!_surfaceCache.ContainsKey(def.SurfaceType)) 
                        _surfaceCache[def.SurfaceType] = new List<PrefabDef>();
                    _surfaceCache[def.SurfaceType].Add(def);
                }
                else if (def.Layer == ObjectLayer.Occupant)
                {
                    if (!_occupantCache.ContainsKey(def.OccupantType)) 
                        _occupantCache[def.OccupantType] = new List<PrefabDef>();
                    _occupantCache[def.OccupantType].Add(def);
                }
            }
            _initialized = true;
        }

        private void ValidateCache()
        {
            if (!_initialized || _occupantCache == null) RebuildCache();
        }

        // --- Retrieval Methods ---

        // get a definition by its unique id
        public PrefabDef GetByID(string id)
        {
            ValidateCache();
            return _idIndex.TryGetValue(id, out var def) ? def : null;
        }

        // get all candidates for a specific occupant type
        public List<PrefabDef> GetCandidates(OccupantType type)
        {
            ValidateCache();
            if (_occupantCache.TryGetValue(type, out var list)) return list;
            return new List<PrefabDef>();
        }

        // get all candidates for a specific surface type
        public List<PrefabDef> GetCandidates(SurfaceType type)
        {
            ValidateCache();
            if (_surfaceCache.TryGetValue(type, out var list)) return list;
            return new List<PrefabDef>();
        }

        // --- Filtering Helpers ---

        // filter a list by attributes and optional tag
        public List<PrefabDef> Filter(List<PrefabDef> source, ObjectAttributes requiredMask, string requiredTag = null)
        {
            var result = new List<PrefabDef>(source.Count);
            foreach(var def in source)
            {
                if ((def.Attributes & requiredMask) != requiredMask) continue;
                if (requiredTag != null && !def.HasTag(requiredTag)) continue;
                result.Add(def);
            }
            return result;
        }

        // --- Weighted Selection ---

        // picks an item from the list based on weight, with an optional modifier function
        public PrefabDef GetWeightedRandom(List<PrefabDef> candidates, System.Random rng, System.Func<PrefabDef, float> weightModifier = null)
        {
            if (candidates == null || candidates.Count == 0) return null;

            float totalWeight = 0f;
            float[] effectiveWeights = new float[candidates.Count];

            // calculate effective weights
            for (int i = 0; i < candidates.Count; i++)
            {
                float w = candidates[i].BaseWeight;
                if (weightModifier != null)
                {
                    // apply external weight adjustments like density penalties
                    w *= Mathf.Max(0f, weightModifier(candidates[i]));
                }
                effectiveWeights[i] = w;
                totalWeight += w;
            }

            if (totalWeight <= 0f) return null;

            // random pick
            float r = (float)rng.NextDouble() * totalWeight;
            float current = 0f;

            for (int i = 0; i < candidates.Count; i++)
            {
                current += effectiveWeights[i];
                if (r <= current) return candidates[i];
            }

            return candidates[candidates.Count - 1]; // fallback
        }


    }
}
