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

    [Header("Height")]
    public AnimationCurve HeightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public float HeightMultiplier = 20f;

    [Header("Texture")]
    [Range(64, 1024)]
    public int TextureResolution = 512;
}