using UnityEngine;

public class CursorLocker : MonoBehaviour
{
    [Header("Behavior")]
    public bool lockOnStart = true;         // Lock/hide right when Play starts
    public bool hideCursor = true;          // Hide cursor when locked
    public bool relockOnLeftClick = true;   // Click to re-lock when unlocked
    public KeyCode toggleKey = KeyCode.Escape; // Press to toggle unlock/lock

    void Start()
    {
        if (lockOnStart) LockCursor();
    }

    void Update()
    {
        // Toggle lock/unlock with Escape
        if (Input.GetKeyDown(toggleKey))
        {
            if (Cursor.lockState == CursorLockMode.Locked) UnlockCursor();
            else LockCursor();
        }

        // If unlocked, optionally re-lock on left mouse click (useful in editor)
        if (relockOnLeftClick && Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0))
        {
            LockCursor();
        }

        // Optional: keep cursor confined in windowed mode
        // (uncomment if you prefer confined instead of fully locked)
        // if (Cursor.lockState == CursorLockMode.Confined) { /* ... */ }
    }

    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;   // Locked = mouse delta used, cursor hidden/centered
        Cursor.visible = !hideCursor;               // Hide cursor if requested
    }

    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;     // Releases the cursor
        Cursor.visible = true;                      // Show cursor
    }

    // Convenience: allow other scripts to set lock state
    public void SetLocked(bool locked) { if (locked) LockCursor(); else UnlockCursor(); }
}
