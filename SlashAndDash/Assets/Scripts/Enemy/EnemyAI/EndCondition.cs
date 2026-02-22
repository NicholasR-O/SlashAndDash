using UnityEngine;

[CreateAssetMenu(menuName = "Enemy AI/Conditions/End Condition", fileName = "EndCondition")]
public sealed class EndCondition : EnemyAICondition
{
    public override EnemyAIConditionResult Evaluate(EnemyAIState currentState)
    {
        if (Machine.IsTransitionLocked)
            return Transition();

        return Stay();
    }
}
