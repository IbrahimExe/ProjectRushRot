using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "CheckWallAhead", story: "if [ClimbDistance] is on [WallNormal] [Self] [CanClimb]", category: "Action", id: "99ebab3cc80de41be2eed3287a9ab3d9")]
public partial class CheckWallAheadAction : Action
{
    [SerializeReference] public BlackboardVariable<float> ClimbDistance;
    [SerializeReference] public BlackboardVariable<Vector3> WallNormal;
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<bool> CanClimb;
    [SerializeField] public Transform TargetTransform;

    // Add this property to hold the context GameObject
    public GameObject Context { get; set; }

    protected override Status OnStart()
    {
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        float distance = (ClimbDistance != null) ? ClimbDistance.Value : 1f;
        Vector3 origin = Self.Value.transform.position + Vector3.up * 0.5f; // adjust to capsule height
        Vector3 forward = Self.Value.transform.forward;

        if (Physics.Raycast(origin, forward, out RaycastHit hit, distance))
        {
            CanClimb.Value = true;
            WallNormal.Value = hit.normal;
        }
        else
        {
            CanClimb.Value = false;
        }

        return Status.Success;
    }

    protected override void OnEnd()
    {
    }
}

