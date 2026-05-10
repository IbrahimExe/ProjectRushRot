using System;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

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

        public LevelGeneratorCommon Common;

        public const int mapChunkSize = 239;

        [Range(0, 6)]
        public int levelOfDetail;

        public float meshHeightMultiplier;
        public AnimationCurve meshHeightCurve;

        [Header("Mesh Scale")]
        [Tooltip("World units per vertex. 1 = one unit per vertex (238x238). Increase to make chunks larger.")]
        public float meshScale = 1f;

        [Tooltip("Scales the noise sampling. Match this to meshScale to keep noise density consistent.")]
        public float noiseWorldScale = 1f;

        public bool autoUpdate;

        Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
        Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

        void Reset()
        {
            meshHeightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }



        public void RequestMapData(Vector2 centre, Action<MapData> callback)
        {
            ThreadStart threadStart = delegate { MapDataThread(centre, callback); };
            new Thread(threadStart).Start();
        }

        void MapDataThread(Vector2 centre, Action<MapData> callback)
        {
            MapData mapData = GenerateMapData(centre);
            lock (mapDataThreadInfoQueue)
                mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
        }

        public void RequestMeshData(MapData mapData, int lod, Action<MeshData> callback)
        {
            ThreadStart threadStart = delegate { MeshDataThread(mapData, lod, callback); };
            new Thread(threadStart).Start();
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

        List<MapThreadInfo<MapData>> _mapDataBuffer = new List<MapThreadInfo<MapData>>();
        List<MapThreadInfo<MeshData>> _meshDataBuffer = new List<MapThreadInfo<MeshData>>();

        public MapData GenerateMapData(Vector2 centre)
        {
            if (Common == null || Common.NoiseConfig == null || Common.TerrainConfig == null)
            {
                Debug.LogWarning("[MapGenerator] Assign Common with NoiseConfig and TerrainConfig.");
                return new MapData();
            }

            int borderedSize = mapChunkSize + 2; // = 243
            float[,] noiseMap = new float[borderedSize, borderedSize];
            for (int y = 0; y < borderedSize; y++)
                for (int x = 0; x < borderedSize; x++)
                {
                    float worldX = centre.x + (x - borderedSize * 0.5f) * meshScale;
                    float worldZ = centre.y - (y - borderedSize * 0.5f) * meshScale;
                    noiseMap[x, y] = NoiseSampler.SampleWorld(Common.NoiseConfig,
                        new Vector2(worldX / noiseWorldScale, worldZ / noiseWorldScale));
                }

            // Apply overlays to heightmap before colour assignment
            if (Common.OverlayConfig != null)
                ApplyOverlays(noiseMap, centre, Common.OverlayConfig, meshScale, noiseWorldScale);

            // Build colour map
            var regions = Common.TerrainConfig.Regions;
            Color[] colourMap = new Color[mapChunkSize * mapChunkSize];
            for (int y = 0; y < mapChunkSize; y++)
                for (int x = 0; x < mapChunkSize; x++)
                {
                    float h = noiseMap[x, y];
                    for (int i = 0; i < regions.Count; i++)
                    {
                        if (h <= regions[i].Height)
                        {
                            colourMap[y * mapChunkSize + x] = regions[i].Color.a > 0f
                                ? regions[i].Color : Color.grey;
                            break;
                        }
                    }
                }

            return new MapData(noiseMap, colourMap);
        }

        static void ApplyOverlays(float[,] noiseMap, Vector2 centre, OverlayConfig overlayConfig, float meshScale, float noiseWorldScale)
        {
            if (overlayConfig?.Overlays == null) return;

            int size = noiseMap.GetLength(0);
            float halfSize = (size - 1) * 0.5f;

            foreach (var overlay in overlayConfig.Overlays)
            {
                if (!overlay.Enabled) continue;

                // Copy curve keys for thread safety
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
                            ? mask
                            : Mathf.Lerp(1f - overlay.Strength, 1f, mask);

                        noiseMap[x, y] = Mathf.Lerp(overlay.FloorValue, noiseMap[x, y], weight);
                    }
                }
            }
        }

        public void DrawMapInEditor()
        {
            MapDisplay display = GetComponent<MapDisplay>();
            if (display == null) return;

            MapData mapData = GenerateMapData(Vector2.zero);

            if (drawMode == DrawMode.NoiseMap)
                display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
            else if (drawMode == DrawMode.ColourMap)
                display.DrawTexture(TextureGenerator.TextureFromColourMap(mapData.colorMap, mapChunkSize, mapChunkSize));
            else if (drawMode == DrawMode.Mesh)
                display.DrawMesh(
                    MeshGenerator.GenerateTerrainMesh(mapData.heightMap, meshHeightMultiplier, meshHeightCurve, levelOfDetail, meshScale),
                    TextureGenerator.TextureFromColourMap(mapData.colorMap, mapChunkSize, mapChunkSize));
            //redo collision mesh if needed
            
        }

        public static MapGenerator mapInstance;
        void Awake()
        {
            mapInstance = this;
        }
        public static string GetRegionAtWorldPosition(Vector3 worldPosition)
        {
            if (mapInstance == null || mapInstance.Common?.TerrainConfig == null)
                return string.Empty;

            float uniformScale = mapInstance.Common.UniformScale;
            int chunkSize = Mathf.RoundToInt((mapChunkSize - 1) * mapInstance.meshScale);

            Vector2 pos2D = new Vector2(worldPosition.x / uniformScale, worldPosition.z / uniformScale);

            Vector2 chunkCoord = new Vector2(
                Mathf.RoundToInt(pos2D.x / chunkSize),
                Mathf.RoundToInt(pos2D.y / chunkSize));

            Vector2 chunkCenter = chunkCoord * chunkSize;

            //Debug.Log($"[Region] worldPos:{worldPosition} pos2D:{pos2D} chunkSize:{chunkSize} chunkCoord:{chunkCoord} chunkCenter:{chunkCenter}");

            MapData? mapData = EndlessTerrain.GetCachedMapData(chunkCoord);
            //Debug.Log($"[Region] mapData found: {mapData.HasValue}");
            if (mapData == null) return string.Empty;

            float u = (pos2D.x - chunkCenter.x) / chunkSize + 0.5f;
            float v = 0.5f - (pos2D.y - chunkCenter.y) / chunkSize; // inverted
            int x = Mathf.Clamp(Mathf.RoundToInt(u * (mapChunkSize - 1)), 0, mapChunkSize - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt(v * (mapChunkSize - 1)), 0, mapChunkSize - 1);

            float noiseValue = mapData.Value.heightMap[x, z];

            //Debug.Log($"[Region] u:{u:F3} v:{v:F3} x:{x} z:{z} noise:{noiseValue:F3}");

            var regions = mapInstance.Common.TerrainConfig.Regions;
            for (int i = 0; i < regions.Count; i++)
                if (noiseValue <= regions[i].Height)
                    return regions[i].Name;

            return string.Empty;
        }

        public static float GetHeightAtWorldPosition(Vector3 worldPosition)
        {
            if (mapInstance == null || mapInstance.Common?.TerrainConfig == null)
                return 0f;

            float uniformScale = mapInstance.Common.UniformScale;
            int chunkSize = Mathf.RoundToInt((mapChunkSize - 1) * mapInstance.meshScale);
            Vector2 pos2D = new Vector2(worldPosition.x / uniformScale, worldPosition.z / uniformScale);

            Vector2 chunkCoord = new Vector2(
                Mathf.RoundToInt(pos2D.x / chunkSize),
                Mathf.RoundToInt(pos2D.y / chunkSize));

            MapData? mapData = EndlessTerrain.GetCachedMapData(chunkCoord);
            if (mapData == null) return 0f;

            Vector2 chunkCenter = chunkCoord * chunkSize;
            float u = (pos2D.x - chunkCenter.x) / chunkSize + 0.5f;
            float v = 0.5f - (pos2D.y - chunkCenter.y) / chunkSize;
            int x = Mathf.Clamp(Mathf.RoundToInt(u * (mapChunkSize - 1)), 0, mapChunkSize - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt(v * (mapChunkSize - 1)), 0, mapChunkSize - 1);

            float noiseValue = mapData.Value.heightMap[x, z];

            // Apply height curve and multiplier — same as the mesh does
            return mapInstance.meshHeightCurve.Evaluate(noiseValue) * mapInstance.meshHeightMultiplier * uniformScale;

            //REPLACE RAYCAST HEIGHT CHECK WITH float groundY = MapGenerator.GetHeightAtWorldPosition(transform.position);
        }

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