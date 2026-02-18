using System.Collections.Generic;
using UnityEngine;

namespace LevelGenerator.Data
{
    public static class PathfindingHelper
    {
        public class Node
        {
            public int Z;
            public int Lane;
            public float G; // Cost from start
            public float H; // Heuristic to end
            public Node Parent;
            public float F => G + H;

            public Node(int z, int lane) { Z = z; Lane = lane; }
        }

        /// <summary>
        /// Finds a path from Start to End within the given grid/chunk bounds.
        /// Returns a list of (z, lane) coordinates.
        /// </summary>
        public static List<(int z, int lane)> FindPath(
            (int z, int lane) start, 
            (int z, int lane) end, 
            int minLane, int maxLane,
            System.Func<int, int, bool> isWalkable,
            float turnCost = 0f)
        {
            var openSet = new List<Node>();
            var closedSet = new HashSet<(int, int)>();
            var nodeIndex = new Dictionary<(int, int), Node>();

            Node startNode = new Node(start.z, start.lane);
            startNode.H = Manhattan(start, end);
            openSet.Add(startNode);
            nodeIndex[start] = startNode;

            while (openSet.Count > 0)
            {
                // 1) Pick node with lowest F (A*). Tie-break: lower H.
                Node current = openSet[0];
                for (int i = 1; i < openSet.Count; i++)
                {
                    Node cand = openSet[i];

                    // Tie-breaker reduces jitter when many nodes share same F
                    if (cand.F < current.F) current = cand;
                    else if (Mathf.Approximately(cand.F, current.F) && cand.H < current.H) current = cand;
                }

                if (current.Z == end.z && current.Lane == end.lane)
                {
                    return RetracePath(current);
                }

                openSet.Remove(current);
                closedSet.Add((current.Z, current.Lane));

                // 2. Neighbors (Forward, Left, Right) - No backwards
                List<(int z, int lane)> neighbors = new List<(int, int)>
                {
                    (current.Z + 1, current.Lane),     // Forward
                    (current.Z + 1, current.Lane - 1),     // Left
                    (current.Z + 1, current.Lane + 1)      // Right
                };

                foreach (var neighborPos in neighbors)
                {
                    if (closedSet.Contains(neighborPos)) continue;

                    // Bounds check
                    if (neighborPos.lane < minLane || neighborPos.lane > maxLane) continue;
                    if (neighborPos.z > end.z) continue; // Don't go past end Z

                    // Walkable Check
                    if (!isWalkable(neighborPos.z, neighborPos.lane)) continue;

                    float moveCost = (neighborPos.lane == current.Lane) ? 1f : 1.25f; // Side movement is slightly pricier, discourages diagonals (Might need tweaks)
                    float newG = current.G + moveCost;

                    // Turn Penalty? If we changed lane from parent's parent
                    if (current.Parent != null)
                    {
                        int prevDeltaLane = current.Lane - current.Parent.Lane;
                        int currDeltaLane = neighborPos.lane - current.Lane;
                        if (prevDeltaLane != currDeltaLane) newG += turnCost; // Penalize zig-zags
                    }

                    Node neighborNode;
                    if (!nodeIndex.TryGetValue(neighborPos, out neighborNode))
                    {
                        neighborNode = new Node(neighborPos.z, neighborPos.lane);
                        nodeIndex[neighborPos] = neighborNode;
                        openSet.Add(neighborNode);
                    }
                    else if (newG >= neighborNode.G)
                    {
                        continue; // Not a better path
                    }

                    neighborNode.G = newG;
                    neighborNode.H = Manhattan(neighborPos, end);
                    neighborNode.Parent = current;
                }
            }

            return null; // No path found
        }

        private static List<(int z, int lane)> RetracePath(Node endNode)
        {
            var path = new List<(int, int)>();
            Node current = endNode;
            while (current != null)
            {
                path.Add((current.Z, current.Lane));
                current = current.Parent;
            }
            path.Reverse();
            return path;
        }

        private static float Manhattan((int z, int lane) a, (int z, int lane) b)
        {
            return Mathf.Abs(a.z - b.z) + Mathf.Abs(a.lane - b.lane);
        }
    }
}
