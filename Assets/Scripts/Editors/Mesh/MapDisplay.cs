using LevelGenerator;
using LevelGenerator.Data;
using UnityEngine;

namespace LevelGenerator
{

    //Attach to a GameObject with a MeshFilter and MeshRenderer.
    //Call GenerateChunk to sample noise, build the mesh, and apply terrain colors.
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class MapDisplay : MonoBehaviour
    {
        public LevelGeneratorCommon Common;

        [Header("Chunk Settings")]
        [Tooltip("Number of vertices along each axis. Keep <= 255 for 16-bit index buffer.")]
        public bool AutoUpdate = false;
 

        MeshFilter   _meshFilter;
        MeshRenderer _meshRenderer;

        void Awake()
        {
            _meshFilter   = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
        }

        // Samples noise from NoiseConfig, builds the mesh, and vertex-colors it
        // using TerrainConfig regions. Call this from the editor or at runtime.
        [ContextMenu("Generate Chunk")]
        public void GenerateChunk()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();

            if (Common == null || Common.NoiseConfig == null || Common.TerrainConfig == null)
            {
                Debug.LogWarning("[MapDisplay] Common or its configs not assigned.");
                return;
            }

            int res = Mathf.Max(2, Common.VertexResolution);
            var heightMap = SampleHeightMap(res, out float[,] rawMap);
            var colorMap = BuildColorMap(rawMap, res);
            var meshData = MeshGenerator.GenerateTerrainMesh(heightMap, Common.HeightMultiplier);
            _meshFilter.sharedMesh = meshData.CreateMesh();
            var texture = BuildTexture(colorMap, res);
            if (_meshRenderer.sharedMaterial == null)
                _meshRenderer.sharedMaterial = new Material(Shader.Find("Standard"));
            _meshRenderer.sharedMaterial.mainTexture = texture;
        }

        float[,] SampleHeightMap(int res, out float[,] rawMap)
        {
            var map = new float[res, res];
            rawMap = new float[res, res];
            float inv = 1f / Mathf.Max(res - 1, 1);

            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float raw = NoiseSampler.Sample(Common.NoiseConfig, new Vector2(x * inv, y * inv), res);
                    rawMap[x, y] = raw;
                    map[x, y] = Common.HeightCurve.Evaluate(raw);
                }
            return map;
        }

        Color[] BuildColorMap(float[,] heightMap, int res)
        {
            var colors = new Color[res * res];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float h = heightMap[x, y];
                    Color c = Color.black;
                    for (int i = 0; i < Common.TerrainConfig.Regions.Count; i++)
                    {
                        var region = Common.TerrainConfig.Regions[i];
                        if (h <= region.Height)
                        {
                            c = region.Color.a > 0f ? region.Color : Color.grey;
                            break;
                        }
                    }
                    colors[y * res + x] = c;
                }
            return colors;
        }

        Texture2D BuildTexture(Color[] colorMap, int res)
        {
            var tex = new Texture2D(res, res)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(colorMap);
            tex.Apply();
            return tex;
        }
    }
}

