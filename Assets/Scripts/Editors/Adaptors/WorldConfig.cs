using System.Collections.Generic;
using UnityEngine;

// ── Runtime struct ────────────────────────────────────────────────────────────

/// <summary>
/// Result of sampling the world at a given position.
/// Contains the one or two biomes relevant to this pixel and the blend weight.
/// </summary>
public struct BiomeSample
{
    public LevelGeneratorCommon Primary;
    public LevelGeneratorCommon Secondary; // null when not on a border
    public float BlendT;    // 0 = fully Primary, 0.5 = 50/50 blend
    public bool IsOcean;

    public bool HasSecondary => Secondary != null && BlendT > 0f;
}

// ── Data ─────────────────────────────────────────────────────────────────────

[System.Serializable]
public class BiomeEntry
{
    [Tooltip("Display name for this biome.")]
    public string Name = "New Biome";

    [Tooltip("The full LevelGeneratorCommon config stack for this biome.")]
    public LevelGeneratorCommon Config;

    [Tooltip("Temperature on the Whittaker diagram (-1 cold, 1 hot).")]
    [Range(-1f, 1f)]
    public float Temperature = 0f;

    [Tooltip("Humidity on the Whittaker diagram (-1 dry, 1 wet).")]
    [Range(-1f, 1f)]
    public float Humidity = 0f;

    public Vector2 ClimatePosition
    {
        get => new Vector2(Temperature, Humidity);
        set
        {
            Temperature = value.x;
            Humidity = value.y;
        }
    }

    [Tooltip("How large this biome's cell is on the Whittaker diagram. Higher = more common.")]
    [Range(0.01f, 2f)]
    public float Weight = 1f;

    [Tooltip("Color used for preview in the World Editor graph.")]
    public Color PreviewColor = Color.green;
}

// ── ScriptableObject ──────────────────────────────────────────────────────────

[CreateAssetMenu(fileName = "WorldConfig", menuName = "Runner/World Config")]
public class WorldConfig : ScriptableObject
{
    [Header("Seed")]
    public int Seed = 0;

    // ── Tab 1: World Noise ────────────────────────────────────────────────────

    [Header("World Noise (Land vs Sea)")]
    [Tooltip("Simple Perlin FBM used only to determine land vs ocean. Separate from biome noise.")]
    public ClimateNoiseSettings WorldNoise;

    [Tooltip("Noise threshold below which is ocean. 0–1 range matching noise output.")]
    [Range(0f, 1f)]
    public float OceanLevel = 0.4f;

    [Tooltip("Config used for ocean tiles.")]
    public LevelGeneratorCommon OceanConfig;

    // ── Tab 2: Climate Noise ──────────────────────────────────────────────────

    [Header("Climate Noise")]
    [Tooltip("Large-scale temperature variation. Red = hot, Blue = cold.")]
    public ClimateNoiseSettings TemperatureNoise;

    [Tooltip("Large-scale humidity variation. Green = humid, Yellow = dry.")]
    public ClimateNoiseSettings HumidityNoise = new ClimateNoiseSettings
    {
        Offset = new Vector2(5000f, 5000f)
    };


    // ── Tab 3: Voronoi / Biomes ───────────────────────────────────────────────

    [Header("Voronoi")]
    [Tooltip("World-unit size of each biome cell. Larger = bigger biomes.")]
    public float CellSize = 5000f;

    [Tooltip("Width of the blend zone between two biomes in world units.")]
    public float BorderWidth = 500f;

    [Tooltip("Frequency of border distortion noise.")]
    public float BiomeDistortionFrequency = 0.0008f;

    [Tooltip("Strength of border distortion in world units.")]
    public float BiomeDistortionStrength = 300f;

    [Header("Biomes")]
    public List<BiomeEntry> Biomes = new List<BiomeEntry>();

    // ── Validation ────────────────────────────────────────────────────────────

    void OnValidate()
    {
        CellSize = Mathf.Max(100f, CellSize);
        BorderWidth = Mathf.Clamp(BorderWidth, 0f, CellSize * 0.5f);
        BiomeDistortionStrength = Mathf.Max(0f, BiomeDistortionStrength);

        // Ensure humidity has a different offset from temperature by default
        if (HumidityNoise != null && HumidityNoise.Offset == Vector2.zero)
            HumidityNoise.Offset = new Vector2(5000f, 5000f);
    }

    // ── Runtime helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns the BiomeEntry whose ClimatePosition is nearest to the given
    /// climate coords, weighted by each entry's Weight field.
    /// </summary>
    public BiomeEntry GetNearestBiome(float temperature, float humidity)
    {
        BiomeEntry best = null;
        float bestScore = float.MaxValue;

        foreach (var entry in Biomes)
        {
            //if (entry.Config == null) continue;
            float dx = temperature - entry.ClimatePosition.x;
            float dy = humidity - entry.ClimatePosition.y;
            float score = (dx * dx + dy * dy) / (entry.Weight * entry.Weight);
            if (score < bestScore) { bestScore = score; best = entry; }
        }

        return best;
    }

    /// <summary>
    /// Returns the two nearest biomes and their weighted scores.
    /// Used for border blending.
    /// </summary>
    public void GetTwoNearestBiomes(float temperature, float humidity,
        out BiomeEntry nearest, out BiomeEntry secondNearest,
        out float nearestScore, out float secondScore)
    {
        nearest = null;
        secondNearest = null;
        nearestScore = float.MaxValue;
        secondScore = float.MaxValue;

        foreach (var entry in Biomes)
        {
            if (entry.Config == null) continue;
            float dx = temperature - entry.ClimatePosition.x;
            float dy = humidity - entry.ClimatePosition.y;
            float score = (dx * dx + dy * dy) / (entry.Weight * entry.Weight);

            if (score < nearestScore)
            {
                secondNearest = nearest; secondScore = nearestScore;
                nearest = entry; nearestScore = score;
            }
            else if (score < secondScore)
            {
                secondNearest = entry; secondScore = score;
            }
        }
    }
}
