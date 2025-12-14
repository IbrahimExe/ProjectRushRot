using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class ZombieSpawner : MonoBehaviour
{
    [Header("Activation")]
    public bool Activated { get; private set; } = false;
    [Tooltip("Tag the player 'Player' in the scene.")]
    public string PlayerTag = "Player";

    [Header("Zombie Prefab")]
    public GameObject ZombiePrefab;               // single zombie prefab to spawn

    [Header("Spawn Settings")]
    public Transform[] SpawnPoints;               // if empty, uses spawner position + random offset
    public float SpawnInterval = 4f;
    public int MaxAliveZombies = 6;
    public float SpawnRadius = 2f;                // used when no spawn points provided
    public float GroundSnapHeight = 2f;           // how high above spawn point to raycast down for ground

    [Header("Misc")]
    public bool AutoAssignPlayerToZombies = true; // auto set EnemyIdleChase.Player after instantiation

    private int _currentAlive = 0;
    private Coroutine _spawnRoutine;
    private Transform _playerTransform;

    private void Reset()
    {
        // ensure collider is a trigger by default for convenience
        Collider c = GetComponent<Collider>();
        if (c)
            c.isTrigger = true;
    }

    private void Awake()
    {
        // try to find player up front (optional)
        GameObject p = GameObject.FindGameObjectWithTag(PlayerTag);
        if (p) _playerTransform = p.transform;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Activated) return;
        if (!other.CompareTag(PlayerTag)) return;

        _playerTransform = other.transform;
        Activated = true;
        _spawnRoutine = StartCoroutine(SpawnLoop());
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(PlayerTag)) return;
        if (!Activated) return;

        Activated = false;
        if (_spawnRoutine != null)
            StopCoroutine(_spawnRoutine);
        _spawnRoutine = null;
    }

    private IEnumerator SpawnLoop()
    {
        while (Activated)
        {
            if (_currentAlive < MaxAliveZombies)
            {
                SpawnZombie();
            }

            yield return new WaitForSeconds(SpawnInterval);
        }
    }

    private void SpawnZombie()
    {
        if (ZombiePrefab == null)
        {
            Debug.LogWarning("[ZombieSpawner] No ZombiePrefab assigned.");
            return;
        }

        Vector3 spawnPos;
        Quaternion spawnRot;

        // pick spawn position
        if (SpawnPoints != null && SpawnPoints.Length > 0)
        {
            Transform sp = SpawnPoints[Random.Range(0, SpawnPoints.Length)];
            spawnPos = sp.position;
            spawnRot = sp.rotation;
        }
        else
        {
            // random offset around spawner's position
            Vector2 rnd = Random.insideUnitCircle * SpawnRadius;
            spawnPos = transform.position + new Vector3(rnd.x, 0f, rnd.y);
            spawnRot = transform.rotation;
        }

        // snap to ground if possible (raycast down)
        if (Physics.Raycast(spawnPos + Vector3.up * GroundSnapHeight, Vector3.down, out RaycastHit gHit, GroundSnapHeight + 1f))
        {
            spawnPos.y = gHit.point.y + 0.05f; // small offset so it doesn't intersect ground
            // optional: align forward to ground normal tangent (we'll just ensure up is world up)
        }
        else
        {
            // if no ground hit, keep spawnPos as-is but slightly raised to avoid intersection
            spawnPos += Vector3.up * 0.2f;
        }

        GameObject go = Instantiate(ZombiePrefab, spawnPos, spawnRot);
        PrepareSpawnedZombie(go);
    }

    private void PrepareSpawnedZombie(GameObject zombie)
    {
        if (zombie == null) return;

        // ensure upright (fix models that are oriented wrongly)
        // This aligns the spawned object's local up to world up while preserving forward as best as possible.
        zombie.transform.up = Vector3.up;

        // set Player reference on EnemyIdleChase if requested
        if (AutoAssignPlayerToZombies)
        {
            EnemyIdleChase eic = zombie.GetComponent<EnemyIdleChase>();
            if (eic != null)
            {
                // if we already know player transform, give it. Otherwise try to find by tag.
                if (_playerTransform == null)
                {
                    GameObject p = GameObject.FindGameObjectWithTag(PlayerTag);
                    if (p) _playerTransform = p.transform;
                }

                if (_playerTransform != null)
                    eic.Player = _playerTransform;
            }
        }

        // ensure Rigidbody exists and is set up
        Rigidbody rb = zombie.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // reset velocities so it doesn't spawn flying or rotating
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // enforce settings for stable spawn:
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.WakeUp();

            // freeze X/Z rotations so the capsule/model stays upright (allow Y rotation)
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        // add life tracker so spawner can count down when it dies/destroyed
        ZombieLifeTracker tracker = zombie.GetComponent<ZombieLifeTracker>();
        if (tracker == null)
            tracker = zombie.AddComponent<ZombieLifeTracker>();

        tracker.Init(this);

        _currentAlive++;
    }

    public void NotifyZombieDied()
    {
        _currentAlive = Mathf.Max(0, _currentAlive - 1);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.2f, 0.2f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, SpawnRadius);

        Gizmos.color = Color.green;
        if (SpawnPoints != null)
        {
            foreach (var s in SpawnPoints)
            {
                if (s != null)
                    Gizmos.DrawSphere(s.position, 0.25f);
            }
        }
    }
}
