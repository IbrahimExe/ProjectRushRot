using UnityEngine;

public class BeanstalkSurface : MonoBehaviour
{
    public float stickForce = 45f;
    public float forwardBoostForce = 18f;
    public float maxStalkSpeed = 35f;

    private void OnCollisionStay(Collision collision)
    {
        PlayerControllerBase player = collision.gameObject.GetComponent<PlayerControllerBase>();

        if (player == null)
            return;

        if (Input.GetButton("Jump"))
            return;

        Rigidbody rb = player.RB;

        Vector3 contactPoint = collision.GetContact(0).point;
        Vector3 directionToStalk = (contactPoint - player.transform.position).normalized;

        rb.AddForce(directionToStalk * stickForce, ForceMode.Acceleration);

        Vector3 stalkForward = transform.up.normalized;
        rb.AddForce(stalkForward * forwardBoostForce, ForceMode.Acceleration);

        Vector3 velocityAlongStalk = Vector3.Project(rb.linearVelocity, stalkForward);

        if (velocityAlongStalk.magnitude > maxStalkSpeed)
        {
            Vector3 sideVelocity = rb.linearVelocity - velocityAlongStalk;
            rb.linearVelocity = sideVelocity + velocityAlongStalk.normalized * maxStalkSpeed;
        }
    }
}