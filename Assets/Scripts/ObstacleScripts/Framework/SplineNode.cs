using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[RequireComponent(typeof(SphereCollider))]
public class SplineNode : MonoBehaviour
{
    [Header("Detection Settings")]
    public float detectionRadius = 5f;
    public string nodeTag = "SplineNode";

    [Header("Spline Settings")]
    [Tooltip("Auto-assigned from SplineNodeManager. Only set manually if not using a manager.")]
    public SplineContainer splineContainer;

    // The knot index this node owns inside its group's Spline. Kept in sync by SplineNodeManager
    private int _knotIndex = -1;
    public int KnotIndex => _knotIndex;
    private SphereCollider _trigger;
    private SplineNodeManager _manager;

    void Awake()
    {
        _trigger = GetComponent<SphereCollider>();
        _trigger.isTrigger = true;
        _trigger.radius = detectionRadius;
        gameObject.tag = nodeTag;
    }

    void Start()
    {
        _manager = SplineNodeManager.Instance != null
            ? SplineNodeManager.Instance
            : FindFirstObjectByType<SplineNodeManager>();

        if (_manager != null)
        {
            splineContainer = _manager.SplineContainer;
            _manager.RegisterNode(this);
        }
        else
        {
            Debug.LogWarning($"[SplineNode] No SplineNodeManager found. " +
                             $"Falling back to direct registration on {name}. " +
                             $"Knot cleanup on destroy will not work correctly.");
            RegisterKnotDirect();
        }
    }

    void OnDestroy()
    {
        if (_manager != null)
        {
            // Triggers a spline rebuild -> SplineInstantiate removes the wall segments for this knot
            _manager.UnregisterNode(this);
        }
        else
        {
            RemoveKnotDirect();
        }
    }

    // Called by SplineNodeManager after every rebuild to keep the index in sync
    public void SetKnotIndex(int index) => _knotIndex = index;

    // Trigger – group merging when nodes move into range of each other
    void OnTriggerEnter(Collider other)
    {
        if (_manager == null) return;
        if (!other.CompareTag(nodeTag)) return;

        var otherNode = other.GetComponent<SplineNode>();
        if (otherNode == null || otherNode == this) return;

        // Ask the manager to merge the two groups into one continuous spline
        _manager.TryMergeGroups(this, otherNode);
    }


    public void UpdateKnotPosition()
    {
        if (_manager != null)
        {
            // Pass 'this' so the manager can look up which Spline group to update.
            _manager.UpdateNodeKnot(this, transform.position);
        }
        else if (splineContainer != null && _knotIndex >= 0 && _knotIndex < splineContainer.Spline.Count)
        {
            Vector3 localPos = splineContainer.transform.InverseTransformPoint(transform.position);
            var knot = splineContainer.Spline[_knotIndex];
            knot.Position = new float3(localPos);
            splineContainer.Spline.SetKnot(_knotIndex, knot);
        }
    }

    void Update()
    {
        if (transform.hasChanged)
        {
            UpdateKnotPosition();
            transform.hasChanged = false;
        }
    }

    // Fallback: direct registration if there's no manager
    void RegisterKnotDirect()
    {
        if (splineContainer == null) return;

        Vector3 localPos = splineContainer.transform.InverseTransformPoint(transform.position);
        _knotIndex = splineContainer.Spline.Count;
        splineContainer.Spline.Add(new BezierKnot(new float3(localPos)), TangentMode.AutoSmooth);
    }

    void RemoveKnotDirect()
    {
        if (splineContainer == null || _knotIndex < 0 || _knotIndex >= splineContainer.Spline.Count) return;
        splineContainer.Spline.RemoveAt(_knotIndex);
        // NOTE: without a manager, remaining nodes' _knotIndex values will be out of sync with the spline

    }
}
