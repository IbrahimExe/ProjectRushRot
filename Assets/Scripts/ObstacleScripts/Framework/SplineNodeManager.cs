using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections.Generic;

/// <summary>
/// Manages multiple independent wall spline groups within a single SplineContainer.
/// SplineInstantiate natively iterates all splines in a container
/// </summary>
[RequireComponent(typeof(SplineContainer))]
public class SplineNodeManager : MonoBehaviour
{
    public static SplineNodeManager Instance { get; private set; }

    [Header("Grouping")]
    [Tooltip("Max distance between a new node and any node in an existing group for the " +
             "new node to join that group. Nodes further away start a new separate Spline.")]
    public float connectionRange = 10f;

    [Header("Knot Ordering")]
    [Tooltip("Axis to sort nodes along – match the player forward direction.")]
    public Vector3 sortAxis = Vector3.forward;

    // Struct to hold a Spline and its associated nodes
    private class SplineGroup
    {
        public Spline spline;
        public readonly List<SplineNode> nodes = new();

        // Sorts nodes along the runner axis then rebuilds this group's Spline knots.
        // SplineInstantiate is disabled by the caller before this is called.
        public void Rebuild(Transform containerTransform, Vector3 sortAxis)
        {
            
            Vector3 axis = sortAxis.normalized;
            
            nodes.Sort((a, b) =>
            {
                float da = Vector3.Dot(a.transform.position, axis);
                float db = Vector3.Dot(b.transform.position, axis);
                return da.CompareTo(db);
            });

            spline.Clear();
            for (int i = 0; i < nodes.Count; i++)
            {
                // Knot positions are always in the container's local space.
                Vector3 localPos = containerTransform.InverseTransformPoint(nodes[i].transform.position);
                spline.Add(new BezierKnot(new float3(localPos)), TangentMode.AutoSmooth);
                nodes[i].SetKnotIndex(i);
            }
        }
    }

    // ------------------------------------------------------------------------------------------------

    private SplineContainer _container;
    private SplineInstantiate _splineInstantiate;

    private readonly List<SplineGroup> _groups = new(); // spline groups
    private readonly Dictionary<SplineNode, SplineGroup> _nodeToGroup = new(); // dictionary to find the spline group of a node

    // Merge requests from OnTriggerEnter are deferred here and executed in LateUpdate.
    private readonly List<(SplineNode, SplineNode)> _pendingMerges = new();

    public SplineContainer SplineContainer => _container; // public getter for the SplineContainer in case SplineNodes need to use something from it

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[SplineNodeManager] Duplicate manager found. Destroying extra instance.");
            Destroy(this);
            return;
        }
        Instance = this;

        _container = GetComponent<SplineContainer>();
        _splineInstantiate = GetComponent<SplineInstantiate>();

        // Start with an empty container, spline groups create their splines at runtime.
        _container.Splines = new List<Spline>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void LateUpdate()
    {
        if (_pendingMerges.Count == 0) return;

        // Snapshot and clear first so any merges triggered during processing don't loop.
        var batch = new List<(SplineNode, SplineNode)>(_pendingMerges);
        _pendingMerges.Clear();

        foreach (var (a, b) in batch)
            ExecuteMerge(a, b);
    }

    //Registers a node. Joins the nearest in range group or creates a new Spline group.
    public void RegisterNode(SplineNode node)
    {
        if (_nodeToGroup.ContainsKey(node)) return; // the node is already registered

        var group = FindNearestGroup(node.transform.position) ?? CreateGroup();

        group.nodes.Add(node);
        _nodeToGroup[node] = group;

        RebuildGroup(group);
    }

    // Unregisters a node called from OnDestroy
    // If the group becomes empty, its Spline is removed from the container — SplineInstantiate
    public void UnregisterNode(SplineNode node)
    {
        if (!_nodeToGroup.TryGetValue(node, out var group)) return;

        _nodeToGroup.Remove(node);
        group.nodes.Remove(node);

        if (group.nodes.Count == 0) // if there are no nodes left in the spline group, the spline is deleted from the container
            RemoveGroup(group);
        else
            RebuildGroup(group);
    }

    // Updates a single knot's position in-place
    public void UpdateNodeKnot(SplineNode node, Vector3 worldPos)
    {
        if (!_nodeToGroup.TryGetValue(node, out var group)) return;

        int idx = node.KnotIndex;
        if (idx < 0 || idx >= group.spline.Count) return;

        Vector3 localPos = _container.transform.InverseTransformPoint(worldPos);
        var knot = group.spline[idx];
        knot.Position = new float3(localPos);
        group.spline.SetKnot(idx, knot);

        // Reposition wall segment instances along the updated spline
        // without this, UpdateInstances() will create new walls without removing the old ones
        _splineInstantiate?.UpdateInstances();
    }

    // This functions if for the SplineNodes to call when they detect another node within range in the OnTriggerEnter
    // It will queue the merge to happen in LateUpdate
    public void TryMergeGroups(SplineNode nodeA, SplineNode nodeB)
    {
        // Validate upfront to not queue invalid pairs
        if (!_nodeToGroup.TryGetValue(nodeA, out var groupA)) return;
        if (!_nodeToGroup.TryGetValue(nodeB, out var groupB)) return;
        if (groupA == groupB) return; // already merged

        _pendingMerges.Add((nodeA, nodeB));
    }

    void ExecuteMerge(SplineNode nodeA, SplineNode nodeB)
    {
        if (!_nodeToGroup.TryGetValue(nodeA, out var groupA)) return;
        if (!_nodeToGroup.TryGetValue(nodeB, out var groupB)) return;
        if (groupA == groupB) return;
        
        // Merge the smaller spline group into the largest to minimize the number of nodes that will be updated
        var (target, source) = groupA.nodes.Count >= groupB.nodes.Count
            ? (groupA, groupB)
            : (groupB, groupA);

        foreach (var n in source.nodes)
            _nodeToGroup[n] = target;
        target.nodes.AddRange(source.nodes);
        _groups.Remove(source);

        WithSplineInstantiateDisabled(() =>
        {
            // remove the source group's Spline from the container
            var list = new List<Spline>(_container.Splines);
            list.Remove(source.spline);
            _container.Splines = list;

            // rebuild the merged group inside the remaining Spline
            target.Rebuild(_container.transform, sortAxis);
        });
    }

    SplineGroup FindNearestGroup(Vector3 position)
    {
        SplineGroup nearest = null;
        float minDist = connectionRange;

        foreach (var group in _groups)
        {
            foreach (var node in group.nodes)
            {
                float dist = Vector3.Distance(position, node.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = group;
                }
            }
        }

        return nearest;
    }

    SplineGroup CreateGroup()
    {
        var newSpline = new Spline();
        var group = new SplineGroup { spline = newSpline };
        _groups.Add(group);

        // Add the new Spline to the container using the Splines setter
        // SplineInstantiate is temporarily disabled
        // enabling it again triggers a clean UpdateInstances() over all splines
        WithSplineInstantiateDisabled(() =>
        {
            var list = new List<Spline>(_container.Splines) { newSpline };
            _container.Splines = list;
        });

        return group;
    }

    // removes a spline group from the container
    void RemoveGroup(SplineGroup group)
    {
        _groups.Remove(group);

        WithSplineInstantiateDisabled(() =>
        {
            var list = new List<Spline>(_container.Splines);
            list.Remove(group.spline);
            _container.Splines = list;
        });
    }

    void RebuildGroup(SplineGroup group)
    {
        // Disable SplineInstantiate while we clear and repopulate knots
        WithSplineInstantiateDisabled(() =>
        {
            group.Rebuild(_container.transform, sortAxis);
        });
    }

    // Function that temporarily disables SplineInstantiate while executing an action then enables it again
    void WithSplineInstantiateDisabled(System.Action action)
    {
        if (_splineInstantiate == null)
        {
            action();
            return;
        }

        bool wasEnabled = _splineInstantiate.enabled;
        _splineInstantiate.enabled = false;

        action();

        if (wasEnabled)
            _splineInstantiate.enabled = true;
    }
}
