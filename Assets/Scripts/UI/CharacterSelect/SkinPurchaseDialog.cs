using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Self-bootstrapping purchase confirmation dialog.
/// Creates its own UI at runtime — no scene setup required.
/// Just call SkinPurchaseDialog.Show(data, callback) from anywhere.
/// </summary>
public class SkinPurchaseDialog : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────────
    private static SkinPurchaseDialog _instance;
    public static SkinPurchaseDialog Instance
    {
        get
        {
            if (_instance == null)
                Bootstrap();
            return _instance;
        }
    }

    // ── Runtime-built UI references ────────────────────────────────────────────
    private GameObject  _root;
    private TMP_Text    _titleText;
    private TMP_Text    _costText;
    private TMP_Text    _coinsText;
    private TMP_Text    _warnText;
    private Button      _confirmBtn;
    private Button      _cancelBtn;

    // ── State ──────────────────────────────────────────────────────────────────
    private PlayerCharacterData              _pendingSkin;
    private Action<PlayerCharacterData>      _onConfirmed;

    // ─────────────────────────────────────────────────────────────────────────
    // Bootstrap: find or create the dialog in the scene
    // ─────────────────────────────────────────────────────────────────────────
    private static void Bootstrap()
    {
        // Find existing canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[SkinPurchaseDialog] No Canvas found in scene!");
            return;
        }

        // Create host GameObject
        GameObject host = new GameObject("SkinPurchaseDialog_Runtime");
        host.transform.SetParent(canvas.transform, false);
        _instance = host.AddComponent<SkinPurchaseDialog>();
        _instance.BuildUI(canvas);
        _instance.SetVisible(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Build the entire UI programmatically
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildUI(Canvas canvas)
    {
        // Full-screen darkened backdrop
        _root = new GameObject("DialogRoot", typeof(RectTransform));
        _root.transform.SetParent(canvas.transform, false);

        RectTransform rootRT = _root.GetComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = rootRT.offsetMax = Vector2.zero;

        Image backdrop = _root.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.75f);
        backdrop.raycastTarget = true;

        // Make sure it sorts on top
        Canvas rootCanvas = _root.AddComponent<Canvas>();
        rootCanvas.overrideSorting = true;
        rootCanvas.sortingOrder = 999;
        _root.AddComponent<GraphicRaycaster>();

        // Card box
        GameObject card = CreateRect("Card", _root.transform, new Vector2(500, 360));
        card.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        Image cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(0.1f, 0.1f, 0.15f, 1f);

        // Title bar
        GameObject titleBar = CreateRect("TitleBar", card.transform, new Vector2(500, 64));
        RectTransform tbRT = titleBar.GetComponent<RectTransform>();
        tbRT.anchorMin = new Vector2(0, 1); tbRT.anchorMax = new Vector2(1, 1);
        tbRT.pivot = new Vector2(0.5f, 1f);
        tbRT.anchoredPosition = Vector2.zero;
        tbRT.sizeDelta = new Vector2(0, 64);
        titleBar.AddComponent<Image>().color = new Color(1f, 0.82f, 0.2f, 1f);

        _titleText = CreateLabel("TitleText", titleBar.transform,
            new Vector2(0, -32), new Vector2(460, 48), "Buy skin?", 22, new Color(0.1f, 0.1f, 0.1f), true);

        // Body labels
        _costText = CreateLabel("CostText", card.transform,
            new Vector2(0, -110), new Vector2(420, 36), "Cost: 0 Coins", 20, Color.white);

        _coinsText = CreateLabel("CoinsText", card.transform,
            new Vector2(0, -155), new Vector2(420, 36), "You have: 0 Coins", 18, new Color(0.2f, 0.9f, 0.4f));

        _warnText = CreateLabel("WarnText", card.transform,
            new Vector2(0, -195), new Vector2(420, 36), "Not enough coins!", 17, new Color(0.95f, 0.3f, 0.3f));

        // Buttons
        _confirmBtn = CreateButton("BuyBtn",  card.transform,
            new Vector2(-100, -290), new Vector2(180, 54), "BUY", new Color(0.18f, 0.75f, 0.36f));
        _confirmBtn.onClick.AddListener(OnConfirm);

        _cancelBtn = CreateButton("CancelBtn", card.transform,
            new Vector2(100, -290), new Vector2(180, 54), "CANCEL", new Color(0.78f, 0.22f, 0.22f));
        _cancelBtn.onClick.AddListener(OnCancel);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────
    public void Show(PlayerCharacterData skinData, Action<PlayerCharacterData> confirmedCallback)
    {
        if (skinData == null) return;
        _pendingSkin = skinData;
        _onConfirmed = confirmedCallback;
        PopulateTexts();
        SetVisible(true);
    }

    public void Hide()
    {
        _pendingSkin = null;
        _onConfirmed = null;
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (_root != null) _root.SetActive(visible);
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void PopulateTexts()
    {
        if (_pendingSkin == null) return;

        int cost  = _pendingSkin.coinCost;
        int coins = InventoryManager.Instance != null
            ? InventoryManager.Instance.GetItemCount("Gold") : 0;
        bool canAfford = coins >= cost;

        string name = !string.IsNullOrEmpty(_pendingSkin.skinName)
            ? _pendingSkin.skinName : _pendingSkin.name;

        if (_titleText  != null) _titleText.text  = $"Buy \"{name}\"?";
        if (_costText   != null) _costText.text   = $"Cost:     {cost:N0} Coins";
        if (_coinsText  != null)
        {
            _coinsText.text  = $"You have: {coins:N0} Coins";
            _coinsText.color = canAfford
                ? new Color(0.2f, 0.9f, 0.4f)
                : new Color(0.95f, 0.3f, 0.3f);
        }
        if (_warnText   != null) _warnText.gameObject.SetActive(!canAfford);
        if (_confirmBtn != null) _confirmBtn.interactable = canAfford;
    }

    private void OnConfirm()
    {
        if (_pendingSkin == null) return;

        bool spent = InventoryManager.Instance != null &&
                     InventoryManager.Instance.SpendItem("Gold", _pendingSkin.coinCost);

        if (!spent) { Hide(); return; }

        SkinUnlockManager.UnlockSkin(_pendingSkin);
        _onConfirmed?.Invoke(_pendingSkin);
        Hide();
    }

    private void OnCancel() => Hide();

    // ─────────────────────────────────────────────────────────────────────────
    // UI helpers
    // ─────────────────────────────────────────────────────────────────────────
    private static GameObject CreateRect(string name, Transform parent, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    private static TMP_Text CreateLabel(string name, Transform parent,
        Vector2 pos, Vector2 size, string text, float fontSize, Color color, bool bold = false)
    {
        GameObject go = CreateRect(name, parent, size);
        go.GetComponent<RectTransform>().anchoredPosition = pos;
        TMP_Text t = go.AddComponent<TextMeshProUGUI>();
        t.text      = text;
        t.fontSize  = fontSize;
        t.color     = color;
        t.alignment = TextAlignmentOptions.Center;
        if (bold) t.fontStyle = FontStyles.Bold;
        return t;
    }

    private static Button CreateButton(string name, Transform parent,
        Vector2 pos, Vector2 size, string label, Color bgColor)
    {
        GameObject go = CreateRect(name, parent, size);
        go.GetComponent<RectTransform>().anchoredPosition = pos;
        Image img = go.AddComponent<Image>();
        img.color = bgColor;
        Button btn = go.AddComponent<Button>();

        ColorBlock cb = btn.colors;
        cb.normalColor      = bgColor;
        cb.highlightedColor = bgColor * 1.2f;
        cb.pressedColor     = bgColor * 0.75f;
        btn.colors = cb;

        // Label
        GameObject labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        RectTransform lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        TMP_Text t = labelGo.AddComponent<TextMeshProUGUI>();
        t.text      = label;
        t.fontSize  = 20;
        t.color     = Color.white;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;

        return btn;
    }
}
