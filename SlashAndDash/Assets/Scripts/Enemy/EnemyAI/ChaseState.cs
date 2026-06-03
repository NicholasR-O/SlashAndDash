using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(menuName = "Enemy AI/States/Chase State", fileName = "ChaseState")]
public sealed class ChaseState : EnemyAIState
{
    [SerializeField] float moveSpeed = 6.75f;
    [SerializeField] float stoppingDistance = 1.25f;
    [SerializeField] float repathDistance = 0.25f;
    [SerializeField] bool autoAcquirePlayer = true;
    [SerializeField] string playerTag = "Player";

    NavMeshAgent agent;
    Enemy enemy;
    Transform target;

    public Transform Target => target;

    public override void Initialize(StateMachine machine)
    {
        base.Initialize(machine);
        agent = machine.GetComponent<NavMeshAgent>();
        enemy = machine.GetComponent<Enemy>();
    }

    public override void Enter(EnemyAIConditionResult transitionData)
    {
        if (transitionData.Target != null)
            target = transitionData.Target;
        else
            TryAutoAcquireTarget();

        ConfigureAgent();
        UpdateDestination(force: true);
    }

    public override void Tick()
    {
        if (!CanUseAgent())
            return;

        if (Machine.IsTransitionLocked)
        {
            StopAgent();
            return;
        }

        if (target == null)
        {
            TryAutoAcquireTarget();
            if (target == null)
            {
                StopAgent();
                return;
            }
        }

        UpdateDestination(force: false);
    }

    public override void Exit()
    {
        StopAgent();
    }

    void ConfigureAgent()
    {
        if (!CanUseAgent())
            return;

        float scaledSpeed = GetMoveSpeed();
        agent.speed = scaledSpeed;
        agent.acceleration = Mathf.Max(agent.acceleration, scaledSpeed * 2f);
        agent.stoppingDistance = stoppingDistance;
        agent.isStopped = false;
    }

    float GetMoveSpeed()
    {
        return moveSpeed * (enemy != null ? enemy.MovementSpeedScale : 1f);
    }

    void UpdateDestination(bool force)
    {
        if (!CanUseAgent() || target == null)
            return;

        Vector3 currentDestination = agent.destination;
        if (!force)
        {
            Vector3 delta = target.position - currentDestination;
            delta.y = 0f;
            if (delta.magnitude < repathDistance)
                return;
        }

        agent.SetDestination(target.position);
        agent.isStopped = false;
    }

    bool CanUseAgent()
    {
        return agent != null && agent.enabled && agent.isOnNavMesh;
    }

    void TryAutoAcquireTarget()
    {
        if (!autoAcquirePlayer || target != null || string.IsNullOrEmpty(playerTag))
            return;

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null)
            target = player.transform;
    }

    void StopAgent()
    {
        if (!CanUseAgent())
            return;

        agent.ResetPath();
        agent.velocity = Vector3.zero;
        agent.isStopped = true;
    }
}
