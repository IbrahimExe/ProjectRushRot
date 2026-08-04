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

    [Tooltip("Amount of force applied to push the tree over when it falls.")]
    [SerializeField] private float pushForce = 10f;

    [Tooltip("Optional: Transform at the top of the tree to apply the push force. If null, a default height offset is used.")]
    [SerializeField] private Transform pushPoint;

    [Tooltip("If pushPoint is null, how far up from the base should the push be applied?")]
    [SerializeField] private float pushHeightOffset = 5f;

    [Header("References")]
    [Tooltip("Big trigger collider used to detect player presence.")]
    [SerializeField] private Collider presenceTrigger;

    private bool hasDecided;
    private Transform player;
    private Rigidbody rb;

    private Quaternion startRot;

    private void Reset()
    {
        // Try to auto-fill the trigger if placed on same GameObject
        presenceTrigger = GetComponent<Collider>();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.freezeRotation = true;
            
            // Prevent the tree from flying into the air if the MapGenerator 
            // spawns its base slightly buried inside the heightmap floor!
            rb.maxDepenetrationVelocity = 0.1f; 
        }

        // Cache the upright rotation once
        startRot = transform.localRotation;
        
        if (presenceTrigger != null && !presenceTrigger.isTrigger)
            Debug.LogWarning($"{name}: presenceTrigger should be set to IsTrigger=true.");
    }

    private void OnEnable()
    {
        hasDecided = false;
        transform.localRotation = startRot;

        // Clean up the hinge joint if it was added in the previous cycle
        HingeJoint hinge = GetComponent<HingeJoint>();
        if (hinge != null)
        {
            Destroy(hinge);
        }

        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
            rb.freezeRotation = true;
        }
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

        // Which side of the X axis is the player on relative to the tree?
        bool playerOnPositiveX = player.position.x > transform.position.x;

        // With probability=biasTowardPlayer, fall toward the player's side
        bool fallTowardPlayer = Random.value < biasTowardPlayer;
        bool fallPositiveX = fallTowardPlayer ? playerOnPositiveX : !playerOnPositiveX;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.freezeRotation = false;

            // Add a HingeJoint to physically pin the base of the tree to the world.
            // This absorbs any extreme forces (like hitting invisible walls or the player) 
            // and makes it physically impossible for the tree to fly into the air!
            HingeJoint hinge = GetComponent<HingeJoint>();
            if (hinge == null) 
            {
                hinge = gameObject.AddComponent<HingeJoint>();
            }
            // place the hinge offseted slightly above the base to prevent it from digging into the ground and causing weird physics reactions
            hinge.anchor = new Vector3(0, 0.3f, 0);
            hinge.axis = transform.InverseTransformDirection(Vector3.forward); // Allow rotation left/right along Global X

            // Push direction based on the fall side (Global X axis)
            Vector3 pushDirection = fallPositiveX ? Vector3.right : Vector3.left;
            
            // Add a little upward force to help it tip over
            pushDirection += Vector3.up * 0.2f;

            // Determine where to apply the push force
            Vector3 applyPoint = pushPoint != null ? pushPoint.position : (transform.position + Vector3.up * pushHeightOffset);
            
            // Apply force to create a tipping torque
            rb.AddForceAtPosition(pushDirection.normalized * pushForce, applyPoint, ForceMode.Impulse);
        }
    }

    //private void OnCollisionEnter(Collision collision)
    //{
    //    if (!collision.collider.CompareTag("Player")) return;
    //    gameObject.SetActive(false);
    //}
}