using UnityEngine;
using Unity.Behavior;

public class CheckForClimbableNode : Action
{
    public BlackboardVariable<GameObject> Self;
    public BlackboardVariable<bool> Climbable;
    public BlackboardVariable<Vector3> ClimbTopPoint;

    protected override Status OnUpdate()
    {
        if (Self.Value == null)
            return Status.Failure;

        var helper = Self.Value.GetComponent<EnemyClimbHelper>();
        if (helper == null)
            return Status.Failure;

        if (helper.CheckClimbable(out Vector3 top))
        {
            Climbable.Value = true;
            ClimbTopPoint.Value = top;
            return Status.Success;
        }
        else
        {
            Climbable.Value = false;
            return Status.Failure;
        }
    }
}