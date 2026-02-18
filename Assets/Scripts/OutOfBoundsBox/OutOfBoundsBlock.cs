using UnityEngine;

public class OutOfBoundsBlock : MonoBehaviour
{
    [SerializeField] private Transform playerPos;
    [SerializeField] private float yOffset = -10f;
    [SerializeField] private float minY = -30f;


    // Update is called once per frame
    void Update()
    {
        if (playerPos == null) return;

        // Desired Y position
        float targetY = playerPos.position.y + yOffset;

        // Clamp so it never goes below minY
        targetY = Mathf.Max(targetY, minY);

        // Follow player
        transform.position = new Vector3(
            playerPos.position.x,
            targetY,
            playerPos.position.z
        );
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            other.GetComponent<OutOfBoundsRespawn>()?.RespawnPlayer();
        }
    }
}
