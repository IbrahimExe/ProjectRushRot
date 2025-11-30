using UnityEngine;

public class DeathWall : MonoBehaviour
{
    [Header("Movement")]
    public Vector3 MoveDirection = Vector3.forward;
    public float BaseSpeed = 5f;

    [Range(0f, 2f)]
    public float SpeedPercentFromTarget = 0.5f;

    private Rigidbody playerRb;

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
            playerRb = player.GetComponent<Rigidbody>();
    }

    void Update()
    {
        float bonus = 0f;

        if (playerRb != null)
            bonus = playerRb.linearVelocity.magnitude * SpeedPercentFromTarget;

        float finalSpeed = BaseSpeed + bonus;

        transform.position += MoveDirection.normalized * finalSpeed * Time.deltaTime;
    }

    private void KillPlayer(GameObject obj)
    {
        if (obj.CompareTag("Player"))
        {
            Destroy(obj);
            Debug.Log("Player killed by death wall.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        KillPlayer(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        KillPlayer(collision.gameObject);
    }
}