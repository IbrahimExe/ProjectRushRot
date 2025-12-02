using UnityEngine;

public class EnemySpawnTracker : MonoBehaviour
{
    public Spawner spawner;

    private void OnDestroy()
    {
        if (spawner != null)
            spawner.EnemyDied();
    }
}
