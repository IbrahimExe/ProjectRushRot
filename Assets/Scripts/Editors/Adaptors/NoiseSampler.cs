using UnityEngine;

// Noise sampler following the scratchapixel.com reference exactly.
//
// COORDINATE SYSTEM
// -----------------
// uv [0,1] → pixel coords px = uv * resolution
// Space frequency then scales px → sample point p used by base noise.
//
// Patterns (Wood, Marble, Turbulence) work exactly as the reference:
//   pNoise = Vec2f(i, j) * frequency   ← their OWN frequency on raw pixel coords
// They fully REPLACE the base noise when enabled (they are not additive tweaks).
// Quantization post-processes whatever value came before it.
//
// BASE NOISE TYPES
// ----------------
//   Default — 5-layer fBm over Value noise.  Cloud/terrain.
//   Value   — Hermite-interpolated lattice scalars.  Blocky/pillowy.
//   Perlin  — Gradient noise (dot product with random unit vectors).  Smooth/organic.
//   Worley  — 1 - nearest feature-point distance.  Crystal/cell/stone.

public static class NoiseSampler
{
    // ─── Entry points ──────────────────────────────────────────────────────────

    // Editor preview: uv [0,1] x resolution → pixel coords → space → noise
    public static float Sample(NoiseConfig cfg, Vector2 uv, int resolution)
    {
        Vector2 px = uv * resolution;
        return SamplePx(cfg, px);
    }

    // Runtime (RunnerLevelGenerator): world XZ treated as pixel coords directly.
    // Seamless across chunks — no resolution dependency.
    public static float SampleWorld(NoiseConfig cfg, Vector2 worldPos)
        => SamplePx(cfg, worldPos);

    static float SamplePx(NoiseConfig cfg, Vector2 px)
    {
        

        // Space-scaled coordinate — used by base noise and domain warp
        Vector2 p = ApplySpace(cfg, px);

        // Domain warp displaces p before base noise is evaluated.
        // Patterns (Wood/Marble/Turbulence) bypass this — they sample px directly
        // with their own frequency and would look wrong if warped.
        if (cfg.warp.enabled && cfg.warp.level > 0)
            p = WarpDomain(cfg.warp, p);

        // Base noise on the (possibly warped) space coordinate
        // pass directly instead of thread-locals
        float n = BaseNoise(cfg.noiseType, p, cfg);

        // Patterns blend into the result via blendWeight.
        // blendWeight = 1 fully overwrites, 0 = no effect, in-between mixes.
        if (cfg.wood.enabled)
            n = Mathf.Lerp(n, WoodPattern(px, cfg.wood, cfg.noiseType, cfg), cfg.wood.blendWeight);

        if (cfg.marble.enabled)
            n = Mathf.Lerp(n, MarblePattern(px, cfg.marble, cfg.noiseType, cfg), cfg.marble.blendWeight);

        if (cfg.turbulence.enabled)
            n = Mathf.Lerp(n, TurbulencePattern(px, cfg.turbulence, cfg.noiseType, cfg), cfg.turbulence.blendWeight);

        // Invert: lerp toward (1 - n)
        if (cfg.invert.enabled)
            n = Mathf.Lerp(n, 1f - n, cfg.invert.blendWeight);

        // Quantization always post-processes (it doesn't replace, it posterises)
        if (cfg.quantization.enabled && cfg.quantization.totalChannels >= 2)
            n = Mathf.Floor(n * cfg.quantization.totalChannels)
                / (cfg.quantization.totalChannels - 1f);

        return Mathf.Clamp01(n);
    }

    // ─── Domain Warp  (Quilez, https://iquilezles.org/articles/warp/) ──────────
    //
    // The key insight: to displace a 2D point we need a 2D offset vector.
    // We synthesise that vector from TWO separate scalar fbm calls, using slightly
    // different input offsets so their outputs are uncorrelated (they point in
    // different "random" directions rather than always along the same axis).
    //
    // Level 1:
    //   q = ( fbm(p + offsetQ0),  fbm(p + offsetQ1) )
    //   warped_p = p + strength * q
    //
    // Level 2: adds a second warp built from the already-warped coordinate:
    //   r = ( fbm(p + strength*q + offsetR0),  fbm(p + strength*q + offsetR1) )
    //   warped_p = p + strength * r
    //
    // The warp fbm always uses FbmNoise (the Default type) regardless of the
    // config's noiseType, because the warp field is a displacement function —
    // using Worley or Perlin here would produce a different character of warp
    // but the displacement magnitudes would be inconsistent. FbmNoise gives
    // smooth, continuous displacements that work well as a flow field.
    // (Advanced: could expose warpNoiseType as a separate field if desired.)

    static Vector2 WarpDomain(WarpSettings w, Vector2 p)
    {
        // First warp pass — build displacement field q from p
        Vector2 q = new Vector2(
            FbmNoise(p + w.offsetQ0),
            FbmNoise(p + w.offsetQ1));

        if (w.level == 1)
            return p + w.strength * q;

        // Second warp pass — build displacement field r from (p + strength*q)
        Vector2 pq = p + w.strength * q;
        Vector2 r = new Vector2(
            FbmNoise(pq + w.offsetR0),
            FbmNoise(pq + w.offsetR1));

        return p + w.strength * r;
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

    static float BaseNoise(NoiseType type, Vector2 p, NoiseConfig cfg)
    {
        switch (type)
        {
            case NoiseType.Value: return ValueNoise(p);
            case NoiseType.Perlin: return PerlinNoise(p);
            case NoiseType.Worley: return WorleyNoise(p);
            case NoiseType.Ridged: return RidgedNoise(p, cfg.ridged);
            case NoiseType.SparseDot: return SparseDotNoise(p, cfg.sparseDot);
            default: return FbmNoise(p);
        }
    }

    // Thread-local config references set by SamplePx before any BaseNoise call.
    [System.ThreadStatic] static RidgedSettings _ridged;
    [System.ThreadStatic] static SparseDotSettings _sparseDot;

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

    // Ridged multifractal noise
    // Reference: Musgrave 1994 / the standard "ridged" variation seen in
    //   shadertoy, GPU Gems, and threejsroadmap.com/blog/10-noise-functions-for-threejs-tsl-shaders
    //
    // Core idea: each octave's noise is folded by  n = 1 - abs(noise)
    // This turns smooth zero-crossings of gradient noise into sharp peaks.
    // Squaring (n = n*n) makes peaks thinner and knife-like.
    //
    // ridgeInfluence (Musgrave's "weight/signal" term):
    //   Each octave's amplitude is multiplied by the previous octave's ridge value.
    //   At 0 this degrades to plain ridged fBm. At 1, detail is concentrated on the
    //   ridges — valleys go quiet, mountain peaks accumulate fine structure.
    //   Try 0.7 for realistic mountain ranges.
    //
    // Uses PerlinNoise internally — gradient noise has signed zero-crossings that
    // produce clean, evenly-spaced ridges. ValueNoise also works but gives blobby
    // humps rather than sharp edges.
    static float RidgedNoise(Vector2 p, RidgedSettings s)
    {
        float sum = 0f;
        float amp = 0.5f;
        float maxAmp = 0f;
        float weight = 1f;         // Musgrave signal term, starts at 1
        Vector2 q = p;
        int layers = Mathf.Max(1, s.octaves);

        for (int i = 0; i < layers; i++)
        {
            // Sample gradient noise in [-0.5, 0.5] (PerlinNoise returns [0,1], centre it)
            float raw = PerlinNoise(q) * 2f - 1f;  // → approx [-1, 1]

            // Ridge fold: invert absolute value so zero-crossings become peaks
            float n = 1f - Mathf.Abs(raw);         // → [0, 1], peaks at 0-crossings

            // Optional squaring — sharpens ridges from broad hills to knife edges
            if (s.squaredRidges) n *= n;            // → [0, 1], sharper peaks

            // Musgrave weighting: amplitude for this octave scales with previous ridge
            float effectiveAmp = amp * weight;
            sum += n * effectiveAmp;
            maxAmp += effectiveAmp;

            // Update signal weight: lerp between 1 (no influence) and n (full influence)
            weight = Mathf.Lerp(1f, n, s.ridgeInfluence);
            weight = Mathf.Clamp01(weight);

            q *= s.lacunarity;
            amp *= s.gain;
        }

        return maxAmp > 0f ? sum / maxAmp : 0f;
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

    // Sparse dot noise
    // Reference: Thorsten Renk — science-and-fiction.org/rendering/noise.html
    //
    // Each cell either has a dot (gated by density) or is empty.
    // The dot's centre is constrained so it can never reach the cell boundary,
    // guaranteeing no overlap between adjacent cells (the "sparse" assumption).
    // The dot profile is a smooth falloff: 1 at centre, 0 at radius edge.
    //
    // p is the space-scaled coordinate — frequency is controlled by the Space
    // settings in NoiseConfig (tilingFrequency / Custom mode), same as every
    // other noise type. Higher frequency = more, smaller cells = denser dots.
    static float SparseDotNoise(Vector2 p, SparseDotSettings s)
    {
        Vector2 i = Floor2(p);          // cell origin (integer part)
        Vector2 f = p - i;              // position within cell (fractional part)

        // Gate: most cells are empty. rand(i + (1,1)) is a stable per-cell coin flip.
        // Using the diagonal corner as the seed avoids correlation with the offset seeds below.
        if (Rand(i + new Vector2(1f, 1f)) > s.density)
            return 0f;

        // Jitter the dot centre within the cell.
        // xoffset/yoffset are in [-0.5, 0.5] so truePos starts near cell centre (0.5, 0.5).
        float xoffset = Rand(i) - 0.5f;
        float yoffset = Rand(i + new Vector2(1f, 0f)) - 0.5f;

        // Randomise dot size per cell. max(0.25, ...) ensures a minimum visible radius.
        // The 0.5 factor keeps actual radius <= 0.5 * maxDotSize, leaving room to
        // shrink the position range so the dot can never overlap the boundary.
        float dotSize = 0.5f * s.maxDotSize
                      * Mathf.Max(0.25f, Rand(i + new Vector2(0f, 1f)));

        // Constrain centre: the (1 - 2*dotSize) factor shrinks the jitter range
        // so the dot edge (centre ± dotSize) stays strictly inside [0, 1].
        Vector2 truePos = new Vector2(
            0.5f + xoffset * (1f - 2f * dotSize),
            0.5f + yoffset * (1f - 2f * dotSize));

        // Smooth radial falloff: 1 at centre, 0 at dotSize, hard 0 beyond.
        // The inner edge (0.3 * dotSize) gives a flat bright core like a real droplet.
        float dist = Vector2.Distance(truePos, f);
        return 1f - Mathf.SmoothStep(0.3f * dotSize, dotSize, dist);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  PATTERNS  —  scratchapixel.com reference, verbatim
    //  Input: raw pixel coords `px`  +  the pattern's own frequency setting.
    //  Each pattern IS its own noise function, not a post-process on base noise.
    // ──────────────────────────────────────────────────────────────────────────

    // WOOD
    // Reference:  g = noise(Vec2f(i,j) * frequency) * multiplier;
    //             result = g - (int)g;      // i.e. frac(g)
    static float WoodPattern(Vector2 px, WoodSettings s, NoiseType noiseType, NoiseConfig cfg)
    {
        Vector2 pNoise = px * s.frequency * 0.01f;
        float g = BaseNoise(noiseType, pNoise, cfg) * s.multiplier;
        return g - Mathf.Floor(g);
    }

    // MARBLE
    // Reference:  pNoise = Vec2f(i,j) * frequency;
    //             for layers: noiseValue += noise(pNoise) * amplitude; pNoise *= freqMult; amp *= ampMult;
    //             result = (sin( (i + j + noiseValue*100) * 2π/200 + phase ) + 1) / 2
    // px.x + px.y gives diagonal veins; noiseValue perturbs the phase of those stripes.
    static float MarblePattern(Vector2 px, MarbleSettings s, NoiseType noiseType, NoiseConfig cfg)
    {
        Vector2 pNoise = px * s.frequency * 0.01f;
        float amplitude = 1f;
        float noiseVal = 0f;

        for (int l = 0; l < (int)s.numLayers; l++)
        {
            noiseVal += BaseNoise(noiseType, pNoise, cfg) * amplitude;
            pNoise *= s.frequencyMult;
            amplitude *= s.amplitudeMult;
        }

        float arg = (px.x + px.y + noiseVal * 100f) * (2f * Mathf.PI / 200f) + s.phase;
        return (Mathf.Sin(arg) + 1f) * 0.5f;
    }

    // TURBULENCE
    // Reference:  pNoise = Vec2f(i,j) * frequency;
    //             for layers: noiseMap += fabs(2*noise(pNoise) - 1) * amplitude;
    //             pNoise *= freqMult; amp *= ampMult;
    //             normalise by maxNoiseVal at the end.
    static float TurbulencePattern(Vector2 px, TurbulenceSettings s, NoiseType noiseType, NoiseConfig cfg)
    {
        Vector2 pNoise = px * s.frequency * 0.01f;
        float amplitude = 1f;
        float sum = 0f;
        float maxAmp = 0f;

        for (int l = 0; l < (int)s.numLayers; l++)
        {
            float n = Mathf.Abs(2f * BaseNoise(noiseType, pNoise, cfg) - 1f);
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
                    p.x * 269.5f * 0.3f + p.y * 183.3f * 0.7f + 1.4f) * 43758.5453f;
        return n - Mathf.Floor(n);
    }

    static Vector2 Floor2(Vector2 v) =>
        new Vector2(Mathf.Floor(v.x), Mathf.Floor(v.y));
}