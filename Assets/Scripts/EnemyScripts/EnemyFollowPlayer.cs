using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class SmartEnemyFollow : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float moveSpeed = 3.5f;     // Base cruising speed
    [SerializeField] private float maxSpeed = 6f;        // Max chase speed
    [SerializeField] private float acceleration = 8f;    // How fast to reach max speed
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float stopDistance = 2f;
    [SerializeField] private float repathRate = 0.25f;
    [SerializeField] private float hoverBuffer = 0.5f;

    private NavMeshAgent agent;
    private float nextPathTime;
    private Vector3 lastPlayerPos;
    private float currentSpeed;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        // NavMeshAgent setup
        agent.speed = moveSpeed;
        agent.acceleration = acceleration;
        agent.stoppingDistance = stopDistance;
        agent.autoBraking = true;
        agent.updateRotation = false;
        agent.updateUpAxis = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

        lastPlayerPos = player.position;
        currentSpeed = moveSpeed;
    }

    void Update()
    {
        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);

        // Update destination only when needed
        if (Time.time >= nextPathTime || Vector3.Distance(lastPlayerPos, player.position) > 0.5f)
        {
            nextPathTime = Time.time + repathRate;
            Vector3 targetPos = player.position - (player.forward * stopDistance);
            agent.SetDestination(targetPos);
            lastPlayerPos = player.position;
        }

        // Stop if close enough
        if (distance <= stopDistance - hoverBuffer)
        {
            agent.ResetPath();
        }

        // Gradually accelerate toward maxSpeed
        currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * Time.deltaTime);
        agent.speed = currentSpeed;

        // Slow down when near player
        if (distance <= stopDistance + 0.5f)
        {
            currentSpeed = Mathf.Lerp(currentSpeed, moveSpeed * 0.5f, Time.deltaTime * 2f);
            agent.speed = currentSpeed;
        }

        // Smooth rotation toward player
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }
    }
}
