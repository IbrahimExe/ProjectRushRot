using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyClimbHelper : MonoBehaviour
{
    [Header("Detection")]
    public float forwardCheckDistance = 0.7f;
    public float maxClimbHeight = 1.5f;
    public LayerMask obstacleMask;

    [Header("Climb")]
    public float climbSpeed = 2.5f;
    public Animator animator;
    public string climbTrigger = "Climb";

    public bool CheckClimbable(out Vector3 topPoint)
    {
        topPoint = Vector3.zero;
        Vector3 origin = transform.position + Vector3.up * 0.5f;

        if (Physics.Raycast(origin, transform.forward, out RaycastHit hit, forwardCheckDistance, obstacleMask))
        {
            Vector3 topOrigin = hit.point + Vector3.up * maxClimbHeight;

            if (Physics.Raycast(topOrigin, Vector3.down, out RaycastHit topHit, maxClimbHeight, obstacleMask))
            {
                float height = topHit.point.y - transform.position.y;
                if (height <= maxClimbHeight)
                {
                    topPoint = topHit.point + Vector3.up * 0.1f;
                    return true;
                }
            }
        }
        return false;
    }

    public IEnumerator DoClimb(Vector3 topPoint)
    {
        if (animator) animator.SetTrigger(climbTrigger);

        while (Vector3.Distance(transform.position, topPoint) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, topPoint, climbSpeed * Time.deltaTime);
            yield return null;
        }
    }
}