using System.Collections.Generic;
using UnityEngine;



    public enum TerrainBlendMode
    {
        Linear,
        Smoothstep,
        BayerOrdered,
        //BlueNoise,  broken bc texture
        VoronoiJitter
    }



    [System.Serializable]
    public class TerrainBlendSettings
    {
        [Range(1, 8)] public int BayerMatrixSize = 4;
        [Range(0f, 1f)] public float BayerStrength = 0.5f;

        public Texture2D BlueNoiseTexture;
        [Range(0f, 1f)] public float BlueNoiseStrength = 0.5f;

        [Range(0f, 1f)] public float VoronoiJitter = 0.5f;
        [Range(1, 8)] public int VoronoiCells = 4;
    }

    [System.Serializable]
    public class TerrainType
    {
        public string Name;
        [Range(0f, 1f)] public float Height;
        public Color Color = new Color(0f, 0f, 0f, 1f);
        [Range(0f, 1f)] public float BlendWidth;
        public TerrainBlendMode BlendMode;
        public Texture2D Texture;
        public TerrainBlendSettings BlendSettings = new TerrainBlendSettings();
    }

    [CreateAssetMenu(fileName = "TerrainConfig", menuName = "Runner/Terrain Config")]
    public class TerrainConfig : ScriptableObject
    {

        public List<TerrainType> Regions = new List<TerrainType>();
    }