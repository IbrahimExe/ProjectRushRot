using JetBrains.Annotations;
using UnityEngine;

public class ZombieBehaviour : MonoBehaviour
{
  
    [SerializeField] private float moveSpeed = 5;
    public Transform playerTarget;
 
    void Start()
    {
        
        Debug.Log($"Zombie speed is {moveSpeed}");
    }

 
    void Update()
    {
        transform.LookAt(playerTarget.position);
        transform.position = Vector3.MoveTowards(transform.position, playerTarget.position, moveSpeed * Time.deltaTime);
    }
}
