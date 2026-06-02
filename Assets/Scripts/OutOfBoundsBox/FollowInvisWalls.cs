using UnityEngine;

public class FollowInvisWalls : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The position of the player to follow")]
    [SerializeField] private Transform playerPos;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (playerPos == null)
        {
            playerPos = GameObject.FindFirstObjectByType<PlayerControllerBase>().transform;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (playerPos != null)
        {
            // follow the player on the y and z axis, but not on the x axis
            transform.position = new Vector3(transform.position.x, playerPos.position.y, playerPos.position.z);
        }
    }
}
