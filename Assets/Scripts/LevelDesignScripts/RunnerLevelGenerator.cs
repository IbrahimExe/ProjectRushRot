// ------------------------------------------------------------
// Runner Level Generator
// - Noise-driven surface placement
// - Safe-path WFC blend pass (tiles adjacent to safe path are
//   re-evaluated by WFC using surface adjacency rules)
// - Budget-based occupant spawning
// - Distinct L/R edge walls
// - Biome (config) switching between chunks
// ------------------------------------------------------------
using LevelGenerator.Data;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RunnerLevelGenerator : MonoBehaviour
{
    // ─── Config ───────────────────────────────────────────────────────────────

    [Header("Generation Config")]
    [Tooltip("Active level generation config. Overridden at runtime by biome pool if populated.")]
    [SerializeField] private RunnerGenConfig config;
    public RunnerGenConfig Config => config;

    [Header("Biome Rotation")]
    [Tooltip("Pool of configs to cycle through. First entry is used at start. Leave empty to use only Config above.")]
    [SerializeField] private List<RunnerGenConfig> biomePool = new List<RunnerGenConfig>();
    [Range(0f, 1f)]
    [Tooltip("Probability per chunk boundary of attempting a biome switch.")]
    [SerializeField] private float biomeSwitchChance = 0.15f;
    [Min(1)]
    [Tooltip("Minimum chunks that must pass before a switch is allowed.")]
    [SerializeField] private int minChunksPerBiome = 5;

    // ─── Constants ────────────────────────────────────────────────────────────

    private const int PATH_BUFFER_PADDING = 10;
    private const float PERLIN_WAVE_OFFSET = 10.0f;

    // ─── Seeding ──────────────────────────────────────────────────────────────

    [Header("Seeding")]
    public string seed = "hjgujklfu";
    private Vector2 seedOffset;

    // ─── Runtime state ────────────────────────────────────────────────────────

    private Dictionary<(int z, int lane), CellState> grid;
    private int generatorZ = 0;
    private System.Random rng;

    // Golden path
    private HashSet<(int z, int lane)> goldenPathSet = new HashSet<(int, int)>();
    private PrefabDef cachedSafePathDef;
    private int pathTipZ = -1;
    private int pathTipLane = 1;
    private float pathLaneF;

    // Wave state
    private int restRowsRemaining = 0;
    private int wavePhaseZ = 0;
    private int restCooldown = 0;

    // Biome switching
    private int currentBiomeIndex = 0;
    private int chunksSinceLastSwitch = 0;

    [Header("References")]
    [SerializeField] private Transform playerTransform;

    // Spawned objects
    private Dictionary<(int z, int lane), GameObject> spawnedSurface = new();
    private Dictionary<(int z, int lane), GameObject> spawnedOccupant = new();

    // Per-lane gap tracking: keyed by (lane, defID) for per-def gaps
    private Dictionary<(int lane, string defID), int> lastSpawnedZ = new();
    private Dictionary<int, int> laneNextAllowedZ = new();

    // Noise slices built once per biome
    private List<NoiseSlice> currentSlices = new List<NoiseSlice>();

    // ─── Computed properties ──────────────────────────────────────────────────

    private int TotalLaneCount => config.laneCount + 2;
    private bool IsLeftEdge(int lane) => lane == 0;
    private bool IsRightEdge(int lane) => lane == TotalLaneCount - 1;
    private bool IsEdgeLane(int lane) => IsLeftEdge(lane) || IsRightEdge(lane);

    // =========================================================================
    // Lifecycle
    // =========================================================================

    void Start()
    {
        if (!ValidateConfiguration()) { enabled = false; return; }

        // Biome pool init
        if (biomePool != null && biomePool.Count > 0)
        {
            biomePool = biomePool.Where(c => c != null).ToList();
            if (biomePool.Count > 0) { currentBiomeIndex = 0; config = biomePool[0]; }
        }
        chunksSinceLastSwitch = 0;

        // RNG + seed offset
        rng = !string.IsNullOrEmpty(seed)
            ? new System.Random(seed.GetHashCode())
            : new System.Random();
        int h1 = seed != null ? seed.GetHashCode() : 0;
        int h2 = seed != null ? (seed + "_y").GetHashCode() : 0;
        seedOffset = new Vector2(h1 * 0.0001f, h2 * 0.0001f);

        InitializeConfig();

        grid = new Dictionary<(int, int), CellState>();

        pathTipLane = config.laneCount / 2;
        pathLaneF = pathTipLane;
        pathTipZ = -1;

        if (playerTransform != null)
        {
            float cx = LaneToWorldX(pathTipLane);
            Vector3 p = playerTransform.position; p.x = cx;
            var cc = playerTransform.GetComponent<CharacterController>();
            if (cc != null) { cc.enabled = false; playerTransform.position = p; cc.enabled = true; }
            else playerTransform.position = p;
        }

        UpdatePathBuffer(config.bufferRows + PATH_BUFFER_PADDING);

        while (generatorZ < config.bufferRows)
        {
            int sz = Mathf.Min(config.chunkSize, config.bufferRows - generatorZ);
            GenerateChunk(generatorZ, sz);
            generatorZ += sz;
        }
    }

    void Update()
    {
        if (playerTransform != null)
            UpdateGeneration(playerTransform.position.z);
    }

    // =========================================================================
    // Config / biome management
    // =========================================================================

    private void InitializeConfig()
    {
        config.catalog.RebuildCache();
        config.weightRules.catalog = config.catalog;
        config.weightRules.BuildCache();
        BuildNoiseSlices();
        cachedSafePathDef = config.catalog.Definitions
            .FirstOrDefault(d => d.Layer == ObjectLayer.Surface && d.SurfaceType == SurfaceType.SafePath);
    }

    private void ApplyConfig(RunnerGenConfig next)
    {
        if (next == null) return;
        config = next;
        InitializeConfig();
        Debug.Log($"[Biome] Switched to: {config.name}", this);
    }

    private void MaybeSwitchBiome()
    {
        if (biomePool == null || biomePool.Count <= 1) return;
        if (chunksSinceLastSwitch < minChunksPerBiome) return;
        if (Rand() > biomeSwitchChance) return;

        int next = rng.Next(biomePool.Count - 1);
        if (next >= currentBiomeIndex) next++;
        currentBiomeIndex = next;
        chunksSinceLastSwitch = 0;
        ApplyConfig(biomePool[currentBiomeIndex]);
    }

    // =========================================================================
    // Golden path (wave)
    // =========================================================================

    private void UpdatePathBuffer(int targetZ)
    {
        while (pathTipZ < targetZ)
        {
            pathTipZ++;

            if (!config.useWavePath)
            { goldenPathSet.Add((pathTipZ, pathTipLane)); continue; }

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
                int minL = Mathf.Max(2, config.restAreaMinLength);
                int maxL = Mathf.Max(minL, config.restAreaMaxLength);
                restRowsRemaining = rng.Next(minL, maxL + 1);
                goldenPathSet.Add((pathTipZ, pathTipLane));
                continue;
            }

            wavePhaseZ++;
            float centerLane = (config.laneCount - 1) * 0.5f;
            float fz = wavePhaseZ;

            float n1 = Mathf.PerlinNoise(fz * config.waveFrequency + seedOffset.x, seedOffset.y + PERLIN_WAVE_OFFSET);
            float n2 = Mathf.PerlinNoise(fz * (config.waveFrequency * 2.2f) + seedOffset.x, seedOffset.y + 77.7f);
            float wave = ((n1 * 2f) - 1f) * 0.85f + ((n2 * 2f) - 1f) * 0.15f;

            float amplitude = config.waveAmplitudeLanes * config.waveStrength;
            float targetLaneF = centerLane + wave * amplitude;

            float minLF = config.edgePadding;
            float maxLF = (config.laneCount - 1) - config.edgePadding;
            if (minLF >= maxLF) { minLF = 0f; maxLF = config.laneCount - 1; }

            targetLaneF = Mathf.Clamp(targetLaneF, minLF, maxLF);
            pathLaneF = Mathf.Lerp(pathLaneF, targetLaneF, config.waveSmoothing);

            float limitedNext = Mathf.MoveTowards((float)pathTipLane, pathLaneF, config.maxLaneChangePerRow);
            int nextLane = Mathf.Clamp(Mathf.RoundToInt(limitedNext), (int)minLF, (int)maxLF);

            if (Mathf.Abs(nextLane - pathTipLane) > 1)
            {
                int dir = (int)Mathf.Sign(nextLane - pathTipLane);
                int fill = pathTipLane + dir;
                while (fill != nextLane) { goldenPathSet.Add((pathTipZ, fill)); fill += dir; }
            }

            pathTipLane = nextLane;
            goldenPathSet.Add((pathTipZ, pathTipLane));
        }
    }

    // =========================================================================
    // Noise slice building
    // =========================================================================

    private struct NoiseSlice
    {
        public PrefabDef def;
        public float min, max;
    }

    private void BuildNoiseSlices()
    {
        currentSlices.Clear();
        if (config.catalog.noiseChannel == null) return;

        var candidates = config.catalog.GetNoiseCandidates()
            .OrderBy(d => d.noiseTier)
            .ToList();

        int count = candidates.Count;
        if (count == 0) return;

        float sliceSize = 1f / count;
        for (int i = 0; i < count; i++)
        {
            currentSlices.Add(new NoiseSlice
            {
                def = candidates[i],
                min = i * sliceSize,
                max = (i + 1) * sliceSize
            });
        }
    }

    // =========================================================================
    // Surface placement
    // =========================================================================

    private PrefabDef EvaluateNoiseSurface(int z, int lane)
    {
        if (currentSlices.Count == 0 || config.catalog.noiseChannel == null)
            return null;

        float scale = Mathf.Max(0.0001f, config.worldNoiseScale);
        float u = (lane * config.laneWidth) / scale;
        float v = (z * config.cellLength) / scale;
        Vector2 uv = new Vector2(u, v) + seedOffset;

        float n = NoiseSampler.Sample(config.catalog.noiseChannel, uv, 100);

        // Highest-tier matching slice wins
        PrefabDef best = null;
        int bestTier = -1;
        foreach (var s in currentSlices)
        {
            if (n >= s.min && n <= s.max && s.def.noiseTier > bestTier)
            {
                bestTier = s.def.noiseTier;
                best = s.def;
            }
        }
        return best;
    }

    // ── Fallback surface def ──────────────────────────────────────────────────

    private PrefabDef FallbackSurfaceDef()
    {
        // Try the debug surface def first, then any normal-layer surface in the catalog
        if (config.catalog.debugSurfaceDef != null) return config.catalog.debugSurfaceDef;
        return config.catalog.Definitions
            .FirstOrDefault(d => d.Layer == ObjectLayer.Surface && d.SurfaceType == SurfaceType.Normal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PlaceSurfaceFromNoise — first pass: every cell gets a noise-driven tile.
    // Edge lanes get EdgeL / EdgeR. Safe-path cells get SafePath.
    // ─────────────────────────────────────────────────────────────────────────
    private void PlaceSurfaceFromNoise(int z)
    {
        for (int lane = 0; lane < TotalLaneCount; lane++)
        {
            // ── Left edge ─────────────────────────────────────────────────────
            if (IsLeftEdge(lane))
            {
                var c = new CellState(SurfaceType.EdgeL, edgeLane: true);
                c.isCollapsed = true;
                SetCell(z, lane, c);
                continue;
            }

            // ── Right edge ────────────────────────────────────────────────────
            if (IsRightEdge(lane))
            {
                var c = new CellState(SurfaceType.EdgeR, edgeLane: true);
                c.isCollapsed = true;
                SetCell(z, lane, c);
                continue;
            }

            // ── Safe path ─────────────────────────────────────────────────────
            if (goldenPathSet.Contains((z, lane)))
            {
                // Use the catalog's dedicated safe-path def if it exists.
                // The catalog field is config.catalog.debugSafePath (GameObject) for the
                // actual prefab; cachedSafePathDef is a PrefabDef entry in Definitions.
                var c = new CellState(SurfaceType.SafePath);
                c.surfaceDef = cachedSafePathDef;
                c.isCollapsed = true;
                SetCell(z, lane, c);
                continue;
            }

            // ── Noise-driven tile ─────────────────────────────────────────────
            PrefabDef def = EvaluateNoiseSurface(z, lane) ?? FallbackSurfaceDef();
            var cell = new CellState(def?.SurfaceType ?? SurfaceType.Normal);
            cell.surfaceDef = def;
            cell.isCollapsed = true;
            SetCell(z, lane, cell);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BlendSafePathWithWFC
    //
    // Second pass after PlaceSurfaceFromNoise.
    // Re-resolves lanes within safePathBlendRadius of any safe-path cell so
    // the scenery transitions smoothly into the path (e.g. a shore tile appears
    // between water and the path, not water directly adjacent).
    //
    // WHY the old left-to-right pass didn't work:
    //   When processing a blend cell, its inner neighbor (closer to the path)
    //   was still uncollapsed — it had been un-collapsed but not yet resolved.
    //   So the WFC had no inner anchor, just the outer noise tile, and couldn't
    //   bridge the gap. The result was incoherent, context-free placement.
    //
    // Fix — OUTSIDE-IN ring processing:
    //
    //   [noise]─ offset 3 ─ offset 2 ─ offset 1 ─[SafePath]
    //    anchor   resolved   resolved   resolved    anchor
    //
    //   Ring at offset 3 is resolved first. Its outer neighbor is the original
    //   noise tile (collapsed anchor) and its inner neighbor is the path or a
    //   previously resolved ring.  Every cell therefore has TWO anchors when it
    //   is solved and can pick a tile that genuinely bridges both sides.
    //
    //   Within each ring, rows are processed in Z order so forward/backward
    //   Z-neighbors resolved earlier in the same ring also constrain the cell.
    //
    // Fallback: if adjacency rules over-constrain a cell to zero candidates,
    //   pick the noise candidate whose tier midpoint is closest to the raw noise
    //   value at that position — degrades gracefully without snapping randomly.
    // ─────────────────────────────────────────────────────────────────────────
    private void BlendSafePathWithWFC(int startZ, int endZ)
    {
        if (config.safePathBlendRadius <= 0) return;

        var allNoiseCandidates = config.catalog.GetNoiseCandidates();
        if (allNoiseCandidates.Count == 0) return;

        int radius = config.safePathBlendRadius;

        // ── Build per-offset rings ────────────────────────────────────────────
        // blendByOffset[o] = all (z,lane) cells at lateral offset o from the path.
        var blendByOffset = new Dictionary<int, HashSet<(int z, int lane)>>();
        for (int o = 1; o <= radius; o++)
            blendByOffset[o] = new HashSet<(int z, int lane)>();

        for (int z = startZ; z < endZ; z++)
        {
            for (int lane = 1; lane < TotalLaneCount - 1; lane++)
            {
                if (!goldenPathSet.Contains((z, lane))) continue;

                for (int o = 1; o <= radius; o++)
                {
                    int lL = lane - o;
                    int lR = lane + o;
                    if (lL >= 1 && !goldenPathSet.Contains((z, lL)) && !IsEdgeLane(lL))
                        blendByOffset[o].Add((z, lL));
                    if (lR < TotalLaneCount - 1 && !goldenPathSet.Contains((z, lR)) && !IsEdgeLane(lR))
                        blendByOffset[o].Add((z, lR));
                }
            }
        }

        if (!blendByOffset.Values.Any(s => s.Count > 0)) return;

        // ── Un-collapse all blend cells ───────────────────────────────────────
        // We un-collapse every ring including the outermost.  The cells one step
        // BEYOND the outermost ring are still the original noise tiles and act as
        // the outer anchor automatically — we never touch them.
        for (int o = 1; o <= radius; o++)
        {
            foreach (var key in blendByOffset[o])
            {
                var cell = getCell(key.z, key.lane);
                cell.isCollapsed = false;
                cell.surfaceCandidates = new List<PrefabDef>(allNoiseCandidates);
                SetCell(key.z, key.lane, cell);
            }
        }

        // ── Collapse outside-in, one ring at a time ───────────────────────────
        for (int o = radius; o >= 1; o--)
        {
            var ring = blendByOffset[o];
            if (ring.Count == 0) continue;

            // Z-order within the ring so forward/backward Z-neighbors already
            // resolved in this ring can propagate.
            var ordered = ring.OrderBy(k => k.z).ThenBy(k => k.lane).ToList();

            foreach (var key in ordered)
            {
                var cell = getCell(key.z, key.lane);
                if (cell.isCollapsed) continue;

                List<PrefabDef> candidates = new List<PrefabDef>(allNoiseCandidates);

                // Constrain against each cardinal neighbor that is already collapsed.
                // Outer ring (o+1, or raw noise): anchors to the biome.
                // Inner ring (o-1, or SafePath):  anchors to the path.
                // Same-ring Z-neighbors:           enforces Z continuity.
                foreach (Direction dir in new[] {
                    Direction.Left, Direction.Right,
                    Direction.Forward, Direction.Backward })
                {
                    var (nz, nl) = NeighborCoord(key.z, key.lane, dir);
                    if (nl < 0 || nl >= TotalLaneCount) continue;

                    var neighbor = getCell(nz, nl);
                    if (!neighbor.isCollapsed) continue;

                    // SafePath with no assigned surfaceDef (debug-only fallback):
                    // skip rather than hard-blocking everything from that side.
                    if (neighbor.surface == SurfaceType.SafePath && neighbor.surfaceDef == null)
                        continue;

                    if (neighbor.surfaceDef == null) continue;

                    candidates = config.weightRules.GetAllowedSurfaceNeighbors(
                        neighbor.surfaceDef, OppositeDir(dir), candidates);
                }

                // Graceful fallback: pick closest-tier candidate to raw noise value.
                if (candidates.Count == 0)
                    candidates = new List<PrefabDef> { ClosestNoiseTierDef(key.z, key.lane, allNoiseCandidates) };

                PrefabDef chosen = WeightedRandom(candidates);

                cell.surfaceDef = chosen;
                cell.surface = chosen?.SurfaceType ?? SurfaceType.Normal;
                cell.isCollapsed = true;
                cell.surfaceCandidates = null;
                SetCell(key.z, key.lane, cell);
            }
        }
    }

    // Returns the noise candidate whose tier-slice midpoint is closest to the
    // raw noise value at (z, lane). Used when WFC rules over-constrain to zero.
    private PrefabDef ClosestNoiseTierDef(int z, int lane, List<PrefabDef> candidates)
    {
        if (candidates.Count == 0) return FallbackSurfaceDef();
        if (currentSlices.Count == 0 || config.catalog.noiseChannel == null)
            return candidates[0];

        float scale = Mathf.Max(0.0001f, config.worldNoiseScale);
        float u = (lane * config.laneWidth) / scale;
        float v = (z * config.cellLength) / scale;
        float n = NoiseSampler.Sample(config.catalog.noiseChannel,
                          new Vector2(u, v) + seedOffset, 100);

        PrefabDef best = null;
        float bestDist = float.MaxValue;

        foreach (var slice in currentSlices)
        {
            if (!candidates.Contains(slice.def)) continue;
            float dist = Mathf.Abs(n - (slice.min + slice.max) * 0.5f);
            if (dist < bestDist) { bestDist = dist; best = slice.def; }
        }

        return best ?? candidates[0];
    }

    // =========================================================================
    // Occupant generation
    // =========================================================================

    private void GenerateOccupantsForChunk(int startZ, int endZ)
    {
        var allOccupants = config.catalog.GetAllOccupants();
        if (allOccupants.Count == 0) return;

        int remainingBudget = config.densityBudget;

        for (int z = startZ; z < endZ; z++)
        {
            // Count walkable lanes for safety guarantee
            int walkableLanes = 0;
            for (int l = 1; l < TotalLaneCount - 1; l++)
                if (IsCellWalkable(z, l)) walkableLanes++;

            var lanes = Enumerable.Range(1, TotalLaneCount - 2)
                                  .OrderBy(_ => rng.Next())
                                  .ToList();

            foreach (int lane in lanes)
            {
                if (laneNextAllowedZ.TryGetValue(lane, out int nextAllowed) && z < nextAllowed) continue;

                CellState c = getCell(z, lane);
                if (c.surface == SurfaceType.Hole) continue;
                if (c.hasOccupant) continue;
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
                    // Safe path cells: only allow walkable-tagged occupants
                    if (goldenPathSet.Contains((z, lane)) && !def.HasTag("walkable")) continue;
                    // Don't block last walkable lane
                    if (!def.HasTag("walkable") && walkableLanes <= 1) continue;

                    // Per-def gap check
                    if (lastSpawnedZ.TryGetValue((lane, def.ID), out int lastZ))
                        if (z - lastZ < Mathf.Max(def.MinRowGap, def.SizeZ)) continue;

                    candidates.Add(def);
                }

                if (candidates.Count == 0) continue;

                PrefabDef selected = WeightedRandom(candidates);
                if (selected == null) continue;

                c.hasOccupant = true;
                c.occupantDef = selected;
                SetCell(z, lane, c);

                remainingBudget -= selected.Cost;
                lastSpawnedZ[(lane, selected.ID)] = z;
                if (!selected.HasTag("walkable")) walkableLanes--;
                laneNextAllowedZ[lane] = z + Mathf.Max(1, selected.SizeZ);

                if (remainingBudget <= 0) break;
            }

            if (remainingBudget <= 0) break;
        }
    }

    // =========================================================================
    // Edge walls  (L and R are now distinct)
    // =========================================================================

    private void GenerateEdgeWalls(int startZ, int endZ)
    {
        var leftPrefabs = config.catalog.leftWallPrefabs;
        var rightPrefabs = config.catalog.rightWallPrefabs;

        bool warnedL = false, warnedR = false;

        for (int z = startZ; z < endZ; z++)
        {
            // Left wall (lane 0)
            int leftLane = 0;
            if (!laneNextAllowedZ.TryGetValue(leftLane, out int nextL) || z >= nextL)
            {
                if (leftPrefabs != null && leftPrefabs.Count > 0)
                {
                    var cell = getCell(z, leftLane);
                    cell.hasOccupant = true;
                    // Store chosen prefab index in a wrapper def — spawning reads directly
                    cell.occupantDef = null; // raw prefab, spawned below via leftWallPrefabs
                    SetCell(z, leftLane, cell);
                    laneNextAllowedZ[leftLane] = z + 1;
                }
                else if (!warnedL)
                {
                    Debug.LogWarning("[EdgeWalls] leftWallPrefabs is empty — assign prefabs in the catalog.", this);
                    warnedL = true;
                }
            }

            // Right wall (last lane)
            int rightLane = TotalLaneCount - 1;
            if (!laneNextAllowedZ.TryGetValue(rightLane, out int nextR) || z >= nextR)
            {
                if (rightPrefabs != null && rightPrefabs.Count > 0)
                {
                    var cell = getCell(z, rightLane);
                    cell.hasOccupant = true;
                    cell.occupantDef = null;
                    SetCell(z, rightLane, cell);
                    laneNextAllowedZ[rightLane] = z + 1;
                }
                else if (!warnedR)
                {
                    Debug.LogWarning("[EdgeWalls] rightWallPrefabs is empty — assign prefabs in the catalog.", this);
                    warnedR = true;
                }
            }
        }
    }

    // =========================================================================
    // Chunk generation
    // =========================================================================

    private void GenerateChunk(int startZ, int chunkSize)
    {
        MaybeSwitchBiome();
        chunksSinceLastSwitch++;

        int endZ = startZ + chunkSize;

        // Pass 1: noise-driven surface placement
        for (int z = startZ; z < endZ; z++)
            PlaceSurfaceFromNoise(z);

        // Pass 2: WFC blend around safe path
        BlendSafePathWithWFC(startZ, endZ);

        // Pass 3: occupants
        GenerateOccupantsForChunk(startZ, endZ);

        // Pass 4: edge walls
        GenerateEdgeWalls(startZ, endZ);

        // Pass 5: spawn visuals
        for (int z = startZ; z < endZ; z++)
            SpawnRowVisuals(z);
    }

    // =========================================================================
    // Visual spawning
    // =========================================================================

    private void SpawnRowVisuals(int z)
    {
        for (int lane = 0; lane < TotalLaneCount; lane++)
        {
            var cell = getCell(z, lane);
            var worldPos = new Vector3(LaneToWorldX(lane), 0, z * config.cellLength);

            // ── Edge lanes ────────────────────────────────────────────────────
            if (IsEdgeLane(lane))
            {
                // Surface tile for edge lane (EdgeL or EdgeR surface type)
                var edgeSurfaces = config.catalog.GetSurfaceCandidates(cell.surface);
                if (edgeSurfaces.Count > 0)
                {
                    var def = edgeSurfaces[rng.Next(edgeSurfaces.Count)];
                    SpawnSurface(z, lane, def, worldPos);
                }

                // Edge wall prefab (L or R)
                var wallList = IsLeftEdge(lane)
                    ? config.catalog.leftWallPrefabs
                    : config.catalog.rightWallPrefabs;

                if (wallList != null && wallList.Count > 0)
                {
                    var wall = wallList[rng.Next(wallList.Count)];
                    if (wall != null)
                    {
                        var obj = Instantiate(wall, worldPos, Quaternion.identity, transform);
                        spawnedOccupant[(z, lane)] = obj;
                    }
                }
                continue;
            }

            // ── Interior surface ──────────────────────────────────────────────
            if (cell.surfaceDef != null && cell.surfaceDef.Prefabs.Count > 0)
            {
                SpawnSurface(z, lane, cell.surfaceDef, worldPos);
            }
            else if (cell.surface == SurfaceType.SafePath)
            {
                // Dedicated safe-path fallback from catalog
                if (config.catalog.debugSafePath != null)
                {
                    var obj = Instantiate(config.catalog.debugSafePath, worldPos, Quaternion.identity, transform);
                    spawnedSurface[(z, lane)] = obj;
                }
            }
            else if (config.catalog.debugSurfaceDef != null && config.catalog.debugSurfaceDef.Prefabs.Count > 0)
            {
                SpawnSurface(z, lane, config.catalog.debugSurfaceDef, worldPos);
            }

            // ── Occupant ──────────────────────────────────────────────────────
            if (!cell.hasOccupant || cell.occupantDef == null) continue;

            // Multi-row occupants: only spawn from the origin cell
            if (cell.occupantDef.SizeZ > 1)
            {
                var prev = getCell(z - 1, lane);
                if (prev.hasOccupant && prev.occupantDef?.ID == cell.occupantDef.ID)
                    continue; // continuation cell, not origin
            }

            if (cell.occupantDef.Prefabs.Count > 0)
            {
                var obj = Instantiate(
                    cell.occupantDef.Prefabs[rng.Next(cell.occupantDef.Prefabs.Count)],
                    worldPos, Quaternion.identity, transform);
                spawnedOccupant[(z, lane)] = obj;
            }
            else if (config.catalog.debugOccupant != null)
            {
                var obj = Instantiate(config.catalog.debugOccupant, worldPos, Quaternion.identity, transform);
                spawnedOccupant[(z, lane)] = obj;
            }
        }
    }

    private void SpawnSurface(int z, int lane, PrefabDef def, Vector3 worldPos)
    {
        if (def == null || def.Prefabs == null || def.Prefabs.Count == 0) return;
        var obj = Instantiate(def.Prefabs[rng.Next(def.Prefabs.Count)], worldPos, Quaternion.identity, transform);
        spawnedSurface[(z, lane)] = obj;
    }

    // =========================================================================
    // Streaming / cleanup
    // =========================================================================

    public void UpdateGeneration(float playerZWorld)
    {
        int playerZIndex = Mathf.FloorToInt(playerZWorld / config.cellLength);
        int targetZ = playerZIndex + config.bufferRows;

        UpdatePathBuffer(targetZ + config.chunkSize + 20);

        while (generatorZ < targetZ)
        {
            GenerateChunk(generatorZ, config.chunkSize);
            generatorZ += config.chunkSize;
        }

        int minKeepZ = playerZIndex - config.keepRowsBehind;
        var toRemove = grid.Keys.Where(k => k.z < minKeepZ).ToList();

        foreach (var key in toRemove)
        {
            if (spawnedSurface.TryGetValue(key, out var s) && s) { Destroy(s); spawnedSurface.Remove(key); }
            if (spawnedOccupant.TryGetValue(key, out var o) && o) { Destroy(o); spawnedOccupant.Remove(key); }
            grid.Remove(key);
        }

        foreach (var k in lastSpawnedZ.Where(kv => kv.Value < minKeepZ).Select(kv => kv.Key).ToList())
            lastSpawnedZ.Remove(k);
        foreach (var k in laneNextAllowedZ.Where(kv => kv.Value < minKeepZ).Select(kv => kv.Key).ToList())
            laneNextAllowedZ.Remove(k);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    float Rand() => (float)rng.NextDouble();

    private CellState getCell(int z, int lane)
    {
        grid.TryGetValue((z, lane), out var s);
        return s ?? new CellState();
    }

    private void SetCell(int z, int lane, CellState state) => grid[(z, lane)] = state;

    private float LaneToWorldX(int lane)
    {
        float center = (TotalLaneCount - 1) * 0.5f;
        return (lane - center) * config.laneWidth;
    }

    private bool IsCellWalkable(int z, int lane)
    {
        var c = getCell(z, lane);
        if (c.isEdgeLane || c.surface == SurfaceType.Hole) return false;
        // A cell is walkable if it has no occupant, or only a walkable-tagged occupant
        if (!c.hasOccupant) return true;
        return c.occupantDef != null && c.occupantDef.HasTag("walkable");
    }

    private bool RowHasAnyWalkable(int z)
    {
        for (int lane = 1; lane < TotalLaneCount - 1; lane++)
            if (IsCellWalkable(z, lane)) return true;
        return false;
    }

    private PrefabDef WeightedRandom(List<PrefabDef> candidates)
    {
        if (candidates == null || candidates.Count == 0) return null;
        float total = candidates.Sum(d => d.OccupantWeight);
        if (total <= 0f) return candidates[rng.Next(candidates.Count)];
        float roll = (float)(rng.NextDouble() * total);
        float acc = 0f;
        foreach (var c in candidates)
        {
            acc += c.OccupantWeight;
            if (roll <= acc) return c;
        }
        return candidates[candidates.Count - 1];
    }

    private (int z, int lane) NeighborCoord(int z, int lane, Direction dir)
    {
        return dir switch
        {
            Direction.Forward => (z + 1, lane),
            Direction.Backward => (z - 1, lane),
            Direction.Left => (z, lane - 1),
            Direction.Right => (z, lane + 1),
            Direction.ForwardLeft => (z + 1, lane - 1),
            Direction.ForwardRight => (z + 1, lane + 1),
            Direction.BackwardLeft => (z - 1, lane - 1),
            Direction.BackwardRight => (z - 1, lane + 1),
            _ => (z, lane)
        };
    }

    private Direction OppositeDir(Direction dir)
    {
        return dir switch
        {
            Direction.Forward => Direction.Backward,
            Direction.Backward => Direction.Forward,
            Direction.Left => Direction.Right,
            Direction.Right => Direction.Left,
            Direction.ForwardLeft => Direction.BackwardRight,
            Direction.ForwardRight => Direction.BackwardLeft,
            Direction.BackwardLeft => Direction.ForwardRight,
            Direction.BackwardRight => Direction.ForwardLeft,
            _ => dir
        };
    }

    // =========================================================================
    // Validation
    // =========================================================================

    private bool ValidateConfiguration()
    {
        if (config == null || config.catalog == null || config.weightRules == null)
        {
            Debug.LogError("[RunnerLevelGenerator] Config, catalog, or weight rules missing.", this);
            return false;
        }
        if (playerTransform == null)
            Debug.LogWarning("[RunnerLevelGenerator] playerTransform not assigned.", this);
        if (config.catalog.noiseChannel == null)
            Debug.LogWarning("[RunnerLevelGenerator] Catalog has no noiseChannel assigned — surfaces will use fallback only.", this);
        return true;
    }
}