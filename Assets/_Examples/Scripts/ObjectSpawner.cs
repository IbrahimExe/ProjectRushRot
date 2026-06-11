using System.Collections;
using UnityEngine;

namespace Examples
{
    public class ObjectSpawner : BaseObjectSpawner
    {
        [SerializeField] private float _spawnInterval = 1f;
        [SerializeField] private float _spawnRadius = 5f;
        [SerializeField] private float _objectLifetime = 5f;
        [SerializeField] private string _poolName;

        private ObjectPoolManager _objectPoolManager = null;
        private bool _initialized = false;
        private WaitForSeconds _spawnWait = null;
        private WaitForSeconds _lifetimeWait = null;
        private Coroutine _spawnCoroutine = null;
        private bool _isSpawning = false;

        public override void Initialize()
        {
            _objectPoolManager = ServiceLocator.Get<ObjectPoolManager>();
            _spawnWait = new WaitForSeconds(_spawnInterval);
            _lifetimeWait = new WaitForSeconds(_objectLifetime);
            _initialized = true;
        }

        public override void StartSpawning()
        {
            if (_spawnCoroutine != null)
            {
                Debug.LogWarning("ObjectSpawner: Already spawning.");
                return;
            }
            _isSpawning = true;
            _spawnCoroutine = StartCoroutine(SpawnRoutine());
        }

        private IEnumerator SpawnRoutine()
        {
            while (_isSpawning)
            {
                SpawnObject();
                yield return _spawnWait;
            }
        }

        private void SpawnObject()
        {
            if (_objectPoolManager.TryFetch(_poolName, out GameObject obj))
            {
                Vector2 random2DPoint = Random.insideUnitCircle;
                Vector3 randomPos = new Vector3(random2DPoint.x, 0f, random2DPoint.y) * _spawnRadius;
                obj.transform.SetPositionAndRotation(randomPos, Quaternion.identity);
                obj.SetActive(true);
                StartCoroutine(RecycleAfterTime(obj));
            }
            else
            {
                Debug.LogWarning($"ObjectSpawner: No objects available in pool '{_poolName}' to spawn.");
            }
        }

        private IEnumerator RecycleAfterTime(GameObject obj)
        {
            yield return _lifetimeWait;
            _objectPoolManager.Recycle(_poolName, obj);
        }

        public void StopSpawning()
        {
            _isSpawning = false;
            StopCoroutine(_spawnCoroutine);
            _spawnCoroutine = null;
        }
    }

}
