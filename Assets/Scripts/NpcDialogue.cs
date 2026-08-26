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
    [Tooltip("How high above the NPC pivot the bubble floats.")]
    public float heightAboveNpc = 2.5f;

    [Header("Bubble Size & Colours")]
    [Tooltip("Size of the speech bubble in canvas pixels (Width x Height).")]
    public Vector2 bubbleSize = new Vector2(220f, 80f);

    [Tooltip("Background colour of the speech bubble.")]
    public Color bubbleColor = Color.white;

    [Tooltip("Colour of the dialogue text.")]
    public Color textColor = Color.black;

    [Header("Bubble Shape")]
    [Range(0f, 0.45f)]
    [Tooltip("Corner roundness — 0 = sharp square, 0.45 = pill shape.")]
    public float cornerRadius = 0.25f;

    [Range(0f, 0.5f)]
    [Tooltip("Width of the speech spike as a fraction of the bubble width.")]
    public float spikeWidth = 0.25f;

    [Range(0f, 0.4f)]
    [Tooltip("Height of the speech spike as a fraction of the bubble height.")]
    public float spikeHeight = 0.28f;

    [Header("References")]
    [Tooltip("The Canvas child of this NPC (set to World Space in the Inspector).")]
    public Canvas dialogueCanvas;

    [Tooltip("The TextMeshPro UGUI component inside the Canvas.")]
    public TextMeshProUGUI dialogueText;

    // -------------------------------------------------------------------------
    private Transform _playerTransform;
    private Transform _canvasTransform;
    private Texture2D _bubbleTexture;

    // -------------------------------------------------------------------------
    void Start()
    {
        if (dialogueCanvas == null || dialogueText == null)
        {
            Debug.LogError("[NpcDialogue] Dialogue Canvas or Dialogue Text is not assigned!", this);
            return;
        }

        dialogueCanvas.renderMode      = RenderMode.WorldSpace;
        _canvasTransform               = dialogueCanvas.transform;
        _canvasTransform.localScale    = Vector3.one * 0.01f;
        _canvasTransform.localPosition = new Vector3(0f, heightAboveNpc, 0f);

        dialogueText.text  = dialogueMessage;
        dialogueText.color = textColor;

        BuildBubbleBackground();
        SetBubbleVisible(false);

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

    void LateUpdate()
    {
        if (_canvasTransform == null || Camera.main == null) return;
        _canvasTransform.rotation = Camera.main.transform.rotation;
    }

    void OnDestroy()
    {
        if (_bubbleTexture != null)
            Destroy(_bubbleTexture);
    }

    // -------------------------------------------------------------------------
    private void BuildBubbleBackground()
    {
        if (_bubbleTexture != null)
            Destroy(_bubbleTexture);

        Transform existing = _canvasTransform.Find("BubbleBackground");
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject bgGo = new GameObject("BubbleBackground");
        bgGo.transform.SetParent(_canvasTransform, false);
        bgGo.transform.SetAsFirstSibling();

        Image bgImage = bgGo.AddComponent<Image>();
        bgImage.color = bubbleColor;

        _bubbleTexture = GenerateBubbleTexture();
        bgImage.sprite = Sprite.Create(
            _bubbleTexture,
            new Rect(0, 0, _bubbleTexture.width, _bubbleTexture.height),
            new Vector2(0.5f, 0.5f),
            100f
        );
        bgImage.type = Image.Type.Simple;

        RectTransform bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin        = new Vector2(0.5f, 0.5f);
        bgRect.anchorMax        = new Vector2(0.5f, 0.5f);
        bgRect.anchoredPosition = Vector2.zero;
        bgRect.sizeDelta        = bubbleSize;
    }

    // -------------------------------------------------------------------------
    // Generates a speech-bubble shaped texture:
    //   Upper portion = rounded rectangle body.
    //   Lower portion = isosceles triangle spike pointing DOWN toward the NPC.
    //   Uses 2x2 supersampling for smooth, anti-aliased edges.
    private Texture2D GenerateBubbleTexture()
    {
        // Texture resolution matches bubble aspect ratio so corners are not distorted.
        int texW = 256;
        int texH = Mathf.Max(32, Mathf.RoundToInt(256f * bubbleSize.y / bubbleSize.x));

        int   spikePxH = Mathf.RoundToInt(texH * spikeHeight);
        int   bodyPxH  = texH - spikePxH;
        int   cornerPx = Mathf.RoundToInt(Mathf.Min(texW, bodyPxH) * cornerRadius);
        float halfSpW  = texW * spikeWidth * 0.5f;

        Texture2D tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[texW * texH];

        for (int y = 0; y < texH; y++)
        {
            for (int x = 0; x < texW; x++)
            {
                // 2x2 supersampling for smooth edges.
                float alpha = 0f;
                for (int sy = 0; sy < 2; sy++)
                for (int sx = 0; sx < 2; sx++)
                {
                    float fx = x + (sx == 0 ? 0.25f : 0.75f);
                    float fy = y + (sy == 0 ? 0.25f : 0.75f);
                    if (IsInsideBubble(fx, fy, texW, spikePxH, bodyPxH, cornerPx, halfSpW))
                        alpha += 0.25f;
                }

                // White pixel; the Image.color tints it to bubbleColor at display time.
                pixels[y * texW + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private bool IsInsideBubble(float x, float y, int w,
                                int spikePxH, int bodyPxH, int cornerPx, float halfSpW)
    {
        // Rounded-rectangle body occupies the top of the texture (y >= spikePxH).
        if (y >= spikePxH)
            return IsInsideRoundedRect(x, y - spikePxH, w, bodyPxH, cornerPx);

        // Triangle spike occupies the bottom (y < spikePxH), pointing downward.
        // Width is zero at the tip (y=0) and full (halfSpW*2) at the base (y=spikePxH).
        if (spikePxH > 0)
        {
            float progress = y / (float)spikePxH; // 0 = tip, 1 = base
            float centre   = w * 0.5f;
            return x >= centre - halfSpW * progress &&
                   x <= centre + halfSpW * progress;
        }

        return false;
    }

    private bool IsInsideRoundedRect(float x, float y, int w, int h, int r)
    {
        if (x < 0 || x >= w || y < 0 || y >= h) return false;
        if (r <= 0) return true;

        bool nearLeft   = x < r;
        bool nearRight  = x >= w - r;
        bool nearBottom = y < r;
        bool nearTop    = y >= h - r;

        // Corner zones: use circle test.
        if ((nearLeft || nearRight) && (nearBottom || nearTop))
        {
            float cx = nearLeft   ? r     : w - r;
            float cy = nearBottom ? r     : h - r;
            float dx = x - cx, dy = y - cy;
            return dx * dx + dy * dy <= (float)r * r;
        }

        return true; // all non-corner pixels are inside the rect
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
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position + Vector3.up * heightAboveNpc,
                            new Vector3(0.8f, 0.4f, 0.01f));
    }
#endif
}
