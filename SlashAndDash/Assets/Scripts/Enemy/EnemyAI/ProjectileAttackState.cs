using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(menuName = "Enemy AI/States/Projectile Attack State", fileName = "ProjectileAttackState")]
public sealed class ProjectileAttackState : EnemyAIState
{
    [SerializeField] private float attackWindup = 0.35f;
    [SerializeField] private float attackCooldown = 1.6f;
    [SerializeField] private bool stopWhileAttacking = true;
    [SerializeField] private float faceTargetTurnSpeed = 14f;

    private NavMeshAgent agent;
    private Transform owner;
    private Transform target;
    private WizardEnemy wizard;
    private float nextAttackAt;
    private float nextWindupAt;
    private bool windupTriggered;

    public Transform Target => target;

    public override void Initialize(StateMachine machine)
    {
        base.Initialize(machine);
        agent = machine.GetComponent<NavMeshAgent>();
        owner = machine.transform;
        wizard = machine.GetComponent<WizardEnemy>();
    }

    public override void Enter(EnemyAIConditionResult transitionData)
    {
        if (transitionData.Target != null)
            target = transitionData.Target;

        ScheduleNextAttack(Mathf.Max(0f, attackWindup));

        if (stopWhileAttacking)
            StopAgent();
    }

    public override void Tick()
    {
        if (stopWhileAttacking)
            StopAgent();

        if (Machine.IsTransitionLocked)
            return;

        if (target == null || wizard == null)
            return;

        FaceTarget();

        if (!windupTriggered && Time.time >= nextWindupAt)
            TriggerWindup();

        if (Time.time < nextAttackAt)
            return;

        bool fired = wizard.FireProjectile(target);
        if (fired)
        {
            wizard.OnAttackStrike(target);
            Machine.RaiseAIEvent(EnemyAIEventType.ProjectileFired, this, target);
        }

        ScheduleNextAttack(fired
            ? Mathf.Max(0.05f, attackCooldown)
            : Mathf.Max(0.1f, attackCooldown * 0.5f));
    }

    public override void Exit()
    {
        if (stopWhileAttacking)
            StopAgent();
    }

    private void ScheduleNextAttack(float delay)
    {
        float windup = Mathf.Max(0f, attackWindup);
        nextAttackAt = Time.time + Mathf.Max(0f, delay);
        nextWindupAt = nextAttackAt - windup;
        windupTriggered = windup <= 0f;

        if (!windupTriggered && Time.time >= nextWindupAt)
            TriggerWindup();
    }

    private void TriggerWindup()
    {
        windupTriggered = true;
        if (wizard == null)
            return;

        wizard.OnAttackWindup(Mathf.Max(0f, attackWindup), target);
        Machine.RaiseAIEvent(EnemyAIEventType.AttackWindup, this, target);
    }

    private void FaceTarget()
    {
        if (owner == null || target == null)
            return;

        Vector3 toTarget = target.position - owner.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
            return;

        Quaternion desiredRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        owner.rotation = Quaternion.Slerp(
            owner.rotation,
            desiredRotation,
            Mathf.Clamp01(faceTargetTurnSpeed * Time.deltaTime));
    }

    private void StopAgent()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        agent.ResetPath();
        agent.velocity = Vector3.zero;
        agent.isStopped = true;
    }
}
