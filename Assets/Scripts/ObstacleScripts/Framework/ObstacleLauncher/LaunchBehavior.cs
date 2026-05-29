using UnityEngine;

public abstract class LaunchBehavior : MonoBehaviour
{
    public abstract void Launch(Rigidbody rb, Vector3 origin, Vector3 targetPosition);
}
