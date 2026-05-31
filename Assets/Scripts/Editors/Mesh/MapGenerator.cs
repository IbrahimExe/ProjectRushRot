using System;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;

namespace LevelGenerator
{
    public struct MapData
    {
        public readonly float[,] heightMap;
        public readonly Color[] colorMap;

        public MapData(float[,] heightMap, Color[] colorMap)
        {
            this.heightMap = heightMap;
            this.colorMap = colorMap;
        }
    }

    public class MapGenerator : MonoBehaviour
    {
        public enum DrawMode { NoiseMap, ColourMap, Mesh }
        public DrawMode drawMode;

        // -- Single-biome path (unchanged) -------------------------------------
        public LevelGeneratorCommon Common;

        // -- Multi-biome path (optional — if null, falls back to Common) -------
        [Header("World Config (optional — overrides Common when assigned)")]
        public WorldConfig WorldConfig;

        // -- Shared settings ----------------------------------------------------
        public const int mapChunkSize = 239;

        [Range(0, 6)]
        public int levelOfDetail;

        public float meshHeightMultiplier;
        public AnimationCurve meshHeightCurve;

        [Header("Mesh Scale")]
        [Tooltip("World units per vertex.")]
        public float meshScale = 1f;

        [Tooltip("Scales noise sampling. Match to meshScale for consistent density.")]
        public float noiseWorldScale = 1f;

        public bool autoUpdate;

        // -- Threading ---------------------------------------------------------
        Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
        Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

        List<MapThreadInfo<MapData>> _mapDataBuffer = new List<MapThreadInfo<MapData>>();
        List<MapThreadInfo<MeshData>> _meshDataBuffer = new List<MapThreadInfo<MeshData>>();

        void Reset() => meshHeightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        // -- Static instance ---------------------------------------------------
        public static MapGenerator mapInstance;
        void Awake() => mapInstance = this;

        // -- Request API -------------------------------------------------------
        public void RequestMapData(Vector2 centre, Action<MapData> callback)
        {
            new Thread((ThreadStart)delegate { MapDataThread(centre, callback); }).Start();
        }

        void MapDataThread(Vector2 centre, Action<MapData> callback)
        {
            MapData mapData = GenerateMapData(centre);
            lock (mapDataThreadInfoQueue)
                mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
        }

        public void RequestMeshData(MapData mapData, int lod, Action<MeshData> callback)
        {
            new Thread((ThreadStart)delegate { MeshDataThread(mapData, lod, callback); }).Start();
        }

        void MeshDataThread(MapData mapData, int lod, Action<MeshData> callback)
        {
            MeshData meshData = MeshGenerator.GenerateTerrainMesh(
                mapData.heightMap, meshHeightMultiplier, meshHeightCurve, lod, meshScale);
            lock (meshDataThreadInfoQueue)
                meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }

        void Update()
        {
            _mapDataBuffer.Clear();
            lock (mapDataThreadInfoQueue)
                while (mapDataThreadInfoQueue.Count > 0)
                    _mapDataBuffer.Add(mapDataThreadInfoQueue.Dequeue());
            foreach (var info in _mapDataBuffer)
                info.callback(info.parameter);

            _meshDataBuffer.Clear();
            lock (meshDataThreadInfoQueue)
                while (meshDataThreadInfoQueue.Count > 0)
                    _meshDataBuffer.Add(meshDataThreadInfoQueue.Dequeue());
            foreach (var info in _meshDataBuffer)
                info.callback(info.parameter);
        }

        // -- Map generation router ---------------------------------------------

        public MapData GenerateMapData(Vector2 centre)
        {
            if (WorldConfig != null && WorldConfig.Biomes.Count > 0)
                return GenerateMapDataMultiBiome(centre);

            if (Common == null || Common.NoiseConfig == null || Common.TerrainConfig == null)
            {
                Debug.LogWarning("[MapGenerator] Assign Common with NoiseConfig and TerrainConfig.");
                return new MapData();
            }

            return GenerateMapDataSingleBiome(centre);
        }

        // -- Single-biome path (original, unchanged) ---------------------------

        MapData GenerateMapDataSingleBiome(Vector2 centre)
        {
            int borderedSize = mapChunkSize + 2;
            float[,] noiseMap = new float[borderedSize, borderedSize];

            for (int y = 0; y < borderedSize; y++)
                for (int x = 0; x < borderedSize; x++)
                {
                    float worldX = centre.x + (x - borderedSize * 0.5f) * meshScale;
                    float worldZ = centre.y - (y - borderedSize * 0.5f) * meshScale;
                    noiseMap[x, y] = NoiseSampler.SampleWorld(Common.NoiseConfig,
                        new Vector2(worldX / noiseWorldScale, worldZ / noiseWorldScale));
                }

            if (Common.OverlayConfig != null)
                ApplyOverlays(noiseMap, centre, Common.OverlayConfig, meshScale, noiseWorldScale);

            var regions = Common.TerrainConfig.Regions;
            Color[] colourMap = new Color[mapChunkSize * mapChunkSize];

            for (int y = 0; y < mapChunkSize; y++)
                for (int x = 0; x < mapChunkSize; x++)
                {
                    float h = noiseMap[x, y];
                    colourMap[y * mapChunkSize + x] = SampleColour(regions, h);
                }

            return new MapData(noiseMap, colourMap);
        }

        // -- Multi-biome path --------------------------------------------------

        MapData GenerateMapDataMultiBiome(Vector2 centre)
        {
            int borderedSize = mapChunkSize + 2;

            // Determine the chunk's dominant biomes from its center
            // Used for chunk-wide overlay application
            BiomeSample chunkSample = WorldGenerator.Sample(
                new Vector2(centre.x, centre.y), WorldConfig, 0f);

            LevelGeneratorCommon configA = chunkSample.Primary ?? WorldConfig.OceanConfig;
            LevelGeneratorCommon configB = chunkSample.Secondary ?? configA;

            // Sample both noise maps for entire chunk
            float[,] noiseMapA = SampleNoiseMap(centre, borderedSize, configA);
            float[,] noiseMapB = chunkSample.HasSecondary
                ? SampleNoiseMap(centre, borderedSize, configB)
                : noiseMapA;

            // Apply per-biome overlays
            if (configA?.OverlayConfig != null)
                ApplyOverlays(noiseMapA, centre, configA.OverlayConfig, meshScale, noiseWorldScale);
            if (chunkSample.HasSecondary && configB?.OverlayConfig != null)
                ApplyOverlays(noiseMapB, centre, configB.OverlayConfig, meshScale, noiseWorldScale);

            // Per-pixel blend — WorldGenerator.Sample gives accurate per-pixel BlendT
            float[,] finalNoise = new float[borderedSize, borderedSize];
            // Cache BlendT per pixel — one WorldGenerator.Sample call per pixel max
            float[,] blendCache = new float[borderedSize, borderedSize];
            Color[] colourMap = new Color[mapChunkSize * mapChunkSize];

            // Pass 1 — noise + blend cache
            for (int y = 0; y < borderedSize; y++)
            {
                for (int x = 0; x < borderedSize; x++)
                {
                    float worldX = centre.x + (x - borderedSize * 0.5f) * meshScale;
                    float worldZ = centre.y - (y - borderedSize * 0.5f) * meshScale;

                    float ha = noiseMapA[x, y];
                    float hb = noiseMapB[x, y];

                    // Ocean check — if ocean, skip biome blending and just use noiseMapA
                    if (ha < WorldConfig.OceanLevel)
                    {
                        finalNoise[x, y] = ha;
                        blendCache[x, y] = 0f;
                        continue;
                    }

                    //per pixel biome blend weight is determined by WorldGenerator.Sample, which does Voronoi border detection and distortion noise in one pass
                    //OPTIMIZATION NEEDED potentially expensive
                    float t = 0f;
                    if (chunkSample.HasSecondary)
                    {
                        BiomeSample px = WorldGenerator.Sample(
                            new Vector2(worldX, worldZ), WorldConfig, ha);
                        t = px.BlendT;
                    }

                    blendCache[x, y] = t;
                    finalNoise[x, y] = Mathf.Lerp(ha, hb, t);
                }
            }

            // Build colour map (inner pixels only, no border)
            for (int y = 0; y < mapChunkSize; y++)
            {
                for (int x = 0; x < mapChunkSize; x++)
                {
                    float ha = noiseMapA[x, y];
                    float hb = noiseMapB[x, y];
                    float t = blendCache[x, y];  // reuse cached value

                    if (ha < WorldConfig.OceanLevel && WorldConfig.OceanConfig?.TerrainConfig != null)
                    {
                        colourMap[y * mapChunkSize + x] =
                            SampleColour(WorldConfig.OceanConfig.TerrainConfig.Regions, ha);
                        continue;
                    }

                    Color ca = configA?.TerrainConfig != null
                        ? SampleColour(configA.TerrainConfig.Regions, ha) : Color.grey;
                    Color cb = (chunkSample.HasSecondary && configB?.TerrainConfig != null)
                        ? SampleColour(configB.TerrainConfig.Regions, hb) : ca;

                    colourMap[y * mapChunkSize + x] = Color.Lerp(ca, cb, t);
                }
            }

            return new MapData(finalNoise, colourMap);
        }

        // -- Helpers -----------------------------------------------------------

        float[,] SampleNoiseMap(Vector2 centre, int borderedSize, LevelGeneratorCommon config)
        {
            float[,] map = new float[borderedSize, borderedSize];
            if (config?.NoiseConfig == null) return map;

            for (int y = 0; y < borderedSize; y++)
                for (int x = 0; x < borderedSize; x++)
                {
                    float worldX = centre.x + (x - borderedSize * 0.5f) * meshScale;
                    float worldZ = centre.y - (y - borderedSize * 0.5f) * meshScale;
                    map[x, y] = NoiseSampler.SampleWorld(config.NoiseConfig,
                        new Vector2(worldX / noiseWorldScale, worldZ / noiseWorldScale));
                }

            return map;
        }

        static Color SampleColour(List<TerrainType> regions, float height)
        {
            if (regions == null) return Color.grey;
            for (int i = 0; i < regions.Count; i++)
                if (height <= regions[i].Height)
                    return regions[i].Color.a > 0f ? regions[i].Color : Color.grey;
            return Color.grey;
        }

        // -- Overlay application (shared) --------------------------------------

        static void ApplyOverlays(float[,] noiseMap, Vector2 centre,
            OverlayConfig overlayConfig, float meshScale, float noiseWorldScale)
        {
            if (overlayConfig?.Overlays == null) return;

            int size = noiseMap.GetLength(0);
            float halfSize = (size - 1) * 0.5f;

            foreach (var overlay in overlayConfig.Overlays)
            {
                if (!overlay.Enabled) continue;
                var curve = new AnimationCurve(overlay.FalloffCurve.keys);

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float worldX = (centre.x + (x - halfSize) * meshScale) / noiseWorldScale;
                        float worldZ = (centre.y - (y - halfSize) * meshScale) / noiseWorldScale;

                        float dist = 0f;
                        switch (overlay.Type)
                        {
                            case OverlayType.Island:
                                float dx = (worldX - overlay.CentreX) / overlay.Scale;
                                float dz = (worldZ - overlay.CentreZ) / overlay.Scale;
                                dist = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dz * dz));
                                break;
                            case OverlayType.Equator:
                                dist = Mathf.Clamp01(Mathf.Abs(worldZ - overlay.WorldOffset) / overlay.Scale);
                                break;
                            case OverlayType.Meridian:
                                dist = Mathf.Clamp01(Mathf.Abs(worldX - overlay.WorldOffset) / overlay.Scale);
                                break;
                        }

                        float falloff = curve.Evaluate(dist);
                        float mask = overlay.GenInvert ? 1f - falloff : falloff;
                        float weight = overlay.Type == OverlayType.Island
                            ? mask : Mathf.Lerp(1f - overlay.Strength, 1f, mask);

                        noiseMap[x, y] = Mathf.Lerp(overlay.FloorValue, noiseMap[x, y], weight);
                    }
                }
            }
        }

        // -- Editor preview ----------------------------------------------------

        public void DrawMapInEditor()
        {
            MapDisplay display = GetComponent<MapDisplay>();
            if (display == null) return;

            MapData mapData = GenerateMapData(Vector2.zero);

            if (drawMode == DrawMode.NoiseMap)
                display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
            else if (drawMode == DrawMode.ColourMap)
                display.DrawTexture(TextureGenerator.TextureFromColourMap(
                    mapData.colorMap, mapChunkSize, mapChunkSize));
            else if (drawMode == DrawMode.Mesh)
                display.DrawMesh(
                    MeshGenerator.GenerateTerrainMesh(mapData.heightMap,
                        meshHeightMultiplier, meshHeightCurve, levelOfDetail, meshScale),
                    TextureGenerator.TextureFromColourMap(mapData.colorMap, mapChunkSize, mapChunkSize));
        }

        // -- Static world queries ----------------------------------------------

        public static string GetRegionAtWorldPosition(Vector3 worldPosition)
        {
            if (mapInstance == null) return string.Empty;

            // Resolve which TerrainConfig to use at this position
            List<TerrainType> regions = ResolveRegionsAtWorldPosition(worldPosition);
            if (regions == null) return string.Empty;

            float noiseValue = SampleCachedNoise(worldPosition);
            if (noiseValue < 0f) return string.Empty;

            for (int i = 0; i < regions.Count; i++)
                if (noiseValue <= regions[i].Height)
                    return regions[i].Name;

            return string.Empty;
        }

        public static float GetHeightAtWorldPosition(Vector3 worldPosition)
        {
            if (mapInstance == null) return 0f;
            float noiseValue = SampleCachedNoise(worldPosition);
            if (noiseValue < 0f) return 0f;
            return mapInstance.meshHeightCurve.Evaluate(noiseValue)
                   * mapInstance.meshHeightMultiplier
                   * mapInstance.Common.UniformScale;
        }

        public static void GetSlope() { /* tbd */ }

        // Returns cached noise value at world position, or -1 if not available
        static float SampleCachedNoise(Vector3 worldPosition)
        {
            if (mapInstance == null) return -1f;

            float uniformScale = mapInstance.Common != null
                ? mapInstance.Common.UniformScale : 1f;

            int chunkSize = Mathf.RoundToInt((mapChunkSize - 1) * mapInstance.meshScale);
            Vector2 pos2D = new Vector2(worldPosition.x / uniformScale, worldPosition.z / uniformScale);
            Vector2 chunkCoord = new Vector2(
                Mathf.RoundToInt(pos2D.x / chunkSize),
                Mathf.RoundToInt(pos2D.y / chunkSize));

            MapData? mapData = EndlessTerrain.GetCachedMapData(chunkCoord);
            if (mapData == null) return -1f;

            Vector2 chunkCenter = chunkCoord * chunkSize;
            float u = (pos2D.x - chunkCenter.x) / chunkSize + 0.5f;
            float v = 0.5f - (pos2D.y - chunkCenter.y) / chunkSize;
            int x = Mathf.Clamp(Mathf.RoundToInt(u * (mapChunkSize - 1)), 0, mapChunkSize - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt(v * (mapChunkSize - 1)), 0, mapChunkSize - 1);

            return mapData.Value.heightMap[x, z];
        }

        // Returns the correct TerrainConfig regions for a world position
        // Uses WorldConfig biome lookup if available, falls back to Common
        static List<TerrainType> ResolveRegionsAtWorldPosition(Vector3 worldPosition)
        {
            if (mapInstance.WorldConfig != null && mapInstance.WorldConfig.Biomes.Count > 0)
            {
                float noiseValue = SampleCachedNoise(worldPosition);
                if (noiseValue < 0f) return null;

                // Ocean check
                if (noiseValue < mapInstance.WorldConfig.OceanLevel)
                    return mapInstance.WorldConfig.OceanConfig?.TerrainConfig?.Regions;

                // Climate lookup
                float worldX = worldPosition.x;
                float worldZ = worldPosition.z;
                float temp = ClimateSampler.Sample(mapInstance.WorldConfig.TemperatureNoise, worldX, worldZ);
                float hum = ClimateSampler.Sample(mapInstance.WorldConfig.HumidityNoise, worldX, worldZ);
                BiomeEntry entry = mapInstance.WorldConfig.GetNearestBiome(temp, hum);
                return entry?.Config?.TerrainConfig?.Regions;
            }

            return mapInstance.Common?.TerrainConfig?.Regions;
        }

        // -- Threading struct --------------------------------------------------

        void OnValidate()
        {
#if UNITY_EDITOR
            if (autoUpdate)
                UnityEditor.EditorApplication.delayCall += () => { if (this != null) DrawMapInEditor(); };
#endif
        }

        struct MapThreadInfo<T>
        {
            public readonly Action<T> callback;
            public readonly T parameter;

            public MapThreadInfo(Action<T> callback, T parameter)
            {
                this.callback = callback;
                this.parameter = parameter;
            }
        }
    }
}