using System.Collections.Generic;
using UnityEngine;

namespace LevelGenerator.Data
{
    /// Delegate for weight calculation rules
    /// Returns a multiplier (0.5 = half weight, 2.0 = double weight, 0 = impossible)
    public delegate float WeightRule(PlacementContext context, PrefabDef candidate);
    /// Modular weight calculation system that uses rules to score placements
    public class WeightSystem
    {
        private List<WeightRule> rules = new List<WeightRule>();
        private WeightRulesConfig config;

        public WeightSystem(WeightRulesConfig config)
        {
            this.config = config;
            InitializeDefaultRules();
        }

 
        /// Add a custom weight rule
        public void AddRule(WeightRule rule)
        {
            rules.Add(rule);
        }

   
        /// Calculate final weight for a candidate at a given context
        public float CalculateWeight(PlacementContext context, PrefabDef candidate)
        {
            if (candidate == null) return 0f;

            float weight = candidate.BaseWeight;

            // Apply all rules
            foreach (var rule in rules)
            {
                float modifier = rule(context, candidate);
                weight *= modifier;
                
                // Early exit if weight becomes 0
                if (weight <= 0f) return 0f;
            }

            return Mathf.Max(0f, weight);
        }


        /// Select a weighted random item from candidates using the weight system
        public PrefabDef SelectWeighted(List<PrefabDef> candidates, PlacementContext context, System.Random rng)
        {
            if (candidates == null || candidates.Count == 0) return null;

            float totalWeight = 0f;
            float[] weights = new float[candidates.Count];

            // Calculate weights for all candidates
            for (int i = 0; i < candidates.Count; i++)
            {
                weights[i] = CalculateWeight(context, candidates[i]);
                totalWeight += weights[i];
            }

            if (totalWeight <= 0f) return null;

            // Weighted random selection
            float r = (float)rng.NextDouble() * totalWeight;
            float current = 0f;

            for (int i = 0; i < candidates.Count; i++)
            {
                current += weights[i];
                if (r <= current) return candidates[i];
            }

            return candidates[candidates.Count - 1];
        }

        /// Initialize default rule set

        private void InitializeDefaultRules()
        {
            // === ATTRIBUTE-BASED RULES ===

            // Physical objects less likely at edges (they block movement)
            AddRule((context, candidate) =>
            {
                if (candidate.HasAttribute(ObjectAttributes.Physical) && context.IsEdgeLane)
                    return config.edgePenaltyPhysical;
                return 1.0f;
            });

            // Hazardous items need spacing from other hazards
            AddRule((context, candidate) =>
            {
                if (candidate.HasAttribute(ObjectAttributes.Hazardous))
                {
                    int nearbyHazards = CountNearbyWithAttribute(context, ObjectAttributes.Hazardous, config.hazardCheckRadius);
                    if (nearbyHazards > 0)
                        return Mathf.Pow(config.hazardSpacingPenalty, nearbyHazards);
                }
                return 1.0f;
            });

            // Floating objects can spawn over holes
            AddRule((context, candidate) =>
            {
                if (context.currentSurface == SurfaceType.Hole)
                {
                    if (!candidate.HasAttribute(ObjectAttributes.Floating))
                        return 0f; // Non-floating objects can't spawn over holes
                }
                return 1.0f;
            });

            // === CONTEXT-BASED RULES ===

            // Edge proximity - most objects avoid edges
            AddRule((context, candidate) =>
            {
                if (context.IsEdgeLane)
                    return config.edgeProximityPenalty;
                return 1.0f;
            });

            // Neighbor density - reduce spawning in dense areas
            AddRule((context, candidate) =>
            {
                int density = CountNearbyOccupants(context, config.densityCheckRadius);
                if (density > 0)
                    return 1.0f - (density * config.densityPenalty);
                return 1.0f;
            });

            // Golden path proximity for collectibles
            AddRule((context, candidate) =>
            {
                if (candidate.OccupantType == OccupantType.Collectible)
                {
                    int dist = context.DistanceToGoldenPath;
                    if (dist == 0) return config.collectibleOnPathBoost;
                    if (dist == 1) return config.collectibleNearPathBoost;
                }
                return 1.0f;
            });

            // === TYPE-SPECIFIC RULES ===

            // Walls favor edges
            AddRule((context, candidate) =>
            {
                if (candidate.OccupantType == OccupantType.Wall)
                {
                    if (context.IsEdgeLane) return config.wallEdgeBoost;
                }
                return 1.0f;
            });

            // Obstacles favor center lanes
            AddRule((context, candidate) =>
            {
                if (candidate.OccupantType == OccupantType.Obstacle)
                {
                    if (context.IsCenterLane) return config.obstacleCenterBoost;
                    if (context.IsEdgeLane) return config.obstacleEdgePenalty;
                }
                return 1.0f;
            });

            // Enemies near (but not on) golden path
            AddRule((context, candidate) =>
            {
                if (candidate.OccupantType == OccupantType.Enemy)
                {
                    int dist = context.DistanceToGoldenPath;
                    if (dist == 0) return config.enemyOnPathPenalty;
                    if (dist == 1) return config.enemyNearPathBoost;
                }
                return 1.0f;
            });
        }

        // === HELPER METHODS ===

        private int CountNearbyOccupants(PlacementContext context, int radius)
        {
            int count = 0;
            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dlane = -radius; dlane <= radius; dlane++)
                {
                    if (dz == 0 && dlane == 0) continue;
                    
                    int checkZ = context.position.z + dz;
                    int checkLane = context.position.lane + dlane;
                    
                    if (checkLane < 0 || checkLane >= context.laneCount) continue;
                    
                    var cell = context.GetCell(checkZ, checkLane);
                    if (cell.occupant != OccupantType.None)
                        count++;
                }
            }
            return count;
        }

        private int CountNearbyWithAttribute(PlacementContext context, ObjectAttributes attr, int radius)
        {
            int count = 0;
            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dlane = -radius; dlane <= radius; dlane++)
                {
                    if (dz == 0 && dlane == 0) continue;
                    
                    int checkZ = context.position.z + dz;
                    int checkLane = context.position.lane + dlane;
                    
                    if (checkLane < 0 || checkLane >= context.laneCount) continue;
                    
                    var cell = context.GetCell(checkZ, checkLane);

                    // Check Occupant Attributes first (primary concern)
                    if (cell.occupantDef != null)
                    {
                        if ((cell.occupantDef.Attributes & attr) == attr)
                            count++;
                    }
                    // Check Surface Attributes (secondary, e.g. Hazardous floor)
                    else if (cell.surfaceDef != null)
                    {
                        if ((cell.surfaceDef.Attributes & attr) == attr)
                            count++;
                    }
                    else
                    {
                        // Fallback
                        if (attr == ObjectAttributes.Hazardous)
                        {
                             if (cell.occupant == OccupantType.Enemy || cell.occupant == OccupantType.Obstacle)
                                count++;
                        }
                    }
                }
            }
            return count;
        }
    }
}
