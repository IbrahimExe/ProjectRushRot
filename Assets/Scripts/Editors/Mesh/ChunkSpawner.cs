using LevelGenerator;
using System.Collections.Generic;
using UnityEngine;

public enum SpawnState
{
    None,
    StandIn,
    Full,
    Simulating
}

public class SpawnedInstance
{
    public Vector3      WorldPosition;
    public Quaternion   Rotation;
    public string       PrefabDefID;
    public PrefabCategory Category;
    public float        Footprint;
    public SpawnState   State;
    public GameObject   ActiveObject;
}

public class ChunkSpawner : MonoBehaviour
{
    SpawnConfig  _spawnConfig;
    PrefabCatalog _catalog;
    Vector2      _chunkCenter;
    int          _seed;
    Transform _spawnRoot;
    ObjectPoolManager _poolManager;
    

    // Heightmap and normals from MapData
    float[,]  _heightMap;
    Vector3[] _normals;
    int       _mapSize;
    float     _chunkWorldSize;
    float     _heightMultiplier;
    AnimationCurve _heightCurve;

    List<SpawnedInstance> _instances = new List<SpawnedInstance>();
    Dictionary<string, int> _poolOverflow = new Dictionary<string, int>();
    bool _placed = false;

    // Called by TerrainChunk after MapData and mesh are ready
    public void Initialise(
        MapData mapData, Vector3[] bakedNormals,
        SpawnConfig spawnConfig, PrefabCatalog catalog,
        Vector2 chunkCenter, float chunkWorldSize,
        float heightMultiplier, AnimationCurve heightCurve, Transform spawnRoot)
    {
        _spawnConfig     = spawnConfig;
        _catalog         = catalog;
        _chunkCenter     = chunkCenter;
        _chunkWorldSize  = chunkWorldSize;
        _heightMultiplier = heightMultiplier;
        _heightCurve     = heightCurve;
        _mapSize         = mapData.heightMap.GetLength(0);
        _normals         = bakedNormals;
        _spawnRoot = spawnRoot;
        _poolManager = ServiceLocator.Get<ObjectPoolManager>();
        Debug.Log($"[ChunkSpawner] PoolManager: {(_poolManager != null ? "found" : "NULL")}");

        _catalog.RebuildCache();

        // Copy heightmap — threading safety, we're on main thread here
        _heightMap = (float[,])mapData.heightMap.Clone();

        // Deterministic seed from chunk position
        _seed = Mathf.RoundToInt(chunkCenter.x * 1000f) ^ Mathf.RoundToInt(chunkCenter.y * 1000f);

        GeneratePlacements();
        _placed = true;
    }

    void GeneratePlacements()
    {

        if (_spawnConfig == null || _catalog == null) { 
            Debug.LogWarning($"[ChunkSpawner] Missing spawn config or prefab catalog for chunk {_chunkCenter}, skipping placement.");
            return;
        }

        foreach (var rule in _spawnConfig.Rules)
        {
            var def = _catalog.GetByID(rule.PrefabDefID);
            //Debug.LogWarning($"[ChunkSpawner] Looking up '{rule.PrefabDefID}' in catalog '{_catalog?.name}', def found: {def != null}");
            if (def == null) continue;

            float effectiveDensity = _spawnConfig.Density * rule.DensityMultiplier;
            float area             = _chunkWorldSize * _chunkWorldSize;
            int   targetCount      = Mathf.Max(1, Mathf.RoundToInt(effectiveDensity * area / 100f));

            List<Vector3> candidates = rule.PlacementMode == PlacementMode.PoissonDisk
                ? GeneratePoissonPoints(rule, targetCount)
                : GenerateGridPoints(rule, targetCount);
            //Debug.Log($"[ChunkSpawner] Center:{_chunkCenter} WorldSize:{_chunkWorldSize} MapSize:{_mapSize}");
            foreach (var candidate in candidates)
            {
                float rawNoise = SampleRawNoise(candidate.x, candidate.z);
                if (!PassesHeightCheck(rawNoise, rule))   continue;
                if (!PassesSlopeCheck(candidate, rule))    continue;
                if (!PassesOverlapCheck(candidate, rule, def)) continue;

                var instance = new SpawnedInstance
                {
                    WorldPosition = candidate,
                    Rotation      = RandomYRotation(candidate),
                    PrefabDefID   = rule.PrefabDefID,
                    Category      = def.Category,
                    Footprint     = def.Footprint,
                    State         = SpawnState.None
                };

                _instances.Add(instance);
                //Debug.Log($"[ChunkSpawner] Rule '{rule.PrefabDefID}' — candidates: {candidates.Count}, placed: {_instances.Count}");
            }
        }
        
        //Debug.Log($"[ChunkSpawner] Chunk {_chunkCenter} placed {_instances.Count} instances across {_spawnConfig.Rules.Count} rules.");
    }

    // Called every time TerrainChunk.UpdateTerrainChunk runs
    public void UpdateLODTier(float viewerDst)
    {
        Vector2 viewerPos = EndlessTerrain.viewerPosition;

        foreach (var inst in _instances)
        {
            float dst = Vector2.Distance(
                viewerPos,
                new Vector2(inst.WorldPosition.x, inst.WorldPosition.z));
            
            // dst is in unscaled units. Multiply by scale to get real world distance!
            float worldDst = dst * transform.localScale.x;

            SpawnState target;

            if (worldDst <= _spawnConfig.SimDistance) target = SpawnState.Simulating;
            else if (worldDst <= _spawnConfig.FullDistance) target = SpawnState.Full;
            else if (worldDst <= _spawnConfig.StandInDistance) target = SpawnState.StandIn;
            else target = SpawnState.None;

            if (target != inst.State)
                TransitionState(inst, target);
        }
    }

    void TransitionState(SpawnedInstance inst, SpawnState target)
    {
        // If already instantiated and just toggling simulation — don't recreate
        if (inst.ActiveObject != null &&
            (target == SpawnState.Full || target == SpawnState.Simulating) &&
            (inst.State == SpawnState.Full || inst.State == SpawnState.Simulating))
        {
            var r = GetRuleForDef(inst.PrefabDefID);
            SetSimulation(inst.ActiveObject, target == SpawnState.Simulating && r != null && r.EnableSimulation);
            inst.State = target;
            return;
        }

        if (inst.ActiveObject != null)
        {
            DestroyInstance(inst);
        }

        var def = _catalog.GetByID(inst.PrefabDefID);
        if (def == null) { inst.State = SpawnState.None; return; }

        switch (target)
        {
            case SpawnState.None:
                break;

            case SpawnState.StandIn:
                break;

            case SpawnState.Full:
            case SpawnState.Simulating:
                if (def.Variants == null || def.Variants.Count == 0) break;

                if (_poolManager != null && _poolManager.TryFetch(inst.PrefabDefID, out GameObject pooled))
                {
                    // Successful fetch — reduce overflow count if it was tracked
                    if (_poolOverflow.ContainsKey(inst.PrefabDefID) && _poolOverflow[inst.PrefabDefID] > 0)
                        _poolOverflow[inst.PrefabDefID]--;

                    pooled.transform.SetPositionAndRotation(inst.WorldPosition, inst.Rotation);
                    pooled.SetActive(true);
                    inst.ActiveObject = pooled;
                }
                else
                {
                    if (!_poolOverflow.ContainsKey(inst.PrefabDefID))
                        _poolOverflow[inst.PrefabDefID] = 0;
                    _poolOverflow[inst.PrefabDefID]++;

                    Debug.LogWarning($"[ChunkSpawner] Pool too small for '{inst.PrefabDefID}' — overflow: {_poolOverflow[inst.PrefabDefID]}");
                    inst.State = SpawnState.None;
                    break;
                }

                var rule = GetRuleForDef(inst.PrefabDefID);
                SetSimulation(inst.ActiveObject, target == SpawnState.Simulating && rule != null && rule.EnableSimulation);
                break;
        }

        inst.State = target;
    }
    SpawnRule GetRuleForDef(string prefabDefID)
{
    if (_spawnConfig == null) return null;
    foreach (var rule in _spawnConfig.Rules)
        if (rule.PrefabDefID == prefabDefID) return rule;
    return null;
}

    // Called by TerrainChunk.SetVisible(false)
    public void Despawn()
    {
        foreach (var inst in _instances)
        {
            DestroyInstance(inst);
            inst.State = SpawnState.None;
        }
    }

    void DestroyInstance(SpawnedInstance inst)
    {
        if (inst.ActiveObject == null) return;

        if (_poolManager != null)
            _poolManager.Recycle(inst.PrefabDefID, inst.ActiveObject);
        else
            Destroy(inst.ActiveObject);

        inst.ActiveObject = null;
    }

    //Placement helpers
    List<Vector3> GeneratePoissonPoints(SpawnRule rule, int targetCount)
    {
        var rng       = new System.Random(_seed ^ rule.PrefabDefID.GetHashCode());
        var result    = new List<Vector3>();
        int attempts  = targetCount * 30;
        float half    = _chunkWorldSize * 0.5f;

        for (int i = 0; i < attempts && result.Count < targetCount; i++)
        {
            float wx = _chunkCenter.x + (float)(rng.NextDouble() * _chunkWorldSize - half);
            float wz = _chunkCenter.y + (float)(rng.NextDouble() * _chunkWorldSize - half);
            float wy = SampleWorldHeight(wx, wz);
            var   pt = new Vector3(wx, wy, wz);

            bool tooClose = false;
            foreach (var existing in result)
            {
                float dx = existing.x - pt.x;
                float dz = existing.z - pt.z;
                if (Mathf.Sqrt(dx * dx + dz * dz) < rule.MinSpacing)
                { tooClose = true; break; }
            }

            if (!tooClose) result.Add(pt);
        }

        return result;
    }

    List<Vector3> GenerateGridPoints(SpawnRule rule, int targetCount)
    {
        var   rng    = new System.Random(_seed ^ rule.PrefabDefID.GetHashCode());
        var   result = new List<Vector3>();
        int   side   = Mathf.CeilToInt(Mathf.Sqrt(targetCount));
        float step   = _chunkWorldSize / side;
        float half   = _chunkWorldSize * 0.5f;
        float jitter = step * 0.4f;

        for (int row = 0; row < side; row++)
        {
            for (int col = 0; col < side; col++)
            {
                float wx = _chunkCenter.x - half + col * step + step * 0.5f
                         + (float)(rng.NextDouble() * 2 - 1) * jitter;
                float wz = _chunkCenter.y - half + row * step + step * 0.5f
                         + (float)(rng.NextDouble() * 2 - 1) * jitter;
                float wy = SampleWorldHeight(wx, wz);
                result.Add(new Vector3(wx, wy, wz));
            }
        }

        return result;
    }

    float SampleRawNoise(float wx, float wz)
    {
        float meshScale = _chunkWorldSize / Mathf.Max(1f, _mapSize - 3f);
        float halfMap = _mapSize * 0.5f;
        
        int x = Mathf.Clamp(Mathf.RoundToInt((wx - _chunkCenter.x) / meshScale + halfMap), 0, _mapSize - 1);
        int z = Mathf.Clamp(Mathf.RoundToInt(halfMap - (wz - _chunkCenter.y) / meshScale), 0, _mapSize - 1);
        
        return _heightMap[x, z];
    }

    float SampleWorldHeight(float wx, float wz)
    {
        return _heightCurve.Evaluate(SampleRawNoise(wx, wz)) * _heightMultiplier;
    }

    Vector3 SampleNormal(float wx, float wz)
    {
        if (_normals == null) return Vector3.up;
        
        float meshScale = _chunkWorldSize / Mathf.Max(1f, _mapSize - 3f);
        float halfMap = _mapSize * 0.5f;
        
        int x = Mathf.Clamp(Mathf.RoundToInt((wx - _chunkCenter.x) / meshScale + halfMap), 0, _mapSize - 1);
        int z = Mathf.Clamp(Mathf.RoundToInt(halfMap - (wz - _chunkCenter.y) / meshScale), 0, _mapSize - 1);
        int i = z * _mapSize + x;
        return i < _normals.Length ? _normals[i] : Vector3.up;
    }

    bool PassesHeightCheck(float rawNoise, SpawnRule rule)
    {
        if (rule.HeightMax <= rule.HeightMin) return true; // disabled
        return rawNoise >= rule.HeightMin && rawNoise <= rule.HeightMax;
    }

    bool PassesSlopeCheck(Vector3 pt, SpawnRule rule)
    {
        var normal = SampleNormal(pt.x, pt.z);
        float slope = Vector3.Angle(normal, Vector3.up);
        return slope <= rule.MaxSlope;
    }

    bool PassesOverlapCheck(Vector3 pt, SpawnRule rule, PrefabDef def)
    {
        if (rule.BlockedBy == null || rule.BlockedBy.Count == 0) return true;

        foreach (var inst in _instances)
        {
            if (!rule.BlockedBy.Contains(inst.Category)) continue;
            float minDist = def.Footprint + inst.Footprint;
            float dx = pt.x - inst.WorldPosition.x;
            float dz = pt.z - inst.WorldPosition.z;
            if (Mathf.Sqrt(dx * dx + dz * dz) < minDist)
                return false;
        }
        return true;
    }

    Quaternion RandomYRotation(Vector3 pt)
    {
        var rng   = new System.Random((int)(pt.x * 1000) ^ (int)(pt.z * 1000) ^ _seed);
        float deg = (float)(rng.NextDouble() * 360f);
        return Quaternion.Euler(0, deg, 0);
    }

    GameObject PickVariant(PrefabDef def, Vector3 pt)
    {
        var rng = new System.Random((int)(pt.x * 73856093) ^ (int)(pt.z * 19349663) ^ _seed);
        int idx = rng.Next(0, def.Variants.Count);
        return def.Variants[idx];
    }

    void SetSimulation(GameObject obj, bool active)
    {
        if (!active) return; // never disable kinematic from here — let the prefab control default state

        foreach (var rb in obj.GetComponentsInChildren<Rigidbody>())
            rb.isKinematic = !active;

        foreach (var col in obj.GetComponentsInChildren<Collider>())
            col.enabled = active;
    }
}
