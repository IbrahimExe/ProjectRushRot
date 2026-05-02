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

        public LevelGeneratorCommon Common;

        public const int mapChunkSize = 241;

        [Range(0, 6)]
        public int levelOfDetail;

        public float meshHeightMultiplier;
        public AnimationCurve meshHeightCurve;

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
                mapData.heightMap, meshHeightMultiplier, meshHeightCurve, lod, Common.ChunkWorldSize);
            lock (meshDataThreadInfoQueue)
                meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }

        void Update()
        {
            lock (mapDataThreadInfoQueue)
            {
                while (mapDataThreadInfoQueue.Count > 0)
                {
                    var threadInfo = mapDataThreadInfoQueue.Dequeue();
                    threadInfo.callback(threadInfo.parameter);
                }
            }

            lock (meshDataThreadInfoQueue)
            {
                while (meshDataThreadInfoQueue.Count > 0)
                {
                    var threadInfo = meshDataThreadInfoQueue.Dequeue();
                    threadInfo.callback(threadInfo.parameter);
                }
            }
        }

        public MapData GenerateMapData(Vector2 centre)
        {
            if (Common == null || Common.NoiseConfig == null || Common.TerrainConfig == null)
            {
                Debug.LogWarning("[MapGenerator] Assign Common with NoiseConfig and TerrainConfig.");
                return new MapData();
            }

            // Sample heightmap
            float[,] noiseMap = new float[mapChunkSize, mapChunkSize];
            for (int y = 0; y < mapChunkSize; y++)
                for (int x = 0; x < mapChunkSize; x++)
                {
                    float worldX = centre.x + (x - (mapChunkSize - 1) * 0.5f);
                    float worldZ = centre.y - (y - (mapChunkSize - 1) * 0.5f);
                    noiseMap[x, y] = NoiseSampler.SampleWorld(Common.NoiseConfig, new Vector2(worldX, worldZ));
                }

            // Apply overlays to heightmap before colour assignment
            if (Common.OverlayConfig != null)
                ApplyOverlays(noiseMap, centre, Common.OverlayConfig);

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

        static void ApplyOverlays(float[,] noiseMap, Vector2 centre, OverlayConfig overlayConfig)
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
                        float worldX = centre.x + (x - halfSize);
                        float worldZ = centre.y - (y - halfSize);

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
                    MeshGenerator.GenerateTerrainMesh(mapData.heightMap, meshHeightMultiplier, meshHeightCurve, levelOfDetail, Common.ChunkWorldSize),
                    TextureGenerator.TextureFromColourMap(mapData.colorMap, mapChunkSize, mapChunkSize));
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