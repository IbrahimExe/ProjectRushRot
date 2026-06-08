using System.Collections;
using UnityEngine;

public class FallingTree : MonoBehaviour
{
    [Header("Editor Fields (requested)")]
    [Range(0, 100)]
    [SerializeField] private int probabilityStayUpright = 20;

    [Tooltip("0 = no preference. 1 = always fall toward player's side.")]
    [Range(0f, 1f)]
    [SerializeField] private float biasTowardPlayer = 0.75f;

    [Tooltip("Seconds to rotate to the 90-degree fallen pose.")]
    [Min(0.01f)]
    [SerializeField] private float fallDuration = 0.25f;

    [Header("References")]
    [Tooltip("Big trigger collider used to detect player presence.")]
    [SerializeField] private Collider presenceTrigger;

    private bool hasDecided;
    private bool isFalling;
    private Transform player;

    private Quaternion startRot;
    private Quaternion targetRot;

    private void Reset()
    {
        // Try to auto-fill the trigger if placed on same GameObject
        presenceTrigger = GetComponent<Collider>();
    }

    private void Awake()
    {
        startRot = transform.rotation;
        if (presenceTrigger != null && !presenceTrigger.isTrigger)
            Debug.LogWarning($"{name}: presenceTrigger should be set to IsTrigger=true.");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasDecided) return;
        if (!other.CompareTag("Player")) return;

        player = other.transform;
        DecideAndMaybeFall();
    }

    private void DecideAndMaybeFall()
    {
        hasDecided = true;

        // Roll stay-upright
        int roll = Random.Range(1, 101); // 1..100
        if (roll <= probabilityStayUpright)
        {
            // stays upright; do nothing
            return;
        }

        // Otherwise decide left/right with bias toward player's side
        Vector3 toPlayer = (player.position - transform.position);
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f)
            toPlayer = transform.right; // fallback

        // Which side is the player on? (relative to tree's right vector)
        float side = Vector3.Dot(transform.right, toPlayer.normalized);
        bool playerOnRightSide = side >= 0f;

        // With probability=biasTowardPlayer, fall toward the player's side
        bool fallTowardPlayer = Random.value < biasTowardPlayer;
        bool fallRight = fallTowardPlayer ? playerOnRightSide : !playerOnRightSide;

        // Rotate 90 degrees around local Z? For typical 3D trees (up = Y), tipping sideways
        // around local forward (Z) leans left/right in X direction if your tree faces camera.
        // More generally in 3D, tipping around local forward is a common setup.
        float angle = fallRight ? -90f : 90f; // sign choice: adjust if your tree falls opposite
        targetRot = startRot * Quaternion.AngleAxis(angle, transform.forward);

        StartCoroutine(FallRoutine());
    }

    private IEnumerator FallRoutine()
    {
        isFalling = true;
        float t = 0f;

        Quaternion from = transform.rotation;
        Quaternion to = targetRot;

        while (t < 1f)
        {
            t += Time.deltaTime / fallDuration;
            transform.rotation = Quaternion.Slerp(from, to, Mathf.Clamp01(t));
            yield return null;
        }

        transform.rotation = to;
        isFalling = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.collider.CompareTag("Player")) return;
        Destroy(gameObject);
    }
}