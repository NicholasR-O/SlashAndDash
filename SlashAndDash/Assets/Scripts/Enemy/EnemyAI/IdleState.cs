using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(menuName = "Enemy AI/States/Idle State", fileName = "IdleState")]
public sealed class IdleState : EnemyAIState
{
    NavMeshAgent agent;

    public override void Initialize(StateMachine machine)
    {
        base.Initialize(machine);
        agent = machine.GetComponent<NavMeshAgent>();
    }

    public override void Enter(EnemyAIConditionResult transitionData)
    {
        StopAgent();
    }

    public override void Tick()
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
}
