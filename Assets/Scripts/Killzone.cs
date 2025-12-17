using UnityEngine;

public class KillZone : MonoBehaviour
{
    [Header("UI / Game Over")]
    [Tooltip("Assign your Death/Game Over panel here.")]
    public GameObject deathScreen;

    private bool hasKilled = false; // Prevent double triggering

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object entering is the player
        if (other.CompareTag("Player"))
        {
            KillPlayer(other.gameObject);
        }
    }

    private void KillPlayer(GameObject player)
    {
        if (hasKilled) return;
        hasKilled = true;

        Debug.Log("Player entered KillZone.");

        // 1. Show the Game Over UI
        if (deathScreen != null)
        {
            deathScreen.SetActive(true);
        }
        else
        {
            Debug.LogWarning("KillZone: Death Screen reference is missing!");
        }

        // 2. Unlock and show cursor so player can click 'Restart'
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // 3. Pause the game
        Time.timeScale = 0f;

        // 4. Destroy the player object (matches your DeathWall logic)
        Destroy(player);
    }
}