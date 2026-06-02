using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LevelUpCardSelector : MonoBehaviour
{
    [Header("Rarity weights")]
    public float commonWeight = 70f;
    public float rareWeight = 25f;
    public float legendaryWeight = 5f;

    [Header("Source deck (ScriptableObjects)")]
    public List<CardSO> deck = new List<CardSO>();

    [Header("Card target positions")]
    public RectTransform[] cardPositions = new RectTransform[3];

    [Header("Optional Prefab")]
    public GameObject optionalCardPrefab;

    [Header("Panel (optional)")]
    public RectTransform panelBehindCards;

    [Header("Layout & animation")]
    public float horizontalSpacing = 220f;
    public float slideInDuration = 0.28f;
    public float slideOutDuration = 0.22f;
    public float easeOvershoot = 0.1f;
    public float offscreenOffset = 300f;

    [Header("Selection visuals")]
    public float highlightScale = 1.25f;
    public float scaleLerpSpeed = 12f;

    [Header("Timing")]
    public float selectedHoldTime = 0.5f;

    [Header("Curve")]
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // ── Runtime ────────────────────────────────────────────────────────

    private List<RectTransform> spawned = new List<RectTransform>();
    private List<GameObject> spawnedObjects = new List<GameObject>();
    private int selectedIndex = 0;

    // isOpen : cards on screen, accepting input
    // isBusy : coroutine is running ANY phase — gates TriggerLevelUp queuing
    private bool isOpen = false;
    private bool isBusy = false;

    private int queuedLevels = 0;

    private Vector2 panelHome;
    private bool panelHomeCached = false;

    // ── Init ───────────────────────────────────────────────────────────

    private void Awake() => CachePanelHome();
    private void OnValidate() => CachePanelHome();

    private void CachePanelHome()
    {
        if (panelBehindCards != null && !panelHomeCached)
        {
            panelHome = panelBehindCards.anchoredPosition;
            panelHomeCached = true;
        }
    }

    // ── Public API ─────────────────────────────────────────────────────

    public void TriggerLevelUp()
    {
        if (deck == null || deck.Count == 0)
        {
            Debug.LogWarning("[LevelUpCardSelector] Deck is empty.");
            return;
        }

        if (isBusy)
        {
            queuedLevels++;
            return;
        }

        StartCoroutine(RunCardSet());
    }

    // ── Master coroutine ───────────────────────────────────────────────

    private IEnumerator RunCardSet()
    {
        isBusy = true;
        isOpen = false;

        // Safety: destroy any leftover cards from a previous broken state
        DestroyAllCards();

        // ── 1. Panel slide-in ─────────────────────────────────────────
        if (panelBehindCards != null)
        {
            panelBehindCards.gameObject.SetActive(true);
            panelBehindCards.SetAsFirstSibling();

            bool alreadyHome = Vector2.Distance(panelBehindCards.anchoredPosition, panelHome) < 2f;
            if (!alreadyHome)
            {
                panelBehindCards.anchoredPosition = panelHome + new Vector2(offscreenOffset, 0f);
                yield return StartCoroutine(Slide(panelBehindCards, panelHome, slideInDuration, true));
            }
        }

        // ── 2. Spawn and slide cards in ───────────────────────────────
        var picks = PickCards(Mathf.Min(3, deck.Count));
        int count = picks.Count;

        bool usePositions = cardPositions != null
            && cardPositions.Length >= count
            && cardPositions.Take(count).All(p => p != null);

        Transform fallbackParent = panelBehindCards != null
            ? panelBehindCards.parent
            : (transform.parent != null ? transform.parent : transform);

        for (int i = 0; i < count; i++)
        {
            RectTransform targetSlot = usePositions ? cardPositions[i] : null;
            Transform parentTransform = targetSlot != null ? targetSlot.parent : fallbackParent;

            if (parentTransform == null)
            {
                var canvas = FindFirstObjectByType<Canvas>();
                parentTransform = canvas != null ? canvas.transform : transform;
            }

            GameObject go = optionalCardPrefab != null
                ? Instantiate(optionalCardPrefab, parentTransform, false)
                : new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parentTransform, false);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

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

            var ui = go.GetComponent<CardUI>() ?? go.GetComponentInChildren<CardUI>();
            if (ui != null)
            {
                ui.Initialize(picks[i], FindFirstObjectByType<UpgradeStats>());
            }
            else
            {
                var img = go.GetComponent<Image>();
                if (img != null && picks[i]?.cardImage != null)
                {
                    img.sprite = picks[i].cardImage;
                    img.preserveAspect = true;
                    img.color = Color.white;
                }
            }

            Vector2 dest;
            if (targetSlot != null)
            {
                dest = targetSlot.anchoredPosition;
            }
            else
            {
                float groupWidth = (count - 1) * horizontalSpacing;
                dest = new Vector2(-groupWidth + i * horizontalSpacing, 0f);
            }

            rt.anchoredPosition = dest + new Vector2(offscreenOffset, 0f);

            if (panelBehindCards != null && panelBehindCards.parent == rt.parent)
                rt.SetAsLastSibling();

            spawned.Add(rt);
            spawnedObjects.Add(go);

            StartCoroutine(Slide(rt, dest, slideInDuration, true));
            yield return new WaitForSecondsRealtime(0.06f);
        }

        yield return new WaitForSecondsRealtime(0.02f);

        // ── 3. Open for input ─────────────────────────────────────────
        selectedIndex = 0;
        UpdateHighlightImmediate();
        isOpen = true;

        // ── 4. Input loop ─────────────────────────────────────────────
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

            if (Input.GetMouseButtonDown(0)
             || Input.GetMouseButtonDown(2)
             || Input.GetKeyDown(KeyCode.Return)
             || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                isOpen = false;
                break;
            }

            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] == null) continue;
                Vector3 t = i == selectedIndex ? Vector3.one * highlightScale : Vector3.one;
                spawned[i].localScale = Vector3.Lerp(
                    spawned[i].localScale, t, Time.unscaledDeltaTime * scaleLerpSpeed);
            }

            yield return null;
        }

        // ── 5. Apply card effect ──────────────────────────────────────
        if (selectedIndex >= 0 && selectedIndex < spawnedObjects.Count)
        {
            var cardGO = spawnedObjects[selectedIndex];
            if (cardGO != null)
            {
                var cardUI = cardGO.GetComponent<CardUI>() ?? cardGO.GetComponentInChildren<CardUI>();
                if (cardUI != null) cardUI.ApplyCardEffect();
            }
        }

        // Pop selected briefly so player sees their pick
        if (selectedIndex >= 0 && selectedIndex < spawned.Count && spawned[selectedIndex] != null)
            spawned[selectedIndex].localScale = Vector3.one * (highlightScale + 0.04f);

        yield return new WaitForSecondsRealtime(selectedHoldTime);

        // ── 6. Slide ALL cards off, yield each, wait for completion
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] == null) continue;
            Vector2 offTarget = spawned[i].anchoredPosition + new Vector2(offscreenOffset * 1.5f, 0f);
            yield return StartCoroutine(Slide(spawned[i], offTarget, slideOutDuration, false));
        }

        // ── 7. All slides done — safe to destroy ──────────────────────
        DestroyAllCards();

        // ── 8. Next in queue or close ─────────────────────────────────
        if (queuedLevels > 0)
        {
            queuedLevels--;
            isBusy = false;
            yield return null; // one frame to clear input state
            StartCoroutine(RunCardSet());
        }
        else
        {
            if (panelBehindCards != null)
            {
                Vector2 offPos = panelHome + new Vector2(offscreenOffset, 0f);
                yield return StartCoroutine(Slide(panelBehindCards, offPos, slideOutDuration, false));
                panelBehindCards.gameObject.SetActive(false);
                panelBehindCards.anchoredPosition = panelHome;
            }

            isBusy = false;
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private void DestroyAllCards()
    {
        foreach (var go in spawnedObjects)
            if (go != null) Destroy(go);

        spawnedObjects.Clear();
        spawned.Clear();
    }

    private CardRarity RollRarity()
    {
        float total = commonWeight + rareWeight + legendaryWeight;
        float roll = Random.value * total;
        if (roll < commonWeight) return CardRarity.Common;
        if (roll < commonWeight + rareWeight) return CardRarity.Rare;
        return CardRarity.Legendary;
    }

    private List<CardSO> PickCards(int count)
    {
        var result = new List<CardSO>();
        var used = new HashSet<CardSO>();
        int safety = 0;

        while (result.Count < count && safety++ < 100)
        {
            var pool = deck.Where(c => c.rarity == RollRarity() && !used.Contains(c)).ToList();
            if (pool.Count == 0) continue;
            var chosen = pool[Random.Range(0, pool.Count)];
            if (chosen.isUnique && used.Contains(chosen)) continue;
            result.Add(chosen);
            used.Add(chosen);
        }

        return result;
    }

    private IEnumerator Slide(RectTransform rt, Vector2 target, float duration, bool overshoot)
    {
        if (rt == null) yield break;
        Vector2 start = rt.anchoredPosition;
        float t = 0f;
        float dur = Mathf.Max(0.001f, duration);

        while (t < dur)
        {
            if (rt == null) yield break;
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / dur);
            float e = easeCurve.Evaluate(p);

            if (overshoot && p > 0.95f)
            {
                float os = Mathf.Sin((p - 0.95f) / 0.05f * Mathf.PI) * easeOvershoot;
                rt.anchoredPosition = Vector2.Lerp(start, target, e) + new Vector2(-os * 100f, 0f);
            }
            else
            {
                rt.anchoredPosition = Vector2.Lerp(start, target, e);
            }

            yield return null;
        }

        if (rt != null) rt.anchoredPosition = target;
    }

    private void UpdateHighlightImmediate()
    {
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null)
                spawned[i].localScale = Vector3.one * (i == selectedIndex ? highlightScale : 1f);
    }

#if UNITY_EDITOR
    [ContextMenu("Debug - force open")]
    public void DebugOpen() => TriggerLevelUp();

    [ContextMenu("Clear queued")]
    public void DebugClear() { queuedLevels = 0; }
#endif
}