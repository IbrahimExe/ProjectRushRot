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
using static UnityEngine.Rendering.STP;

//main class
public class RunnerLevelGenerator : MonoBehaviour
{
    [Header("Biome Configs")]
    [Tooltip("List of biome generator configs. Each can point to a different PrefabCatalog + NeighborRulesConfig.")]
    [SerializeField] private List<RunnerGenConfig> biomeConfigs = new List<RunnerGenConfig>();

    [Tooltip("Starting biome config index in the list above.")]
    [SerializeField] private int startBiomeIndex = 0;

    [Tooltip("Probability (0..1) to switch biome config on the NEXT generated chunk.")]
    [Range(0f, 1f)] public float biomeBias = 0.25f;

    // Active config (selected from biomeConfigs). All generation reads from this.
    private RunnerGenConfig config;
    private int currentBiomeIndex = -1;

    // CONSTANTS
    private const int PATH_BUFFER_PADDING = 10;
    private const int WFC_MAX_ITERATIONS_PER_CELL = 10;
    private const float ENTROPY_EPSILON = 1e-6f;
    private const float ENTROPY_TIEBREAKER = 0.001f;
    private const float WEIGHT_EPSILON = 0.0001f;
    private const float PERLIN_SEED_SCALE = 0.001f;
    private const float PERLIN_WAVE_OFFSET = 10.0f;

    [Header("Seeding")]
    [Tooltip("Seed for deterministic generation")]
    public string seed = "hjgujklfu";

    private Dictionary<(int z, int lane), CellState> grid;
    private int generatorZ = 0;
    private System.Random rng;

    private Queue<(int z, int lane)> propagationQueue = new Queue<(int z, int lane)>();
    private HashSet<(int z, int lane)> queuedCells = new HashSet<(int, int)>();

    private int restRowsRemaining = 0;
    private int wavePhaseZ = 0;
    private int restCooldown = 0;

    // Golden Path State
    private HashSet<(int z, int lane)> goldenPathSet = new HashSet<(int, int)>();
    private int pathTipZ = -1;
    private int pathTipLane = 1;
    private float pathLaneF;
    private float waveSeedOffset;

    [Header("References")]
    [SerializeField] private Transform playerTransform;

    private Dictionary<(int z, int lane), GameObject> spawnedObjects = new Dictionary<(int, int), GameObject>();
    private Dictionary<(int z, int lane), GameObject> spawnedOccupant = new();
    private Dictionary<(int lane, OccupantType type), int> lastSpawnedZ = new Dictionary<(int, OccupantType), int>();

    private int TotalLaneCount => config.laneCount + 2;
    private bool IsEdgeLaneIndex(int lane) => lane == 0 || lane == TotalLaneCount - 1;

    private Dictionary<int, int> laneNextAllowedZ = new Dictionary<int, int>();

    // -------------------------------------------------------
    // TERRAIN ZONE SYSTEM
    // Noise > hard zones > pre-collapse deep cells > WFC handles transitions only.
    // This gives large smooth clumps without fighting WFC's constraint propagation.
    // -------------------------------------------------------
    private enum TerrainZone { Forest, Grass, Sandy, Water }

    // Noise thresholds that map the 0..1 FBM value to a zone.
    // Adjust these to change how much of the world each zone occupies.
    private static readonly float[] ZoneThresholds = { 0.25f, 0.50f, 0.75f, 1.0f };
    private static readonly TerrainZone[] ZoneOrder = { TerrainZone.Forest, TerrainZone.Grass, TerrainZone.Sandy, TerrainZone.Water };

    // IDs of the dominant tile for each zone  must match your catalog exactly.
    // These are the tiles that get stamped into deep-zone cells before WFC runs.
    private static readonly Dictionary<TerrainZone, string> ZoneDominantID = new()
    {
        { TerrainZone.Forest, "Forest_SUR"  },
        { TerrainZone.Grass,  "GRASS_SUR"   },
        { TerrainZone.Sandy,  "Sand_SUR"    },
        { TerrainZone.Water,  "Water_SUR"   },
    };

    // Biome affinity mapping used for boundary cell weight bias.
    private static readonly BiomeType[] ZoneBiomes =
        { BiomeType.Grassy, BiomeType.Rocky, BiomeType.Sandy, BiomeType.Crystalline };

    // How many cells in every direction must share the same zone before a cell
    // is considered "deep" and pre-collapsed. Raise for bigger solid clumps.
    // ZONE_DEPTH_CHECK = 1 > only immediate cardinal neighbors must match.
    // ZONE_DEPTH_CHECK = 2 > a 5x5 neighbourhood must all match (larger clumps).
    private const int ZONE_DEPTH_CHECK = 2;

    // Noise offsets (seeded from string seed)
    private float biomeNoiseOffsetX;
    private float biomeNoiseOffsetZ;

    private TerrainZone GetZone(float noise)
    {
        for (int i = 0; i < ZoneThresholds.Length; i++)
            if (noise < ZoneThresholds[i]) return ZoneOrder[i];
        return ZoneOrder[ZoneOrder.Length - 1];
    }

    private float GetZoneCenter(TerrainZone zone)
    {
        // Midpoint of each zone's threshold band
        float lo = zone == ZoneOrder[0] ? 0f : ZoneThresholds[(int)zone - 1];
        float hi = ZoneThresholds[(int)zone];
        return (lo + hi) * 0.5f;
    }

    // Returns true when every cell in the neighbourhood shares the same zone.
    // Pure noise math - no grid reads, so safe to call before any cell is written.
    private bool IsDeeplyInsideZone(int z, int lane, TerrainZone zone)
    {
        for (int dz = -ZONE_DEPTH_CHECK; dz <= ZONE_DEPTH_CHECK; dz++)
        {
            for (int dl = -ZONE_DEPTH_CHECK; dl <= ZONE_DEPTH_CHECK; dl++)
            {
                if (dz == 0 && dl == 0) continue;
                int nl = lane + dl;
                if (nl <= 0 || nl >= TotalLaneCount - 1) continue; // ignore edge lanes
                float n = SampleBiomeNoise(z + dz, nl);
                if (GetZone(n) != zone) return false;
            }
        }
        return true;
    }

    // Raw smooth noise value for a cell - deterministic, no rng consumption.
    private float SampleBiomeNoise(int z, int lane)
    {
        if (!config.useBiomeSystem) return 0.5f;
        float x = LaneToWorldX(lane) / (config.biomeNoiseScale * config.laneWidth);
        float y = (z * config.cellLength) / (config.biomeNoiseScale * config.cellLength);
        return DomainWarpedFbm01(x, y);
    }

    // Weight for a tile at a boundary cell: highest for tiles near their preferred zone.
    private float GetBoundaryWeight(PrefabDef def, float noise)
    {
        float best = 0.001f;
        for (int i = 0; i < ZoneOrder.Length; i++)
        {
            float raw = def.GetBiomeAffinity(ZoneBiomes[i]);
            if (raw <= 0f) continue;
            float center = GetZoneCenter(ZoneOrder[i]);
            float dist = Mathf.Abs(noise - center);
            // Linear falloff, squared to sharpen the preference
            float w = Mathf.Max(0f, 1f - dist / 0.5f);
            best = Mathf.Max(best, raw * w * w);
        }
        return best;
    }

    // -------------------------------------------------------

    void Start()
    {
        if (!ValidateConfiguration())
        {
            Debug.LogError("[RunnerLevelGenerator] Configuration validation failed. Generator not enabled.");
            enabled = false;
            return;
        }

        if (!TryApplyBiomeConfig(Mathf.Clamp(startBiomeIndex, 0, Mathf.Max(0, biomeConfigs.Count - 1))))
        {
            Debug.LogError("[RunnerLevelGenerator] No valid biome config selected. Generator not enabled.");
            enabled = false;
            return;
        }

        grid = new Dictionary<(int, int), CellState>();
        rng = !string.IsNullOrEmpty(seed)
            ? new System.Random(seed.GetHashCode())
            : new System.Random();

        // Seed noise offsets
        biomeNoiseOffsetX = (seed != null ? seed.GetHashCode() : 0) * 0.01f;
        biomeNoiseOffsetZ = (seed != null ? (seed.GetHashCode() * 7919) : 0) * 0.01f;

        pathTipLane = config.laneCount / 2;
        pathLaneF = pathTipLane;
        restRowsRemaining = 0;
        restCooldown = 0;
        wavePhaseZ = 0;
        waveSeedOffset = (seed != null ? seed.GetHashCode() : 0) * PERLIN_SEED_SCALE;
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
            MaybeSwitchBiomeForNextChunk();
            GenerateChunk(chunkStart, actualChunkSize);
            generatorZ += actualChunkSize;
        }
    }

    void Update()
    {
        if (playerTransform != null)
            UpdateGeneration(playerTransform.position.z);
    }

    private PlacementContext CreateContext(int z, int lane)
    {
        CellState cell = getCell(z, lane);
        return new PlacementContext
        {
            position = (z, lane),
            currentSurface = cell.surface,
            currentOccupant = cell.occupant,
            GetCell = (checkZ, checkLane) => getCell(checkZ, checkLane),
            IsOnGoldenPath = (pos) => goldenPathSet.Contains(pos),
            laneCount = config.laneCount,
            playerZIndex = playerTransform != null
                ? Mathf.FloorToInt(playerTransform.position.z / config.cellLength) : 0
        };
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

            float n1 = Mathf.PerlinNoise(z * config.waveFrequency, waveSeedOffset + PERLIN_WAVE_OFFSET);
            float n2 = Mathf.PerlinNoise(z * (config.waveFrequency * 2.2f), waveSeedOffset + 77.7f);
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

    private void GenerateChunk(int startZ, int chunkSize)
    {
        int endZ = startZ + chunkSize;

        for (int z = startZ; z < endZ; z++) InitializeRowForWFC(z);
        for (int z = startZ; z < endZ; z++) PreCollapseSafePath(z);

        SolveChunkSurfaces(startZ, endZ);
        GenerateOccupantsForChunk(startZ, endZ);
        GenerateEdgeWalls(startZ, endZ);

        for (int z = startZ; z < endZ; z++) SpawnRowVisuals(z);
    }

    // -------------------------------------------------------
    // PASS 1: Initialize WFC State
    // Zone-first: deep cells get a single candidate and are
    // collapsed immediately so they seed propagation before
    // WFC makes any entropy-based decision. Boundary cells
    // get all candidates with noise-biased weights so WFC
    // finds legal transitions between zones naturally.
    // -------------------------------------------------------
    private void InitializeRowForWFC(int z)
    {
        // allSurfaces: every non-edge surface.
        // Used by neighbor rules (CheckNeighbor, seam pre-filter) so that tiles with
        // AllowWFC=false (SafePath, EdgeWall, etc.) still participate in adjacency legality.
        var allSurfaces = config.catalog.Definitions
            .Where(d => d.Layer == ObjectLayer.Surface && d.SurfaceType != SurfaceType.Edge)
            .ToList();

        // wfcSurfaces: tiles WFC is allowed to randomly collapse to.
        // AllowWFC=false tiles are excluded — they can only be placed by explicit systems
        // (PreCollapseSafePath, GenerateEdgeWalls, etc.).
        var wfcSurfaces = allSurfaces
            .Where(d => d.AllowWFC)
            .ToList();

        // Build zone->dominant tile lookup once per row (uses allSurfaces so zone dominants
        // can themselves be AllowWFC=false if you want them placed only by the zone system).
        var zoneTiles = new Dictionary<TerrainZone, PrefabDef>();
        foreach (var kv in ZoneDominantID)
        {
            var tile = allSurfaces.FirstOrDefault(d => d.ID == kv.Value);
            if (tile != null) zoneTiles[kv.Key] = tile;
            else Debug.LogWarning($"[Biome] Zone tile '{kv.Value}' not found in catalog. " +
                "Update ZoneDominantID to match your catalog IDs.", this);
        }

        for (int lane = 0; lane < TotalLaneCount; lane++)
        {
            // Edge lanes — structural boundary, WFC never touches them
            if (IsEdgeLaneIndex(lane))
            {
                CellState edgeCell = new CellState(SurfaceType.Edge, OccupantType.None, edgeLane: true);
                edgeCell.isCollapsed = true;
                edgeCell.entropy = 0f;
                SetCell(z, lane, edgeCell);
                continue;
            }

            // Golden path cells — PreCollapseSafePath (Pass 2) forces SafePath here.
            // Candidates must include ALL surfaces (including AllowWFC=false) because
            // PreCollapseSafePath searches surfaceCandidates for the SafePath def.
            // Weights heavily favour SafePath; everything else is near-zero so WFC
            // would never pick them, but the forced collapse in Pass 2 runs first anyway.
            if (goldenPathSet.Contains((z, lane)))
            {
                CellState cell = new CellState(SurfaceType.Solid, OccupantType.None);
                cell.surfaceCandidates = new List<PrefabDef>(allSurfaces);
                cell.candidateWeights = new Dictionary<PrefabDef, float>();
                foreach (var def in allSurfaces)
                    cell.candidateWeights[def] = def.SurfaceType == SurfaceType.SafePath ? 1f : 0.001f;
                cell.isCollapsed = false;
                cell.entropy = CalculateEntropy(cell);
                SetCell(z, lane, cell);
                continue;
            }

            float noise = SampleBiomeNoise(z, lane);
            TerrainZone zone = GetZone(noise);

            // Deep inside a zone — pre-collapse to the dominant tile immediately.
            // CollapseCell marks it collapsed and enqueues it for propagation so the
            // entire zone radiates constraints before WFC's main loop starts.
            if (config.useBiomeSystem && IsDeeplyInsideZone(z, lane, zone) && zoneTiles.TryGetValue(zone, out PrefabDef dominant))
            {
                CellState cell = new CellState(SurfaceType.Solid, OccupantType.None);
                cell.surfaceCandidates = new List<PrefabDef> { dominant };
                cell.candidateWeights = new Dictionary<PrefabDef, float> { { dominant, 1f } };
                cell.isCollapsed = false; // CollapseCell will set this
                cell.entropy = 0f;
                SetCell(z, lane, cell);
                CollapseCell(z, lane, dominant); // immediately collapsed + enqueued
                continue;
            }

            // Boundary / open cell — WFC resolves this using wfcSurfaces only.
            // AllowWFC=false tiles are excluded so they cannot be randomly chosen,
            // but allSurfaces (used in CheckNeighbor) still sees them for rule checks.
            {
                CellState cell = new CellState(SurfaceType.Solid, OccupantType.None);
                cell.surfaceCandidates = new List<PrefabDef>();
                cell.candidateWeights = new Dictionary<PrefabDef, float>();

                foreach (var def in wfcSurfaces)
                {
                    float weight = def.SurfaceType == SurfaceType.SafePath
                        ? 0.001f
                        : Mathf.Max(0.001f, GetBoundaryWeight(def, noise));
                    cell.surfaceCandidates.Add(def);
                    cell.candidateWeights[def] = weight;
                }

                cell.isCollapsed = false;
                cell.entropy = CalculateEntropy(cell);
                SetCell(z, lane, cell);
            }
        }
    }

    // -------------------------------------------------------
    // PASS 2: Force SafePath on golden path cells
    // -------------------------------------------------------
    private void PreCollapseSafePath(int z)
    {
        for (int lane = 0; lane < TotalLaneCount; lane++)
        {
            if (IsEdgeLaneIndex(lane)) continue;
            if (!goldenPathSet.Contains((z, lane))) continue;

            CellState cell = getCell(z, lane);
            PrefabDef safePathDef = cell.surfaceCandidates?.FirstOrDefault(d => d.SurfaceType == SurfaceType.SafePath);
            if (safePathDef == null)
                safePathDef = config.catalog.Definitions.FirstOrDefault(
                    d => d != null && d.Layer == ObjectLayer.Surface && d.SurfaceType == SurfaceType.SafePath);

            if (safePathDef == null)
                Debug.LogWarning($"[GoldenPath] No SafePath surface def found in catalog for ({z},{lane}).", this);
            else
                CollapseCell(z, lane, safePathDef);
        }
    }

    private void CollapseCell(int z, int lane, PrefabDef forcedDef)
    {
        if (forcedDef == null)
        {
            Debug.LogError($"[CollapseCell] Attempted to collapse ({z},{lane}) with a null PrefabDef.", this);
            return;
        }
        CellState cell = getCell(z, lane);
        if (cell.isCollapsed) return;

        cell.surfaceDef = forcedDef;
        cell.surface = forcedDef.SurfaceType;
        cell.isCollapsed = true;
        cell.surfaceCandidates?.Clear();
        if (cell.surfaceCandidates == null) cell.surfaceCandidates = new List<PrefabDef>();
        cell.surfaceCandidates.Add(forcedDef);
        if (cell.candidateWeights == null) cell.candidateWeights = new Dictionary<PrefabDef, float>();
        cell.candidateWeights.Clear();
        cell.candidateWeights[forcedDef] = 1.0f;
        cell.entropy = 0f;

        SetCell(z, lane, cell);
        if (queuedCells.Add((z, lane)))
            propagationQueue.Enqueue((z, lane));
    }

    // -------------------------------------------------------
    // PASS 3: WFC Solver
    // -------------------------------------------------------
    private void SolveChunkSurfaces(int startZ, int endZ)
    {
        propagationQueue.Clear();
        queuedCells.Clear();

        int seedStartZ = Mathf.Max(0, startZ - config.contextRows);

        if (startZ > 0 && config.contextRows > 0)
        {
            int contextBoundary = startZ - config.contextRows;
            bool contextMissing = false;
            for (int cl = 1; cl < TotalLaneCount - 1 && !contextMissing; cl++)
                if (!grid.ContainsKey((contextBoundary, cl))) contextMissing = true;
            if (contextMissing)
                Debug.LogWarning($"[WFC] Context row z={contextBoundary} cleaned up before chunk z={startZ}. " +
                    $"Increase keepRowsBehind to at least {config.chunkSize + config.contextRows}.", this);
        }

        // Seed propagation from all already-collapsed cells (zone tiles + SafePath + prior chunk context)
        for (int z = seedStartZ; z < endZ; z++)
        {
            for (int l = 0; l < TotalLaneCount; l++)
            {
                if (IsEdgeLaneIndex(l)) continue;
                if (!grid.ContainsKey((z, l))) continue;
                var c = getCell(z, l);
                if (c.isCollapsed && c.surfaceDef != null)
                    if (queuedCells.Add((z, l)))
                        propagationQueue.Enqueue((z, l));
            }
        }

        // Chunk seam pre-filter: prune boundary cells based on previous chunk's last row
        if (startZ > 0)
        {
            var allSurfaces = config.catalog.Definitions
                .Where(d => d.Layer == ObjectLayer.Surface)
                .ToList();

            for (int l = 1; l < TotalLaneCount - 1; l++)
            {
                int contextZ = startZ - 1;
                if (!grid.ContainsKey((contextZ, l))) continue;
                CellState contextCell = getCell(contextZ, l);
                if (!contextCell.isCollapsed || contextCell.surfaceDef == null) continue;
                if (!grid.ContainsKey((startZ, l))) continue;
                CellState boundaryCell = getCell(startZ, l);
                if (boundaryCell.isCollapsed) continue;
                if (boundaryCell.surfaceCandidates == null || boundaryCell.surfaceCandidates.Count == 0) continue;

                List<float> fwdWeights;
                List<PrefabDef> allowedFwd = config.weightRules.GetAllowedNeighbors(
                    contextCell.surfaceDef, Direction.Forward, allSurfaces, out fwdWeights);
                var fwdSet = new HashSet<PrefabDef>(allowedFwd);

                bool anyPruned = false;
                for (int i = boundaryCell.surfaceCandidates.Count - 1; i >= 0; i--)
                {
                    PrefabDef cand = boundaryCell.surfaceCandidates[i];
                    bool passedFwd = fwdSet.Count == 0 || fwdSet.Contains(cand);
                    bool passedBwd = config.weightRules.IsNeighborAllowed(cand, contextCell.surfaceDef, Direction.Backward);
                    if (!passedFwd || !passedBwd)
                    {
                        boundaryCell.surfaceCandidates.RemoveAt(i);
                        boundaryCell.candidateWeights?.Remove(cand);
                        anyPruned = true;
                    }
                }

                if (anyPruned)
                {
                    if (boundaryCell.surfaceCandidates.Count == 0)
                    {
                        Debug.LogError($"[WFC Seam] Pre-filter wiped all candidates at ({startZ},{l}). " +
                            $"Context tile: {contextCell.surfaceDef.ID}. Check Forward/Backward rule symmetry.", this);
                        HandleSurfaceContradiction(startZ, l, "Seam pre-filter contradiction",
                            $"Context tile: {contextCell.surfaceDef.ID}");
                    }
                    else
                    {
                        boundaryCell.entropy = CalculateEntropy(boundaryCell);
                        SetCell(startZ, l, boundaryCell);
                        if (boundaryCell.surfaceCandidates.Count == 1)
                            CollapseCell(startZ, l, boundaryCell.surfaceCandidates[0]);
                    }
                }
            }
        }

        // Initial full drain - propagate all pre-collapsed cells (zone tiles + SafePath + seam)
        // before any entropy-based collapse decision is made.
        while (propagationQueue.Count > 0)
        {
            (int pz, int pl) = propagationQueue.Dequeue();
            queuedCells.Remove((pz, pl));
            Propagate(pz, pl, startZ, endZ);
        }

        // Main WFC loop
        int maxIterations = (endZ - startZ) * config.laneCount * WFC_MAX_ITERATIONS_PER_CELL;
        int iter = 0;

        while (UncollapsedCellsExist(startZ, endZ) && iter < maxIterations)
        {
            iter++;

            while (propagationQueue.Count > 0)
            {
                (int pz, int pl) = propagationQueue.Dequeue();
                queuedCells.Remove((pz, pl));
                Propagate(pz, pl, startZ, endZ);
            }

            var target = GetLowestEntropyCell(startZ, endZ);
            if (target.z != -1)
                PerformWeightedCollapse(target.z, target.lane);
        }

        ForceCollapseRemaining(startZ, endZ);
    }

    private bool ValidateConfiguration()
    {
        if (biomeConfigs == null || biomeConfigs.Count == 0)
        {
            Debug.LogError("[RunnerLevelGenerator] No biome configs assigned.", this);
            return false;
        }

        if (config == null)
        {
            int idx = Mathf.Clamp(startBiomeIndex, 0, biomeConfigs.Count - 1);
            if (!TryApplyBiomeConfig(idx))
            {
                Debug.LogError("[RunnerLevelGenerator] Could not apply starting biome config.", this);
                return false;
            }
        }

        bool valid = true;
        var baseCfg = biomeConfigs.FirstOrDefault(c => c != null);
        if (baseCfg == null) { Debug.LogError("[RunnerLevelGenerator] All biome configs are null.", this); return false; }

        for (int i = 0; i < biomeConfigs.Count; i++)
        {
            var cfg = biomeConfigs[i];
            if (cfg == null) { Debug.LogError($"[RunnerLevelGenerator] Biome config at index {i} is null.", this); valid = false; continue; }
            if (cfg.catalog == null) { Debug.LogError($"[RunnerLevelGenerator] Config '{cfg.name}' missing catalog.", this); valid = false; }
            if (cfg.weightRules == null) { Debug.LogError($"[RunnerLevelGenerator] Config '{cfg.name}' missing weightRules.", this); valid = false; }
            if (cfg.laneCount != baseCfg.laneCount ||
                !Mathf.Approximately(cfg.laneWidth, baseCfg.laneWidth) ||
                !Mathf.Approximately(cfg.cellLength, baseCfg.cellLength))
            {
                Debug.LogError($"[RunnerLevelGenerator] Lane geometry mismatch: '{baseCfg.name}' vs '{cfg.name}'.", this);
                valid = false;
            }
        }

        if (playerTransform == null)
            Debug.LogWarning("[RunnerLevelGenerator] playerTransform not assigned.", this);

        return valid;
    }

    private void ForceCollapseRemaining(int startZ, int endZ)
    {
        int forcedCount = 0;
        for (int z = startZ; z < endZ; z++)
            for (int l = 0; l < TotalLaneCount; l++)
            {
                if (IsEdgeLaneIndex(l)) continue;
                if (!getCell(z, l).isCollapsed) { PerformWeightedCollapse(z, l); forcedCount++; }
            }
        if (forcedCount > 0)
            Debug.LogWarning($"[WFC] Force-collapsed {forcedCount} cells in chunk {startZ}-{endZ}.", this);
    }

    private bool UncollapsedCellsExist(int startZ, int endZ)
    {
        for (int z = startZ; z < endZ; z++)
            for (int l = 0; l < TotalLaneCount; l++)
            {
                if (IsEdgeLaneIndex(l)) continue;
                if (!getCell(z, l).isCollapsed) return true;
            }
        return false;
    }

    private (int z, int lane) GetLowestEntropyCell(int startZ, int endZ)
    {
        float minEntropy = float.MaxValue;
        (int z, int lane) best = (-1, -1);
        for (int z = startZ; z < endZ; z++)
            for (int l = 0; l < TotalLaneCount; l++)
            {
                if (IsEdgeLaneIndex(l)) continue;
                CellState c = getCell(z, l);
                if (!c.isCollapsed && c.entropy < minEntropy) { minEntropy = c.entropy; best = (z, l); }
            }
        return best;
    }

    // Weights already baked at init time - just roll against them.
    private void PerformWeightedCollapse(int z, int lane)
    {
        CellState cell = getCell(z, lane);
        if (cell.isCollapsed) return;

        if (cell.surfaceCandidates == null || cell.surfaceCandidates.Count == 0)
        {
            HandleSurfaceContradiction(z, lane, cell.surfaceCandidates == null
                ? "surfaceCandidates was NULL"
                : "No candidates before weighted collapse");
            return;
        }

        if (CollapseIfSingleCandidate(z, lane, ref cell, "PerformWeightedCollapse-entry"))
            return;

        float totalWeight = 0f;
        var weights = new float[cell.surfaceCandidates.Count];

        for (int i = 0; i < cell.surfaceCandidates.Count; i++)
        {
            float w = cell.candidateWeights != null && cell.candidateWeights.TryGetValue(cell.surfaceCandidates[i], out var cw) ? cw : 1f;
            weights[i] = Mathf.Max(0.001f, w);
            totalWeight += weights[i];
        }

        float r = (float)rng.NextDouble() * totalWeight;
        float current = 0f;
        PrefabDef selected = cell.surfaceCandidates[cell.surfaceCandidates.Count - 1];

        for (int i = 0; i < cell.surfaceCandidates.Count; i++)
        {
            current += weights[i];
            if (r <= current) { selected = cell.surfaceCandidates[i]; break; }
        }

        CollapseCell(z, lane, selected);
    }

    private float CalculateEntropy(CellState cell)
    {
        var candidates = cell.surfaceCandidates;
        if (candidates == null || candidates.Count == 0) return 0f;

        float sumWeights = 0f;
        float sumWLogW = 0f;

        foreach (var cand in candidates)
        {
            float w = cell.candidateWeights != null && cell.candidateWeights.TryGetValue(cand, out float cw) ? cw : 1f;
            if (w <= ENTROPY_EPSILON) continue;
            sumWeights += w;
            sumWLogW += w * Mathf.Log(w);
        }

        if (sumWeights <= ENTROPY_EPSILON) return 0f;
        return Mathf.Log(sumWeights) - (sumWLogW / sumWeights) + (float)rng.NextDouble() * ENTROPY_TIEBREAKER;
    }

    private void Propagate(int z, int lane, int chunkStartZ, int chunkEndZ)
    {
        CellState collapsed = getCell(z, lane);
        PrefabDef sourceDef = collapsed.surfaceDef;
        if (sourceDef == null) return;

        CheckNeighbor(z, lane, z + 1, lane, Direction.Forward, sourceDef, chunkStartZ, chunkEndZ);
        CheckNeighbor(z, lane, z - 1, lane, Direction.Backward, sourceDef, chunkStartZ, chunkEndZ);
        CheckNeighbor(z, lane, z, lane - 1, Direction.Left, sourceDef, chunkStartZ, chunkEndZ);
        CheckNeighbor(z, lane, z, lane + 1, Direction.Right, sourceDef, chunkStartZ, chunkEndZ);
        CheckNeighbor(z, lane, z + 1, lane - 1, Direction.ForwardLeft, sourceDef, chunkStartZ, chunkEndZ);
        CheckNeighbor(z, lane, z + 1, lane + 1, Direction.ForwardRight, sourceDef, chunkStartZ, chunkEndZ);
        CheckNeighbor(z, lane, z - 1, lane - 1, Direction.BackwardLeft, sourceDef, chunkStartZ, chunkEndZ);
        CheckNeighbor(z, lane, z - 1, lane + 1, Direction.BackwardRight, sourceDef, chunkStartZ, chunkEndZ);
    }

    private void CheckNeighbor(int sz, int sl, int tz, int tl, Direction dir, PrefabDef sourceDef, int minZ, int maxZ)
    {
        if (tz >= maxZ || tz < 0 || tl < 0 || tl >= TotalLaneCount) return;
        if (tz < minZ && !grid.ContainsKey((tz, tl))) return;

        CellState target = getCell(tz, tl);
        if (target.isCollapsed) return;
        if (target.surfaceCandidates == null || target.surfaceCandidates.Count == 0) return;

        var beforeCandidates = new List<PrefabDef>(target.surfaceCandidates);

        var allSurfaces = config.catalog.Definitions
            .Where(d => d.Layer == ObjectLayer.Surface && d.SurfaceType != SurfaceType.Edge)
            .ToList();

        List<float> relativeWeights;
        List<PrefabDef> allowedBySource = config.weightRules.GetAllowedNeighbors(sourceDef, dir, allSurfaces, out relativeWeights);

        var incomingWeights = new Dictionary<PrefabDef, float>();
        for (int i = 0; i < allowedBySource.Count; i++)
            incomingWeights[allowedBySource[i]] = relativeWeights[i];

        Direction oppositeDir = GetOppositeDirection(dir);
        bool changed = false;

        for (int i = target.surfaceCandidates.Count - 1; i >= 0; i--)
        {
            PrefabDef cand = target.surfaceCandidates[i];

            // Fail 1: source doesn't allow this candidate in dir
            if (!incomingWeights.TryGetValue(cand, out float w))
            {
                target.surfaceCandidates.RemoveAt(i);
                target.candidateWeights?.Remove(cand);
                changed = true;
                continue;
            }

            // Fail 2: arc consistency - candidate must allow source back
            if (!config.weightRules.IsNeighborAllowed(cand, sourceDef, oppositeDir))
            {
                target.surfaceCandidates.RemoveAt(i);
                target.candidateWeights?.Remove(cand);
                changed = true;
                continue;
            }

            // Weight: take minimum (most restrictive constraint wins)
            if (target.candidateWeights != null)
            {
                float oldW = target.candidateWeights.TryGetValue(cand, out float existing) ? existing : 1f;
                float newW = Mathf.Min(oldW, w);
                target.candidateWeights[cand] = newW;
                if (Mathf.Abs(oldW - newW) > WEIGHT_EPSILON) changed = true;
            }
        }

        if (!changed) return;

        if (target.surfaceCandidates.Count == 0)
        {
            LogPropagationContradiction(sz, sl, tz, tl, dir, sourceDef, beforeCandidates, allowedBySource);
            HandleSurfaceContradiction(tz, tl, "Propagation eliminated all candidates",
                $"Triggered by source '{sourceDef?.ID}' dir={dir}");
            return;
        }

        target.entropy = CalculateEntropy(target);
        SetCell(tz, tl, target);

        if (target.surfaceCandidates.Count == 1)
        {
            var only = target.surfaceCandidates[0];
            Debug.Log($"[WFC] Forced by propagation at ({tz},{tl}) -> {only.ID} (from {sourceDef.ID} dir={dir})", this);
            CollapseCell(tz, tl, only);
            return;
        }

        if (queuedCells.Add((tz, tl)))
            propagationQueue.Enqueue((tz, tl));
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
            MaybeSwitchBiomeForNextChunk();
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

    // -------------------------------------------------------
    // Noise Helpers
    // -------------------------------------------------------
    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static float BandScore(float x01, float center, float halfWidth, float feather)
    {
        float d = Mathf.Abs(x01 - center);
        float t = Mathf.InverseLerp(halfWidth + feather, halfWidth, d);
        return Smooth01(t);
    }

    private float Perlin01(float x, float y) => Mathf.PerlinNoise(x, y);

    private float Fbm01(float x, float y, int octaves, float lacunarity, float gain, float seedX, float seedY)
    {
        float amp = 1f, freq = 1f, sum = 0f, norm = 0f;
        const float rot = 0.5f;
        float rx = Mathf.Cos(rot), ry = Mathf.Sin(rot);

        for (int i = 0; i < octaves; i++)
        {
            float nx = x * freq, ny = y * freq;
            float rnx = nx * rx - ny * ry, rny = nx * ry + ny * rx;
            float n = Perlin01(rnx + seedX + i * 17.13f, rny + seedY + i * 31.77f);
            sum += n * amp; norm += amp; amp *= gain; freq *= lacunarity;
        }
        return norm > 0f ? sum / norm : 0.5f;
    }

    private float DomainWarpedFbm01(float x, float y)
    {
        int oct = Mathf.Max(1, config.biomeOctaves);
        float lac = Mathf.Max(1.01f, config.biomeLacunarity);
        float gain = Mathf.Clamp(config.biomeGain, 0.05f, 0.95f);
        float warpStrength = Mathf.Max(0f, config.biomeWarpStrength);
        float warpScale = Mathf.Max(0.001f, config.biomeWarpScale);
        float sx = biomeNoiseOffsetX, sy = biomeNoiseOffsetZ;

        float wx = Perlin01(sx + x * warpScale, sy + y * warpScale) * 2f - 1f;
        float wy = Perlin01(sx + 100f + x * warpScale, sy + 100f + y * warpScale) * 2f - 1f;
        float warpedX = x + wx * warpStrength, warpedY = y + wy * warpStrength;

        float n = Fbm01(warpedX, warpedY, oct, lac, gain, sx, sy);

        float blur = Mathf.Clamp01(config.biomeBlur);
        if (blur > 0f)
        {
            const float br = 0.6f;
            float avg = (n * 4f
                + Fbm01(warpedX, warpedY + br, oct, lac, gain, sx, sy)
                + Fbm01(warpedX, warpedY - br, oct, lac, gain, sx, sy)
                + Fbm01(warpedX + br, warpedY, oct, lac, gain, sx, sy)
                + Fbm01(warpedX - br, warpedY, oct, lac, gain, sx, sy)) / 8f;
            n = Mathf.Lerp(n, avg, blur);
        }
        return n;
    }

    // -------------------------------------------------------
    // Misc Helpers
    // -------------------------------------------------------
    private int GetFootprintZ(PrefabDef def)
    {
        if (def == null) return 1;
        return Mathf.Max(1, def.SizeZ > 1 ? def.SizeZ : def.Size.z);
    }

    private bool TryApplyBiomeConfig(int index)
    {
        if (biomeConfigs == null || biomeConfigs.Count == 0) return false;
        index = Mathf.Clamp(index, 0, biomeConfigs.Count - 1);
        var next = biomeConfigs[index];
        if (next == null || next.catalog == null || next.weightRules == null) return false;
        config = next;
        currentBiomeIndex = index;
        config.catalog.RebuildCache();
        config.weightRules.catalog = config.catalog;
        config.weightRules.BuildCache();
        return true;
    }

    private Direction GetOppositeDirection(Direction dir) => dir switch
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

    private void MaybeSwitchBiomeForNextChunk()
    {
        if (rng == null || biomeConfigs == null || biomeConfigs.Count <= 1) return;
        if (rng.NextDouble() > biomeBias) return;
        int newIndex = rng.Next(biomeConfigs.Count - 1);
        if (newIndex >= currentBiomeIndex) newIndex++;
        TryApplyBiomeConfig(newIndex);
    }

    private bool CollapseIfSingleCandidate(int z, int lane, ref CellState cell, string reason)
    {
        if (cell.isCollapsed) return true;
        if (cell.surfaceCandidates == null || cell.surfaceCandidates.Count != 1) return false;
        var only = cell.surfaceCandidates[0];
        if (only == null) { Debug.LogError($"[WFC] Single candidate NULL at ({z},{lane}) during {reason}", this); return false; }
        Debug.Log($"[WFC] Forced collapse at ({z},{lane}) -> {only.ID} ({reason})", this);
        CollapseCell(z, lane, only);
        cell = getCell(z, lane);
        return true;
    }

    private void HandleSurfaceContradiction(int z, int lane, string reason, string extra = null)
    {
        Debug.LogError($"\n[WFC] CONTRADICTION at ({z},{lane}) {reason}\n{extra}", this);

        PrefabDef fallback = config.catalog.debugSurfaceDef;
        if (fallback == null)
        {
            Debug.LogError("[WFC] debugSurfaceDef is null on catalog - cannot place fallback.", this);
            return;
        }

        Debug.LogWarning($"[WFC] Falling back to '{fallback.ID}' at ({z},{lane})", this);

        // Write directly - do NOT call CollapseCell (debug tile has no rules, must not propagate)
        CellState cell = getCell(z, lane);
        cell.surfaceDef = fallback;
        cell.surface = fallback.SurfaceType;
        cell.isCollapsed = true;
        if (cell.surfaceCandidates == null) cell.surfaceCandidates = new List<PrefabDef>();
        cell.surfaceCandidates.Clear();
        cell.surfaceCandidates.Add(fallback);
        if (cell.candidateWeights == null) cell.candidateWeights = new Dictionary<PrefabDef, float>();
        cell.candidateWeights.Clear();
        cell.candidateWeights[fallback] = 1f;
        cell.entropy = 0f;
        SetCell(z, lane, cell);
        // Intentionally NOT enqueuing
    }

    private void LogPropagationContradiction(int sz, int sl, int tz, int tl, Direction dir,
        PrefabDef sourceDef, List<PrefabDef> beforeCandidates, List<PrefabDef> allowedBySource)
    {
        string before = beforeCandidates != null ? string.Join(", ", beforeCandidates.Select(c => c?.ID ?? "null")) : "null";
        string allowed = allowedBySource != null ? string.Join(", ", allowedBySource.Select(a => a?.ID ?? "null")) : "null";
        Debug.LogError(
            $"[WFC] Propagation wiped candidates!\n" +
            $"  Source ({sz},{sl}) = {sourceDef?.ID ?? "null"}\n" +
            $"  Target ({tz},{tl}) dir={dir}\n" +
            $"  Target BEFORE: [{before}]\n" +
            $"  AllowedBySource: [{allowed}]\n" +
            $"  Check: rules entry missing, direction mask mismatch, or cache stale.", this);
    }
}