using System.Reflection;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DestructableWall : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Which layers can destroy this wall (use player layer).")]
    [SerializeField] private LayerMask affectedLayers = ~0;

    [Tooltip("Optional: require the object to have this tag (leave empty to skip).")]
    [SerializeField] private string requiredTag = "Player";

    [Header("Destroy options")]
    [Tooltip("Destroy immediately on dash. If false, the wall will be disabled (useful to play VFX).")]
    [SerializeField] private bool destroyGameObject = true;

    [Tooltip("If true, the wall will only be destroyed by a dash. Otherwise any collision from the player will destroy it.")]
    [SerializeField] private bool requireDash = true;

    // Called for non-trigger colliders
    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"DestructableWall: OnCollisionEnter with '{collision.collider.gameObject.name}' (Layer: {LayerMask.LayerToName(collision.collider.gameObject.layer)}, Tag: {collision.collider.gameObject.tag})");
        TryHandleHit(collision.collider);
    }

    // Called for trigger colliders
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"DestructableWall: OnTriggerEnter with '{other.gameObject.name}' (Layer: {LayerMask.LayerToName(other.gameObject.layer)}, Tag: {other.gameObject.tag})");
        TryHandleHit(other);
    }

    private void TryHandleHit(Collider col)
    {
        if (col == null)
        {
            Debug.LogWarning("DestructableWall: Collider is null on hit.");
            return;
        }
        else
        {
            Debug.Log($"DestructableWall: Hit by '{col.gameObject.name}' (Layer: {LayerMask.LayerToName(col.gameObject.layer)}, Tag: {col.gameObject.tag})");
        }
        // Determine the actor root (prefer the object with the Rigidbody)
        var root = col.attachedRigidbody != null
            ? col.attachedRigidbody.gameObject
            : col.gameObject;

        // Layer filter (use the actor/root so child collider can belong to the player)
        if ((affectedLayers.value & (1 << root.layer)) == 0)
            return;

        // Tag filter if set (check the actor/root instead of the child collider)
        if (!string.IsNullOrEmpty(requiredTag) && !root.CompareTag(requiredTag))
            return;

        bool isDashHit = IsDashObject(root);

        if (requireDash && !isDashHit)
            return;

        // Destroy or disable
        if (destroyGameObject)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    private bool IsDashObject(GameObject go)
    {
        if (go == null) return false;

        // 1) Prefer DashAbility (new system)
        var dash = go.GetComponentInParent<DashAbility>();
        if (dash != null)
        {
            if (dash.IsDashing || dash.IsSideDashing || dash.IsDashFlipping)
                return true;
        }

        // 2) Fallback: PlayerController2 (older controller) — read private bools via reflection
        var pc2 = go.GetComponentInParent<PlayerController2>();
        if (pc2 != null)
        {
            var t = pc2.GetType();
            string[] fieldNames = { "isDashing", "isSideDashing", "isDashFlipping" };

            foreach (var name in fieldNames)
            {
                var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (f != null && f.FieldType == typeof(bool))
                {
                    object val = f.GetValue(pc2);
                    if (val is bool b && b)
                        return true;
                }
            }
        }

        // 3) As a last resort, consider a high-speed impact as a dash (optional)
        var rb = go.GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            const float dashSpeedThreshold = 18f;
            if (rb.linearVelocity.magnitude >= dashSpeedThreshold)
                return true;
        }

        return false;
    }
}
