using UnityEngine;

public class FinishLine : MonoBehaviour
{
    // The LevelTimer script attached to your UI Text object should be dragged here.
    // [SerializeField] is often implicit if it was dragged in, but keep it for clarity.
    public LevelTimer timer;

    // Unity automatically calls this function when another Collider enters the trigger zone
    private void OnTriggerEnter(Collider other)
    {
        // 1. Check if the object entering the trigger is the player.
        // The PlayerController.cs is on the same GameObject as the CharacterController.
        // We use CompareTag for performance and reliability.
        if (other.CompareTag("Player"))
        {
            // 2. Check if the 'timer' reference has been set in the Inspector.
            if (timer != null)
            {
                // 3. Call the public method on the LevelTimer script.
                timer.StopTimer();

                // Add a confirmation log to the Console
                Debug.Log("LEVEL COMPLETED: Timer stopped by Finish Line trigger.");
            }
            else
            {
                // This is a vital check for debugging
                Debug.LogError("FinishLine: Timer reference is missing! Did you drag the TimerText object into the 'Timer' slot?");
            }
        }
    }
}