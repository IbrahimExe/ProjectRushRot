using System;
using UnityEngine;

[CreateAssetMenu(fileName = "NoiseConfig", menuName = "Noise/Noise Config")]
public class NoiseConfig : ScriptableObject
{
    [Header("Noise Type")]
    public NoiseType noiseType = NoiseType.Default;

    [Header("Space")]
    public SpaceMode spaceMode = SpaceMode.Tiling;

    [Tooltip("Tiling mode: one frequency scales both axes uniformly.")]
    public float tilingFrequency = 3f;

    [Tooltip("Custom mode: independent frequency/offset per axis.")]
    public SpaceChannel position = new SpaceChannel { value = new Vector3(3f, 3f, 0f) };
    public SpaceChannel rotation = new SpaceChannel { value = Vector3.zero };
    public SpaceChannel scale = new SpaceChannel { value = Vector3.one };

    [Header("Custom Patterns")]
    public WoodSettings wood;
    public QuantizationSettings quantization;
    public MarbleSettings marble;
    public TurbulenceSettings turbulence;
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
    Worley
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
    public float frequency;
    public bool enabled;
    [Tooltip("Grain ring density — higher = more rings. Try 8–20.")]
    public float multiplier;
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
}