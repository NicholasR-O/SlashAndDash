using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public readonly struct EnemyAIConditionResult
{
    public bool ShouldTransition { get; }
    public Transform Target { get; }

    public EnemyAIConditionResult(bool shouldTransition, Transform target = null)
    {
        ShouldTransition = shouldTransition;
        Target = target;
    }

    public static EnemyAIConditionResult Trigger(Transform target = null)
    {
        return new EnemyAIConditionResult(true, target);
    }

    public static EnemyAIConditionResult NoTrigger()
    {
        return new EnemyAIConditionResult(false, null);
    }
}

public abstract class EnemyAIState : ScriptableObject
{
    protected StateMachine Machine { get; private set; }

    public virtual void Initialize(StateMachine machine)
    {
        Machine = machine;
    }

    public virtual void Enter(EnemyAIConditionResult transitionData) { }

    public virtual void Exit() { }

    public virtual void Tick() { }

    public virtual void FixedTick() { }
}

public abstract class EnemyAICondition : ScriptableObject
{
    protected StateMachine Machine { get; private set; }

    public virtual void Initialize(StateMachine machine)
    {
        Machine = machine;
    }

    public virtual void OnStateEntered() { }

    public abstract EnemyAIConditionResult Evaluate(EnemyAIState currentState);

    protected EnemyAIConditionResult Trigger(Transform target = null)
    {
        return EnemyAIConditionResult.Trigger(target);
    }

    protected EnemyAIConditionResult NoTrigger()
    {
        return EnemyAIConditionResult.NoTrigger();
    }

    // Backward-compatible aliases for existing condition assets/code.
    protected EnemyAIConditionResult Transition(Transform target = null)
    {
        return Trigger(target);
    }

    protected EnemyAIConditionResult Stay()
    {
        return NoTrigger();
    }
}

[System.Serializable]
public sealed class EnemyAIExitRulePrefab
{
    [SerializeField] EnemyAICondition conditionPrefab;
    [SerializeField] EnemyAIState nextStatePrefab;

    public EnemyAICondition ConditionPrefab => conditionPrefab;
    public EnemyAIState NextStatePrefab => nextStatePrefab;
}

[System.Serializable]
public sealed class EnemyAIStateEntryPrefab
{
    [SerializeField] EnemyAIState statePrefab;
    [SerializeField] List<EnemyAIExitRulePrefab> exitConditions = new List<EnemyAIExitRulePrefab>();

    public EnemyAIState StatePrefab => statePrefab;
    public List<EnemyAIExitRulePrefab> ExitConditions => exitConditions;
}

sealed class EnemyAIExitRule
{
    public EnemyAICondition Condition { get; }
    public EnemyAIState NextState { get; }

    public EnemyAIExitRule(EnemyAICondition condition, EnemyAIState nextState)
    {
        Condition = condition;
        NextState = nextState;
    }
}

[RequireComponent(typeof(Rigidbody), typeof(NavMeshAgent))]
public class StateMachine : MonoBehaviour
{
    [Header("FSM")]
    [SerializeField] List<EnemyAIStateEntryPrefab> states = new List<EnemyAIStateEntryPrefab>();

    [Header("Runtime Lock")]
    [SerializeField] bool transitionLock;

    EnemyAIState currentState;

    readonly Dictionary<EnemyAIState, EnemyAIState> runtimeStatesByPrefab = new Dictionary<EnemyAIState, EnemyAIState>();
    readonly Dictionary<EnemyAIState, List<EnemyAIExitRule>> runtimeExitRulesByState = new Dictionary<EnemyAIState, List<EnemyAIExitRule>>();
    readonly List<ScriptableObject> runtimeInstances = new List<ScriptableObject>();

    public EnemyAIState CurrentState => currentState;
    public bool IsTransitionLocked => transitionLock;

    void Awake()
    {
        BuildRuntimeGraph();
        ValidateRuntimeGraph();

        EnemyAIState initialState = ResolveInitialRuntimeState();
        ChangeState(initialState, EnemyAIConditionResult.NoTrigger());
    }

    void OnDestroy()
    {
        CleanupRuntimeGraph();
    }

    void Update()
    {
        if (currentState == null)
            return;

        currentState.Tick();

        if (!transitionLock)
            TryTransition();
    }

    void FixedUpdate()
    {
        if (currentState == null)
            return;

        currentState.FixedTick();
    }

    public void SetTransitionLock(bool shouldLock)
    {
        transitionLock = shouldLock;
    }

    void BuildRuntimeGraph()
    {
        runtimeStatesByPrefab.Clear();
        runtimeExitRulesByState.Clear();
        runtimeInstances.Clear();

        HashSet<EnemyAIState> uniqueStatePrefabs = new HashSet<EnemyAIState>();
        for (int i = 0; i < states.Count; i++)
        {
            EnemyAIStateEntryPrefab entry = states[i];
            if (entry == null)
                continue;

            CollectStatePrefab(entry.StatePrefab, uniqueStatePrefabs);

            List<EnemyAIExitRulePrefab> exits = entry.ExitConditions;
            for (int j = 0; j < exits.Count; j++)
            {
                EnemyAIExitRulePrefab exit = exits[j];
                if (exit == null)
                    continue;

                CollectStatePrefab(exit.NextStatePrefab, uniqueStatePrefabs);
            }
        }

        foreach (EnemyAIState statePrefab in uniqueStatePrefabs)
        {
            EnemyAIState runtimeState = Instantiate(statePrefab);
            runtimeState.name = statePrefab.name;
            runtimeState.Initialize(this);

            runtimeStatesByPrefab[statePrefab] = runtimeState;
            runtimeInstances.Add(runtimeState);
        }

        for (int i = 0; i < states.Count; i++)
        {
            EnemyAIStateEntryPrefab entry = states[i];
            if (entry == null)
                continue;

            EnemyAIState sourceStateRuntime = ResolveRuntimeState(entry.StatePrefab);
            if (sourceStateRuntime == null)
                continue;

            if (!runtimeExitRulesByState.TryGetValue(sourceStateRuntime, out List<EnemyAIExitRule> exitsForState))
            {
                exitsForState = new List<EnemyAIExitRule>();
                runtimeExitRulesByState[sourceStateRuntime] = exitsForState;
            }

            List<EnemyAIExitRulePrefab> exitPrefabs = entry.ExitConditions;
            for (int j = 0; j < exitPrefabs.Count; j++)
            {
                EnemyAIExitRulePrefab exitPrefab = exitPrefabs[j];
                if (exitPrefab == null || exitPrefab.ConditionPrefab == null || exitPrefab.NextStatePrefab == null)
                    continue;

                EnemyAIState nextStateRuntime = ResolveRuntimeState(exitPrefab.NextStatePrefab);
                if (nextStateRuntime == null)
                    continue;

                EnemyAICondition runtimeCondition = Instantiate(exitPrefab.ConditionPrefab);
                runtimeCondition.name = exitPrefab.ConditionPrefab.name;
                runtimeCondition.Initialize(this);
                runtimeInstances.Add(runtimeCondition);

                exitsForState.Add(new EnemyAIExitRule(runtimeCondition, nextStateRuntime));
            }
        }
    }

    void ValidateRuntimeGraph()
    {
        if (states.Count == 0)
        {
            Debug.LogWarning("StateMachine has no state entries configured.", this);
            return;
        }

        HashSet<EnemyAIState> configuredEntryStates = new HashSet<EnemyAIState>();

        for (int i = 0; i < states.Count; i++)
        {
            EnemyAIStateEntryPrefab entry = states[i];
            if (entry == null)
            {
                Debug.LogWarning("StateMachine has a null state entry at index " + i + ".", this);
                continue;
            }

            if (entry.StatePrefab == null)
            {
                Debug.LogWarning("StateMachine entry " + i + " has no state prefab assigned.", this);
                continue;
            }

            if (!configuredEntryStates.Add(entry.StatePrefab))
                Debug.LogWarning("StateMachine has duplicate state entry for '" + entry.StatePrefab.name + "'.", this);
        }

        for (int i = 0; i < states.Count; i++)
        {
            EnemyAIStateEntryPrefab entry = states[i];
            if (entry == null)
                continue;

            List<EnemyAIExitRulePrefab> exits = entry.ExitConditions;
            for (int j = 0; j < exits.Count; j++)
            {
                EnemyAIExitRulePrefab exit = exits[j];
                if (exit == null)
                {
                    Debug.LogWarning("StateMachine entry " + i + " has a null exit rule at index " + j + ".", this);
                    continue;
                }

                if (exit.ConditionPrefab == null)
                    Debug.LogWarning("StateMachine entry " + i + " exit " + j + " has no condition prefab.", this);

                if (exit.NextStatePrefab == null)
                {
                    Debug.LogWarning("StateMachine entry " + i + " exit " + j + " has no next state prefab.", this);
                    continue;
                }

                if (!configuredEntryStates.Contains(exit.NextStatePrefab))
                    Debug.LogWarning("StateMachine entry " + i + " exit " + j + " targets '" + exit.NextStatePrefab.name + "' which is not a configured state entry.", this);
            }
        }
    }

    void CleanupRuntimeGraph()
    {
        for (int i = 0; i < runtimeInstances.Count; i++)
        {
            if (runtimeInstances[i] != null)
                Destroy(runtimeInstances[i]);
        }

        runtimeInstances.Clear();
        runtimeExitRulesByState.Clear();
        runtimeStatesByPrefab.Clear();
    }

    EnemyAIState ResolveInitialRuntimeState()
    {
        for (int i = 0; i < states.Count; i++)
        {
            EnemyAIStateEntryPrefab entry = states[i];
            if (entry == null || entry.StatePrefab == null)
                continue;

            EnemyAIState runtimeState = ResolveRuntimeState(entry.StatePrefab);
            if (runtimeState != null)
                return runtimeState;
        }

        return null;
    }

    void CollectStatePrefab(EnemyAIState statePrefab, HashSet<EnemyAIState> sink)
    {
        if (statePrefab != null)
            sink.Add(statePrefab);
    }

    EnemyAIState ResolveRuntimeState(EnemyAIState statePrefab)
    {
        if (statePrefab == null)
            return null;

        runtimeStatesByPrefab.TryGetValue(statePrefab, out EnemyAIState runtimeState);
        return runtimeState;
    }

    void ChangeState(EnemyAIState nextState, EnemyAIConditionResult transitionData)
    {
        if (nextState == null || nextState == currentState)
            return;

        currentState?.Exit();
        currentState = nextState;
        currentState.Enter(transitionData);
        PrepareCurrentStateConditions();
    }

    void PrepareCurrentStateConditions()
    {
        if (!runtimeExitRulesByState.TryGetValue(currentState, out List<EnemyAIExitRule> exits))
            return;

        for (int i = 0; i < exits.Count; i++)
            exits[i].Condition.OnStateEntered();
    }

    void TryTransition()
    {
        if (!runtimeExitRulesByState.TryGetValue(currentState, out List<EnemyAIExitRule> exits))
            return;

        for (int i = 0; i < exits.Count; i++)
        {
            EnemyAIExitRule exit = exits[i];
            EnemyAIConditionResult result = exit.Condition.Evaluate(currentState);
            if (!result.ShouldTransition)
                continue;

            ChangeState(exit.NextState, result);
            return;
        }
    }
}
