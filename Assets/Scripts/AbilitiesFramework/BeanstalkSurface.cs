using UnityEngine;

public class BeanstalkSurface : MonoBehaviour
{
    public float stickForce = 150f;
    public float upwardHoldForce = 50f;
    public float forwardBoostForce = 45f;

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
        rb.AddForce(Vector3.up * upwardHoldForce, ForceMode.Acceleration);

        Vector3 stalkForward = transform.up.normalized;
        rb.AddForce(stalkForward * forwardBoostForce, ForceMode.Acceleration);
    }
}