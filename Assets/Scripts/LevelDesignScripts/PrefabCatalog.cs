using System.Collections.Generic;
using UnityEngine;
using LevelGenerator.Data;

[CreateAssetMenu(fileName = "PrefabCatalog", menuName = "Runner/Prefab Catalog")]
public class PrefabCatalog : ScriptableObject
{
    // -- Noise channel ---------------------------------------------------------
    // One channel for the whole catalog. All noise-candidate surface defs
    // compete within this single channel's [0,1] range, ranked by noiseTier.
    [Header("Noise")]
    [Tooltip("The noise config used to drive surface tile placement for all entries in this catalog.")]
    public NoiseConfig noiseChannel;

    // -- Registered prefabs ----------------------------------------------------
    [Header("Registered Prefabs")]
    public List<PrefabDef> Definitions = new List<PrefabDef>();

    // -- Debug / Fallbacks -----------------------------------------------------
    [Header("Debug / Fallbacks")]

    [Tooltip("Fallback surface for the golden/safe path if no SafePath surface def exists in Definitions.")]
    public GameObject debugSafePath;

    [Tooltip("Fallback solid surface used when noise placement finds no matching tile. " +
             "Assign a real Surface PrefabDef from your catalog.")]
    public PrefabDef debugSurfaceDef;

    [Tooltip("Fallback occupant spawned when an occupant is required but no candidate is found.")]
    public GameObject debugOccupant;

    [Tooltip("Required: prefab variants for the LEFT edge wall (lane 0). " +
             "One is picked at random each row. These replace the old debugEdgeWall single field.")]
    public List<GameObject> leftWallPrefabs = new List<GameObject>();

    [Tooltip("Required: prefab variants for the RIGHT edge wall (last lane). " +
             "One is picked at random each row.")]
    public List<GameObject> rightWallPrefabs = new List<GameObject>();

    // -- Fast lookup caches ----------------------------------------------------
    private Dictionary<SurfaceType, List<PrefabDef>> _surfaceCache;
    private Dictionary<string, PrefabDef> _idIndex;
    private bool _initialized;

    // -------------------------------------------------------------------------

    private void OnEnable() => RebuildCache();
    private void OnValidate() { ValidateDefinitions(); _initialized = false; }

    // -- Validation ------------------------------------------------------------

    private void ValidateDefinitions()
    {
        if (Definitions == null) return;

        var validSurfaceIDs = new HashSet<string>();

        // Pass 1: auto-generate IDs, collect surface IDs
        foreach (var def in Definitions)
        {
            if (def == null) continue;
            if (string.IsNullOrEmpty(def.ID) && !string.IsNullOrEmpty(def.Name))
                def.ID = def.Name.ToUpperInvariant().Replace(" ", "_");
            if (def.Layer == ObjectLayer.Surface && !string.IsNullOrEmpty(def.ID))
                validSurfaceIDs.Add(def.ID);
        }

        // Pass 2: warn on unknown AllowedSurfaceIDs
        foreach (var def in Definitions)
        {
            if (def == null || def.Layer != ObjectLayer.Occupant) continue;
            if (def.AllowedSurfaceIDs == null || def.AllowedSurfaceIDs.Count == 0) continue;

            for (int i = def.AllowedSurfaceIDs.Count - 1; i >= 0; i--)
            {
                string sid = def.AllowedSurfaceIDs[i];
                if (string.IsNullOrEmpty(sid)) { def.AllowedSurfaceIDs.RemoveAt(i); continue; }
                if (!validSurfaceIDs.Contains(sid))
                    Debug.LogWarning($"[PrefabCatalog] Occupant '{def.Name}' references unknown surface ID '{sid}'.", this);
            }
        }

        // Warn if edge wall lists are empty
        if (leftWallPrefabs == null || leftWallPrefabs.Count == 0)
            Debug.LogWarning("[PrefabCatalog] leftWallPrefabs is empty - left edge lane will spawn nothing.", this);
        if (rightWallPrefabs == null || rightWallPrefabs.Count == 0)
            Debug.LogWarning("[PrefabCatalog] rightWallPrefabs is empty - right edge lane will spawn nothing.", this);
    }

    // -- Cache -----------------------------------------------------------------

    public void RebuildCache()
    {
        _surfaceCache = new Dictionary<SurfaceType, List<PrefabDef>>();
        _idIndex = new Dictionary<string, PrefabDef>();
        _initialized = false;

        foreach (SurfaceType t in System.Enum.GetValues(typeof(SurfaceType)))
            _surfaceCache[t] = new List<PrefabDef>();

        if (Definitions == null) { _initialized = true; return; }

        foreach (var def in Definitions)
        {
            if (def == null || string.IsNullOrEmpty(def.ID)) continue;

            if (!_idIndex.ContainsKey(def.ID))
                _idIndex[def.ID] = def;

            if (def.Layer == ObjectLayer.Surface)
                _surfaceCache[def.SurfaceType].Add(def);
        }

        _initialized = true;
    }

    private void EnsureCache()
    {
        if (!_initialized || _surfaceCache == null || _idIndex == null)
            RebuildCache();
    }

    // -- Retrieval -------------------------------------------------------------

    public PrefabDef GetByID(string id)
    {
        EnsureCache();
        return (!string.IsNullOrEmpty(id) && _idIndex.TryGetValue(id, out var def)) ? def : null;
    }

    public List<PrefabDef> GetSurfaceCandidates(SurfaceType type)
    {
        EnsureCache();
        return _surfaceCache.TryGetValue(type, out var list) ? list : new List<PrefabDef>();
    }

    /// Returns all occupant-layer defs (filtering done by caller).
    public List<PrefabDef> GetAllOccupants()
    {
        EnsureCache();
        var result = new List<PrefabDef>();
        foreach (var def in Definitions)
            if (def != null && def.Layer == ObjectLayer.Occupant)
                result.Add(def);
        return result;
    }

    /// Returns all surface defs that are noise candidates (isNoiseCandidate = true).
    public List<PrefabDef> GetNoiseCandidates()
    {
        EnsureCache();
        var result = new List<PrefabDef>();
        foreach (var def in Definitions)
            if (def != null && def.Layer == ObjectLayer.Surface && def.isNoiseCandidate)
                result.Add(def);
        return result;
    }
}