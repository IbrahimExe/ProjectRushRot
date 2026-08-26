using UnityEngine;
using UnityEngine.UI;
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
    [Tooltip("How high above the NPC pivot the bubble floats. Default 2.5 works for a standard capsule.")]
    public float heightAboveNpc = 2.5f;

    [Header("Bubble Appearance")]
    [Tooltip("Size of the speech bubble in canvas pixels (Width x Height). Tune this to fit your text.")]
    public Vector2 bubbleSize = new Vector2(220f, 80f);

    [Tooltip("Padding in canvas pixels between the text and the bubble edge.")]
    public float bubblePadding = 20f;

    [Tooltip("Background colour of the speech bubble.")]
    public Color bubbleColor = Color.white;

    [Tooltip("Colour of the dialogue text.")]
    public Color textColor = Color.black;

    [Header("References")]
    [Tooltip("The Canvas child of this NPC (set to World Space in the Inspector).")]
    public Canvas dialogueCanvas;

    [Tooltip("The TextMeshPro UGUI component inside the Canvas.")]
    public TextMeshProUGUI dialogueText;

    // -------------------------------------------------------------------------
    private Transform _playerTransform;
    private Transform _canvasTransform;

    // -------------------------------------------------------------------------
    void Start()
    {
        if (dialogueCanvas == null || dialogueText == null)
        {
            Debug.LogError("[NpcDialogue] Dialogue Canvas or Dialogue Text is not assigned!", this);
            return;
        }

        // Force World Space so the bubble lives in 3D, not as a screen overlay.
        dialogueCanvas.renderMode = RenderMode.WorldSpace;

        _canvasTransform = dialogueCanvas.transform;

        // Scale down: 1 canvas pixel = 0.01 world unit.
        // A 300x100 px canvas becomes a compact 3x1 world-unit label.
        _canvasTransform.localScale = Vector3.one * 0.01f;

        // Place the bubble above the NPC pivot.
        _canvasTransform.localPosition = new Vector3(0f, heightAboveNpc, 0f);

        // Apply text content and colour.
        dialogueText.text  = dialogueMessage;
        dialogueText.color = textColor;

        // Build the white rounded bubble background behind the text.
        BuildBubbleBackground();

        // Hide until the player steps close.
        SetBubbleVisible(false);

        // Cache the player transform by tag.
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            _playerTransform = player.transform;
        else
            Debug.LogWarning("[NpcDialogue] No GameObject tagged 'Player' found. " +
                             "Assign the 'Player' tag to the player object.", this);
    }

    // -------------------------------------------------------------------------
    void Update()
    {
        if (_playerTransform == null) return;

        float distance = Vector3.Distance(transform.position, _playerTransform.position);
        SetBubbleVisible(distance <= detectionRange);
    }

    // LateUpdate: after all movement — bubble never lags a frame behind.
    void LateUpdate()
    {
        if (_canvasTransform == null || Camera.main == null) return;

        // Copy the camera's own rotation so the canvas faces it perfectly.
        // This is the correct billboard approach and fixes the Y-axis 180 flip
        // that Quaternion.LookRotation was causing.
        _canvasTransform.rotation = Camera.main.transform.rotation;
    }

    // -------------------------------------------------------------------------
    /// <summary>Creates a white rounded-rectangle Image behind the TMP text at runtime.</summary>
    private void BuildBubbleBackground()
    {
        // Remove stale background if this is called more than once (e.g. hot reload).
        Transform existing = _canvasTransform.Find("BubbleBackground");
        if (existing != null)
            Destroy(existing.gameObject);

        // --- Background panel ---
        GameObject bgGo = new GameObject("BubbleBackground");
        bgGo.transform.SetParent(_canvasTransform, false);
        bgGo.transform.SetAsFirstSibling(); // behind the text

        Image bgImage = bgGo.AddComponent<Image>();
        bgImage.color = bubbleColor;

        // Unity's built-in rounded-rectangle sprite gives a softer bubble look.
        Sprite roundedSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
        if (roundedSprite != null)
        {
            bgImage.sprite = roundedSprite;
            bgImage.type   = Image.Type.Sliced;
        }

        // Use the inspector-defined bubble size, centered in the canvas.
        RectTransform bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin        = new Vector2(0.5f, 0.5f);
        bgRect.anchorMax        = new Vector2(0.5f, 0.5f);
        bgRect.anchoredPosition = Vector2.zero;
        bgRect.sizeDelta        = bubbleSize;
    }

    // -------------------------------------------------------------------------
    private void SetBubbleVisible(bool visible)
    {
        if (dialogueCanvas != null)
            dialogueCanvas.gameObject.SetActive(visible);
    }

    // -------------------------------------------------------------------------
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Yellow sphere = detection range.
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Cyan box = approximate bubble position above the NPC.
        Gizmos.color = Color.cyan;
        Vector3 bubblePos = transform.position + Vector3.up * heightAboveNpc;
        Gizmos.DrawWireCube(bubblePos, new Vector3(0.8f, 0.4f, 0.01f));
    }
#endif
}
