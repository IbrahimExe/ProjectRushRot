using UnityEngine;
using TMPro;

public class NpcDialogue : MonoBehaviour
{
    [Header("Dialogue Settings")]
    [Tooltip("The message shown inside the NPC speech bubble.")]
    [TextArea(2, 5)]
    public string dialogueMessage = "Hello, traveler!";

    [Header("Detection Settings")]
    [Tooltip("How close the player must be to trigger the speech bubble.")]
    [Min(0f)]
    public float detectionRange = 5f;

    [Header("Bubble Position")]
    [Tooltip("Offset from the NPC pivot. Raise Y to push the bubble above the NPC's head.")]
    public Vector3 bubbleOffset = new Vector3(0f, 2.5f, 0f);

    [Header("References")]
    [Tooltip("The World Space Canvas that acts as the speech bubble.")]
    public Canvas dialogueCanvas;

    [Tooltip("The TextMeshPro UGUI component that displays the message.")]
    public TextMeshProUGUI dialogueText;

    private Transform _playerTransform;
    private Transform _canvasTransform;

    void Start()
    {
        // Write the message into the text component.
        if (dialogueText != null)
            dialogueText.text = dialogueMessage;

        // Cache canvas transform.
        if (dialogueCanvas != null)
            _canvasTransform = dialogueCanvas.transform;

        // Hide the bubble at startup.
        SetBubbleVisible(false);

        // Cache the player transform by tag.
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            _playerTransform = player.transform;
        else
            Debug.LogWarning("[NpcDialogue] No GameObject with tag 'Player' found. " +
                             "Make sure the player has the 'Player' tag.", this);
    }

    void Update()
    {
        if (_playerTransform == null) return;

        float distance = Vector3.Distance(transform.position, _playerTransform.position);
        SetBubbleVisible(distance <= detectionRange);
    }

    // LateUpdate runs after all movement, so the bubble never lags behind.
    void LateUpdate()
    {
        if (_canvasTransform == null) return;

        // 1. Keep the bubble above the NPC at all times.
        _canvasTransform.position = transform.position + bubbleOffset;

        // 2. Billboard: rotate canvas so it always faces the camera.
        if (Camera.main != null)
        {
            Vector3 lookDir = _canvasTransform.position - Camera.main.transform.position;
            lookDir.y = 0f;   // Stay perfectly upright, no tilt.
            if (lookDir.sqrMagnitude > 0.001f)
                _canvasTransform.rotation = Quaternion.LookRotation(-lookDir);
        }
    }

    private void SetBubbleVisible(bool visible)
    {
        if (dialogueCanvas != null)
            dialogueCanvas.gameObject.SetActive(visible);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Yellow sphere = detection range.
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Cyan box = where the bubble will appear.
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position + bubbleOffset, new Vector3(0.6f, 0.3f, 0.01f));
    }
#endif
}

