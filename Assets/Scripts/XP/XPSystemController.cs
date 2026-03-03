// Attach this to a persistent GameObject (same one as AltExpManager).
// Controls whether the XP / level-up / upgrade system is active.
// Call EnableXPSystem() / DisableXPSystem() 

using UnityEngine;
using UnityEngine.SceneManagement;

public class XPSystemController : MonoBehaviour
{
    public static XPSystemController Instance { get; private set; }

    [Header("Scene Allowlist")]
    [Tooltip("Names of scenes where the XP system should be ACTIVE. " +
             "Leave empty to manage manually via EnableXPSystem() / DisableXPSystem().")]
    public string[] allowedSceneNames;

    [Header("Debug")]
    public bool debugLog = true;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        // Apply the correct state for whichever scene we start in.
        ApplyForScene(SceneManager.GetActiveScene().name);
    }

    // ─────────────────────────────────────────────
    //  Scene callback
    // ─────────────────────────────────────────────

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyForScene(scene.name);
    }

    private void ApplyForScene(string sceneName)
    {
        if (allowedSceneNames == null || allowedSceneNames.Length == 0)
            return; // manual-only mode — don't auto-switch

        bool allowed = System.Array.Exists(allowedSceneNames, s => s == sceneName);

        if (allowed)
            EnableXPSystem();
        else
            DisableXPSystem();
    }

    public void EnableXPSystem()
    {
        if (AltExpManager.Instance == null) return;

        AltExpManager.Instance.xpSystemEnabled = true;

        // Show the XP bar
        if (AltExpManager.Instance.xpBar != null)
            AltExpManager.Instance.xpBar.gameObject.SetActive(true);

        if (debugLog)
            Debug.Log("[XPSystemController] XP system ENABLED.");
    }

    public void DisableXPSystem()
    {
        if (AltExpManager.Instance == null) return;

        AltExpManager.Instance.xpSystemEnabled = false;

        // Hide the XP bar so it doesn't show in tutorial
        if (AltExpManager.Instance.xpBar != null)
            AltExpManager.Instance.xpBar.gameObject.SetActive(false);

        if (debugLog)
            Debug.Log("[XPSystemController] XP system DISABLED.");
    }

    // Convenience toggle for inspector buttons / UI
    public void ToggleXPSystem()
    {
        if (AltExpManager.Instance == null) return;

        if (AltExpManager.Instance.xpSystemEnabled)
            DisableXPSystem();
        else
            EnableXPSystem();
    }
}