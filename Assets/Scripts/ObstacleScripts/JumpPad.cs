using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class JumpPad : MonoBehaviour
{
    [Header("Pad settings")]
    [Tooltip("Strength multiplier applied in the chosen direction.")]
    [SerializeField] private float strength = 12f;

    [Tooltip("Direction of the launch (local or world depending on Use Local Direction).")]
    [SerializeField] private Vector3 direction = Vector3.up;

    [Tooltip("If true, direction is interpreted in the pad's local space.")]
    [SerializeField] private bool useLocalDirection = true;

    [Tooltip("If true, the pad sets velocity directly. Otherwise it adds an impulse.")]
    [SerializeField] private bool setVelocity = true;

    [Tooltip("When setVelocity is true, preserve the object's current horizontal (X/Z) velocity.")]
    [SerializeField] private bool preserveHorizontalVelocity = true;

    [Tooltip("Minimum seconds between activations for the same Rigidbody.")]
    [SerializeField] private float cooldown = 0.25f;

    [Tooltip("Which layers the pad affects.")]
    [SerializeField] private LayerMask affectedLayers = ~0;

    // Tracks last activation time per rigidbody to enforce cooldown
    private readonly Dictionary<Rigidbody, float> _lastActivated = new Dictionary<Rigidbody, float>();

    private Collider _collider;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        if (_collider == null)
        {
            Debug.LogWarning("JumpPad requires a Collider. Please add one.", this);
        }
        else
        {
            // Jump pads should be triggers by design
            if (!_collider.isTrigger)
            {
#if UNITY_EDITOR
                // In the editor we try to set it automatically to reduce mistakes
                _collider.isTrigger = true;
#else
                Debug.LogWarning("JumpPad collider is not a trigger. Set __isTrigger__ to true.", this);
#endif
            }
        }
    }

    private void Update()
    {
        // Clean up stale entries occasionally to avoid growth
        if (_lastActivated.Count == 0) return;
        float now = Time.time;
        var toRemove = new List<Rigidbody>();
        foreach (var kv in _lastActivated)
        {
            if (now - kv.Value > 10f) // arbitrary cleanup threshold
                toRemove.Add(kv.Key);
        }
        foreach (var rb in toRemove)
            _lastActivated.Remove(rb);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Layer filter
        if ((affectedLayers.value & (1 << other.gameObject.layer)) == 0) return;

        // Must have an attached Rigidbody (on self or parent)
        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;

        // Cooldown per rigidbody
        if (_lastActivated.TryGetValue(rb, out float last) && Time.time - last < cooldown) return;
        _lastActivated[rb] = Time.time;

        // Compute launch vector
        Vector3 dir = useLocalDirection ? transform.TransformDirection(direction.normalized) : direction.normalized;
        Vector3 launch = dir * strength;

        if (setVelocity)
        {
            Vector3 final;
            if (preserveHorizontalVelocity)
            {
                // Keep existing horizontal velocity, replace vertical component
                final = new Vector3(rb.linearVelocity.x, launch.y, rb.linearVelocity.z);
            }
            else
            {
                // Use full launch vector as new velocity
                final = launch;
            }
            rb.linearVelocity = final;
        }
        else
        {
            // Apply an instantaneous change
            rb.AddForce(launch, ForceMode.VelocityChange);
        }
    }
}