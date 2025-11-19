    using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "CheckForClimbable", story: "Check For [Climbable]", category: "Action", id: "6c0bc0a677282b33650de32e1288126d")]
public partial class CheckForClimbableAction : Action
{
    [SerializeReference] public BlackboardVariable<bool> Climbable;

    protected override Status OnStart()
    {
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        return Status.Success;
    }

    protected override void OnEnd()
    {
    }
}

