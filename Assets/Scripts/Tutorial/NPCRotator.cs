using UnityEngine;

public class NPCRotator : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 30f;
    private Transform originalRoationPos;

    private GameObject player;
    void Start()
    {
        originalRoationPos = transform;
    }

    // Update is called once per frame
    void Update()
    {
        if (player != null)
        {
            Vector3 directionToPlayer = player.transform.position - transform.position;
            directionToPlayer.y = 0; // ignore vertical difference
            Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        else
        {
            // Rotate back to original position
            transform.rotation = Quaternion.RotateTowards(transform.rotation, originalRoationPos.rotation, rotationSpeed * Time.deltaTime);
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            player = other.gameObject;
        }
    }

    public void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            player = null;
        }
    }
}
