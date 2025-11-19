using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

[DisallowMultipleComponent]
public class LevelUpImageSelector : MonoBehaviour
{
    [Header("Source sprites (drag your raw images here)")]
    [Tooltip("Drag your card sprites (raw images) here.")]
    public List<Sprite> cardSprites = new List<Sprite>();

    [Header("UI")]
    [Tooltip("RectTransform of the panel/container anchored to the right where cards will appear.")]
    public RectTransform container;

    [Tooltip("Optional: a prefab with an Image component for each card. If null, a default Image will be created.")]
    public GameObject optionalCardPrefab;

    [Header("Layout")]
    public float verticalSpacing = 170f;
    public float slideDistance = 700f;         // how far offscreen they start and slide out to
    public float slideInDuration = 0.28f;
    public float slideOutDuration = 0.22f;
    public float easeOvershoot = 0.1f;         // small overshoot for 'easy-ease' feeling

    [Header("Selection visuals")]
    public float highlightScale = 1.15f;
    public float scaleLerpSpeed = 12f;

    [Header("Timing")]
    public float selectedHoldTime = 0.5f;      // selected card stays while others slide away

    [Header("Animation curve (optional)")]
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // runtime
    private List<RectTransform> spawned = new List<RectTransform>();
    private int selectedIndex = 0;
    private bool isOpen = false;
    private int queuedLevels = 0;
    private Coroutine showRoutine;

    // PUBLIC API: call this when a level-up occurs.
    // Example: ExperienceManager.Instance.OnLevelUp += () => levelUpUI.TriggerLevelUp();
    public void TriggerLevelUp()
    {
        // If nothing is available to show (no sprites), just increment queue and return
        if (cardSprites == null || cardSprites.Count == 0)
        {
            Debug.LogWarning("[LevelUpImageSelector] No cardSprites assigned.");
            queuedLevels++;
            return;
        }

        // If menu already open, queue this level-up to show later
        if (isOpen)
        {
            queuedLevels++;
            return;
        }

        // open now
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(ShowRandomCardSet());
    }

    private IEnumerator ShowRandomCardSet()
    {
        isOpen = true;
        selectedIndex = 0;
        spawned.Clear();

        // activate container if disabled
        if (container != null && !container.gameObject.activeSelf) container.gameObject.SetActive(true);

        // pick up to 3 unique sprites
        var pool = new List<Sprite>(cardSprites);
        var picks = pool.OrderBy(x => Random.value).Take(Mathf.Min(3, pool.Count)).ToList();

        // spawn card UI objects (anchored positions are relative to container)
        for (int i = 0; i < picks.Count; i++)
        {
            GameObject go;
            if (optionalCardPrefab != null)
            {
                go = Instantiate(optionalCardPrefab, container, false);
            }
            else
            {
                // create a simple Image GameObject if prefab not supplied
                go = new GameObject("CardImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(container, false);
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                // optional visual tweak: add a white background with subtle border if you want later
            }

            var rt = go.GetComponent<RectTransform>();
            // anchor and pivot: center-left works best if container is right-anchored
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(220f, 120f); // default card size; change in inspector or prefab

            // place offscreen to the right initially
            float y = -i * verticalSpacing;
            rt.anchoredPosition = new Vector2(slideDistance, y);

            // set sprite
            var image = go.GetComponent<Image>();
            image.sprite = picks[i];
            image.preserveAspect = true;

            spawned.Add(rt);

            // slide in with small stagger
            StartCoroutine(Slide(rt, new Vector2(0f, y), slideInDuration, true));
            yield return new WaitForSeconds(0.06f);
        }

        // small settle time
        yield return new WaitForSeconds(0.02f);

        // highlight initially
        UpdateHighlightImmediate();

        // input loop - wait until user selects
        while (isOpen)
        {
            // navigation input
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                selectedIndex = Mathf.Max(0, selectedIndex - 1);
                UpdateHighlightImmediate();
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                selectedIndex = Mathf.Min(spawned.Count - 1, selectedIndex + 1);
                UpdateHighlightImmediate();
            }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                // choose
                StartCoroutine(HandleSelection(selectedIndex));
                yield break; // selection coroutine will handle the rest (and then check queue)
            }

            // smooth scaling for highlight
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
        // Freeze further inputs while animating
        isOpen = false;

        // Keep the selected card on screen for selectedHoldTime while others slide away to the right
        for (int i = 0; i < spawned.Count; i++)
        {
            if (i == index) continue;
            Vector2 start = spawned[i].anchoredPosition;
            Vector2 target = new Vector2(slideDistance * 1.1f, start.y + 40f); // slide right + small drop
            StartCoroutine(Slide(spawned[i], target, slideOutDuration, false));
        }

        // optional small pop for selected card
        // ensure selected card is fully scaled to highlightScale instantly
        if (index >= 0 && index < spawned.Count)
        {
            spawned[index].localScale = Vector3.one * (highlightScale + 0.04f);
        }

        yield return new WaitForSecondsRealtime(selectedHoldTime);

        // destroy all spawned cards
        foreach (var r in spawned)
            if (r != null) Destroy(r.gameObject);

        spawned.Clear();

        // hide container if you like
        if (container != null) container.gameObject.SetActive(false);

        // if queued levels exist, open next set
        if (queuedLevels > 0)
        {
            queuedLevels--;
            // wait a single frame to ensure cleanup before next open
            yield return null;
            showRoutine = StartCoroutine(ShowRandomCardSet());
        }
        else
        {
            // done
            showRoutine = null;
        }
    }

    // Slide helper with easy-ease (uses easeCurve and a tiny overshoot)
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
            // optional overshoot
            if (includeOvershoot && p > 0.95f)
            {
                // small overshoot and back
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
        {
            spawned[i].localScale = Vector3.one * (i == selectedIndex ? highlightScale : 1f);
        }
    }

    // Editor convenience: open debug queue or force spawn
#if UNITY_EDITOR
    [ContextMenu("Debug - force open")]
    public void DebugOpen() => TriggerLevelUp();

    [ContextMenu("Clear queued")]
    public void DebugClear() { queuedLevels = 0; }
#endif
}
