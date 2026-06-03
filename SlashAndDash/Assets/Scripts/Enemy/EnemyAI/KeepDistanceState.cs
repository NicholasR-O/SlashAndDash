using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(menuName = "Enemy AI/States/Keep Distance State", fileName = "KeepDistanceState")]
public sealed class KeepDistanceState : EnemyAIState
{
    [SerializeField] float moveSpeed = 6.75f;
    [SerializeField] float minDistance = 6f;
    [SerializeField] float maxDistance = 9f;
    [SerializeField] float retreatDistance = 4f;
    [SerializeField] float repathDistance = 0.35f;
    [SerializeField] float sampleRadius = 2f;
    [SerializeField] float faceTargetTurnSpeed = 10f;
    [SerializeField] bool autoAcquirePlayer = true;
    [SerializeField] string playerTag = "Player";

    Transform owner;
    NavMeshAgent agent;
    Enemy enemy;
    Transform target;
    Vector3 lastDestination;
    bool hasDestination;

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

        FaceTarget();
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
        agent.isStopped = false;
    }

    float GetMoveSpeed()
    {
        return moveSpeed * (enemy != null ? enemy.MovementSpeedScale : 1f);
    }

    void UpdateDestination(bool force)
    {
        if (!CanUseAgent() || owner == null || target == null)
            return;

        Vector3 toTarget = target.position - owner.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        if (distance < 0.001f)
        {
            StopAgent();
            return;
        }

        float min = Mathf.Max(0f, minDistance);
        float max = Mathf.Max(min + 0.1f, maxDistance);

        Vector3 desiredDestination;
        bool shouldStop = false;

        if (distance < min)
        {
            Vector3 retreatDir = -toTarget.normalized;
            float retreat = Mathf.Max(min - distance, retreatDistance);
            desiredDestination = owner.position + retreatDir * retreat;
            agent.stoppingDistance = 0f;
        }
        else if (distance > max)
        {
            desiredDestination = target.position;
            agent.stoppingDistance = min;
        }
        else
        {
            shouldStop = true;
            desiredDestination = owner.position;
        }

        if (shouldStop)
        {
            StopAgent();
            return;
        }

        if (!force && hasDestination)
        {
            Vector3 delta = desiredDestination - lastDestination;
            delta.y = 0f;
            if (delta.magnitude < repathDistance)
                return;
        }

        if (TrySampleNavMesh(desiredDestination, out Vector3 sampled))
            desiredDestination = sampled;

        agent.SetDestination(desiredDestination);
        agent.isStopped = false;
        lastDestination = desiredDestination;
        hasDestination = true;
    }

    bool TrySampleNavMesh(Vector3 point, out Vector3 sampledPosition)
    {
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, Mathf.Max(0.1f, sampleRadius), NavMesh.AllAreas))
        {
            sampledPosition = hit.position;
            return true;
        }

        sampledPosition = point;
        return false;
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

    void FaceTarget()
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

    void StopAgent()
    {
        if (!CanUseAgent())
            return;

        agent.ResetPath();
        agent.velocity = Vector3.zero;
        agent.isStopped = true;
    }
}
