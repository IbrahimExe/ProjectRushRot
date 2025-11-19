using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "MoveTowardsTarget", story: "Uses [TargetPos] to move [Self] to the target with [MoveSpeed] speed", category: "Action", id: "7af6a4721b5fbba71ea3e0fd23278496")]
public partial class MoveTowardsTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<Vector3> TargetPos;
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<float> MoveSpeed;
    // Add this field to the class to fix CS0103 errors
    [SerializeField] public GameObject ContextObject;

    protected override Status OnStart()
    {
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        Vector3 target = (TargetPos != null) ? TargetPos.Value : Self.Value.transform.position;
        float speed = (MoveSpeed != null) ? MoveSpeed.Value : 4f;

        // Direction vector including Y (allows ramps)
        Vector3 dir = target - Self.Value.transform.position;
        if (dir.sqrMagnitude < 0.001f)
            return Status.Success; // reached target

        dir = dir.normalized;

        // Move in world space
        Self.Value.transform.Translate(dir * speed * Time.deltaTime, Space.World);

        // Rotate to face movement direction (optional)
        Vector3 lookDir = new Vector3(dir.x, 0f, dir.z); // only rotate horizontally
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(lookDir);
            Self.Value.transform.rotation = Quaternion.Slerp(
                Self.Value.transform.rotation,
                look,
                10f * Time.deltaTime
            );
        }

        return Status.Running;
    }

    protected override void OnEnd()
    {
    }
}

