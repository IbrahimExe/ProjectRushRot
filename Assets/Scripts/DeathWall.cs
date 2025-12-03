using UnityEngine;
using TMPro;
using System.Reflection;

public class DeathWall : MonoBehaviour
{
    [Header("Movement")]
    public Vector3 MoveDirection = Vector3.forward;
    public float BaseSpeed = 5f;

    [Range(0f, 2f)]
    public float SpeedPercentFromTarget = 0.5f;

    [Header("UI / Timer (optional)")]
    [Tooltip("Optional: assign your Death (Game Over) panel here.")]
    public GameObject deathScreen;

    private Rigidbody playerRb;
    private bool hasKilled = false; // prevent multiple triggers

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
            playerRb = player.GetComponent<Rigidbody>();

        // Ensure deathScreen is hidden at start
        if (deathScreen != null)
            deathScreen.SetActive(false);
    }

    void Update()
    {
        float bonus = 0f;

        if (playerRb != null)
        {
            // try to read velocity robustly (some projects use linearVelocity)
            Vector3 velocity = Vector3.zero;

            // attempt to read 'linearVelocity' property if present, otherwise use velocity
            try
            {
                var prop = typeof(Rigidbody).GetProperty("linearVelocity");
                if (prop != null)
                {
                    var val = prop.GetValue(playerRb);
                    if (val is Vector3 v) velocity = v;
                }
            }
            catch { }

            if (velocity == Vector3.zero)
                velocity = playerRb.linearVelocity;

            bonus = velocity.magnitude * SpeedPercentFromTarget;
        }

        float finalSpeed = BaseSpeed + bonus;
        transform.position += MoveDirection.normalized * finalSpeed * Time.deltaTime;
    }

    private void KillPlayer(GameObject obj)
    {
        if (hasKilled) return; // already handled
        if (!obj.CompareTag("Player")) return;

        hasKilled = true;

        // Activate death screen BEFORE setting text so GetComponentInChildren can find TMP elements
        if (deathScreen != null)
            deathScreen.SetActive(true);

        // Show cursor so player can press buttons
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Pause the game
        Time.timeScale = 0f;

        // Finally destroy (or disable) the player GameObject — keep UI intact
        Destroy(obj);

        Debug.Log("Player killed by death wall. Death screen displayed");
    }

    private void OnTriggerEnter(Collider other)
    {
        KillPlayer(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        KillPlayer(collision.gameObject);
    }
}
