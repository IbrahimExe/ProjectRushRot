using UnityEngine;

/// <summary>
/// Lightweight fractional Brownian motion sampler for climate axes.
/// Uses only Mathf.PerlinNoise — no warp, no marble, no ridged layers.
/// Intended for large-scale temperature and humidity variation only.
/// </summary>

[System.Serializable]
public class ClimateNoiseSettings
{
    [Tooltip("Base frequency. Keep very low (0.0001–0.001) for continental-scale variation.")]
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

    [Tooltip("World-space offset. Acts as a seed per axis — use different values for temp and humidity.")]
    public Vector2 Offset = Vector2.zero;
}

public static class ClimateSampler
{
    /// <summary>
    /// Samples climate noise at a world position.
    /// Returns a value in the range [-1, 1].
    /// </summary>
    public static float Sample(ClimateNoiseSettings settings, float worldX, float worldZ)
    {
        float value    = 0f;
        float amplitude = 1f;
        float frequency = settings.Frequency;
        float maxValue  = 0f;

        for (int i = 0; i < settings.Octaves; i++)
        {
            float sampleX = (worldX + settings.Offset.x + 10000f) * frequency;
            float sampleZ = (worldZ + settings.Offset.y + 10000f) * frequency;

            // PerlinNoise returns 0–1, center around 0 by subtracting 0.5
            value    += (Mathf.PerlinNoise(sampleX, sampleZ) - 0.5f) * amplitude;
            maxValue += amplitude * 0.5f;

            amplitude *= settings.Persistence;
            frequency *= settings.Lacunarity;
        }

        // Normalize to [-1, 1]
        return maxValue > 0f ? Mathf.Clamp(value / maxValue, -1f, 1f) : 0f;
    }
}
