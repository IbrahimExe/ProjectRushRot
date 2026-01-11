using UnityEngine;

public class EnemyChase_Transform : MonoBehaviour
{
    [Header("Targets")]
    public Transform Player;

    [Header("Movement Settings")]
    public float MoveSpeed = 4f;
    public float ClimbSpeed = 3f;
    public float ClimbCheckDistance = 1f;
    public float StoppingDistance = 0.5f; // prevents vibration

    [Header("Optional Settings")]
    public bool FaceMovementDirection = true;

    private void Update()
    {
        if (Player == null)
            return;

        Vector3 dir = Player.position - transform.position;
        float distanceToPlayer = dir.magnitude;

        // Check wall ahead
        bool canClimb = false;
        Vector3 wallNormal = Vector3.zero;
        RaycastHit hit;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;

        if (Physics.Raycast(rayOrigin, transform.forward, out hit, ClimbCheckDistance))
        {
            canClimb = true;
            wallNormal = hit.normal;
        }

        if (canClimb)
        {
            Vector3 climbDir = Vector3.up;
            if (wallNormal.sqrMagnitude > 0.001f)
            {
                Vector3 tangent = Vector3.Cross(Vector3.up, wallNormal).normalized;
                climbDir += tangent * 0.2f;
            }

            transform.Translate(climbDir * ClimbSpeed * Time.deltaTime, Space.World);

            if (wallNormal.sqrMagnitude > 0.001f)
            {
                Quaternion look = Quaternion.LookRotation(-wallNormal);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 10f * Time.deltaTime);
            }
        }
        else
        {
            Vector3 moveDir = dir.normalized;

            // Stop if close to player
            if (distanceToPlayer < StoppingDistance)
                moveDir = Vector3.zero;

            transform.Translate(moveDir * MoveSpeed * Time.deltaTime, Space.World);

            if (FaceMovementDirection && moveDir.sqrMagnitude > 0.001f)
            {
                Vector3 lookDir = new Vector3(moveDir.x, 0f, moveDir.z);
                Quaternion look = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 10f * Time.deltaTime);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        Gizmos.DrawLine(rayOrigin, rayOrigin + transform.forward * ClimbCheckDistance);
    }
}