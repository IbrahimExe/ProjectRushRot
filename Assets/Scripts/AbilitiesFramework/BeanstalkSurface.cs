using UnityEngine;

public class BeanstalkSurface : MonoBehaviour
{
    public float forwardBoostForce = 28f;
    public float minimumForwardSpeed = 18f;
    public float maxStalkSpeed = 40f;

    private void OnCollisionStay(Collision collision)
    {
        PlayerControllerBase player = collision.gameObject.GetComponent<PlayerControllerBase>();

        if (player == null)
            return;

        Rigidbody rb = player.RB;

        Vector3 stalkForward = transform.forward.normalized;

        float currentForwardSpeed = Vector3.Dot(rb.linearVelocity, stalkForward);

        if (currentForwardSpeed < minimumForwardSpeed)
        {
            rb.AddForce(stalkForward * forwardBoostForce, ForceMode.Acceleration);
        }

        Vector3 velocityAlongStalk = Vector3.Project(rb.linearVelocity, stalkForward);

        if (velocityAlongStalk.magnitude > maxStalkSpeed)
        {
            Vector3 sideVelocity = rb.linearVelocity - velocityAlongStalk;
            rb.linearVelocity = sideVelocity + velocityAlongStalk.normalized * maxStalkSpeed;
        }
    }
}