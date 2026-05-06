using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(menuName = "Enemy AI/States/Attack State", fileName = "AttackState")]
public sealed class AttackState : EnemyAIState
{
    [SerializeField] float attackWindup = 0.25f;
    [SerializeField] float attackCooldown = 1.2f;
    [SerializeField] float attackDamage = 10f;
    [SerializeField] float attackRange = 2f;
    [SerializeField] bool horizontalDistanceOnly = true;

    Transform owner;
    NavMeshAgent agent;
    Transform target;
    Enemy enemy;
    float nextAttackAt;
    float nextWindupAt;
    bool windupTriggered;

    public Transform Target => target;

    public override void Initialize(StateMachine machine)
    {
        base.Initialize(machine);
        owner = machine.transform;
        agent = machine.GetComponent<NavMeshAgent>();
        enemy = machine.GetComponent<Enemy>();
    }

    public override void Enter(EnemyAIConditionResult transitionData)
    {
        if (transitionData.Target != null)
            target = transitionData.Target;
        ScheduleNextAttack(Mathf.Max(0f, attackWindup));
        StopAgent();
    }

    public override void Tick()
    {
        StopAgent();

        if (Machine.IsTransitionLocked)
            return;

        if (target == null)
            return;

        if (!windupTriggered && Time.time >= nextWindupAt)
            TriggerWindup();

        if (Time.time < nextAttackAt)
            return;

        bool attacked = TryDealDamage();
        if (attacked && enemy != null)
        {
            enemy.OnAttackStrike(target);
            Machine.RaiseAIEvent(EnemyAIEventType.AttackStrike, this, target);
        }

        ScheduleNextAttack(Mathf.Max(0f, attackCooldown));
    }

    public override void Exit()
    {
        StopAgent();
    }

    void StopAgent()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        agent.ResetPath();
        agent.velocity = Vector3.zero;
        agent.isStopped = true;
    }

    bool TryDealDamage()
    {
        if (owner == null || target == null || attackDamage <= 0f)
            return false;

        float distance = TargetingUtility.GetColliderDistance(owner, target, horizontalDistanceOnly);
        if (distance > Mathf.Max(0f, attackRange))
            return false;

        IDamageable damageable = DamageUtility.FindDamageable(target);
        if (damageable == null || !damageable.IsAlive)
            return false;

        damageable.TakeDamage(attackDamage, owner.gameObject);
        return true;
    }

    void ScheduleNextAttack(float delay)
    {
        float windup = Mathf.Max(0f, attackWindup);
        nextAttackAt = Time.time + Mathf.Max(0f, delay);
        nextWindupAt = nextAttackAt - windup;
        windupTriggered = windup <= 0f;

        if (!windupTriggered && Time.time >= nextWindupAt)
            TriggerWindup();
    }

    void TriggerWindup()
    {
        windupTriggered = true;
        if (enemy != null)
        {
            enemy.OnAttackWindup(Mathf.Max(0f, attackWindup), target);
            Machine.RaiseAIEvent(EnemyAIEventType.AttackWindup, this, target);
        }
    }
}
