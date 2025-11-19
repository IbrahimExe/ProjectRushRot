using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "CanClimbIsTrueOrFalse", story: "[CanClimb]", category: "Conditions", id: "7bfa68547767c976f0e8b2979cffac24")]
public partial class CanClimbIsTrueOrFalseCondition : Condition
{
    [SerializeReference] public BlackboardVariable<bool> CanClimb;

    public override bool IsTrue()
    {
        return true;
    }

    public override void OnStart()
    {
    }

    public override void OnEnd()
    {
    }
}
