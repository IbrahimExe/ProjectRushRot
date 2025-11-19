using UnityEngine;
using UnityEngine;
using Unity.Behavior;

public class ClimbNode : Action
{
    public BlackboardVariable<GameObject> Self;
    public BlackboardVariable<Vector3> ClimbTopPoint;

    private Coroutine climbRoutine;

    protected override Status OnStart()
    {
        var helper = Self.Value.GetComponent<EnemyClimbHelper>();
        if (helper != null)
            climbRoutine = helper.StartCoroutine(helper.DoClimb(ClimbTopPoint.Value));

        // Always return Running, because climbing takes time
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        // Keep running until coroutine finishes
        return climbRoutine == null ? Status.Success : Status.Running;
    }
}