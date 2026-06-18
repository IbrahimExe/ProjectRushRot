using UnityEngine;

/// <summary>
/// Static 5-pass world generation sampler.
///
/// Pass 1 - Ocean mask:        samples WorldNoise internally vs OceanLevel
/// Pass 2 - Cell hashing:      deterministic random point per Voronoi cell
/// Pass 3 - Voronoi overlay:   land pixels only, find nearest cell point
/// Pass 4 - Border blending:   distortion noise + dual nearest-point detection
/// Pass 5 - Climate mapping:   cell climate coords - nearest BiomeEntry on Whittaker diagram
///
/// BlendT range: 0 = fully Primary, 0.5 = 50/50 blend at border center.
/// Secondary can never fully dominate (by design - borders are shared zones, not flips).
/// </summary>
public static class WorldGenerator
{
    // -- Public entry point ----------------------------------------------------

    /// <summary>
    /// Returns the BiomeSample for a given world position.
    /// WorldNoise is sampled internally - callers do not need to provide a noise value.
    /// </summary>
    public static BiomeSample Sample(Vector2 worldPos, WorldConfig config)
    {
        if (config == null)
            return default;

        // Pass 1 - Ocean mask (sampled internally - single source of truth)
        float worldNoise = ClimateSampler.Sample01(config.WorldNoise, worldPos.x, worldPos.y);
        if (worldNoise < config.OceanLevel)
        {
            return new BiomeSample
            {
                Primary = config.OceanConfig,
                Secondary = null,
                BlendT = 0f,
                IsOcean = true
            };
        }

        return SampleVoronoi(worldPos, config);
    }

    // -- Voronoi sampling ------------------------------------------------------

    static BiomeSample SampleVoronoi(Vector2 worldPos, WorldConfig config)
    {
        float cellSize = config.CellSize;

        // Pass 4a - distortion noise offsets worldPos before distance checks
        float distFreq = config.BiomeDistortionFrequency;
        float distX = (Mathf.PerlinNoise(
                           (worldPos.x + 10000f) * distFreq,
                           (worldPos.y + 10000f) * distFreq) - 0.5f)
                      * config.BiomeDistortionStrength;
        float distY = (Mathf.PerlinNoise(
                           (worldPos.x + 11739f) * distFreq,
                           (worldPos.y + 13319f) * distFreq) - 0.5f)
                      * config.BiomeDistortionStrength;
        Vector2 distortedPos = worldPos + new Vector2(distX, distY);

        int baseCellX = Mathf.FloorToInt(distortedPos.x / cellSize);
        int baseCellY = Mathf.FloorToInt(distortedPos.y / cellSize);

        float nearestDist = float.MaxValue;
        float secondDist = float.MaxValue;
        int nearestCellX = baseCellX;
        int nearestCellY = baseCellY;
        int secondCellX = baseCellX;
        int secondCellY = baseCellY;

        // Pass 3 - check 3x3 neighboring cells
        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                int cx = baseCellX + i;
                int cy = baseCellY + j;

                Vector2 cellPoint = GetCellPoint(cx, cy, cellSize, config.Seed);
                float dist = Vector2.Distance(distortedPos, cellPoint);

                if (dist < nearestDist)
                {
                    secondDist = nearestDist;
                    secondCellX = nearestCellX;
                    secondCellY = nearestCellY;

                    nearestDist = dist;
                    nearestCellX = cx;
                    nearestCellY = cy;
                }
                else if (dist < secondDist)
                {
                    secondDist = dist;
                    secondCellX = cx;
                    secondCellY = cy;
                }
            }
        }

        // Pass 4b - border detection
        // BlendT range: 0 (fully primary) > 0.5 (50/50 at border center)
        // Secondary never dominates by design - the border is a shared blend zone.
        float borderDistance = secondDist - nearestDist;
        float blendT = 0f;

        if (borderDistance < config.BorderWidth)
            blendT = 0.5f - (borderDistance / config.BorderWidth * 0.5f);

        // Pass 5 - climate mapping - BiomeEntry
        LevelGeneratorCommon primary = GetBiomeConfig(nearestCellX, nearestCellY, cellSize, config);
        LevelGeneratorCommon secondary = blendT > 0f
            ? GetBiomeConfig(secondCellX, secondCellY, cellSize, config)
            : null;

        // If both cells resolved to the same config, no blend needed
        if (secondary == primary) { secondary = null; blendT = 0f; }

        return new BiomeSample
        {
            Primary = primary ?? config.OceanConfig,
            Secondary = secondary,
            BlendT = blendT,
            IsOcean = false
        };
    }

    // -- Cell helpers ----------------------------------------------------------

    /// <summary>
    /// Returns the random point inside a cell using hash-based placement.
    /// </summary>
    static Vector2 GetCellPoint(int cellX, int cellY, float cellSize, int seed)
    {
        uint h = HashCell(cellX, cellY, seed);
        float rx = (h & 0xFFFF) / (float)0x10000;
        float ry = ((h >> 16) & 0xFFFF) / (float)0x10000;
        return new Vector2((cellX + rx) * cellSize, (cellY + ry) * cellSize);
    }

    /// <summary>
    /// Resolves a cell's climate coords via ClimateSampler and returns the nearest BiomeEntry config.
    /// Climate is sampled at the cell center - constant per cell by design (performance).
    /// </summary>
    static LevelGeneratorCommon GetBiomeConfig(int cellX, int cellY, float cellSize, WorldConfig config)
    {
        // Cell center in world space
        float centerX = (cellX + 0.5f) * cellSize;
        float centerZ = (cellY + 0.5f) * cellSize;

        // Pass 5 - sample climate at cell center via shared ClimateSampler
        float temperature = ClimateSampler.Sample(config.TemperatureNoise, centerX, centerZ);
        float humidity = ClimateSampler.Sample(config.HumidityNoise, centerX, centerZ);

        BiomeEntry entry = config.GetNearestBiome(temperature, humidity);
        return entry?.Config;
    }

    // -- Hash functions (from video) ------------------------------------------

    static uint Hash(uint x)
    {
        x ^= x >> 16;
        x *= 0x7feb352d;
        x ^= x >> 15;
        x *= 0x846ca68b;
        x ^= x >> 16;
        return x;
    }

    static uint HashCell(int gridX, int gridY, int seed)
    {
        uint h = (uint)seed;
        h ^= Hash((uint)gridX);
        h ^= Hash((uint)gridY);
        return Hash(h);
    }
}