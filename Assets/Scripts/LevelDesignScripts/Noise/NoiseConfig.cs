using System;
using UnityEngine;

[CreateAssetMenu(fileName = "NoiseConfig", menuName = "Noise/Noise Config")]
public class NoiseConfig : ScriptableObject
{
    [Header("Noise Type")]
    public NoiseType noiseType = NoiseType.Default;

    [Header("Ridged Settings")]
    [Tooltip("Only used when Noise Type is Ridged.")]
    public RidgedSettings ridged = RidgedSettings.Defaults();

    [Header("Sparse Dot Settings")]
    [Tooltip("Only used when Noise Type is Sparse Dot.")]
    public SparseDotSettings sparseDot = SparseDotSettings.Defaults();

    [Header("Space")]
    public SpaceMode spaceMode = SpaceMode.Tiling;

    [Tooltip("Tiling mode: one frequency scales both axes uniformly.")]
    public float tilingFrequency = 3f;

    [Tooltip("Custom mode: independent frequency/offset per axis.")]
    public SpaceChannel position = new SpaceChannel { value = new Vector3(3f, 3f, 0f) };
    public SpaceChannel rotation = new SpaceChannel { value = Vector3.zero };
    public SpaceChannel scale = new SpaceChannel { value = Vector3.one };

    [Header("Domain Warp")]
    public WarpSettings warp;

    [Header("Custom Patterns")]
    public WoodSettings wood;
    public QuantizationSettings quantization;
    public MarbleSettings marble;
    public TurbulenceSettings turbulence;
    public InvertSettings invert;
}

// ─── Enums ────────────────────────────────────────────────────────────────────

public enum NoiseType
{
    /// <summary>Fractal Brownian Motion — natural cloud/terrain. Good default.</summary>
    Default,
    /// <summary>Interpolated random scalars at lattice points — blocky/pillowy cells.</summary>
    Value,
    /// <summary>True gradient noise — smooth, organic, no grid artefacts.</summary>
    Perlin,
    /// <summary>Cellular/Worley — crystal, stone, cell membrane pattern.</summary>
    Worley,
    /// <summary>Ridged multifractal — sharp mountain ridges, lightning, cracks.</summary>
    Ridged,
    /// <summary>Sparse dot — rare, randomly sized and placed soft dots. Rain, pores, spots.</summary>
    SparseDot
}

public enum SpaceMode
{
    /// <summary>Single frequency slider. Both axes scale uniformly. Easy tiling.</summary>
    Tiling,
    /// <summary>Full control: Position (frequency), Rotation (offset), Scale per axis.</summary>
    Custom
}

// ─── Space ────────────────────────────────────────────────────────────────────

[Serializable]
public struct SpaceChannel
{
    [Tooltip("XY = 2D frequency / offset. Z reserved for 3D.")]
    public Vector3 value;
}

// ─── Pattern structs ──────────────────────────────────────────────────────────

[Serializable]
public struct WoodSettings
{
    public bool enabled;
    [Tooltip("Base sampling frequency. Controls zoom of the underlying noise. Try 0.5–5.")]
    public float frequency;
    [Tooltip("Grain ring density — higher = more rings. Try 8–20.")]
    public float multiplier;
    [Range(0f, 1f), Tooltip("0 = no effect, 1 = fully overwrites previous layers.")]
    public float blendWeight;
}

[Serializable]
public struct QuantizationSettings
{
    public bool enabled;
    [Min(2), Tooltip("Number of discrete posterisation bands.")]
    public int totalChannels;
}

[Serializable]
public struct MarbleSettings
{
    public bool enabled;
    [Tooltip("Phase offset of the stripe sine wave (radians).")]
    public float phase;
    [Tooltip("Base sampling frequency. Try 0.01–0.05.")]
    public float frequency;
    [Tooltip("Lacunarity — frequency multiplier per layer. Typically 1.8–2.0.")]
    public float frequencyMult;
    [Tooltip("Gain — amplitude multiplier per layer. Typically 0.35–0.5.")]
    public float amplitudeMult;
    [Min(1)] public uint numLayers;
    [Range(0f, 1f), Tooltip("0 = no effect, 1 = fully overwrites previous layers.")]
    public float blendWeight;
}

[Serializable]
public struct TurbulenceSettings
{
    public bool enabled;
    [Tooltip("Base sampling frequency. Try 0.01–0.05.")]
    public float frequency;
    [Tooltip("Lacunarity — frequency multiplier per layer. Typically 1.8–2.0.")]
    public float frequencyMult;
    [Tooltip("Gain — amplitude multiplier per layer. Typically 0.35–0.5.")]
    public float amplitudeMult;
    [Min(1)] public uint numLayers;
    [Tooltip("Divide final sum by this to normalise. 0 = automatic.")]
    public float maxNoiseVal;
    [Range(0f, 1f), Tooltip("0 = no effect, 1 = fully overwrites previous layers.")]
    public float blendWeight;
}

[Serializable]
public struct InvertSettings
{
    public bool enabled;
    [Range(0f, 1f), Tooltip("0 = no effect, 1 = full invert (1 - n). In-between blends toward the inverted value.")]
    public float blendWeight;
}

// ─── Domain Warp ──────────────────────────────────────────────────────────────
//
// Quilez two-pass warping:  https://iquilezles.org/articles/warp/
//
//   Level 0: f(p)                        — no warp, plain noise
//   Level 1: q = vec2(fbm(p+dq0), fbm(p+dq1))
//            f(p + strength * q)
//   Level 2: same q, then
//            r = vec2(fbm(p+strength*q+dr0), fbm(p+strength*q+dr1))
//            f(p + strength * r)
//
// The offset vectors (dq0/dq1/dr0/dr1) decorrelate the two scalar fbm calls
// that form each 2D displacement field so they don't point in the same direction.
// Quilez's paper uses (0,0),(5.2,1.3),(1.7,9.2),(8.3,2.8) — those are the defaults.
//
[Serializable]
public struct WarpSettings
{
    public bool enabled;

    [Range(0, 2), Tooltip(
        "0 = no warp.\n" +
        "1 = one warp pass: domain is displaced by fbm(p).\n" +
        "2 = two warp passes: domain is displaced by fbm(fbm(p)) — more convoluted.")]
    public int level;

    [Tooltip("How far the domain is displaced. Quilez uses 4.0. " +
             "Try 2–8 for interesting results.")]
    public float strength;

    [Header("Decorrelation Offsets (Quilez defaults work well — change for variety)")]
    [Tooltip("Offset added to p when sampling the X component of the first warp field q.")]
    public Vector2 offsetQ0;   // Quilez: (0.0, 0.0)
    [Tooltip("Offset added to p when sampling the Y component of the first warp field q.")]
    public Vector2 offsetQ1;   // Quilez: (5.2, 1.3)
    [Tooltip("Offset added when sampling the X component of the second warp field r.")]
    public Vector2 offsetR0;   // Quilez: (1.7, 9.2)
    [Tooltip("Offset added when sampling the Y component of the second warp field r.")]
    public Vector2 offsetR1;   // Quilez: (8.3, 2.8)

    // Returns a copy with Quilez's original offsets filled in.
    // Called by the editor Reset button.
    public static WarpSettings Defaults() => new WarpSettings
    {
        enabled = false,
        level = 1,
        strength = 4f,
        offsetQ0 = new Vector2(0.0f, 0.0f),
        offsetQ1 = new Vector2(5.2f, 1.3f),
        offsetR0 = new Vector2(1.7f, 9.2f),
        offsetR1 = new Vector2(8.3f, 2.8f),
    };
}

// ─── Ridged Noise ─────────────────────────────────────────────────────────────
//
// Ridged multifractal noise: per-octave transform is  n = 1 - abs(noise)
// which flips the signed zero-crossings of gradient noise into sharp peaks.
// Optionally squared (n = n*n) to make ridges thinner and more knife-like.
//
// Unlike fBm's additive accumulation, ridged noise can also weight each octave
// by the previous octave's value (the "weight" or "signal" term from Musgrave's
// original paper) — this makes high-frequency detail appear only on the ridges
// themselves, not in the valleys. Controlled by ridgeInfluence below.
//
[Serializable]
public struct RidgedSettings
{
    [Range(1, 8), Tooltip("Number of fBm octaves. More = finer ridge detail.")]
    public int octaves;

    [Range(1.01f, 4f), Tooltip("Frequency multiplier per octave. 2.0 = each layer twice as dense.")]
    public float lacunarity;

    [Range(0.1f, 0.9f), Tooltip("Amplitude multiplier per octave. 0.5 = each layer half as loud.")]
    public float gain;

    [Tooltip("Squared ridge sharpening: if true, n = n*n after the 1-abs() step. " +
             "Produces thin knife-like ridges. If false, ridges are broader and softer.")]
    public bool squaredRidges;

    [Range(0f, 1f), Tooltip(
        "How much the previous octave's ridge value weights the next octave's amplitude " +
        "(Musgrave signal term). 0 = plain ridged fBm. 1 = full weighting — detail " +
        "concentrates on ridges, valleys go smooth. Try 0.7 for mountain ranges.")]
    public float ridgeInfluence;

    public static RidgedSettings Defaults() => new RidgedSettings
    {
        octaves = 5,
        lacunarity = 2.0f,
        gain = 0.5f,
        squaredRidges = true,
        ridgeInfluence = 0.7f,
    };
}

// ─── Sparse Dot Noise ─────────────────────────────────────────────────────────
//
// Reference: Thorsten Renk — science-and-fiction.org/rendering/noise.html
//
// Rare, randomly sized soft dots distributed irregularly at roughly one dot per
// cell. "Sparse" means the density is low enough that dots never overlap —
// the algorithm enforces this by constraining each dot's centre to stay at
// least dotSize away from every cell boundary.
//
// Algorithm per cell:
//   1. Gate: rand(cell+diag) > density  →  return 0  (most cells are empty)
//   2. xoffset = rand(cell.x,   cell.y  ) - 0.5      (position jitter)
//      yoffset = rand(cell.x+1, cell.y  ) - 0.5
//   3. dotSize = 0.5 * maxDotSize * max(0.25, rand(cell.x, cell.y+1))
//   4. truePos = (0.5 + xoffset*(1-2*dotSize),        (constrained to avoid overlap)
//                 0.5 + yoffset*(1-2*dotSize))
//   5. return 1 - smoothstep(0.3*dotSize, dotSize, length(truePos - frac))
//
[Serializable]
public struct SparseDotSettings
{
    [Range(0f, 1f), Tooltip(
        "Probability that any given cell contains a dot. " +
        "Keep low (0.1–0.4) for sparse rain/pore look. " +
        "Higher values produce denser clusters but may look clumped.")]
    public float density;

    [Range(0.01f, 1f), Tooltip(
        "Maximum dot radius as a fraction of cell size. " +
        "0.1 = tiny specks, 0.5 = large blobs that nearly fill the cell. " +
        "Actual radius is randomised per dot: min is 0.25× this value.")]
    public float maxDotSize;

    public static SparseDotSettings Defaults() => new SparseDotSettings
    {
        density = 0.3f,
        maxDotSize = 0.4f,
    };
}