using UnityEngine;

namespace LevelGenerator
{
    public class MapGenerator : MonoBehaviour
    {
        public enum DrawMode { NoiseMap, ColourMap, Mesh }
        public DrawMode drawMode;

        public LevelGeneratorCommon Common;

        const int mapChunkSize = 241;

        [Range(0, 6)]
        public int levelOfDetail;

        public float meshHeightMultiplier;
        public AnimationCurve meshHeightCurve;

        public bool autoUpdate;

        void Reset()
        {
            meshHeightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }

        public void GenerateMap()
        {
            if (Common == null || Common.NoiseConfig == null || Common.TerrainConfig == null)
            {
                Debug.LogWarning("[MapGenerator] Assign Common with NoiseConfig and TerrainConfig.");
                return;
            }
            if (meshHeightCurve == null || meshHeightCurve.length == 0)
                meshHeightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            // Sample heightmap
            float[,] noiseMap = new float[mapChunkSize, mapChunkSize];
            float inv = 1f / Mathf.Max(mapChunkSize - 1, 1);
            for (int y = 0; y < mapChunkSize; y++)
                for (int x = 0; x < mapChunkSize; x++)
                    noiseMap[x, y] = NoiseSampler.Sample(Common.NoiseConfig, new Vector2(x * inv, y * inv), mapChunkSize);

            // Build colour map from terrain regions
            var regions = Common.TerrainConfig.Regions;
            Color[] colourMap = new Color[mapChunkSize * mapChunkSize];
            for (int y = 0; y < mapChunkSize; y++)
            {
                for (int x = 0; x < mapChunkSize; x++)
                {
                    float h = noiseMap[x, y];
                    for (int i = 0; i < regions.Count; i++)
                    {
                        if (h <= regions[i].Height)
                        {
                            colourMap[y * mapChunkSize + x] = regions[i].Color.a > 0f ? regions[i].Color : Color.grey;
                            break;
                        }
                    }
                }
            }


            MapDisplay display = GetComponentInChildren<MapDisplay>();

            if (drawMode == DrawMode.NoiseMap)
                display.DrawTexture(TextureGenerator.TextureFromHeightMap(noiseMap));
            else if (drawMode == DrawMode.ColourMap)
                display.DrawTexture(TextureGenerator.TextureFromColourMap(colourMap, mapChunkSize, mapChunkSize));
            else if (drawMode == DrawMode.Mesh)
                display.DrawMesh(
                    MeshGenerator.GenerateTerrainMesh(noiseMap, meshHeightMultiplier, meshHeightCurve, levelOfDetail),
                    TextureGenerator.TextureFromColourMap(colourMap, mapChunkSize, mapChunkSize));
        }

        void OnValidate()
        {
#if UNITY_EDITOR
            if (autoUpdate)
                UnityEditor.EditorApplication.delayCall += () => { if (this != null) GenerateMap(); };
#endif
        }
    }
}