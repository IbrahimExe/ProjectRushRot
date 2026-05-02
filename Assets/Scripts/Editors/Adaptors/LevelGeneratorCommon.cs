using UnityEngine;

[CreateAssetMenu(fileName = "LevelGeneratorCommon", menuName = "Runner/Level Generator Common")]
public class LevelGeneratorCommon : ScriptableObject
{
    [Header("Project Configs")]
    public NoiseConfig NoiseConfig;
    public TerrainConfig TerrainConfig;
    public PrefabCatalog PrefabCatalog;
    public OverlayConfig OverlayConfig;

    [Header("World")]
    public float UniformScale = 1f;

    [Header("Chunk Dimensions")]
    public float ChunkWidth = 100f;
    public float ChunkLength = 100f;

    [Header("Height")]
    public AnimationCurve HeightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public float HeightMultiplier = 20f;

    [Header("Chunk")]
    public float ChunkWorldSize = 240f;

    [Header("Texture")]
    [Range(64, 1024)]
    public int TextureResolution = 512;

    [Header("Mesh")]
    [Range(1, 240)]
    public int VertexResolution = 240; //must be divissable by 2
}