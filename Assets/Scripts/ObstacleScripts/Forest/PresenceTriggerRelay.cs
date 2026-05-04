using UnityEngine;

/// <summary>
/// It does two things:
///   1. Forwards OnTriggerEnter/Exit to the parent AfraidAnimal
///   2. Acts as the PresenceTriggerMarker so DashAbility ignores this collider for kill detection
/// </summary>
[RequireComponent(typeof(Collider))]
public class PresenceTriggerRelay : MonoBehaviour
{
    private AfraidAnimal _owner;

    private void Awake()
    {
        _owner = GetComponentInParent<AfraidAnimal>();

        if (_owner == null)
            Debug.LogWarning($"[PresenceTriggerRelay] No AfraidAnimal found in parent of '{name}'.", this);

        // Make sure the collider is flagged as a trigger
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        _owner?.OnPresenceTriggerEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        _owner?.OnPresenceTriggerExit(other);
    }
}
