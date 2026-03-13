using System.Collections.Generic;
using UnityEngine;

namespace LevelGenerator.Data
{
    public enum PrefabCategory
    {
        Obstacle,
        Enemy,
        Collectable,
        Decoration,
        Wall
    }

    [System.Serializable]
    public class NewPrefabDef
    {
        public string Name;

        // Auto-generated from Name on the editor panel. Used by Spawning tab to reference entries.
        public string ID;

        public PrefabCategory Category;

        // Visual variants only — same footprint and category, different meshes.
        // Generator picks one at random at spawn time.
        public List<GameObject> Variants = new List<GameObject>();

        // Grid space this prefab occupies. X = lanes wide, Y = rows deep. Minimum (1,1).
        public Vector2Int Footprint = Vector2Int.one;
    }

    [CreateAssetMenu(fileName = "NewPrefabCatalog", menuName = "Runner/New Prefab Catalog")]
    public class NewPrefabCatalog : ScriptableObject
    {
        public List<NewPrefabDef> Definitions = new List<NewPrefabDef>();

        private Dictionary<string, NewPrefabDef> _idIndex;
        private Dictionary<PrefabCategory, List<NewPrefabDef>> _categoryIndex;
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
                    Debug.LogWarning($"[NewPrefabCatalog] Duplicate ID '{def.ID}' — only the first entry is reachable by ID.", this);

                if (def.Variants == null || def.Variants.Count == 0)
                    Debug.LogWarning($"[NewPrefabCatalog] '{def.Name}' has no variants assigned.", this);

                if (def.Footprint.x < 1) def.Footprint.x = 1;
                if (def.Footprint.y < 1) def.Footprint.y = 1;
            }
        }

        public void RebuildCache()
        {
            _idIndex = new Dictionary<string, NewPrefabDef>();
            _categoryIndex = new Dictionary<PrefabCategory, List<NewPrefabDef>>();
            _initialized = false;

            foreach (PrefabCategory c in System.Enum.GetValues(typeof(PrefabCategory)))
                _categoryIndex[c] = new List<NewPrefabDef>();

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

        public NewPrefabDef GetByID(string id)
        {
            EnsureCache();
            return (!string.IsNullOrEmpty(id) && _idIndex.TryGetValue(id, out var def)) ? def : null;
        }

        public List<NewPrefabDef> GetByCategory(PrefabCategory category)
        {
            EnsureCache();
            return _categoryIndex.TryGetValue(category, out var list) ? list : new List<NewPrefabDef>();
        }
    }
}