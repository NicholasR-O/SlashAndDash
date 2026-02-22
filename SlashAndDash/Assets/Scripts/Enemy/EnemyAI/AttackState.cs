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
    float nextAttackAt;

    public Transform Target => target;

    public override void Initialize(StateMachine machine)
    {
        base.Initialize(machine);
        owner = machine.transform;
        agent = machine.GetComponent<NavMeshAgent>();
    }

    public override void Enter(EnemyAIConditionResult transitionData)
    {
        if (transitionData.Target != null)
            target = transitionData.Target;
        nextAttackAt = Time.time + Mathf.Max(0f, attackWindup);
        StopAgent();
    }

    public override void Tick()
    {
        StopAgent();

        if (Machine.IsTransitionLocked)
            return;

        if (target == null)
            return;

        if (Time.time < nextAttackAt)
            return;

        nextAttackAt = Time.time + attackCooldown;
        TryDealDamage();
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

    void TryDealDamage()
    {
        if (owner == null || target == null || attackDamage <= 0f)
            return;

        Vector3 delta = target.position - owner.position;
        if (horizontalDistanceOnly)
            delta.y = 0f;

        if (delta.magnitude > Mathf.Max(0f, attackRange))
            return;

        IDamageable damageable = DamageUtility.FindDamageable(target);
        if (damageable == null || !damageable.IsAlive)
            return;

        damageable.TakeDamage(attackDamage, owner.gameObject);
    }
}
