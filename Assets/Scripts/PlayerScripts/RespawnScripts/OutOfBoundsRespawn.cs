using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class OutOfBoundsRespawn : MonoBehaviour
{
    [SerializeField] private PlayerControllerBase playerController;

    [Header("Checkpoint Saving")]
    [SerializeField] private float timeToSavePosition = 5f;
    [SerializeField] private Transform originOfRaycast;
    [SerializeField] private float lengthOfRaycast = 100f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Respawn Rules")]
    [SerializeField] private int maxCheckpoints = 3;
    [SerializeField] private int maxRespawnAttempts = 2;
    [SerializeField] private float timeBetweenRespawnAttempts = 3f;

    private List<Vector3> savedCheckpoints = new List<Vector3>();

    private float groundedSaveTimer = 0f;
    private int currentRespawnAttempts = 0;
    private float lastRespawnTime = -Mathf.Infinity;
    [SerializeField] private float respawnCooldown = 0.2f;

    private void Start()
    {
        Ray ray = new Ray(originOfRaycast.position, Vector3.down);

        if (Physics.Raycast(ray, out RaycastHit hit, lengthOfRaycast, groundLayer))
        {
             SaveCheckpoint(hit.point.y);
        }
    }

    void Update()
    {
        HandleCheckpointSaving();
    }

    void HandleCheckpointSaving()
    {
        Ray ray = new Ray(originOfRaycast.position, Vector3.down);

        if (Physics.Raycast(ray, out RaycastHit hit, lengthOfRaycast, groundLayer))
        {
            groundedSaveTimer += Time.deltaTime;

            if (groundedSaveTimer >= timeToSavePosition)
            {
                SaveCheckpoint(hit.point.y);
                groundedSaveTimer = 0f;
            }
        }
        else
        {
            // Pause timer while not grounded
            groundedSaveTimer = 0f;
        }
    }

    void SaveCheckpoint(float groundY)
    {
        Vector3 checkpoint = new Vector3(
            transform.position.x,
            groundY + 5, // <---------------------------------------- increase the number if the player still respawns inside the ground
            transform.position.z
        );

        if (savedCheckpoints.Count >= maxCheckpoints)
        {
            // Remove oldest
            savedCheckpoints.RemoveAt(0);
        }

        savedCheckpoints.Add(checkpoint);
        currentRespawnAttempts = 0;
        Debug.Log("Checkpoint saved at: " + checkpoint);
    }

    public void RespawnPlayer()
    {
        if (savedCheckpoints.Count == 0)
        {
            return;
        }

        if (Time.time - lastRespawnTime < respawnCooldown)
        {
            return;
        }

        lastRespawnTime = Time.time;
        StartCoroutine(RespawnRoutine());
    }


    IEnumerator RespawnRoutine()
    {
        currentRespawnAttempts++;

        // If this checkpoint is killing the player, discard it
        if (currentRespawnAttempts >= maxRespawnAttempts && savedCheckpoints.Count > 1)
        {
            // Remove newest (bad) checkpoint
            savedCheckpoints.RemoveAt(savedCheckpoints.Count - 1);
            currentRespawnAttempts = 0;
        }

        Vector3 respawnPoint = savedCheckpoints[savedCheckpoints.Count - 1];
        transform.position = respawnPoint;
        playerController.Respawn();

        yield return new WaitForSeconds(timeBetweenRespawnAttempts);
    }
}
