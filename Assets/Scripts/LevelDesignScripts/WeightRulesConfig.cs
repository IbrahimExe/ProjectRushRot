using System.Collections.Generic;
using UnityEngine;
using LevelGenerator.Data;

/// Weight System Configuration
/// This is a ScriptableObject that can be created as an asset
[CreateAssetMenu(fileName = "WeightRulesConfig", menuName = "Runner/Weight Rules Config")]
public class WeightRulesConfig : ScriptableObject
{
    [Header("Weight System Settings")]
    [Tooltip("Penalty for physical objects at edge lanes (0-1)")]
    [Range(0f, 1f)] public float edgePenaltyPhysical = 0.3f;

    [Tooltip("General edge proximity penalty for most objects (0-1)")]
    [Range(0f, 1f)] public float edgeProximityPenalty = 0.7f;

    [Tooltip("Penalty per nearby hazard (0-1)")]
    [Range(0f, 1f)] public float hazardSpacingPenalty = 0.5f;
    public int hazardCheckRadius = 2;

    [Tooltip("Penalty per nearby occupant for density")]
    [Range(0f, 0.5f)] public float densityPenalty = 0.1f;
    public int densityCheckRadius = 2;

    [Tooltip("Boost for collectibles on golden path")]
    [Range(1f, 3f)] public float collectibleOnPathBoost = 2.5f;
    [Tooltip("Boost for collectibles near golden path")]
    [Range(1f, 2f)] public float collectibleNearPathBoost = 1.5f;

    [Tooltip("Boost for walls at edges")]
    [Range(1f, 3f)] public float wallEdgeBoost = 2.0f;

    [Tooltip("Boost for obstacles in center")]
    [Range(1f, 2f)] public float obstacleCenterBoost = 1.5f;
    [Tooltip("Penalty for obstacles at edges")]
    [Range(0f, 1f)] public float obstacleEdgePenalty = 0.5f;

    [Tooltip("Penalty for enemies on golden path")]
    [Range(0f, 0.5f)] public float enemyOnPathPenalty = 0.1f;
    [Tooltip("Boost for enemies near golden path")]
    [Range(1f, 2f)] public float enemyNearPathBoost = 1.3f;
}
