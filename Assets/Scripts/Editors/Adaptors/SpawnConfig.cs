using System.Collections.Generic;
using UnityEngine;

public enum PlacementMode
{
    PoissonDisk,  // Organic, naturally spaced — good for trees, rocks, enemies
    GridJitter    // Grid with random offset — good for obstacles, collectables
}

[System.Serializable]
public class SpawnRule
{
    [Tooltip("Must match a PrefabDef ID in the loaded PrefabCatalog.")]
    public string PrefabDefID;

    public PlacementMode PlacementMode = PlacementMode.PoissonDisk;

    [Tooltip("Multiplier on the global Density. 1 = base rate, 2 = twice as dense.")]
    [Range(0.01f, 10f)]
    public float DensityMultiplier = 1f;

    [Tooltip("Minimum world-unit distance between instances of this rule.")]
    public float MinSpacing = 5f;

    [Tooltip("Normalized noise height (0 to 1). Match this to your TerrainConfig Region Heights.")]
    [Range(0f, 1f)]
    public float HeightMin = 0f;
    [Range(0f, 1f)]
    public float HeightMax = 1f;

    [Tooltip("Maximum slope in degrees. 0 = flat only, 90 = any angle.")]
    [Range(0f, 90f)]
    public float MaxSlope = 30f;

    [Tooltip("Categories this rule cannot overlap with. Empty = overlaps everything.")]
    public List<PrefabCategory> BlockedBy = new List<PrefabCategory>();
}

[CreateAssetMenu(fileName = "SpawnConfig", menuName = "Runner/Spawn Config")]
public class SpawnConfig : ScriptableObject
{
    [Header("Global Distances (world units)")]
    [Tooltip("Within this distance, full prefabs are active.")]
    public float FullDistance = 200f;

    [Tooltip("Within this distance, simulation (physics, AI) is active. Must be <= FullDistance.")]
    public float SimDistance = 100f;

    [Tooltip("Beyond FullDistance and within this, stand-in imposters are shown.")]
    public float StandInDistance = 400f;

    [Header("Global Density")]
    [Tooltip("Base objects per 100 square world units. Scaled per rule by DensityMultiplier.")]
    public float Density = 1f;

    [Header("Rules")]
    public List<SpawnRule> Rules = new List<SpawnRule>();

    void OnValidate()
    {
        SimDistance    = Mathf.Min(SimDistance,    FullDistance);
        FullDistance   = Mathf.Min(FullDistance,   StandInDistance);
    }
}
