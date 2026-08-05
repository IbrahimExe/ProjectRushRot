#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// Editor utility — creates the SkinPurchaseDialog panel inside the active scene's
/// Canvas hierarchy with one menu click.
/// 
/// Usage:  Tools → Rush Rot → Create Skin Purchase Dialog Panel
///
/// After running:
///   1. The panel is created under the first Canvas found in the scene.
///   2. Select the CharacterSelectManager GameObject.
///   3. In the Inspector, drag the new "SkinPurchaseDialog" panel into every
///      field exposed by SkinPurchaseDialog.cs.
/// </summary>
public static class SkinPurchaseDialogBuilder
{
    [MenuItem("Tools/Rush Rot/Create Skin Purchase Dialog Panel")]
    public static void CreateSkinPurchaseDialogPanel()
    {
        // ── Find or create a Canvas ──────────────────────────────────────────
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[SkinPurchaseDialogBuilder] No Canvas found in the active scene. " +
                           "Please open your Main Menu scene first.");
            return;
        }

        // ── Guard: don't create twice ────────────────────────────────────────
        SkinPurchaseDialog existing = Object.FindFirstObjectByType<SkinPurchaseDialog>();
        if (existing != null)
        {
            Debug.LogWarning("[SkinPurchaseDialogBuilder] A SkinPurchaseDialog already exists in the scene.");
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        // ── Colour palette ───────────────────────────────────────────────────
        Color darkBg        = new Color(0.08f, 0.08f, 0.12f, 0.97f);
        Color cardBg        = new Color(0.14f, 0.14f, 0.20f, 1f);
        Color accentGold    = new Color(1f,    0.82f, 0.20f, 1f);
        Color confirmGreen  = new Color(0.18f, 0.75f, 0.36f, 1f);
        Color cancelRed     = new Color(0.80f, 0.22f, 0.22f, 1f);
        Color textWhite     = new Color(0.95f, 0.95f, 0.95f, 1f);
        Color textRed       = new Color(0.95f, 0.25f, 0.25f, 1f);
        Color textGreen     = new Color(0.20f, 0.90f, 0.40f, 1f);

        // ── Root: full-screen darkened backdrop ──────────────────────────────
        GameObject rootObj = CreateUIObject("SkinPurchaseDialog", canvas.transform);
        RectTransform rootRT = rootObj.GetComponent<RectTransform>();
        StretchFull(rootRT);

        Image rootImg = rootObj.AddComponent<Image>();
        rootImg.color = new Color(0f, 0f, 0f, 0.65f);
        rootImg.raycastTarget = true;   // blocks clicks on UI behind

        // Attach the script here
        SkinPurchaseDialog dialogScript = rootObj.AddComponent<SkinPurchaseDialog>();

        // ── Card box (centred, fixed size) ───────────────────────────────────
        GameObject cardObj = CreateUIObject("DialogCard", rootObj.transform);
        RectTransform cardRT = cardObj.GetComponent<RectTransform>();
        cardRT.anchorMin = cardRT.anchorMax = cardRT.pivot = new Vector2(0.5f, 0.5f);
        cardRT.anchoredPosition = Vector2.zero;
        cardRT.sizeDelta = new Vector2(480f, 340f);

        Image cardImg = cardObj.AddComponent<Image>();
        cardImg.color = cardBg;

        // Subtle rounded-feel via a child shadow image (solid slightly larger dark rect)
        // (Unity built-in UI has no native border-radius; this gives a clean flat look)

        // ── Title bar strip ──────────────────────────────────────────────────
        GameObject titleBar = CreateUIObject("TitleBar", cardObj.transform);
        RectTransform titleBarRT = titleBar.GetComponent<RectTransform>();
        titleBarRT.anchorMin = new Vector2(0f, 1f);
        titleBarRT.anchorMax = new Vector2(1f, 1f);
        titleBarRT.pivot     = new Vector2(0.5f, 1f);
        titleBarRT.anchoredPosition = Vector2.zero;
        titleBarRT.sizeDelta = new Vector2(0f, 60f);
        Image titleBarImg = titleBar.AddComponent<Image>();
        titleBarImg.color = accentGold;

        // Title label (e.g. "Buy "Cyber Skateboard"?")
        GameObject titleTxtObj = CreateUIObject("SkinNameText", titleBar.transform);
        RectTransform titleTxtRT = titleTxtObj.GetComponent<RectTransform>();
        StretchFull(titleTxtRT);
        titleTxtRT.offsetMin = new Vector2(12f, 4f);
        titleTxtRT.offsetMax = new Vector2(-12f, -4f);
        TMP_Text titleTxt = titleTxtObj.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "Buy \"Skin Name\"?";
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.fontSize = 22f;
        titleTxt.color = new Color(0.1f, 0.1f, 0.1f, 1f);  // dark on gold

        // ── Body area ────────────────────────────────────────────────────────
        // Cost row
        TMP_Text costTxt = AddBodyLabel(cardObj.transform, "CostText",
            "💰  Cost:  300", 20f, textWhite, new Vector2(0f, -90f));

        // Current coins row
        TMP_Text coinsTxt = AddBodyLabel(cardObj.transform, "CurrentCoinsText",
            "You have:  1 250 💰", 18f, textGreen, new Vector2(0f, -130f));

        // Insufficient funds warning (hidden by default)
        TMP_Text insuffTxt = AddBodyLabel(cardObj.transform, "InsufficientFundsText",
            "⚠  Not enough coins!", 17f, textRed, new Vector2(0f, -165f));
        insuffTxt.gameObject.SetActive(false);

        // ── Buttons row ──────────────────────────────────────────────────────
        // Confirm (Buy)
        Button confirmBtn = AddDialogButton(cardObj.transform, "ConfirmButton",
            "Buy", confirmGreen, new Vector2(-90f, -250f), new Vector2(160f, 50f));

        // Cancel
        Button cancelBtn = AddDialogButton(cardObj.transform, "CancelButton",
            "Cancel", cancelRed, new Vector2(90f, -250f), new Vector2(160f, 50f));

        // ── Wire serialized fields via SerializedObject ──────────────────────
        SerializedObject so = new SerializedObject(dialogScript);

        so.FindProperty("dialogPanel").objectReferenceValue     = rootObj;
        so.FindProperty("skinNameText").objectReferenceValue    = titleTxt;
        so.FindProperty("costText").objectReferenceValue        = costTxt;
        so.FindProperty("currentCoinsText").objectReferenceValue = coinsTxt;
        so.FindProperty("insufficientFundsText").objectReferenceValue = insuffTxt;
        so.FindProperty("confirmButton").objectReferenceValue   = confirmBtn;
        so.FindProperty("cancelButton").objectReferenceValue    = cancelBtn;

        so.ApplyModifiedProperties();

        // Start hidden
        rootObj.SetActive(false);

        // Mark scene dirty and select the new object
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = rootObj;
        Undo.RegisterCreatedObjectUndo(rootObj, "Create SkinPurchaseDialog Panel");

        Debug.Log("[SkinPurchaseDialogBuilder] ✅ SkinPurchaseDialog panel created and all " +
                  "references auto-wired! Make sure to save your scene (Ctrl+S).");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static TMP_Text AddBodyLabel(Transform parent, string name, string defaultText,
        float fontSize, Color color, Vector2 anchoredPos)
    {
        GameObject go = CreateUIObject(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(420f, 36f);

        TMP_Text txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = defaultText;
        txt.alignment = TextAlignmentOptions.Center;
        txt.fontSize = fontSize;
        txt.color = color;
        return txt;
    }

    private static Button AddDialogButton(Transform parent, string name, string label,
        Color bgColor, Vector2 anchoredPos, Vector2 size)
    {
        // Container with Image
        GameObject go = CreateUIObject(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = go.AddComponent<Image>();
        img.color = bgColor;

        Button btn = go.AddComponent<Button>();

        // Subtle tint transition
        ColorBlock cb = btn.colors;
        cb.normalColor      = bgColor;
        cb.highlightedColor = bgColor * 1.15f;
        cb.pressedColor     = bgColor * 0.80f;
        cb.disabledColor    = new Color(0.4f, 0.4f, 0.4f, 0.6f);
        btn.colors = cb;

        // Label inside
        GameObject labelObj = CreateUIObject("Label", go.transform);
        RectTransform labelRT = labelObj.GetComponent<RectTransform>();
        StretchFull(labelRT);

        TMP_Text txt = labelObj.AddComponent<TextMeshProUGUI>();
        txt.text = label;
        txt.alignment = TextAlignmentOptions.Center;
        txt.fontStyle = FontStyles.Bold;
        txt.fontSize = 20f;
        txt.color = Color.white;
        txt.raycastTarget = false;

        return btn;
    }
}
#endif
