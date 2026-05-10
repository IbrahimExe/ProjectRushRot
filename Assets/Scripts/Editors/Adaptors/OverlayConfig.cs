using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "OverlayConfig", menuName = "Runner/Overlay Config")]
public class OverlayConfig : ScriptableObject
{
    public List<OverlayDef> Overlays = new List<OverlayDef>();
}

public enum OverlayType
{
    Island,
    Equator,
    Meridian
}

[System.Serializable]
public class OverlayDef
{
    public string Name = "New Overlay";
    public bool Enabled = true;
    public OverlayType Type = OverlayType.Island;

    [Tooltip("Inverts the pattern — island becomes lake, equator becomes river.")]
    public bool GenInvert = false;

    public AnimationCurve FalloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Tooltip("Island centre X in world space.")]
    public float CentreX = 0f;

    [Tooltip("Island centre Z in world space.")]
    public float CentreZ = 0f;

    [Tooltip("World-space offset of the line (Equator/Meridian only).")]
    public float WorldOffset = 0f;

    [Tooltip("Radius for Island, width for Equator/Meridian in world units.")]
    public float Scale = 1000f;

    [Tooltip("How strongly Equator/Meridian nudges the noise. Ignored for Island.")]
    [Range(0f, 1f)]
    public float Strength = 1f;

    [Tooltip("Noise value outside the overlay area. 0 = sea, 1 = mountains.")]
    public float FloorValue = 0f;
}