using UnityEngine;

public class EnemyMotor : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float climbSpeed = 3f;
    public CharacterController controller;

    private Vector3 climbNormal;

    public void MoveToward(Vector3 target)
    {
        Vector3 dir = (target - transform.position).normalized;
        controller.Move(dir * moveSpeed * Time.deltaTime);
        transform.forward = Vector3.Lerp(transform.forward, dir, 10f * Time.deltaTime);
    }

    public void StartClimbing(Vector3 normal)
    {
        climbNormal = normal;
    }

    public void ClimbTo(Vector3 climbPoint)
    {
        Vector3 upward = -climbNormal; // move upward along surface
        controller.Move(upward * climbSpeed * Time.deltaTime);
    }
}