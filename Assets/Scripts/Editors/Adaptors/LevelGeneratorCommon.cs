using UnityEngine;

[CreateAssetMenu(fileName = "LevelGeneratorCommon", menuName = "Runner/Level Generator Common")]
public class LevelGeneratorCommon : ScriptableObject
{
    [Header("Project Configs")]
    public NoiseConfig NoiseConfig;
    public TerrainConfig TerrainConfig;
    public PrefabCatalog PrefabCatalog;

    [Header("Chunk Dimensions")]
    public float ChunkWidth = 100f;
    public float ChunkLength = 100f;

    [Header("Texture")]
    [Range(64, 1024)]
    public int TextureResolution = 512;
}