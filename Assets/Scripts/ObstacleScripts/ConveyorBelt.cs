using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ConveyorBelt : MonoBehaviour
{
    [Header("Trap settings")]
    [Tooltip("Fraction of normal speed while inside (0.5 = 50% speed).")]
    [SerializeField] private float slowFactor = 0.5f;

    [Tooltip("Conveyor direction (local when Use Local Direction is checked).")]
    [SerializeField] private Vector3 conveyorDirection = Vector3.forward;

    [Tooltip("Speed of conveyor movement (meters / second) added along conveyor direction.")]
    [SerializeField] private float conveyorSpeed = 2f;

    [Tooltip("If true, conveyorDirection is interpreted in the trap's local space.")]
    [SerializeField] private bool useLocalDirection = true;

    [Tooltip("Which layers the trap affects.")]
    [SerializeField] private LayerMask affectedLayers = ~0;

    // Track PlayerControllerBase states (supports stacking)
    private readonly Dictionary<PlayerControllerBase, MotorState> _motorStates = new Dictionary<PlayerControllerBase, MotorState>();
    // Track Rigidbody-only states (supports stacking of factors)
    private readonly Dictionary<Rigidbody, RigidbodyState> _rbStates = new Dictionary<Rigidbody, RigidbodyState>();

    private void Reset()
    {
        // default to trigger collider for convenience
        var c = GetComponent<Collider>();
        if (c) c.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if ((affectedLayers.value & (1 << other.gameObject.layer)) == 0) return;

        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;

        // Prefer PlayerControllerBase on the same GameObject as the Rigidbody
        PlayerControllerBase motor = rb.GetComponent<PlayerControllerBase>();
        if (motor != null)
        {
            if (!_motorStates.TryGetValue(motor, out var state))
            {
                state = new MotorState(motor);
                _motorStates[motor] = state;
            }
            state.PushFactor(slowFactor);
            return;
        }

        // Fallback: pure Rigidbody handling
        if (!_rbStates.TryGetValue(rb, out var rbState))
        {
            rbState = new RigidbodyState(rb);
            _rbStates[rb] = rbState;
        }
        rbState.PushFactor(slowFactor);
    }

    private void OnTriggerExit(Collider other)
    {
        if ((affectedLayers.value & (1 << other.gameObject.layer)) == 0) return;

        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;

        PlayerControllerBase motor = rb.GetComponent<PlayerControllerBase>();
        if (motor != null)
        {
            if (_motorStates.TryGetValue(motor, out var state))
            {
                state.PopFactor(slowFactor);
                if (state.IsEmpty)
                    _motorStates.Remove(motor);
            }
            return;
        }

        if (_rbStates.TryGetValue(rb, out var rbState))
        {
            rbState.PopFactor(slowFactor);
            if (rbState.IsEmpty)
                _rbStates.Remove(rb);
        }
    }

    private void FixedUpdate()
    {
        // Conveyor direction in world space
        Vector3 worldDir = useLocalDirection ? transform.TransformDirection(conveyorDirection.normalized) : conveyorDirection.normalized;
        Vector3 conveyorVel = worldDir * conveyorSpeed;

        // Apply to motors (PlayerControllerBase) first so conveyor affects players using the motor
        if (_motorStates.Count > 0)
        {
            var entries = new List<KeyValuePair<PlayerControllerBase, MotorState>>(_motorStates);
            foreach (var kv in entries)
            {
                var motor = kv.Key;
                var state = kv.Value;
                if (motor == null) { _motorStates.Remove(motor); continue; }

                Rigidbody rb = motor.RB;
                if (rb == null)
                {
                    // still maintain MaxSpeedMultiplier via MotorState; nothing to apply physically
                    continue;
                }

                // Apply slow factor to planar velocity and add conveyor movement
                Vector3 vel = rb.linearVelocity;
                Vector3 planar = new Vector3(vel.x, 0f, vel.z) * state.CurrentFactor;
                rb.linearVelocity = new Vector3(planar.x + conveyorVel.x, vel.y, planar.z + conveyorVel.z);
            }
        }

        // Update Rigidbody fallback targets
        if (_rbStates.Count > 0)
        {
            // Copy keys to avoid modification during iteration
            var entries = new List<KeyValuePair<Rigidbody, RigidbodyState>>(_rbStates);
            foreach (var kv in entries)
            {
                Rigidbody rb = kv.Key;
                var state = kv.Value;

                if (rb == null)
                {
                    _rbStates.Remove(rb);
                    continue;
                }

                float factor = state.CurrentFactor;
                // Apply slow to planar velocity and add conveyor movement
                Vector3 vel = rb.linearVelocity;
                Vector3 planar = new Vector3(vel.x, 0f, vel.z) * factor;
                rb.linearVelocity = new Vector3(planar.x + conveyorVel.x, vel.y, planar.z + conveyorVel.z);
            }
        }
    }

    private void OnDisable()
    {
        // restore motors
        foreach (var kv in _motorStates)
            kv.Value.Clear();
        _motorStates.Clear();

        // clear rb states
        _rbStates.Clear();
    }

    // --- Helper classes ---

    private class MotorState
    {
        private readonly PlayerControllerBase motor;
        private readonly List<float> factors = new List<float>();
        private readonly float originalBase;

        public MotorState(PlayerControllerBase motor)
        {
            this.motor = motor;
            // store the current multiplier as the base
            this.originalBase = motor.MaxSpeedMultiplier;
        }

        // Add a slow factor (stack multiplicatively)
        public void PushFactor(float factor)
        {
            factors.Add(factor);
            Recompute();
        }

        // Remove one instance of the factor (if present) and recompute
        public void PopFactor(float factor)
        {
            // remove last matching factor (handles stacked same factors)
            for (int i = factors.Count - 1; i >= 0; --i)
            {
                if (Mathf.Approximately(factors[i], factor))
                {
                    factors.RemoveAt(i);
                    Recompute();
                    return;
                }
            }
            // if not found, still recompute in case of other modifications
            Recompute();
        }

        public bool IsEmpty => factors.Count == 0;

        // Product of active factors (1.0 if none)
        public float CurrentFactor
        {
            get
            {
                float prod = 1f;
                foreach (var f in factors) prod *= f;
                return prod;
            }
        }

        private void Recompute()
        {
            if (motor == null) return;
            float prod = 1f;
            foreach (var f in factors) prod *= f;
            motor.MaxSpeedMultiplier = originalBase * prod;
        }

        // Restore to original and clear
        public void Clear()
        {
            if (motor != null)
                motor.MaxSpeedMultiplier = originalBase;
            factors.Clear();
        }
    }

    private class RigidbodyState
    {
        private readonly Rigidbody rb;
        private readonly List<float> factors = new List<float>();

        public RigidbodyState(Rigidbody rb)
        {
            this.rb = rb;
        }

        public void PushFactor(float factor)
        {
            factors.Add(factor);
        }

        public void PopFactor(float factor)
        {
            for (int i = factors.Count - 1; i >= 0; --i)
            {
                if (Mathf.Approximately(factors[i], factor))
                {
                    factors.RemoveAt(i);
                    return;
                }
            }
        }

        public bool IsEmpty => factors.Count == 0;

        // Product of active factors (1.0 if none)
        public float CurrentFactor
        {
            get
            {
                float prod = 1f;
                foreach (var f in factors) prod *= f;
                return prod;
            }
        }
    }
}