using UnityEngine;

[CreateAssetMenu(menuName = "Enemy AI/Conditions/Timer Condition", fileName = "TimerCondition")]
public sealed class TimerCondition : EnemyAICondition
{
    [SerializeField] float minDelay = 1f;
    [SerializeField] float maxDelay = 3f;

    float clampedMinDelay;
    float clampedMaxDelay;
    float triggerTime;

    public override void Initialize(StateMachine machine)
    {
        base.Initialize(machine);
        float min = Mathf.Min(minDelay, maxDelay);
        float max = Mathf.Max(minDelay, maxDelay);
        clampedMinDelay = Mathf.Max(0f, min);
        clampedMaxDelay = Mathf.Max(clampedMinDelay, max);
        ResetTimer();
    }

    public override void OnStateEntered()
    {
        ResetTimer();
    }

    public override EnemyAIConditionResult Evaluate(EnemyAIState currentState)
    {
        if (Time.time >= triggerTime)
            return Transition();

        return Stay();
    }

    void ResetTimer()
    {
        if (Mathf.Approximately(clampedMinDelay, clampedMaxDelay))
            triggerTime = Time.time + clampedMinDelay;
        else
            triggerTime = Time.time + Random.Range(clampedMinDelay, clampedMaxDelay);
    }
}
