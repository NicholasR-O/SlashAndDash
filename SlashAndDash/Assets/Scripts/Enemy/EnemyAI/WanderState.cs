using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(menuName = "Enemy AI/States/Wander State", fileName = "WanderState")]
public sealed class WanderState : EnemyAIState
{
    [SerializeField] float moveSpeed = 3.5f;
    [SerializeField] float stoppingDistance = 0.2f;
    [SerializeField] float wanderRadius = 20f;
    [SerializeField] float maxDistanceFromSpawn = 30f;
    [SerializeField] float repathDistance = 0.2f;
    [SerializeField] float sampleRadius = 3f;

    Transform owner;
    NavMeshAgent agent;
    Vector3 spawnPosition;
    Vector3 destination;

    public override void Initialize(StateMachine machine)
    {
        base.Initialize(machine);
        owner = machine.transform;
        agent = machine.GetComponent<NavMeshAgent>();
        spawnPosition = owner.position;
    }

    public override void Enter(EnemyAIConditionResult transitionData)
    {
        ConfigureAgent();
        SetNewDestination();
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

        if (agent.pathPending)
            return;

        if (agent.remainingDistance <= Mathf.Max(repathDistance, stoppingDistance))
            SetNewDestination();
    }

    public override void Exit()
    {
        StopAgent();
    }

    void ConfigureAgent()
    {
        if (!CanUseAgent())
            return;

        agent.speed = moveSpeed;
        agent.stoppingDistance = stoppingDistance;
        agent.isStopped = false;
    }

    void SetNewDestination()
    {
        if (!CanUseAgent() || owner == null)
            return;

        destination = GetRandomWanderPoint();
        if (TrySampleNavMesh(destination, out Vector3 sampled))
            destination = sampled;

        agent.SetDestination(destination);
        agent.isStopped = false;
    }

    Vector3 GetRandomWanderPoint()
    {
        Vector3 currentPosition = owner.position;

        for (int i = 0; i < 16; i++)
        {
            Vector2 circle = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = currentPosition + new Vector3(circle.x, 0f, circle.y);
            candidate = ClampToSpawnRadius(candidate);

            Vector3 toCandidate = candidate - currentPosition;
            toCandidate.y = 0f;
            if (toCandidate.magnitude > 1f)
                return candidate;
        }

        return ClampToSpawnRadius(currentPosition + owner.forward * 2f);
    }

    Vector3 ClampToSpawnRadius(Vector3 candidate)
    {
        Vector3 offset = candidate - spawnPosition;
        offset.y = 0f;

        if (offset.magnitude > maxDistanceFromSpawn)
            offset = offset.normalized * maxDistanceFromSpawn;

        Vector3 clamped = spawnPosition + offset;
        clamped.y = owner.position.y;
        return clamped;
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

    void StopAgent()
    {
        if (!CanUseAgent())
            return;

        agent.ResetPath();
        agent.velocity = Vector3.zero;
        agent.isStopped = true;
    }
}
