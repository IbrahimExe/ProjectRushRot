using System.Collections.Generic;
using UnityEngine;

    public enum PrefabCategory
    {
        Obstacle,
        Enemy,
        Collectable,
        Decoration,
        Wall
    }

    [System.Serializable]
    public class PrefabDef
    {
        public string Name;
        public string ID;
        public PrefabCategory Category;
        public List<GameObject> Variants = new List<GameObject>();
        public float Footprint = 1f;
    }

    [CreateAssetMenu(fileName = "PrefabCatalog", menuName = "Runner/Prefab Catalog (New)")]
    public class PrefabCatalog : ScriptableObject
    {
        public List<PrefabDef> Definitions = new List<PrefabDef>();

        private Dictionary<string, PrefabDef> _idIndex;
        private Dictionary<PrefabCategory, List<PrefabDef>> _categoryIndex;
        private bool _initialized;

        private void OnEnable() => RebuildCache();
        private void OnValidate() { ValidateDefinitions(); _initialized = false; }

        private void ValidateDefinitions()
        {
            if (Definitions == null) return;
            var usedIDs = new HashSet<string>();

            foreach (var def in Definitions)
            {
                if (def == null) continue;

                if (string.IsNullOrEmpty(def.ID) && !string.IsNullOrEmpty(def.Name))
                    def.ID = def.Name.ToUpperInvariant().Replace(" ", "_");

                if (!string.IsNullOrEmpty(def.ID) && !usedIDs.Add(def.ID))
                    Debug.LogWarning($"[PrefabCatalog] Duplicate ID '{def.ID}' — only the first entry is reachable by ID.", this);

                if (def.Variants == null || def.Variants.Count == 0)
                    Debug.LogWarning($"[PrefabCatalog] '{def.Name}' has no variants assigned.", this);

                if (def.Footprint < 1f) def.Footprint = 1f;
            }
        }

        public void RebuildCache()
        {
            _idIndex = new Dictionary<string, PrefabDef>();
            _categoryIndex = new Dictionary<PrefabCategory, List<PrefabDef>>();
            _initialized = false;

            foreach (PrefabCategory c in System.Enum.GetValues(typeof(PrefabCategory)))
                _categoryIndex[c] = new List<PrefabDef>();

            if (Definitions == null) { _initialized = true; return; }

            foreach (var def in Definitions)
            {
                if (def == null || string.IsNullOrEmpty(def.ID)) continue;
                if (!_idIndex.ContainsKey(def.ID))
                    _idIndex[def.ID] = def;
                _categoryIndex[def.Category].Add(def);
            }

            _initialized = true;
        }

        private void EnsureCache()
        {
            if (!_initialized || _idIndex == null || _categoryIndex == null)
                RebuildCache();
        }

        public PrefabDef GetByID(string id)
        {
            EnsureCache();
            return (!string.IsNullOrEmpty(id) && _idIndex.TryGetValue(id, out var def)) ? def : null;
        }

        public List<PrefabDef> GetByCategory(PrefabCategory category)
        {
            EnsureCache();
            return _categoryIndex.TryGetValue(category, out var list) ? list : new List<PrefabDef>();
        }
    }