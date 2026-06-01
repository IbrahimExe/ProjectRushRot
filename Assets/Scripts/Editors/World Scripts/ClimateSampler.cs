using UnityEngine;

/// <summary>
/// Single source of truth for all climate FBM sampling.
/// Used by both runtime (WorldGenerator, MapGenerator) and editor panels.
///
/// NOTE: All samples are offset by +10000 to avoid Unity's Perlin noise
/// symmetry artifact around the origin.
/// </summary>

[System.Serializable]
public class ClimateNoiseSettings
{
    [Tooltip("Base frequency. Keep very low (0.00001–0.001) for continental-scale variation. " +
             "Values above 0.01 produce high-frequency noise unsuitable for world shapes.")]
    [Range(0.001f, 0.10f)]
    public float Frequency = 0.0005f;

    [Tooltip("Number of noise octaves. 2–4 is sufficient for climate.")]
    [Range(1, 6)]
    public int Octaves = 3;

    [Tooltip("Amplitude multiplier per octave. Lower = smoother.")]
    [Range(0f, 1f)]
    public float Persistence = 0.5f;

    [Tooltip("Frequency multiplier per octave.")]
    [Range(1f, 4f)]
    public float Lacunarity = 2f;

    [Tooltip("World-space offset. Use different values for temperature and humidity " +
             "to avoid correlated patterns.")]
    public Vector2 Offset = Vector2.zero;
}

public static class ClimateSampler
{
    // Constant offset to avoid Unity's Perlin symmetry around (0,0)
    const float ORIGIN_OFFSET = 10000f;

    /// <summary>
    /// Samples climate noise at a world position.
    /// Returns a value in the range [-1, 1].
    /// Used for temperature and humidity axes.
    /// </summary>
    public static float Sample(ClimateNoiseSettings settings, float worldX, float worldZ)
    {
        if (settings == null) return 0f;

        float value = 0f;
        float amplitude = 1f;
        float frequency = Mathf.Max(0.00001f, settings.Frequency);
        float maxValue = 0f;

        for (int i = 0; i < settings.Octaves; i++)
        {
            float sx = (worldX + settings.Offset.x + ORIGIN_OFFSET) * frequency;
            float sz = (worldZ + settings.Offset.y + ORIGIN_OFFSET) * frequency;

            value += (Mathf.PerlinNoise(sx, sz) - 0.5f) * amplitude;
            maxValue += amplitude * 0.5f;

            amplitude *= settings.Persistence;
            frequency *= settings.Lacunarity;
        }

        return maxValue > 0f ? Mathf.Clamp(value / maxValue, -1f, 1f) : 0f;
    }

    /// <summary>
    /// Samples noise at a world position.
    /// Returns a value in the range [0, 1].
    /// Used for the world/ocean noise mask.
    /// </summary>
    public static float Sample01(ClimateNoiseSettings settings, float worldX, float worldZ)
    {
        if (settings == null) return 0.5f;

        float value = 0f;
        float amplitude = 1f;
        float frequency = Mathf.Max(0.00001f, settings.Frequency);
        float maxValue = 0f;

        for (int i = 0; i < settings.Octaves; i++)
        {
            float sx = (worldX + settings.Offset.x + ORIGIN_OFFSET) * frequency;
            float sz = (worldZ + settings.Offset.y + ORIGIN_OFFSET) * frequency;

            value += Mathf.PerlinNoise(sx, sz) * amplitude;
            maxValue += amplitude;

            amplitude *= settings.Persistence;
            frequency *= settings.Lacunarity;
        }

        return maxValue > 0f ? Mathf.Clamp01(value / maxValue) : 0f;
    }
}