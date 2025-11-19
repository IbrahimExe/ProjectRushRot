using UnityEngine;

public class EnemySensors : MonoBehaviour
{
    public float climbCheckDistance = 1.2f;
    public LayerMask climbableMask;

    public bool CheckForClimbable(out Vector3 hitPoint, out Vector3 hitNormal)
    {
        Ray ray = new Ray(transform.position + Vector3.up * 0.5f, transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, climbCheckDistance, climbableMask))
        {
            hitPoint = hit.point;
            hitNormal = hit.normal;
            return true;
        }

        hitPoint = Vector3.zero;
        hitNormal = Vector3.zero;
        return false;
    }

    public Transform FindPlayer()
    {
        return GameObject.FindGameObjectWithTag("Player").transform;
    }
}