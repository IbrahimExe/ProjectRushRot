// ------------------------------------------------------------
// Runner Level Generator 
// - Generates forward-only rows ahead of the player
// - Uses local neighborhood to weight placements
// - Guarantees: each Z row has >= 1 walkable lane
// - Prefabs handle their own behavior; generator only picks what/where
// ------------------------------------------------------------
using LevelGenerator.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

//main class
public class RunnerLevelGenerator : MonoBehaviour
{
    [Header("Generation Config")]
    [Tooltip("The active level generation config. Points to PrefabCatalog and Gen parameters.")]
    [SerializeField] private RunnerGenConfig config;
    public RunnerGenConfig Config => config;

    // CONSTANTS
    private const int PATH_BUFFER_PADDING = 10;
    private const float WEIGHT_EPSILON = 0.0001f;
    private const float PERLIN_WAVE_OFFSET = 10.0f;

    [Header("Seeding")]
    [Tooltip("Seed for deterministic generation")]
    public string seed = "hjgujklfu";

    private Dictionary<(int z, int lane), CellState> grid;
    private int generatorZ = 0;
    private System.Random rng;

    private int restRowsRemaining = 0;
    private int wavePhaseZ = 0;
    private int restCooldown = 0;

    // Golden Path State
    private HashSet<(int z, int lane)> goldenPathSet = new HashSet<(int, int)>();
    private PrefabDef cachedSafePathDef;
    private int pathTipZ = -1;
    private int pathTipLane = 1;
    private float pathLaneF;
    private Vector2 seedOffset;

    [Header("References")]
    [SerializeField] private Transform playerTransform;

    private Dictionary<(int z, int lane), GameObject> spawnedObjects = new Dictionary<(int, int), GameObject>();
    private Dictionary<(int z, int lane), GameObject> spawnedOccupant = new();
    private Dictionary<(int lane, OccupantType type), int> lastSpawnedZ = new Dictionary<(int, OccupantType), int>();

    private int TotalLaneCount => config.laneCount + 2;
    private bool IsEdgeLaneIndex(int lane) => lane == 0 || lane == TotalLaneCount - 1;

    private Dictionary<int, int> laneNextAllowedZ = new Dictionary<int, int>();

    // -------------------------------------------------------

    void Start()
    {
        if (!ValidateConfiguration())
        {
            Debug.LogError("[RunnerLevelGenerator] Configuration validation failed. Generator not enabled.");
            enabled = false;
            return;
        }

        if (config == null || config.catalog == null || config.weightRules == null)
        {
            Debug.LogError("[RunnerLevelGenerator] Missing config, catalog, or weight rules. Generator not enabled.");
            enabled = false;
            return;
        }

        config.catalog.RebuildCache();
        config.weightRules.catalog = config.catalog;
        config.weightRules.BuildCache();

        BuildNoiseSlices();
        cachedSafePathDef = config.catalog.Definitions.FirstOrDefault(d => d.Layer == ObjectLayer.Surface && d.SurfaceType == SurfaceType.SafePath);

        grid = new Dictionary<(int, int), CellState>();
        rng = !string.IsNullOrEmpty(seed)
            ? new System.Random(seed.GetHashCode())
            : new System.Random();

        // Seed noise offsets
        int h1 = seed != null ? seed.GetHashCode() : 0;
        int h2 = seed != null ? (seed + "_y").GetHashCode() : 0;
        seedOffset = new Vector2(h1 * 0.0001f, h2 * 0.0001f);

        pathTipLane = config.laneCount / 2;
        pathLaneF = pathTipLane;
        restRowsRemaining = 0;
        restCooldown = 0;
        wavePhaseZ = 0;
        pathTipZ = -1;

        if (playerTransform != null)
        {
            float centerX = LaneToWorldX(pathTipLane);
            Vector3 pPos = playerTransform.position;
            pPos.x = centerX;
            CharacterController cc = playerTransform.GetComponent<CharacterController>();
            if (cc != null) { cc.enabled = false; playerTransform.position = pPos; cc.enabled = true; }
            else playerTransform.position = pPos;
        }

        if (config.catalog != null) config.catalog.RebuildCache();

        UpdatePathBuffer(config.bufferRows + PATH_BUFFER_PADDING);

        while (generatorZ < config.bufferRows)
        {
            int chunkStart = generatorZ;
            int actualChunkSize = Mathf.Min(config.chunkSize, config.bufferRows - generatorZ);
            GenerateChunk(chunkStart, actualChunkSize);
            generatorZ += actualChunkSize;
        }
    }

    void Update()
    {
        if (playerTransform != null)
            UpdateGeneration(playerTransform.position.z);
    }



    private void UpdatePathBuffer(int targetZ)
    {
        while (pathTipZ < targetZ)
        {
            pathTipZ++;

            if (!config.useWavePath)
            {
                goldenPathSet.Add((pathTipZ, pathTipLane));
                continue;
            }

            if (restCooldown > 0) restCooldown--;

            if (restRowsRemaining > 0)
            {
                restRowsRemaining--;
                goldenPathSet.Add((pathTipZ, pathTipLane));
                if (restRowsRemaining == 0)
                    restCooldown = Mathf.Max(0, config.restAreaMinLength / 2);
                continue;
            }

            if (restCooldown == 0 && Rand() < config.restAreaFrequency)
            {
                int minLen = Mathf.Max(2, config.restAreaMinLength);
                int maxLen = Mathf.Max(minLen, config.restAreaMaxLength);
                restRowsRemaining = rng.Next(minLen, maxLen + 1);
                goldenPathSet.Add((pathTipZ, pathTipLane));
                continue;
            }

            wavePhaseZ++;
            float centerLane = (config.laneCount - 1) * 0.5f;
            float z = wavePhaseZ;

            float n1 = Mathf.PerlinNoise(z * config.waveFrequency + seedOffset.x, seedOffset.y + PERLIN_WAVE_OFFSET);
            float n2 = Mathf.PerlinNoise(z * (config.waveFrequency * 2.2f) + seedOffset.x, seedOffset.y + 77.7f);
            float wave = ((n1 * 2f) - 1f) * 0.85f + ((n2 * 2f) - 1f) * 0.15f;

            float amplitude = config.waveAmplitudeLanes * config.waveStrength;
            float targetLaneF = centerLane + wave * amplitude;

            float minLaneF = config.edgePadding;
            float maxLaneF = (config.laneCount - 1) - config.edgePadding;
            if (minLaneF >= maxLaneF) { minLaneF = 0f; maxLaneF = config.laneCount - 1; }

            targetLaneF = Mathf.Clamp(targetLaneF, minLaneF, maxLaneF);
            pathLaneF = Mathf.Lerp(pathLaneF, targetLaneF, config.waveSmoothing);

            float limitedNext = Mathf.MoveTowards((float)pathTipLane, pathLaneF, config.maxLaneChangePerRow);
            int nextLane = Mathf.Clamp(Mathf.RoundToInt(limitedNext), (int)minLaneF, (int)maxLaneF);

            if (Mathf.Abs(nextLane - pathTipLane) > 1)
            {
                int direction = (int)Mathf.Sign(nextLane - pathTipLane);
                int fill = pathTipLane + direction;
                while (fill != nextLane) { goldenPathSet.Add((pathTipZ, fill)); fill += direction; }
            }

            pathTipLane = nextLane;
            goldenPathSet.Add((pathTipZ, pathTipLane));
        }
    }

    private float LaneToWorldX(int lane)
    {
        float centerLane = (TotalLaneCount - 1) * 0.5f;
        return (lane - centerLane) * config.laneWidth;
    }

    // -------------------------------------------------------
    // Helpers
    // -------------------------------------------------------

    private bool LaneIsBorder(int lane) => lane == 1 || lane == config.laneCount;

    private bool LaneIsCenter(int lane)
    {
        int mid = (config.laneCount - 1) / 2;
        return lane == mid || (lane == mid + (config.laneCount % 2 == 0 ? 1 : 0));
    }

    float Rand() => (float)rng.NextDouble();
    private float Clamp(float v) => Mathf.Clamp01(v);

    private CellState getCell(int z, int lane)
    {
        if (grid.TryGetValue((z, lane), out CellState state)) return state;
        return new CellState(SurfaceType.Solid, OccupantType.None);
    }

    private void SetCell(int z, int lane, CellState state) => grid[(z, lane)] = state;

    private bool IsWalkable(int z, int lane)
    {
        CellState c = getCell(z, lane);
        if (c.isEdgeLane || c.surface == SurfaceType.Hole || c.surface == SurfaceType.Edge) return false;
        return c.occupant == OccupantType.None || c.occupant == OccupantType.Collectible;
    }

    private bool RowHasAnyWalkable(int z)
    {
        for (int lane = 0; lane < TotalLaneCount; lane++)
        {
            if (IsEdgeLaneIndex(lane)) continue;
            if (IsWalkable(z, lane)) return true;
        }
        return false;
    }

    private bool WouldBreakRowIfPlaced(int z, int lane, CellState newState)
    {
        CellState old = getCell(z, lane);
        SetCell(z, lane, newState);
        bool ok = RowHasAnyWalkable(z);
        SetCell(z, lane, old);
        return !ok;
    }

    // -------------------------------------------------------
    // Chunk Generation
    // -------------------------------------------------------

    private struct NoiseSlice
    {
        public PrefabDef def;
        public float min;
        public float max;
    }

    private Dictionary<NoiseConfig, List<NoiseSlice>> currentChunkSlices = new Dictionary<NoiseConfig, List<NoiseSlice>>();

    private void BuildNoiseSlices()
    {
        currentChunkSlices.Clear();
        var candidates = config.catalog.Definitions.Where(d => d.Layer == ObjectLayer.Surface && d.isNoiseCandidate && d.noiseChannel != null).ToList();
        var grouped = candidates.GroupBy(d => d.noiseChannel);

        foreach (var group in grouped)
        {
            var sorted = group.OrderBy(d => d.noiseTier).ToList();
            int count = sorted.Count;
            if (count == 0) continue;

            float sliceSize = 1f / count;
            var slices = new List<NoiseSlice>();

            for (int i = 0; i < count; i++)
            {
                var def = sorted[i];
                float baseMin = i * sliceSize;
                float baseMax = (i + 1) * sliceSize;

                float min = baseMin;
                float max = baseMax + def.noiseBlend;

                slices.Add(new NoiseSlice { def = def, min = min, max = max });
            }
            currentChunkSlices[group.Key] = slices;
        }
    }

    private PrefabDef EvaluateNoiseSurface(int z, int lane)
    {
        // Both axes in world units so noise scales identically in X and Z.
        // worldNoiseScale controls how many world units fit in one full noise period.
        float scale = Mathf.Max(0.0001f, config.worldNoiseScale);
        float u = (lane * config.laneWidth) / scale;
        float v = (z * config.cellLength) / scale;
        Vector2 uv = new Vector2(u, v) + seedOffset;

        PrefabDef bestDef = null;
        int bestTier = -1;

        foreach (var kvp in currentChunkSlices)
        {
            NoiseConfig channel = kvp.Key;
            List<NoiseSlice> slices = kvp.Value;

            // Fixed resolution so the sampler's internal px = uv * res scale is consistent.
            float n = NoiseSampler.Sample(channel, uv, 100);

            PrefabDef channelBest = null;
            int channelBestTier = -1;

            foreach (var slice in slices)
            {
                if (n >= slice.min && n <= slice.max)
                {
                    if (slice.def.noiseTier > channelBestTier)
                    {
                        channelBestTier = slice.def.noiseTier;
                        channelBest = slice.def;
                    }
                }
            }

            if (channelBest != null && channelBestTier > bestTier)
            {
                bestTier = channelBestTier;
                bestDef = channelBest;
            }
        }

        return bestDef;
    }

    private void PlaceSurfaceFromNoise(int z)
    {
        for (int lane = 0; lane < TotalLaneCount; lane++)
        {
            if (IsEdgeLaneIndex(lane))
            {
                CellState edgeCell = new CellState(SurfaceType.Edge, OccupantType.None, edgeLane: true);
                edgeCell.isCollapsed = true;
                edgeCell.entropy = 0f;
                SetCell(z, lane, edgeCell);
                continue;
            }

            if (goldenPathSet.Contains((z, lane)))
            {
                CellState safeCell = new CellState(SurfaceType.SafePath, OccupantType.None);
                safeCell.surfaceDef = cachedSafePathDef;
                safeCell.isCollapsed = true;
                SetCell(z, lane, safeCell);
                continue;
            }

            PrefabDef surfaceDef = EvaluateNoiseSurface(z, lane);
            if (surfaceDef == null)
            {
                surfaceDef = config.catalog.Definitions.FirstOrDefault(d => d.Layer == ObjectLayer.Surface && d.SurfaceType == SurfaceType.Solid);
            }

            CellState cell = new CellState(surfaceDef != null ? surfaceDef.SurfaceType : SurfaceType.Solid, OccupantType.None);
            cell.surfaceDef = surfaceDef;
            cell.isCollapsed = true;
            SetCell(z, lane, cell);
        }
    }

    private void GenerateChunk(int startZ, int chunkSize)
    {
        int endZ = startZ + chunkSize;

        for (int z = startZ; z < endZ; z++) PlaceSurfaceFromNoise(z);

        GenerateOccupantsForChunk(startZ, endZ);
        GenerateEdgeWalls(startZ, endZ);

        for (int z = startZ; z < endZ; z++) SpawnRowVisuals(z);
    }

    // -------------------------------------------------------
    // WFC Solver for Occupants
    // -------------------------------------------------------
    // (Old surface-based SolveChunkSurfaces removed)
    // To be wired up to GenerateOccupantsForChunk in the future 
    // to handle occupants procedurally via WFC constraints!
    // -------------------------------------------------------

    private bool ValidateConfiguration()
    {
        if (config == null || config.catalog == null || config.weightRules == null)
        {
            Debug.LogError("[RunnerLevelGenerator] Config, catalog, or weight rules missing.", this);
            return false;
        }

        if (playerTransform == null)
            Debug.LogWarning("[RunnerLevelGenerator] playerTransform not assigned.", this);

        return true;
    }


    // -------------------------------------------------------
    // Occupant Generation
    // -------------------------------------------------------
    private void GenerateOccupantsForChunk(int startZ, int endZ)
    {
        var allOccupants = config.catalog.Definitions
            .Where(d => d.Layer == ObjectLayer.Occupant)
            .Where(d => d.OccupantType != OccupantType.EdgeWall)
            .ToList();

        if (allOccupants.Count == 0) return;

        int remainingBudget = config.densityBudget;

        for (int z = startZ; z < endZ; z++)
        {
            int walkableLanes = 0;
            for (int l = 0; l < TotalLaneCount; l++)
            {
                if (IsEdgeLaneIndex(l)) continue;
                if (IsWalkable(z, l)) walkableLanes++;
            }

            var lanes = Enumerable.Range(0, TotalLaneCount).OrderBy(_ => rng.Next()).ToList();

            foreach (int lane in lanes)
            {
                if (IsEdgeLaneIndex(lane)) continue;
                if (laneNextAllowedZ.TryGetValue(lane, out int nextAllowed) && z < nextAllowed) continue;

                CellState c = getCell(z, lane);
                if (c.surface == SurfaceType.Hole) continue;
                if (c.occupant != OccupantType.None) continue;
                if (rng.NextDouble() > config.globalSpawnChance) continue;

                var candidates = new List<PrefabDef>();

                foreach (var def in allOccupants)
                {
                    if (remainingBudget - def.Cost < 0) continue;
                    if (def.AllowedSurfaceIDs != null && def.AllowedSurfaceIDs.Count > 0)
                    {
                        if (c.surfaceDef == null) continue;
                        if (!def.AllowedSurfaceIDs.Contains(c.surfaceDef.ID)) continue;
                    }
                    if (goldenPathSet.Contains((z, lane)) && !def.IsWalkable) continue;
                    if (!def.IsWalkable && walkableLanes <= 1) continue;

                    int effectiveTypeGap = Mathf.Max(def.MinRowGap, GetFootprintZ(def));
                    if (lastSpawnedZ.TryGetValue((lane, def.OccupantType), out int lastZ))
                        if (z - lastZ < effectiveTypeGap) continue;

                    candidates.Add(def);
                }

                if (candidates.Count == 0) continue;

                float totalWeight = candidates.Sum(d => d.OccupantWeight);
                float roll = (float)(rng.NextDouble() * totalWeight);
                float acc = 0f;
                PrefabDef selected = candidates[candidates.Count - 1];
                foreach (var cand in candidates) { acc += cand.OccupantWeight; if (roll <= acc) { selected = cand; break; } }

                c.occupant = selected.OccupantType;
                c.occupantDef = selected;
                SetCell(z, lane, c);

                remainingBudget -= selected.Cost;
                lastSpawnedZ[(lane, selected.OccupantType)] = z;
                if (!selected.IsWalkable) walkableLanes--;
                laneNextAllowedZ[lane] = z + Mathf.Max(1, GetFootprintZ(selected));

                if (remainingBudget <= 0) break;
            }

            if (remainingBudget <= 0) break;
        }
    }

    private void GenerateEdgeWalls(int startZ, int endZ)
    {
        var edgeWallDefs = config.catalog.GetCandidates(OccupantType.EdgeWall);
        if (edgeWallDefs == null || edgeWallDefs.Count == 0)
        {
            Debug.LogWarning("[EdgeWalls] No EdgeWall occupants found in catalog!");
            return;
        }

        foreach (int lane in new[] { 0, TotalLaneCount - 1 })
        {
            for (int z = startZ; z < endZ; z++)
            {
                if (laneNextAllowedZ.TryGetValue(lane, out int nextAllowed) && z < nextAllowed) continue;
                CellState cell = getCell(z, lane);
                if (cell.occupant != OccupantType.None) continue;

                PrefabDef selectedWall = edgeWallDefs[rng.Next(edgeWallDefs.Count)];
                cell.occupant = OccupantType.EdgeWall;
                cell.occupantDef = selectedWall;
                SetCell(z, lane, cell);

                laneNextAllowedZ[lane] = z + Mathf.Max(1, GetFootprintZ(selectedWall));
                lastSpawnedZ[(lane, OccupantType.EdgeWall)] = z;
            }
        }
    }

    // -------------------------------------------------------
    // Visual Spawning
    // -------------------------------------------------------
    private void SpawnRowVisuals(int z)
    {
        for (int lane = 0; lane < TotalLaneCount; lane++)
        {
            var cell = getCell(z, lane);
            Vector3 worldPos = new Vector3(LaneToWorldX(lane), 0, z * config.cellLength);

            if (IsEdgeLaneIndex(lane))
            {
                GameObject surfObj = null;
                var edgeSurfaceCandidates = config.catalog.GetCandidates(SurfaceType.Edge);
                if (edgeSurfaceCandidates != null && edgeSurfaceCandidates.Count > 0)
                {
                    PrefabDef edgeSurfaceDef = edgeSurfaceCandidates[rng.Next(edgeSurfaceCandidates.Count)];
                    if (edgeSurfaceDef.Prefabs != null && edgeSurfaceDef.Prefabs.Count > 0)
                        surfObj = Instantiate(edgeSurfaceDef.Prefabs[rng.Next(edgeSurfaceDef.Prefabs.Count)], worldPos, Quaternion.identity, transform);
                }
                if (surfObj != null) spawnedObjects[(z, lane)] = surfObj;

                GameObject edgeWallObj = null;
                if (cell.occupant == OccupantType.EdgeWall && cell.occupantDef != null && cell.occupantDef.Prefabs.Count > 0)
                    edgeWallObj = Instantiate(cell.occupantDef.Prefabs[rng.Next(cell.occupantDef.Prefabs.Count)], worldPos, Quaternion.identity, transform);
                else if (cell.occupant == OccupantType.EdgeWall && config.catalog.debugEdgeWall != null)
                    edgeWallObj = Instantiate(config.catalog.debugEdgeWall, worldPos, Quaternion.identity, transform);

                if (edgeWallObj != null) spawnedOccupant[(z, lane)] = edgeWallObj;
                continue;
            }

            // Surface
            GameObject surfaceObj = null;
            if (cell.surfaceDef != null && cell.surfaceDef.Prefabs.Count > 0)
                surfaceObj = Instantiate(cell.surfaceDef.Prefabs[rng.Next(cell.surfaceDef.Prefabs.Count)], worldPos, Quaternion.identity, transform);
            else if (cell.surface == SurfaceType.SafePath && config.catalog.debugSafePath != null)
                surfaceObj = Instantiate(config.catalog.debugSafePath, worldPos, Quaternion.identity, transform);
            else if (config.catalog.debugSurfaceDef != null && config.catalog.debugSurfaceDef.Prefabs.Count > 0)
                surfaceObj = Instantiate(config.catalog.debugSurfaceDef.Prefabs[rng.Next(config.catalog.debugSurfaceDef.Prefabs.Count)], worldPos, Quaternion.identity, transform);

            if (surfaceObj != null) spawnedObjects[(z, lane)] = surfaceObj;

            // Occupant
            bool isOriginCell = true;
            if (cell.occupantDef != null && cell.occupantDef.SizeZ > 1)
            {
                CellState prevCell = getCell(z - 1, lane);
                if (prevCell.occupantDef != null && prevCell.occupant == cell.occupant && prevCell.occupantDef.ID == cell.occupantDef.ID)
                    isOriginCell = false;
            }

            if (isOriginCell)
            {
                GameObject occObj = null;
                if (cell.occupantDef != null && cell.occupantDef.Prefabs.Count > 0)
                    occObj = Instantiate(cell.occupantDef.Prefabs[rng.Next(cell.occupantDef.Prefabs.Count)], worldPos, Quaternion.identity, transform);
                else if (cell.occupant != OccupantType.None && config.catalog.debugOccupant != null)
                    occObj = Instantiate(config.catalog.debugOccupant, worldPos, Quaternion.identity, transform);

                if (occObj != null) spawnedOccupant[(z, lane)] = occObj;
            }
        }
    }

    // -------------------------------------------------------
    // Update / Cleanup
    // -------------------------------------------------------
    public void UpdateGeneration(float playerZWorld)
    {
        int playerZIndex = Mathf.FloorToInt(playerZWorld / config.cellLength);
        int targetZ = playerZIndex + config.bufferRows;

        UpdatePathBuffer(targetZ + config.chunkSize + 20);

        while (generatorZ < targetZ)
        {
            int chunkStart = generatorZ;
            GenerateChunk(chunkStart, config.chunkSize);
            generatorZ += config.chunkSize;
        }

        int minKeepZ = playerZIndex - config.keepRowsBehind;
        var toRemove = grid.Keys.Where(k => k.z < minKeepZ).ToList();

        foreach (var key in toRemove)
        {
            if (spawnedObjects.TryGetValue(key, out GameObject surf) && surf != null) { Destroy(surf); spawnedObjects.Remove(key); }
            if (spawnedOccupant.TryGetValue(key, out GameObject occ) && occ != null) { Destroy(occ); spawnedOccupant.Remove(key); }
            grid.Remove(key);
        }

        foreach (var k in lastSpawnedZ.Where(kv => kv.Value < minKeepZ).Select(kv => kv.Key).ToList())
            lastSpawnedZ.Remove(k);

        foreach (var k in laneNextAllowedZ.Where(kv => kv.Value < minKeepZ).Select(kv => kv.Key).ToList())
            laneNextAllowedZ.Remove(k);
    }

    private int GetFootprintZ(PrefabDef def)
    {
        if (def == null) return 1;
        return Mathf.Max(1, def.SizeZ > 1 ? def.SizeZ : def.Size.z);
    }

    // -------------------------------------------------------

}
