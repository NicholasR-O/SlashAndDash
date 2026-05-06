using UnityEngine;

[CreateAssetMenu(menuName = "Enemy AI/Conditions/Target Distance Condition", fileName = "TargetDistanceCondition")]
public sealed class TargetDistanceCondition : EnemyAICondition
{
    [SerializeField] float distanceThreshold = 1.6f;
    [SerializeField] bool horizontalOnly = true;
    [SerializeField] bool triggerWhenWithin = true;

    Transform owner;

    public override void Initialize(StateMachine machine)
    {
        base.Initialize(machine);
        owner = machine.transform;
    }

    public override EnemyAIConditionResult Evaluate(EnemyAIState currentState)
    {
        if (owner == null)
            return Stay();

        Transform target = TryGetCurrentStateTarget(currentState);
        if (target == null)
            return Stay();

        float threshold = Mathf.Max(0f, distanceThreshold);
        float distance = TargetingUtility.GetColliderDistance(owner, target, horizontalOnly);
        bool shouldTrigger = triggerWhenWithin ? distance <= threshold : distance > threshold;

        if (shouldTrigger)
            return Transition(target);

        return Stay();
    }

    Transform TryGetCurrentStateTarget(EnemyAIState currentState)
    {
        if (currentState is ChaseState chaseState)
            return chaseState.Target;

        if (currentState is KeepDistanceState keepDistanceState)
            return keepDistanceState.Target;

        if (currentState is AttackState attackState)
            return attackState.Target;

        if (currentState is ProjectileAttackState projectileAttackState)
            return projectileAttackState.Target;

        return null;
    }
}
