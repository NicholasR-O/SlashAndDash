using UnityEngine;

[CreateAssetMenu(menuName = "Enemy AI/Conditions/Target Not Seen Condition", fileName = "TargetNotSeenCondition")]
public sealed class TargetNotSeenCondition : EnemyAICondition
{
    [SerializeField] float notSeenDuration = 2.5f;
    [SerializeField] float viewDistance = 18f;
    [SerializeField, Range(1f, 180f)] float viewAngle = 110f;
    [SerializeField] LayerMask viewBlockMask = ~0;
    [SerializeField] Vector3 eyeOffset = new Vector3(0f, 0.9f, 0f);

    Transform owner;
    float firstNotSeenAt = -1f;

    public override void Initialize(StateMachine machine)
    {
        base.Initialize(machine);
        owner = machine.transform;
    }

    public override void OnStateEntered()
    {
        firstNotSeenAt = -1f;
    }

    public override EnemyAIConditionResult Evaluate(EnemyAIState currentState)
    {
        if (!(currentState is ChaseState) && !(currentState is AttackState))
            return Stay();

        Transform target = TryGetCurrentStateTarget(currentState);
        if (target == null)
            return Transition();

        if (IsVisible(target))
        {
            firstNotSeenAt = -1f;
            return Stay();
        }

        if (firstNotSeenAt < 0f)
            firstNotSeenAt = Time.time;

        if (Time.time - firstNotSeenAt >= Mathf.Max(0f, notSeenDuration))
            return Transition();

        return Stay();
    }

    Transform TryGetCurrentStateTarget(EnemyAIState currentState)
    {
        if (currentState is ChaseState chaseState)
            return chaseState.Target;

        if (currentState is AttackState attackState)
            return attackState.Target;

        return null;
    }

    bool IsVisible(Transform candidate)
    {
        if (owner == null || candidate == null)
            return false;

        Vector3 origin = owner.position + eyeOffset;
        Vector3 toTarget = candidate.position - origin;
        float distance = toTarget.magnitude;
        if (distance < 0.001f || distance > viewDistance)
            return false;

        Vector3 direction = toTarget / distance;
        if (Vector3.Angle(owner.forward, direction) > viewAngle * 0.5f)
            return false;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, distance, viewBlockMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform == candidate)
                return true;

            if (hit.rigidbody != null && hit.rigidbody.transform == candidate)
                return true;

            return false;
        }

        return true;
    }
}
