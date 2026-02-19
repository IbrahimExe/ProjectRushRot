using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class MovingObstacle : MonoBehaviour
{
    public enum TravelMode { PingPong, Loop, Once }

    [Header("Path")]
    [SerializeField] private Transform[] waypoints; 

    [Header("Motion")]
    [SerializeField] private float speed = 3f;
    [SerializeField] private float waitTime = 0.0f;
    [SerializeField] private TravelMode mode = TravelMode.PingPong;

    [Header("Rotation")]
    [SerializeField] private bool rotateToDirection = false;
    [SerializeField] private bool spinInPlace = false;
    [SerializeField] private float spinSpeed = 90f;
    [SerializeField] private float rotationSlerpSpeed = 10f;

    [Header("Push")]
    [Tooltip("How hard the obstacle pushes rigidbodies")]
    [SerializeField] private float pushForce = 8f;

    // runtime
    private Rigidbody rb;
    private int index = 0;
    private int dir = 1;
    private float waitTimer = 0f;
    private bool moving = true;

    private void Reset()
    {
        var c = GetComponent<Collider>();
        if (c) c.isTrigger = false;

        var r = GetComponent<Rigidbody>();
        if (r)
        {
            r.isKinematic = true;
            r.useGravity = false;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning($"{nameof(MovingObstacle)} on '{name}' has no waypoints assigned.", this);
            moving = false;
        }
    }

    private void Start()
    {
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

        rb.MovePosition(next);

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

        if (Vector3.Distance(next, targetPos) <= 0.001f)
        {
            if (waitTime > 0f) waitTimer = waitTime;

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
                    moving = false;
            }
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        Rigidbody otherRb = collision.rigidbody;
        if (otherRb == null || otherRb.isKinematic) return;

        foreach (ContactPoint contact in collision.contacts)
        {
            // push away from obstacle, only sideways
            Vector3 pushDir = new Vector3(contact.normal.x, 0f, contact.normal.z);
            otherRb.AddForce(-pushDir * pushForce, ForceMode.VelocityChange);
        }
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
