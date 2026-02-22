using UnityEngine;

[CreateAssetMenu(menuName = "Enemy AI/Conditions/Tag In View Condition", fileName = "TagInViewCondition")]
public sealed class TagInViewCondition : EnemyAICondition
{
    enum RangeMode
    {
        Any,
        InRange,
        OutOfRange
    }

    [SerializeField] string tagToDetect = "Player";
    [SerializeField] bool useCurrentStateTargetOnly;
    [SerializeField] float viewDistance = 18f;
    [SerializeField, Range(1f, 180f)] float viewAngle = 110f;
    [SerializeField] RangeMode rangeMode = RangeMode.Any;
    [SerializeField] float range = 2f;
    [SerializeField] bool horizontalRangeCheck = true;
    [SerializeField] LayerMask targetMask = ~0;
    [SerializeField] LayerMask viewBlockMask = ~0;
    [SerializeField] Vector3 eyeOffset = new Vector3(0f, 0.9f, 0f);

    readonly Collider[] overlapBuffer = new Collider[32];
    Transform owner;

    public override void Initialize(StateMachine machine)
    {
        base.Initialize(machine);
        owner = machine.transform;
    }

    public override EnemyAIConditionResult Evaluate(EnemyAIState currentState)
    {
        if (owner == null)
            return Stay();

        Transform candidate;
        if (useCurrentStateTargetOnly)
        {
            Vector3 origin = owner.position + eyeOffset;
            candidate = TryGetCurrentStateTarget(currentState);
            if (candidate == null || !IsVisible(origin, candidate))
                return Stay();
        }
        else
        {
            candidate = FindVisibleTaggedTarget();
        }

        if (candidate == null)
            return Stay();

        if (!PassesRangeFilter(candidate))
            return Stay();

        return Transition(candidate);
    }

    Transform FindVisibleTaggedTarget()
    {
        Vector3 origin = owner.position + eyeOffset;
        int hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            viewDistance,
            overlapBuffer,
            targetMask,
            QueryTriggerInteraction.Collide
        );

        Transform best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null || !col.CompareTag(tagToDetect))
                continue;

            Transform candidate = col.attachedRigidbody != null ? col.attachedRigidbody.transform : col.transform;
            if (!IsVisible(origin, candidate))
                continue;

            Vector3 toCandidate = candidate.position - origin;
            float distance = toCandidate.magnitude;
            if (distance < 0.001f)
                continue;

            float score = Vector3.Dot(owner.forward, toCandidate / distance) * 100f - distance;
            if (score <= bestScore)
                continue;

            bestScore = score;
            best = candidate;
        }

        for (int i = 0; i < hitCount; i++)
            overlapBuffer[i] = null;

        return best;
    }

    Transform TryGetCurrentStateTarget(EnemyAIState currentState)
    {
        if (currentState is ChaseState chaseState)
            return chaseState.Target;

        if (currentState is AttackState attackState)
            return attackState.Target;

        return null;
    }

    bool PassesRangeFilter(Transform target)
    {
        if (rangeMode == RangeMode.Any)
            return true;

        Vector3 delta = target.position - owner.position;
        if (horizontalRangeCheck)
            delta.y = 0f;

        float distance = delta.magnitude;
        float clampedRange = Mathf.Max(0f, range);

        if (rangeMode == RangeMode.InRange)
            return distance <= clampedRange;

        return distance > clampedRange;
    }

    bool IsVisible(Vector3 origin, Transform candidate)
    {
        if (owner == null || candidate == null)
            return false;

        Vector3 toTarget = candidate.position - origin;
        float distance = toTarget.magnitude;
        if (distance < 0.001f || distance > viewDistance)
            return false;

        Vector3 direction = toTarget / distance;
        if (Vector3.Angle(owner.forward, direction) > viewAngle * 0.5f)
            return false;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, distance, viewBlockMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform == candidate)
                return true;

            if (hit.rigidbody != null && hit.rigidbody.transform == candidate)
                return true;

            return false;
        }

        return true;
    }
}
