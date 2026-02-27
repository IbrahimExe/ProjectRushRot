using UnityEngine;

/// <summary>
/// Noise sampler following the scratchapixel.com reference exactly.
///
/// COORDINATE SYSTEM
/// -----------------
/// uv [0,1] → pixel coords px = uv * resolution
/// Space frequency then scales px → sample point p used by base noise.
///
/// Patterns (Wood, Marble, Turbulence) work exactly as the reference:
///   pNoise = Vec2f(i, j) * frequency   ← their OWN frequency on raw pixel coords
/// They fully REPLACE the base noise when enabled (they are not additive tweaks).
/// Quantization post-processes whatever value came before it.
///
/// BASE NOISE TYPES
/// ----------------
///   Default — 5-layer fBm over Value noise.  Cloud/terrain.
///   Value   — Hermite-interpolated lattice scalars.  Blocky/pillowy.
///   Perlin  — Gradient noise (dot product with random unit vectors).  Smooth/organic.
///   Worley  — 1 - nearest feature-point distance.  Crystal/cell/stone.
/// </summary>
public static class NoiseSampler
{
    // ─── Entry point ───────────────────────────────────────────────────────────

    public static float Sample(NoiseConfig cfg, Vector2 uv, int resolution)
    {
        // Raw pixel coordinate — used directly by patterns that carry their own frequency
        Vector2 px = uv * resolution;

        // Space-scaled coordinate — used by base noise
        Vector2 p = ApplySpace(cfg, px);

        float n;

        // ── Patterns that fully replace the base noise ─────────────────────────
        // Priority: Wood → Marble → Turbulence (last enabled one wins, like a stack)
        // Quantization always post-processes.

        bool hasPattern = cfg.wood.enabled || cfg.marble.enabled || cfg.turbulence.enabled;

        if (!hasPattern)
        {
            // Pure base noise
            n = BaseNoise(cfg.noiseType, p);
        }
        else
        {
            // Start with base noise as the foundation; patterns override sequentially
            n = BaseNoise(cfg.noiseType, p);

            if (cfg.wood.enabled)
                n = WoodPattern(px, cfg.wood);

            if (cfg.marble.enabled)
                n = MarblePattern(px, cfg.marble);

            if (cfg.turbulence.enabled)
                n = TurbulencePattern(px, cfg.turbulence);
        }

        // Quantization always post-processes (it doesn't replace, it posterises)
        if (cfg.quantization.enabled && cfg.quantization.totalChannels >= 2)
            n = Mathf.Floor(n * cfg.quantization.totalChannels)
                / (cfg.quantization.totalChannels - 1f);

        return Mathf.Clamp01(n);
    }

    // ─── Space ─────────────────────────────────────────────────────────────────

    static Vector2 ApplySpace(NoiseConfig cfg, Vector2 px)
    {
        if (cfg.spaceMode == SpaceMode.Tiling)
        {
            // Single uniform frequency — like FastNoiseLite SetFrequency(f)
            // 0.01 scale makes a frequency of "1" look like one full noise cycle
            // across a 100px region, matching the reference's noiseFrequency idiom.
            return px * (cfg.tilingFrequency * 0.01f);
        }
        else
        {
            Vector2 freq = new Vector2(cfg.position.value.x, cfg.position.value.y);
            Vector2 offset = new Vector2(cfg.rotation.value.x, cfg.rotation.value.y);
            Vector2 scl = new Vector2(cfg.scale.value.x, cfg.scale.value.y);
            return new Vector2(
                px.x * freq.x * 0.01f * scl.x + offset.x,
                px.y * freq.y * 0.01f * scl.y + offset.y);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  BASE NOISE
    // ──────────────────────────────────────────────────────────────────────────

    static float BaseNoise(NoiseType type, Vector2 p)
    {
        switch (type)
        {
            case NoiseType.Value: return ValueNoise(p);
            case NoiseType.Perlin: return PerlinNoise(p);
            case NoiseType.Worley: return WorleyNoise(p);
            default: return FbmNoise(p);
        }
    }

    // Default: 5-layer fBm
    static float FbmNoise(Vector2 p)
    {
        const int layers = 5;
        const float lacunarity = 2.0f;
        const float gain = 0.5f;
        float sum = 0f, amp = 0.5f, maxAmp = 0f;
        Vector2 q = p;
        for (int i = 0; i < layers; i++)
        {
            sum += ValueNoise(q) * amp;
            maxAmp += amp;
            q *= lacunarity;
            amp *= gain;
        }
        return sum / maxAmp;
    }

    // Value: random scalars at lattice corners, Hermite blend
    static float ValueNoise(Vector2 p)
    {
        Vector2 i = Floor2(p);
        Vector2 f = p - i;
        float v00 = Rand(i);
        float v10 = Rand(i + new Vector2(1, 0));
        float v01 = Rand(i + new Vector2(0, 1));
        float v11 = Rand(i + new Vector2(1, 1));
        float ux = f.x * f.x * (3f - 2f * f.x);
        float uy = f.y * f.y * (3f - 2f * f.y);
        return Mathf.Lerp(Mathf.Lerp(v00, v10, ux), Mathf.Lerp(v01, v11, ux), uy);
    }

    // Perlin: gradient noise with quintic fade
    static float PerlinNoise(Vector2 p)
    {
        Vector2 i = Floor2(p);
        Vector2 f = p - i;
        float ux = f.x * f.x * f.x * (f.x * (f.x * 6f - 15f) + 10f);
        float uy = f.y * f.y * f.y * (f.y * (f.y * 6f - 15f) + 10f);
        float n00 = GradDot(i + new Vector2(0, 0), f - new Vector2(0, 0));
        float n10 = GradDot(i + new Vector2(1, 0), f - new Vector2(1, 0));
        float n01 = GradDot(i + new Vector2(0, 1), f - new Vector2(0, 1));
        float n11 = GradDot(i + new Vector2(1, 1), f - new Vector2(1, 1));
        float raw = Mathf.Lerp(Mathf.Lerp(n00, n10, ux), Mathf.Lerp(n01, n11, ux), uy);
        return raw * 0.7071f + 0.5f;   // [-0.707,0.707] → [0,1]
    }

    static float GradDot(Vector2 lattice, Vector2 offset)
    {
        float a = Rand(lattice) * Mathf.PI * 2f;
        return offset.x * Mathf.Cos(a) + offset.y * Mathf.Sin(a);
    }

    // Worley: 1 - nearest feature-point distance
    static float WorleyNoise(Vector2 p)
    {
        Vector2 cell = Floor2(p);
        float minDist = float.MaxValue;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                Vector2 nb = cell + new Vector2(dx, dy);
                Vector2 pt = nb + new Vector2(Rand(nb), Rand(nb + new Vector2(37.3f, 17.9f)));
                float d = Vector2.Distance(p, pt);
                if (d < minDist) minDist = d;
            }
        return Mathf.Clamp01(1f - minDist / 1.4142f);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  PATTERNS  —  scratchapixel.com reference, verbatim
    //  Input: raw pixel coords `px`  +  the pattern's own frequency setting.
    //  Each pattern IS its own noise function, not a post-process on base noise.
    // ──────────────────────────────────────────────────────────────────────────

    // WOOD
    // Reference:  g = noise(Vec2f(i,j) * frequency) * multiplier;
    //             result = g - (int)g;      // i.e. frac(g)
    static float WoodPattern(Vector2 px, WoodSettings s)
    {
        // frequency drives how "zoomed in" the underlying noise is
        Vector2 pNoise = px * s.frequency * 0.01f;
        float g = ValueNoise(pNoise) * s.multiplier;
        return g - Mathf.Floor(g);
    }

    // MARBLE
    // Reference:  pNoise = Vec2f(i,j) * frequency;
    //             for layers: noiseValue += noise(pNoise) * amplitude; pNoise *= freqMult; amp *= ampMult;
    //             result = (sin( (i + noiseValue*100) * 2π/200 + phase ) + 1) / 2
    static float MarblePattern(Vector2 px, MarbleSettings s)
    {
        Vector2 pNoise = px * s.frequency * 0.01f;
        float amplitude = 1f;
        float noiseVal = 0f;

        for (int l = 0; l < (int)s.numLayers; l++)
        {
            noiseVal += ValueNoise(pNoise) * amplitude;
            pNoise *= s.frequencyMult;
            amplitude *= s.amplitudeMult;
        }

        // px.x plays the role of 'i' in the reference formula
        float arg = (px.x + noiseVal * 100f) * (2f * Mathf.PI / 200f) + s.phase;
        return (Mathf.Sin(arg) + 1f) * 0.5f;
    }

    // TURBULENCE
    // Reference:  pNoise = Vec2f(i,j) * frequency;
    //             for layers: noiseMap += fabs(2*noise(pNoise) - 1) * amplitude;
    //             pNoise *= freqMult; amp *= ampMult;
    //             normalise by maxNoiseVal at the end.
    static float TurbulencePattern(Vector2 px, TurbulenceSettings s)
    {
        Vector2 pNoise = px * s.frequency * 0.01f;
        float amplitude = 1f;
        float sum = 0f;
        float maxAmp = 0f;

        for (int l = 0; l < (int)s.numLayers; l++)
        {
            // fabs(2*noise - 1): convert [0,1] → signed [-1,1] then abs → [0,1] bumps
            float n = Mathf.Abs(2f * ValueNoise(pNoise) - 1f);
            sum += n * amplitude;
            maxAmp += amplitude;
            pNoise *= s.frequencyMult;
            amplitude *= s.amplitudeMult;
        }

        float divisor = s.maxNoiseVal > 0f ? s.maxNoiseVal : maxAmp;
        return divisor > 0f ? sum / divisor : sum;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  MATH UTILS
    // ──────────────────────────────────────────────────────────────────────────

    // Deterministic pseudo-random [0,1) for integer lattice point
    static float Rand(Vector2 p)
    {
        float n = Mathf.Sin(p.x * 127.1f + p.y * 311.7f +
                            p.x * 269.5f * 0.3f + p.y * 183.3f * 0.7f) * 43758.5453f;
        return n - Mathf.Floor(n);
    }

    static Vector2 Floor2(Vector2 v) =>
        new Vector2(Mathf.Floor(v.x), Mathf.Floor(v.y));
}