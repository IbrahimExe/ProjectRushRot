using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections.Generic;

/// <summary>
/// Manages independent wall spline groups. Each group owns a child GameObject with
/// its own SplineContainer + SplineInstantiate, so modifying one group never affects
/// another group's wall layout or prefab selection.
///
/// Setup:
///   • Add this script, a SplineContainer, and a SplineInstantiate to your manager GO.
///   • Configure the SplineInstantiate in the Inspector (prefabs, spacing, axes, etc.).
///     It acts as a template — wall groups copy from it at creation time.
///   • Use "Sync All Groups with Template" (right-click menu) to push inspector changes
///     to already-running groups.
/// </summary>
[RequireComponent(typeof(SplineContainer))]
public class SplineNodeManager : MonoBehaviour
{
    public static SplineNodeManager Instance { get; private set; }

    [Header("Grouping")]
    [Tooltip("Max distance for a new node to join an existing group. Further nodes start a new group.")]
    public float connectionRange = 10f;

    [Header("Knot Ordering")]
    [Tooltip("Sort axis for nodes – match your runner's forward direction (usually Z).")]
    public Vector3 sortAxis = Vector3.forward;

    // -------------------------------------------------------------------------
    // SplineGroup
    // -------------------------------------------------------------------------

    private class SplineGroup
    {
        public GameObject go;
        public SplineContainer container;
        public SplineInstantiate splineInstantiate;
        public readonly List<SplineNode> nodes = new();

        /// <summary>
        /// Incremental rebuild: never calls spline.Clear(). Only adds, removes, or
        /// repositions knots that actually changed, minimising Spline.Changed events.
        /// Uses TangentMode.Linear so appending a knot at the end never alters the
        /// tangents (and therefore positions) of existing knots.
        /// </summary>
        public void Rebuild(Vector3 sortAxis)
        {
            var spline = container.Spline;
            Vector3 axis = sortAxis.normalized;

            nodes.Sort((a, b) =>
                Vector3.Dot(a.transform.position, axis)
                    .CompareTo(Vector3.Dot(b.transform.position, axis)));

            // Remove excess knots from the tail first.
            while (spline.Count > nodes.Count)
                spline.RemoveAt(spline.Count - 1);

            for (int i = 0; i < nodes.Count; i++)
            {
                var localPos = new float3(
                    container.transform.InverseTransformPoint(nodes[i].transform.position));
                var knot = new BezierKnot(localPos);

                if (i < spline.Count)
                {
                    // Skip SetKnot if position is unchanged — avoids a spurious
                    // Spline.Changed -> UpdateInstances() call.
                    if (math.any(spline[i].Position != localPos))
                        spline.SetKnot(i, knot);
                }
                else
                {
                    // Linear: appending never changes any earlier knot's tangent.
                    spline.Add(knot, TangentMode.Linear);
                }

                nodes[i].SetKnotIndex(i);
            }
        }

        public void DestroyGroup() => Object.Destroy(go);
    }

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    private SplineInstantiate _template;  // user-configured, on this GO
    private readonly List<SplineGroup> _groups = new();
    private readonly Dictionary<SplineNode, SplineGroup> _nodeToGroup = new();
    private readonly List<(SplineNode, SplineNode)> _pendingMerges = new();
    private int _groupCounter;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        _template = GetComponent<SplineInstantiate>();

        // Keep the manager's own SplineContainer empty so the template
        // SplineInstantiate generates nothing — it's config-only.
        GetComponent<SplineContainer>().Splines = new List<Spline>();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void LateUpdate()
    {
        if (_pendingMerges.Count == 0) return;
        var batch = new List<(SplineNode, SplineNode)>(_pendingMerges);
        _pendingMerges.Clear();
        foreach (var (a, b) in batch) ExecuteMerge(a, b);
    }

    // -------------------------------------------------------------------------
    // Public API – called by SplineNode
    // -------------------------------------------------------------------------

    public void RegisterNode(SplineNode node)
    {
        if (_nodeToGroup.ContainsKey(node)) return;
        var group = FindNearestGroup(node.transform.position) ?? CreateGroup();
        group.nodes.Add(node);
        _nodeToGroup[node] = group;
        group.Rebuild(sortAxis);
    }

    public void UnregisterNode(SplineNode node)
    {
        if (!_nodeToGroup.TryGetValue(node, out var group)) return;
        _nodeToGroup.Remove(node);
        group.nodes.Remove(node);

        if (group.nodes.Count == 0)
            RemoveGroup(group);
        else
            group.Rebuild(sortAxis);
    }

    public void UpdateNodeKnot(SplineNode node, Vector3 worldPos)
    {
        if (!_nodeToGroup.TryGetValue(node, out var group)) return;
        int idx = node.KnotIndex;
        if (idx < 0 || idx >= group.container.Spline.Count) return;

        var localPos = new float3(group.container.transform.InverseTransformPoint(worldPos));
        var knot = group.container.Spline[idx];
        knot.Position = localPos;
        group.container.Spline.SetKnot(idx, knot);
        // Spline.Changed fires -> only this group's SplineInstantiate reacts.
    }

    public void TryMergeGroups(SplineNode nodeA, SplineNode nodeB)
    {
        if (!_nodeToGroup.TryGetValue(nodeA, out var groupA)) return;
        if (!_nodeToGroup.TryGetValue(nodeB, out var groupB)) return;
        if (groupA == groupB) return;
        _pendingMerges.Add((nodeA, nodeB));
    }

    // -------------------------------------------------------------------------
    // Editor utility
    // -------------------------------------------------------------------------

    /// <summary>Push template inspector settings to all live groups.</summary>
    [ContextMenu("Sync All Groups with Template")]
    public void SyncAllGroups()
    {
        if (_template == null) return;
        foreach (var group in _groups)
        {
            CopySettings(_template, group.splineInstantiate);
            group.splineInstantiate.SetDirty();
            group.splineInstantiate.UpdateInstances();
        }
    }

    // -------------------------------------------------------------------------
    // Internals
    // -------------------------------------------------------------------------

    SplineGroup FindNearestGroup(Vector3 position)
    {
        SplineGroup nearest = null;
        float minDist = connectionRange;
        foreach (var group in _groups)
            foreach (var node in group.nodes)
            {
                float d = Vector3.Distance(position, node.transform.position);
                if (d < minDist) { minDist = d; nearest = group; }
            }
        return nearest;
    }

    SplineGroup CreateGroup()
    {
        var go = new GameObject($"WallGroup_{_groupCounter++}");
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        var container = go.AddComponent<SplineContainer>();
        container.Spline.Clear();

        var si = go.AddComponent<SplineInstantiate>();
        if (_template != null) CopySettings(_template, si);
        // Unique seed per group = independent random sequence per group.
        si.Seed = go.GetInstanceID();

        var group = new SplineGroup { go = go, container = container, splineInstantiate = si };
        _groups.Add(group);
        return group;
    }

    void RemoveGroup(SplineGroup group)
    {
        _groups.Remove(group);
        foreach (var node in group.nodes) _nodeToGroup.Remove(node);
        group.nodes.Clear();
        group.DestroyGroup(); // child GO destroyed -> SplineInstantiate + all instances gone
    }

    void ExecuteMerge(SplineNode nodeA, SplineNode nodeB)
    {
        if (!_nodeToGroup.TryGetValue(nodeA, out var groupA)) return;
        if (!_nodeToGroup.TryGetValue(nodeB, out var groupB)) return;
        if (groupA == groupB) return;

        var (target, source) = groupA.nodes.Count >= groupB.nodes.Count
            ? (groupA, groupB) : (groupB, groupA);

        foreach (var n in source.nodes) _nodeToGroup[n] = target;
        target.nodes.AddRange(source.nodes);
        _groups.Remove(source);
        source.DestroyGroup(); // cleans up source instances immediately
        target.Rebuild(sortAxis);
    }

    static void CopySettings(SplineInstantiate src, SplineInstantiate dst)
    {
        dst.InstantiateMethod   = src.InstantiateMethod;
        dst.MinSpacing          = src.MinSpacing;
        dst.MaxSpacing          = Mathf.Max(src.MinSpacing, src.MaxSpacing);
        dst.UpAxis              = src.UpAxis;
        dst.ForwardAxis         = src.ForwardAxis;
        dst.CoordinateSpace     = src.CoordinateSpace;
        dst.itemsToInstantiate  = src.itemsToInstantiate;
        dst.MinPositionOffset   = src.MinPositionOffset;
        dst.MaxPositionOffset   = src.MaxPositionOffset;
        dst.PositionSpace       = src.PositionSpace;
        dst.MinRotationOffset   = src.MinRotationOffset;
        dst.MaxRotationOffset   = src.MaxRotationOffset;
        dst.RotationSpace       = src.RotationSpace;
        dst.MinScaleOffset      = src.MinScaleOffset;
        dst.MaxScaleOffset      = src.MaxScaleOffset;
        dst.ScaleSpace          = src.ScaleSpace;
    }
}
