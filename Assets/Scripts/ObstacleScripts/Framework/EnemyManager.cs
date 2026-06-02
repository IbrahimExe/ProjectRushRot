using UnityEngine;
using System.Collections.Generic;

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager Instance { get; private set; }

    private List<MovingEnemy> _enemies = new();
    private int _updateIndex = 0;

    [Tooltip("How many enemies update their steering per frame")]
    public int enemiesPerFrame = 5;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (_enemies.Count == 0) return;

        int count = Mathf.Min(enemiesPerFrame, _enemies.Count);
        for (int i = 0; i < count; i++)
        {
            _updateIndex = (_updateIndex + 1) % _enemies.Count;
            _enemies[_updateIndex].ManualUpdate();
        }
    }

    public void Register(MovingEnemy enemy)
    {
        if (!_enemies.Contains(enemy))
            _enemies.Add(enemy);
    }

    public void Unregister(MovingEnemy enemy) => _enemies.Remove(enemy);
}
