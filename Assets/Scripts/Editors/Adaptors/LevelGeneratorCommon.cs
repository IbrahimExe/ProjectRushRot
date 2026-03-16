using UnityEngine;

namespace LevelGenerator.Data
{
    [CreateAssetMenu(fileName = "LevelGeneratorCommon", menuName = "Runner/Level Generator Common")]
    public class LevelGeneratorCommon : ScriptableObject
    {
        [Header("Project Configs")]
        public NoiseConfig NoiseConfig;
        public TerrainConfig TerrainConfig;
        public PrefabCatalog PrefabCatalog;


        [Header("Chunk Dimensions")]
        [Tooltip("World units wide per chunk.")]
        public float ChunkWidth = 100f;

        [Tooltip("World units long per chunk.")]
        public float ChunkLength = 100f;

        [Header("Mesh")]
        [Tooltip("Vertices per axis. Max 255 for 16-bit index buffer (255x255 = 65025 verts).")]
        [Range(2, 255)]
        public int VertexResolution = 129;

        [Header("Height")]
        public AnimationCurve HeightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("World unit height scale applied to noise values [0,1].")]
        public float HeightMultiplier = 20f;
    }
}
