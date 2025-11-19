using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "UpdateTargetPos", story: "[Self] grabs [Player] [TargetPos]", category: "Action", id: "8da770fceca6dfc014e3b5650b8cd845")]
public partial class UpdateTargetPosAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Player;
    [SerializeReference] public BlackboardVariable<Vector3> TargetPos;
    protected override Status OnStart()
    {
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        if (Player == null || Player.Value == null)
            return Status.Failure;

        TargetPos.Value = Player.Value.transform.position;
        return Status.Success;
    }

    protected override void OnEnd()
    {
    }

}

