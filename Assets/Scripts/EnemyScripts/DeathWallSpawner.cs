using UnityEngine;

public class DeathWallTrigger : MonoBehaviour
{
    [Header("Scene Reference")]
    [SerializeField] private GameObject deathWall;

    private bool activated = false;

    private void OnTriggerEnter(Collider other)
    {
        if (activated) return;

        if (other.CompareTag("Player"))
        {
            deathWall.SetActive(true);
            activated = true;
        }
    }
}
