using UnityEngine;

public class ZombieLifeTracker : MonoBehaviour
{
    private ZombieSpawner _spawner;

    public void Init(ZombieSpawner spawner)
    {
        _spawner = spawner;
    }

    private void OnDestroy()
    {
        if (_spawner != null)
            _spawner.NotifyZombieDied();
    }
}
