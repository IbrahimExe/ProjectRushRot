using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class MovingWall : MonoBehaviour
{
    public enum TravelMode { PingPong, Loop, Once }

    [Header("Path")]
    [Tooltip("Waypoints for the obstacle. Order matters.")]
    [SerializeField] private Transform[] waypoints;

    [Header("Motion")]
    [Tooltip("Meters per second")]
    [SerializeField] private float speed = 3f;
    [Tooltip("Seconds to wait at each waypoint")]
    [SerializeField] private float waitTime = 0.0f;
    [Tooltip("How the obstacle travels between waypoints")]
    [SerializeField] private TravelMode mode = TravelMode.PingPong;

    [Header("Rotation")]
    [Tooltip("If true the obstacle will rotate to face movement direction")]
    [SerializeField] private bool rotateToDirection = false;
    [Tooltip("Continuous spin instead of (or alongside) facing movement")]
    [SerializeField] private bool spinInPlace = false;
    [Tooltip("Spin speed in degrees per second when spinInPlace is enabled")]
    [SerializeField] private float spinSpeed = 90f;
    [Tooltip("Slerp speed used when rotating to face movement")]
    [SerializeField] private float rotationSlerpSpeed = 10f;

    [Header("Carrying / Interaction")]
    [Tooltip("If true, only objects tagged 'Player' will be carried; otherwise all dynamic rigidbodies will be carried.")]
    [SerializeField] private bool carryOnlyPlayer = true;
    [Tooltip("Minimum upward contact normal to consider the object 'standing on top'")]
    [SerializeField][Range(0.5f, 0.99f)] private float topContactThreshold = 0.7f;

    // runtime
    private Rigidbody rb;
    private int index = 0;
    private int dir = 1;
    private float waitTimer = 0f;
    private bool moving = true;

    // track original parents so we can restore them
    private readonly Dictionary<Transform, Transform> originalParents = new Dictionary<Transform, Transform>();

    private void Reset()
    {
        // convenience: configure collider as non-trigger so obstacle collides
        var c = GetComponent<Collider>();
        if (c) c.isTrigger = false;
        var r = GetComponent<Rigidbody>();
        if (r) r.isKinematic = true;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true; // we move via MovePosition
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning($"{nameof(MovingObstacle)} on '{name}' has no waypoints assigned.", this);
            moving = false;
        }
    }

    private void Start()
    {
        // clamp index
        index = Mathf.Clamp(index, 0, Mathf.Max(0, waypoints.Length - 1));
    }

    private void FixedUpdate()
    {
        if (!moving || waypoints == null || waypoints.Length == 0) return;

        if (waitTimer > 0f)
        {
            waitTimer -= Time.fixedDeltaTime;
            return;
        }

        Transform target = waypoints[index];
        if (target == null) return;

        Vector3 current = rb.position;
        Vector3 targetPos = target.position;
        Vector3 next = Vector3.MoveTowards(current, targetPos, speed * Time.fixedDeltaTime);

        // apply movement
        rb.MovePosition(next);

        // rotation: continuous spin or face movement (or both if both flags are true)
        if (spinInPlace)
        {
            Quaternion spinDelta = Quaternion.Euler(0f, spinSpeed * Time.fixedDeltaTime, 0f);
            rb.MoveRotation(rb.rotation * spinDelta);
        }

        if (rotateToDirection)
        {
            Vector3 moveDelta = next - current;
            if (moveDelta.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDelta.normalized, Vector3.up);
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, rotationSlerpSpeed * Time.fixedDeltaTime));
            }
        }

        // reached?
        if (Vector3.Distance(next, targetPos) <= 0.001f)
        {
            // arrived
            if (waitTime > 0f) waitTimer = waitTime;

            // advance index based on mode
            if (mode == TravelMode.PingPong)
            {
                if (index == waypoints.Length - 1) dir = -1;
                else if (index == 0) dir = 1;
                index += dir;
            }
            else if (mode == TravelMode.Loop)
            {
                index = (index + 1) % waypoints.Length;
            }
            else // Once
            {
                if (index < waypoints.Length - 1)
                    index++;
                else
                    moving = false; // stop at last waypoint
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // For each contact check if object is on top of the obstacle
        foreach (var contact in collision.contacts)
        {
            if (contact.normal.y >= topContactThreshold)
            {
                var t = collision.collider.transform;
                if (ShouldCarry(t))
                {
                    TryParent(t);
                    return;
                }
            }
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        // ensure object remains parented if staying on top (handles edge cases)
        foreach (var contact in collision.contacts)
        {
            if (contact.normal.y >= topContactThreshold)
            {
                var t = collision.collider.transform;
                if (ShouldCarry(t))
                {
                    if (!originalParents.ContainsKey(t))
                        TryParent(t);
                    return;
                }
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        var t = collision.collider.transform;
        if (originalParents.ContainsKey(t))
            RestoreParent(t);
    }

    private bool ShouldCarry(Transform other)
    {
        if (other == null) return false;
        if (carryOnlyPlayer)
            return other.CompareTag("Player");
        // carry any dynamic rigidbody (not kinematic)
        var otherRb = other.GetComponentInParent<Rigidbody>();
        return (otherRb != null && otherRb.isKinematic == false);
    }

    private void TryParent(Transform child)
    {
        if (child == null) return;
        if (originalParents.ContainsKey(child)) return;

        // store original parent
        originalParents[child] = child.parent;
        child.SetParent(transform, true);
    }

    private void RestoreParent(Transform child)
    {
        if (child == null) return;
        if (!originalParents.TryGetValue(child, out var orig)) return;

        // restore and remove record
        child.SetParent(orig, true);
        originalParents.Remove(child);
    }

    private void OnDisable()
    {
        // restore any remaining parents
        var keys = new List<Transform>(originalParents.Keys);
        foreach (var k in keys)
            RestoreParent(k);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            Gizmos.DrawSphere(waypoints[i].position, 0.15f);
            if (i + 1 < waypoints.Length && waypoints[i + 1] != null)
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
        }
    }
#endif
}