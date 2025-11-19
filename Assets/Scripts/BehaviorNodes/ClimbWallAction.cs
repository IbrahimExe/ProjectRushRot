using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "ClimbWall", story: "[Self] uses [WallNormal] and [ClimbSpeed] to climb wall", category: "Action", id: "543c8cca744ae45a8254c808be85813d")]
public partial class ClimbWallAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<Vector3> WallNormal;
    [SerializeReference] public BlackboardVariable<float> ClimbSpeed;
    protected override Status OnStart()
    {
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        float speed = (ClimbSpeed != null) ? ClimbSpeed.Value : 3f;

        // Move straight up. To hug the wall, add a small tangent component along the wall:
        Vector3 climbDir = Vector3.up;
        if (WallNormal != null && WallNormal.Value.sqrMagnitude > 0.001f)
        {
            Vector3 tangent = Vector3.Cross(Vector3.up, WallNormal.Value).normalized;
            climbDir += tangent * 0.2f; // small wall hugging movement
        }

        Self.Value.transform.Translate(climbDir * speed * Time.deltaTime, Space.World);

        //  rotate to face wall
        if (WallNormal != null && WallNormal.Value.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(-WallNormal.Value); // face wall
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

