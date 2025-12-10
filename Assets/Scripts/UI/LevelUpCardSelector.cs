using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LevelUpCardSelector : MonoBehaviour
{
    [Header("Source deck (ScriptableObjects)")]
    [Tooltip("Drag your CardSO ScriptableObjects here.")]
    public List<CardSO> deck = new List<CardSO>();

    [Header("Card target positions (place these empty RectTransforms where you want cards to land)")]
    [Tooltip("Provide up to 3 RectTransforms (CardPos_1, CardPos_2, CardPos_3).")]
    public RectTransform[] cardPositions = new RectTransform[3];

    [Header("Optional Prefab")]
    [Tooltip("Optional card prefab that contains a CardUI component. If null, a simple Image GameObject will be created.")]
    public GameObject optionalCardPrefab;

    [Header("Panel (optional)")]
    [Tooltip("Optional panel RectTransform to show behind cards. It will be animated in/out behind the cards.")]
    public RectTransform panelBehindCards;

    [Header("Layout & animation")]
    public float horizontalSpacing = 220f;     // fallback spacing if positions not set
    public float slideInDuration = 0.28f;
    public float slideOutDuration = 0.22f;
    public float easeOvershoot = 0.1f;
    public float offscreenOffset = 300f;      // how far off to the right cards start

    [Header("Selection visuals")]
    public float highlightScale = 1.25f;
    public float scaleLerpSpeed = 12f;

    [Header("Timing")]
    public float selectedHoldTime = 0.5f;

    [Header("Curve")]
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // runtime
    private List<RectTransform> spawned = new List<RectTransform>();
    private List<GameObject> spawnedGameObjects = new List<GameObject>();
    private int selectedIndex = 0;
    private bool isOpen = false;
    private int queuedLevels = 0;
    private Coroutine showRoutine;

    // panel animation state (cached once)
    private Vector2 panelOriginalAnchoredPos;
    private bool panelWasActiveInitially = false;

    private void Awake()
    {
        // Cache panel home position once so it never drifts
        if (panelBehindCards != null)
        {
            panelOriginalAnchoredPos = panelBehindCards.anchoredPosition;
            panelWasActiveInitially = panelBehindCards.gameObject.activeSelf;
        }
    }

    private void OnValidate()
    {
        // Keep editor changes reflected
        if (panelBehindCards != null)
        {
            panelOriginalAnchoredPos = panelBehindCards.anchoredPosition;
            panelWasActiveInitially = panelBehindCards.gameObject.activeSelf;
        }
    }

    // PUBLIC API
    public void TriggerLevelUp()
    {
        if (deck == null || deck.Count == 0)
        {
            Debug.LogWarning("[LevelUpCardSelector] Deck empty.");
            queuedLevels++;
            return;
        }

        if (isOpen)
        {
            queuedLevels++;
            return;
        }

        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(ShowRandomCardSet());
    }

    private IEnumerator ShowRandomCardSet()
    {
        isOpen = true;
        selectedIndex = 0;
        spawned.Clear();
        spawnedGameObjects.Clear();

        // ensure panel is prepared and animate it in behind cards (if assigned)
        if (panelBehindCards != null)
        {
            // ensure active
            if (!panelBehindCards.gameObject.activeSelf) panelBehindCards.gameObject.SetActive(true);

            // start the panel offscreen to the right using the cached home position (do NOT overwrite the cached home)
            panelBehindCards.anchoredPosition = panelOriginalAnchoredPos + new Vector2(offscreenOffset, 0f);

            // place panel behind by making it the first sibling in its parent
            panelBehindCards.SetAsFirstSibling();

            // slide panel into its cached home position
            StartCoroutine(Slide(panelBehindCards, panelOriginalAnchoredPos, slideInDuration, true));
        }

        // pick up to 3 unique cards
        var picks = deck.OrderBy(x => Random.value).Take(Mathf.Min(3, deck.Count)).ToList();
        int count = picks.Count;

        // determine whether specific card positions were provided
        bool usePositions = cardPositions != null && cardPositions.Length >= count && cardPositions.Take(count).All(p => p != null);

        // fallback parent for spawned cards (attempt to use panel's parent if available)
        Transform fallbackParent = (panelBehindCards != null) ? panelBehindCards.parent : (transform.parent ? transform.parent : transform);

        for (int i = 0; i < count; i++)
        {
            RectTransform targetSlot = usePositions ? cardPositions[i] : null;
            Transform parentTransform = (targetSlot != null) ? targetSlot.parent : fallbackParent;

            if (parentTransform == null)
            {
                var canvas = FindFirstObjectByType<Canvas>();
                parentTransform = (canvas != null) ? canvas.transform : transform;
            }

            GameObject go = optionalCardPrefab != null
                ? Instantiate(optionalCardPrefab, parentTransform, false)
                : new GameObject("CardImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parentTransform, false);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            // copy anchor/pivot from slot if present, otherwise anchor to right-middle by default
            if (targetSlot != null)
            {
                rt.anchorMin = targetSlot.anchorMin;
                rt.anchorMax = targetSlot.anchorMax;
                rt.pivot = targetSlot.pivot;
            }
            else
            {
                rt.anchorMin = new Vector2(1f, 0.5f);
                rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
            }

            // initialize CardUI if present
            CardUI ui = go.GetComponent<CardUI>() ?? go.GetComponentInChildren<CardUI>();
            if (ui != null)
            {
                var stats = FindFirstObjectByType<UpgradeStats>();
                ui.Initialize(picks[i], stats);
            }
            else
            {
                var img = go.GetComponent<Image>();
                if (img != null && picks[i] != null && picks[i].cardImage != null)
                {
                    img.sprite = picks[i].cardImage;
                    img.preserveAspect = true;
                    img.color = Color.white;
                }
            }

            // compute target anchored position
            Vector2 targetAnchored;
            if (targetSlot != null)
            {
                targetAnchored = targetSlot.anchoredPosition;
            }
            else
            {
                RectTransform parentRect = rt.parent as RectTransform;
                float groupWidth = (count - 1) * horizontalSpacing;
                float startX = -groupWidth;
                float x = startX + i * horizontalSpacing;
                float y = 0f;
                targetAnchored = new Vector2(x, y);
            }

            // start offscreen to the right relative to the cached home of the parent
            float startOff = offscreenOffset;
            rt.anchoredPosition = targetAnchored + new Vector2(startOff, 0f);

            // ensure cards render above the panel
            if (panelBehindCards != null && panelBehindCards.parent == rt.parent)
            {
                // panel is first sibling, make sure this card is last so it appears on top
                rt.SetAsLastSibling();
            }

            spawned.Add(rt);
            spawnedGameObjects.Add(go);

            // slide in
            StartCoroutine(Slide(rt, targetAnchored, slideInDuration, true));

            yield return new WaitForSeconds(0.06f);
        }

        // small settle
        yield return new WaitForSeconds(0.02f);

        UpdateHighlightImmediate();

        // Input loop: use mouse wheel for navigation, middle click or Enter to select
        while (isOpen)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (scroll > 0.001f)
            {
                selectedIndex = Mathf.Max(0, selectedIndex - 1);
                UpdateHighlightImmediate();
            }
            else if (scroll < -0.001f)
            {
                selectedIndex = Mathf.Min(spawned.Count - 1, selectedIndex + 1);
                UpdateHighlightImmediate();
            }

            if (Input.GetMouseButtonDown(2) // Middle Mouse Scroll Wheel Click
                || Input.GetMouseButtonDown(0) // Left Click
                || Input.GetKeyDown(KeyCode.Return) 
                || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                StartCoroutine(HandleSelection(selectedIndex));
                yield break;
            }

            for (int i = 0; i < spawned.Count; i++)
            {
                Vector3 target = (i == selectedIndex) ? Vector3.one * highlightScale : Vector3.one;
                spawned[i].localScale = Vector3.Lerp(spawned[i].localScale, target, Time.unscaledDeltaTime * scaleLerpSpeed);
            }

            yield return null;
        }
    }

    private IEnumerator HandleSelection(int index)
    {
        // apply card effect first (if present)
        if (index >= 0 && index < spawnedGameObjects.Count)
        {
            var cardGO = spawnedGameObjects[index];
            var cardUI = cardGO.GetComponent<CardUI>() ?? cardGO.GetComponentInChildren<CardUI>();
            if (cardUI != null)
                cardUI.ApplyCardEffect();
            else
                Debug.LogWarning("Selected card has no CardUI to apply effect.");
        }

        // freeze input
        isOpen = false;

        // slide non-selected away to the right while keeping selected in place
        for (int i = 0; i < spawned.Count; i++)
        {
            if (i == index) continue;
            RectTransform rt = spawned[i];
            Vector2 start = rt.anchoredPosition;
            Vector2 target = start + new Vector2(offscreenOffset * 1.2f, 40f);
            StartCoroutine(Slide(rt, target, slideOutDuration, false));
        }

        // pop selected
        if (index >= 0 && index < spawned.Count)
            spawned[index].localScale = Vector3.one * (highlightScale + 0.04f);

        yield return new WaitForSecondsRealtime(selectedHoldTime);

        // animate panel out (if assigned) while cleaning up
        if (panelBehindCards != null)
        {
            Vector2 offPos = panelOriginalAnchoredPos + new Vector2(offscreenOffset, 0f);
            StartCoroutine(Slide(panelBehindCards, offPos, slideOutDuration, false));
            // hide after a short delay and reset to original anchored pos so it doesn't drift
            StartCoroutine(DisableAfterDelay(panelBehindCards.gameObject, slideOutDuration + 0.02f));
        }

        // cleanup
        StartCoroutine(CleanupAndMaybeOpenNext());
    }

    private IEnumerator DisableAfterDelay(GameObject go, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (go != null)
        {
            // Only disable if it wasn't active before we opened it (respect original state)
            if (!panelWasActiveInitially)
                go.SetActive(false);

            // always reset anchoredPosition back to the cached home so future opens use correct home
            if (panelBehindCards != null)
                panelBehindCards.anchoredPosition = panelOriginalAnchoredPos;
        }
    }

    private IEnumerator CleanupAndMaybeOpenNext()
    {
        // destroy spawned objects
        foreach (var go in spawnedGameObjects)
            if (go != null) Destroy(go);

        spawned.Clear();
        spawnedGameObjects.Clear();

        // queued levels
        if (queuedLevels > 0)
        {
            queuedLevels--;
            yield return null;
            showRoutine = StartCoroutine(ShowRandomCardSet());
        }
        else
        {
            showRoutine = null;
        }
    }

    // Slide helper with ease curve + small overshoot
    private IEnumerator Slide(RectTransform rt, Vector2 targetAnchored, float duration, bool includeOvershoot)
    {
        Vector2 start = rt.anchoredPosition;
        float t = 0f;
        float dur = Mathf.Max(0.0001f, duration);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / dur);
            float e = easeCurve.Evaluate(p);

            if (includeOvershoot && p > 0.95f)
            {
                float overshootT = (p - 0.95f) / 0.05f;
                float os = Mathf.Sin(overshootT * Mathf.PI) * easeOvershoot;
                rt.anchoredPosition = Vector2.Lerp(start, targetAnchored, e) + new Vector2(-os * 100f, 0f);
            }
            else
            {
                rt.anchoredPosition = Vector2.Lerp(start, targetAnchored, e);
            }

            yield return null;
        }
        rt.anchoredPosition = targetAnchored;
    }

    private void UpdateHighlightImmediate()
    {
        for (int i = 0; i < spawned.Count; i++)
            spawned[i].localScale = Vector3.one * (i == selectedIndex ? highlightScale : 1f);
    }

#if UNITY_EDITOR
    [ContextMenu("Debug - force open")]
    public void DebugOpen() => TriggerLevelUp();

    [ContextMenu("Clear queued")]
    public void DebugClear() { queuedLevels = 0; }
#endif
}
