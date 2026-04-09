using UnityEngine;

using Debug = UnityEngine.Debug;

namespace LevelGenerator
{
    public class MapGenerator : MonoBehaviour
    {
        public enum DrawMode { NoiseMap, ColourMap, Mesh }
        public DrawMode drawMode;

        public LevelGeneratorCommon Common;

        public const int mapChunkSize = 241;
        //public static int GetChunkSize(LevelGeneratorCommon common) => common.VertexResolution + 1;

        [Range(0, 6)]
        public int levelOfDetail;
        public float meshHeightMultiplier;
        public AnimationCurve meshHeightCurve;

        public bool autoUpdate;

        [Space]
        [Header("Noise Settings")]
        public int scale;
        public int octaves;
        public float persistence;
        public float lacunarity;
        public int seed;
        public Vector2 NoiseOffset;
        

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

            float inv = 1f / Mathf.Max(mapChunkSize - 1, 1);

            // First loop — heightmap
            float[,] noiseMap = Noise.GenerateNoiseMap(mapChunkSize, mapChunkSize, scale, octaves, persistence, lacunarity, seed, NoiseOffset);

            float cornerX0 = 0 + NoiseOffset.x * (mapChunkSize - 1);
            float cornerZ0 = 0 + NoiseOffset.y * (mapChunkSize - 1);
            float cornerX1 = (mapChunkSize - 1) + NoiseOffset.x * (mapChunkSize - 1);
            float cornerZ1 = -(mapChunkSize - 1) + NoiseOffset.y * (mapChunkSize - 1);

            // Second loop — colour map, same UV calculation
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
                            colourMap[y * mapChunkSize + x] = regions[i].Color.a > 0f ? regions[i].Color : Color.grey;
                            break;
                        }
                    }
                }

            MapDisplay display = GetComponent<MapDisplay>();
            if (drawMode == DrawMode.NoiseMap)
                display.DrawTexture(TextureGenerator.TextureFromHeightMap(noiseMap));
            else if (drawMode == DrawMode.ColourMap)
                display.DrawTexture(TextureGenerator.TextureFromColourMap(colourMap, mapChunkSize, mapChunkSize));
            else if (drawMode == DrawMode.Mesh)
                display.DrawMesh(
                    MeshGenerator.GenerateTerrainMesh(noiseMap, meshHeightMultiplier, meshHeightCurve, levelOfDetail),
                    TextureGenerator.TextureFromColourMap(colourMap, mapChunkSize, mapChunkSize));

            Debug.Log($"[Chunk {NoiseOffset}] HeightCurve keys: {meshHeightCurve?.length}, " +
          $"Sample at 0.5: {meshHeightCurve?.Evaluate(0.5f)}");
            Debug.Log($"[Chunk {NoiseOffset}] WorldPos: {transform.position}");
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