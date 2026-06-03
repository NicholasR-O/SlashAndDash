using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GrappleController : MonoBehaviour
{
    struct TargetCandidate
    {
        public Vector3 direction;
        public Vector3 viewportPoint;
        public float distance;
        public float angle;
        public float screenDistance;
    }

    [Header("References")]
    public Camera mainCamera;
    public Rigidbody carRigidbody;

    [Header("Hold Points")]
    public Transform carHoldPoint;
    public Transform cameraHoldPoint;

    [Header("Grapple")]
    public GrappleProjectile grappleProjectilePrefab;
    public float fireCooldown = 0.25f;
    public LayerMask enemyLayerMask = ~0;

    [Header("Aiming")]
    public float aimTimeScale = 0.35f;
    public float lockAcquireRadius = 20f;
    public float lockMaxAngle = 55f;
    public float lockStickAngleGrace = 10f;
    public float fastTargetingFullSpeed = 25f;
    public float fastTargetForwardBias = 2f;
    public float fastTargetVelocityBias = 14f;

    [Header("Target Switching")]
    public float targetSwitchInputThreshold = 2.5f;
    [Range(-1f, 1f)]
    public float targetSwitchMinDot = 0.35f;
    public float targetSwitchCooldown = 0.15f;

    [Header("Throw Targeting")]
    public float throwForce = 45f;
    public float throwArcHeight = 1.4f;
    public float throwTargetMaxDistance = 80f;
    public float holdEnemyLockRange = 40f;
    public float heldEnemyBigTargetScoreBonus = 2000f;
    public float heldEnemyBigTargetMinSize = 10f;
    public float heldEnemyRockTargetScoreBonus = 1000f;
    public float throwTrackingDuration = 2f;
    public float throwFlyingSpeedThreshold = 2.5f;
    public float throwTargetLeadTime = 0.45f;
    public float throwTargetLeadMaxDistance = 10f;
    public float throwReachRadius = 0.6f;
    public int throwReachSteps = 18;
    public LayerMask visibilityMask = ~0;

    [Header("Thrown Trail Particles")]
    public bool enableThrownTrailParticles = true;
    public float thrownTrailMinDuration = 0.25f;
    public float thrownTrailSpeedThresholdMultiplier = 0.7f;

    [Header("Input")]
    public InputActionReference aimAction;
    public InputActionReference fireAction;

    [Header("Target Indicator")]
    public bool showTargetIndicator = true;
    public Color targetIndicatorColor = new Color(1f, 0.9f, 0.25f, 0.95f);
    public Color throwTargetIndicatorColor = new Color(1f, 0.35f, 0.12f, 0.95f);
    public float targetIndicatorHeightOffset = 0.25f;
    public float targetIndicatorPulseAmplitude = 0.12f;
    public float targetIndicatorPulseSpeed = 6f;
    public int targetIndicatorSegments = 28;
    public float targetIndicatorConeLength = 0.95f;
    public float targetIndicatorConeRadius = 0.18f;
    public float targetIndicatorDistanceFromOrigin = 1.1f;

    [Header("Audio")]
    [SerializeField] private AudioClip grappleFireSFX;
    [SerializeField] private float grappleFireVolume = 1f;
    [SerializeField] private float grappleAudioSpatialBlend = 1f;
    [SerializeField] private float grappleAudioMinDistance = 1f;
    [SerializeField] private float grappleAudioMaxDistance = 24f;

    public static bool IsAimingStatic { get; private set; }

    GrappleProjectile activeProjectile;
    float fireTimer;
    float throwStartTime;
    Rigidbody recentlyThrownEnemy;
    Transform lockedTarget;
    MeshRenderer targetIndicatorRenderer;
    MeshFilter targetIndicatorFilter;
    Material targetIndicatorMaterial;
    AudioSource grappleAudioSource;
    int lastIndicatorSegmentCount = -1;
    float lastIndicatorConeRadius = -1f;
    float lastIndicatorConeLength = -1f;
    float nextTargetSwitchTime;

    public Transform LockedTarget => lockedTarget;
    public Vector3 LockedTargetAimPoint => lockedTarget != null ? GetTargetAimPoint(lockedTarget) : Vector3.zero;
    public bool IsAiming => IsAimingStatic;
    public event Action<bool> AimStateChanged;
    public event Action FirePerformed;
    public event Action EnemyGrappled;
    public event Action EnemyThrown;

    public Transform CurrentHoldPoint => IsAimingStatic
        ? (cameraHoldPoint != null ? cameraHoldPoint : carHoldPoint)
        : (carHoldPoint != null ? carHoldPoint : transform);

    void Awake()
    {
        ResolveReferenceCache();
    }

    void OnEnable()
    {
        ResolveReferenceCache();
        aimAction?.action.Enable();
        fireAction?.action.Enable();
        if (fireAction != null)
            fireAction.action.performed += OnFirePerformed;

        EnsureAudioSource();
        EnsureTargetIndicator();
        SetTargetIndicatorVisible(false);
    }

    void OnDisable()
    {
        if (fireAction != null)
            fireAction.action.performed -= OnFirePerformed;
        aimAction?.action.Disable();
        fireAction?.action.Disable();
        if (IsAimingStatic)
        {
            IsAimingStatic = false;
            AimStateChanged?.Invoke(false);
        }
        SetTargetIndicatorVisible(false);
    }

    void OnValidate()
    {
        fireCooldown = Mathf.Max(0f, fireCooldown);
        lockAcquireRadius = Mathf.Max(0.1f, lockAcquireRadius);
        lockMaxAngle = Mathf.Clamp(lockMaxAngle, 1f, 180f);
        lockStickAngleGrace = Mathf.Max(0f, lockStickAngleGrace);
        fastTargetingFullSpeed = Mathf.Max(0.01f, fastTargetingFullSpeed);
        fastTargetForwardBias = Mathf.Max(0f, fastTargetForwardBias);
        fastTargetVelocityBias = Mathf.Max(0f, fastTargetVelocityBias);
        targetSwitchInputThreshold = Mathf.Max(0f, targetSwitchInputThreshold);
        targetSwitchCooldown = Mathf.Max(0f, targetSwitchCooldown);
        throwForce = Mathf.Max(0.1f, throwForce);
        throwArcHeight = Mathf.Max(0f, throwArcHeight);
        throwTargetMaxDistance = Mathf.Max(0.1f, throwTargetMaxDistance);
        holdEnemyLockRange = Mathf.Max(0.1f, holdEnemyLockRange);
        heldEnemyBigTargetScoreBonus = Mathf.Max(0f, heldEnemyBigTargetScoreBonus);
        heldEnemyBigTargetMinSize = Mathf.Max(0.01f, heldEnemyBigTargetMinSize);
        heldEnemyRockTargetScoreBonus = Mathf.Max(0f, heldEnemyRockTargetScoreBonus);
        throwTrackingDuration = Mathf.Max(0f, throwTrackingDuration);
        throwFlyingSpeedThreshold = Mathf.Max(0f, throwFlyingSpeedThreshold);
        throwTargetLeadTime = Mathf.Max(0f, throwTargetLeadTime);
        throwTargetLeadMaxDistance = Mathf.Max(0f, throwTargetLeadMaxDistance);
        throwReachRadius = Mathf.Max(0.05f, throwReachRadius);
        throwReachSteps = Mathf.Max(3, throwReachSteps);
        thrownTrailMinDuration = Mathf.Max(0f, thrownTrailMinDuration);
        thrownTrailSpeedThresholdMultiplier = Mathf.Max(0.1f, thrownTrailSpeedThresholdMultiplier);
        targetIndicatorHeightOffset = Mathf.Max(0f, targetIndicatorHeightOffset);
        targetIndicatorPulseAmplitude = Mathf.Max(0f, targetIndicatorPulseAmplitude);
        targetIndicatorPulseSpeed = Mathf.Max(0f, targetIndicatorPulseSpeed);
        targetIndicatorSegments = Mathf.Max(6, targetIndicatorSegments);
        targetIndicatorConeLength = Mathf.Max(0.08f, targetIndicatorConeLength);
        targetIndicatorConeRadius = Mathf.Max(0.02f, targetIndicatorConeRadius);
        targetIndicatorDistanceFromOrigin = Mathf.Max(0f, targetIndicatorDistanceFromOrigin);
    }

    void Update()
    {
        ResolveReferenceCache();
        HandleAimingState();
        UpdateLockOn();
        UpdateTargetIndicator();
        if (fireTimer > 0f) fireTimer -= Time.unscaledDeltaTime;
    }

    void ResolveReferenceCache()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (carRigidbody == null)
            carRigidbody = GetComponentInParent<Rigidbody>();

        if (carHoldPoint == null)
            carHoldPoint = transform;

        if (cameraHoldPoint == null && mainCamera != null)
        {
            Transform cameraHold = mainCamera.transform.Find("HoldPointCamera");
            cameraHoldPoint = cameraHold != null ? cameraHold : mainCamera.transform;
        }
    }

    void HandleAimingState()
    {
        bool aiming = aimAction != null && aimAction.action.ReadValue<float>() > 0.1f;
        if (aiming == IsAimingStatic) return;

        IsAimingStatic = aiming;
        AimStateChanged?.Invoke(IsAimingStatic);

        if (!aiming)
        {
            lockedTarget = null;
        }
    }

    void UpdateLockOn()
    {
        if (!GameOptions.AutoAim || !IsAimingStatic || mainCamera == null || grappleProjectilePrefab == null)
        {
            lockedTarget = null;
            return;
        }

        bool isHoldingEnemy = activeProjectile != null && activeProjectile.IsHoldingEnemy;
        Transform heldEnemy = activeProjectile != null ? activeProjectile.HeldEnemyTransform : null;
        Transform excludedTarget = isHoldingEnemy ? heldEnemy : GetRecentlyThrownTargetToIgnore();
        Vector3 origin = GetTargetingOrigin(isHoldingEnemy, heldEnemy);
        float maxRange = GetLockRange(isHoldingEnemy);

        if (IsTargetLockable(lockedTarget, origin, maxRange, excludedTarget, isHoldingEnemy, lockStickAngleGrace))
            return;

        lockedTarget = FindBestLockTarget(origin, maxRange, excludedTarget, isHoldingEnemy);
    }

    float GetLockRange(bool isHoldingEnemy)
    {
        if (isHoldingEnemy)
            return Mathf.Max(0.1f, holdEnemyLockRange);

        float grappleRange = grappleProjectilePrefab != null ? grappleProjectilePrefab.maxRange : 0f;
        float acquireRange = Mathf.Max(0.1f, lockAcquireRadius);
        return grappleRange > 0f ? Mathf.Min(grappleRange, acquireRange) : acquireRange;
    }

    Vector3 GetTargetingOrigin(bool isHoldingEnemy, Transform heldEnemy)
    {
        if (isHoldingEnemy)
        {
            if (heldEnemy != null)
                return heldEnemy.position;

            if (carHoldPoint != null)
                return carHoldPoint.position;
        }

        Transform hold = CurrentHoldPoint != null ? CurrentHoldPoint : transform;
        return hold.position;
    }

    Transform FindBestLockTarget(Vector3 origin, float maxRange, Transform excludedTarget, bool isHoldingEnemy)
    {
        Collider[] hits = Physics.OverlapSphere(origin, maxRange, enemyLayerMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
            return null;

        HashSet<Transform> seen = new HashSet<Transform>();
        Vector3 carVelocity = carRigidbody != null ? carRigidbody.linearVelocity : Vector3.zero;
        carVelocity.y = 0f;
        Vector3 carMoveDirection = carVelocity.sqrMagnitude > 0.01f ? carVelocity.normalized : Vector3.zero;
        float speedRatio = Mathf.Clamp01(carVelocity.magnitude / Mathf.Max(0.01f, fastTargetingFullSpeed));
        float forwardBias = Mathf.Lerp(10f, 10f * Mathf.Max(1f, fastTargetForwardBias), speedRatio);
        float velocityBias = Mathf.Max(0f, fastTargetVelocityBias) * speedRatio;
        Transform bestTarget = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Transform candidate = GetCandidateTarget(hits[i]);
            if (candidate == null || !seen.Add(candidate))
                continue;
            if (!TryBuildTargetCandidate(candidate, origin, maxRange, excludedTarget, isHoldingEnemy, 0f, out TargetCandidate target))
                continue;

            float maxAngle = Mathf.Max(0.01f, lockMaxAngle);
            float angleScore = 1f - Mathf.Clamp01(target.angle / maxAngle);
            float screenScore = 1f - Mathf.Clamp01(target.screenDistance / 0.65f);
            float distanceScore = 1f - Mathf.Clamp01(target.distance / Mathf.Max(0.01f, maxRange));
            float score = angleScore * forwardBias + screenScore * 30f + distanceScore * 20f;
            if (carMoveDirection.sqrMagnitude > 0.001f)
                score += Vector3.Dot(carMoveDirection, target.direction) * velocityBias;
            if (isHoldingEnemy)
                score += GetHeldEnemyTargetScoreBonus(candidate);

            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }

    float GetHeldEnemyTargetScoreBonus(Transform candidate)
    {
        if (candidate == null)
            return 0f;

        if (IsBigEnemyTarget(candidate))
            return Mathf.Max(0f, heldEnemyBigTargetScoreBonus);

        if (IsRockEnemyTarget(candidate))
            return Mathf.Max(0f, heldEnemyRockTargetScoreBonus);

        return 0f;
    }

    bool IsBigEnemyTarget(Transform candidate)
    {
        Enemy enemy = candidate != null ? candidate.GetComponent<Enemy>() : null;
        return enemy != null && enemy.Size >= Mathf.Max(0.01f, heldEnemyBigTargetMinSize);
    }

    static bool IsRockEnemyTarget(Transform candidate)
    {
        return candidate != null && candidate.GetComponent<StoneEnemy>() != null;
    }

    public void HandleTargetSwitchInput(Vector2 lookInput)
    {
        if (!GameOptions.AutoAim)
            return;
        if (!IsAimingStatic || mainCamera == null || lockedTarget == null || grappleProjectilePrefab == null)
            return;
        if (Time.unscaledTime < nextTargetSwitchTime)
            return;
        if (lookInput.sqrMagnitude < targetSwitchInputThreshold * targetSwitchInputThreshold)
            return;

        bool isHoldingEnemy = activeProjectile != null && activeProjectile.IsHoldingEnemy;
        Transform heldEnemy = activeProjectile != null ? activeProjectile.HeldEnemyTransform : null;
        Transform excludedTarget = isHoldingEnemy ? heldEnemy : GetRecentlyThrownTargetToIgnore();
        Vector3 origin = GetTargetingOrigin(isHoldingEnemy, heldEnemy);
        float maxRange = GetLockRange(isHoldingEnemy);

        if (!IsTargetLockable(lockedTarget, origin, maxRange, excludedTarget, isHoldingEnemy))
            return;

        Vector2 inputDir = lookInput.normalized;
        Transform next = FindDirectionalTarget(origin, maxRange, excludedTarget, inputDir, isHoldingEnemy);
        if (next == null)
            return;

        lockedTarget = next;
        nextTargetSwitchTime = Time.unscaledTime + Mathf.Max(0f, targetSwitchCooldown);
    }

    Transform FindDirectionalTarget(Vector3 origin, float maxRange, Transform excludedTarget, Vector2 inputDir, bool isHoldingEnemy)
    {
        Collider[] hits = Physics.OverlapSphere(origin, maxRange, enemyLayerMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
            return null;

        if (!TryBuildTargetCandidate(lockedTarget, origin, maxRange, excludedTarget, isHoldingEnemy, lockStickAngleGrace, out TargetCandidate currentTarget))
            return null;

        Vector2 currentScreenPoint = new Vector2(currentTarget.viewportPoint.x, currentTarget.viewportPoint.y);
        HashSet<Transform> seen = new HashSet<Transform>();
        Transform bestTarget = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Transform candidate = GetCandidateTarget(hits[i]);
            if (candidate == null || candidate == lockedTarget || !seen.Add(candidate))
                continue;
            if (!TryBuildTargetCandidate(candidate, origin, maxRange, excludedTarget, isHoldingEnemy, 0f, out TargetCandidate target))
                continue;

            Vector2 candidateScreenPoint = new Vector2(target.viewportPoint.x, target.viewportPoint.y);
            Vector2 toCandidate = new Vector2(
                candidateScreenPoint.x - currentScreenPoint.x,
                candidateScreenPoint.y - currentScreenPoint.y
            );
            if (toCandidate.sqrMagnitude < 0.0001f)
                continue;

            Vector2 candidateDir = toCandidate.normalized;
            float alignment = Vector2.Dot(inputDir, candidateDir);
            if (alignment < targetSwitchMinDot)
                continue;

            float score = alignment * 100f - target.distance - target.screenDistance * 10f;
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }

    Transform GetCandidateTarget(Collider c)
    {
        if (c == null)
            return null;

        if (c.attachedRigidbody != null)
        {
            Enemy bodyEnemy = c.attachedRigidbody.GetComponent<Enemy>();
            if (bodyEnemy != null)
                return bodyEnemy.transform;

            ThrowableVase bodyThrowable = c.attachedRigidbody.GetComponent<ThrowableVase>();
            if (bodyThrowable != null)
                return bodyThrowable.transform;
        }

        Enemy enemy = c.GetComponentInParent<Enemy>();
        if (enemy != null)
            return enemy.transform;

        ThrowableVase throwable = c.GetComponentInParent<ThrowableVase>();
        if (throwable != null)
            return throwable.transform;

        return TargetingUtility.FindTaggedTransform(c, "Enemy");
    }

    bool IsTargetLockable(Transform candidate, Vector3 origin, float maxRange, Transform excludedTarget, bool isHoldingEnemy, float angleGrace = 0f)
    {
        return TryBuildTargetCandidate(candidate, origin, maxRange, excludedTarget, isHoldingEnemy, angleGrace, out _);
    }

    bool TryBuildTargetCandidate(
        Transform candidate,
        Vector3 origin,
        float maxRange,
        Transform excludedTarget,
        bool isHoldingEnemy,
        float angleGrace,
        out TargetCandidate target)
    {
        target = default;
        if (candidate == null || candidate == excludedTarget)
            return false;
        if ((enemyLayerMask.value & (1 << candidate.gameObject.layer)) == 0)
            return false;

        Enemy enemy = candidate.GetComponent<Enemy>();
        ThrowableVase throwable = candidate.GetComponent<ThrowableVase>();

        if (isHoldingEnemy)
        {
            if (enemy == null || !enemy.IsAlive)
                return false;
        }
        else
        {
            if (enemy != null)
            {
                if (!enemy.IsAlive || !enemy.CanBeGrappled)
                    return false;
            }
            else if (throwable != null)
            {
                if (!throwable.CanBeGrappled)
                    return false;
            }
            else
            {
                return false;
            }
        }

        Collider candidateCollider = TargetingUtility.GetBestCollider(candidate);
        if (candidateCollider == null)
            return false;

        Vector3 targetPoint = GetTargetAimPoint(candidate);
        Vector3 rangePoint = GetTargetRangePoint(candidate, candidateCollider, origin);
        float distance = Vector3.Distance(origin, rangePoint);
        if (distance > maxRange)
            return false;

        Vector3 cameraOrigin = mainCamera != null ? mainCamera.transform.position : origin;
        Vector3 toTargetFromCamera = targetPoint - cameraOrigin;
        if (toTargetFromCamera.sqrMagnitude < 0.001f)
            return false;

        Vector3 targetDirection = toTargetFromCamera.normalized;
        float angle = mainCamera != null
            ? Vector3.Angle(mainCamera.transform.forward, targetDirection)
            : 0f;

        Vector3 viewportPoint = mainCamera != null
            ? mainCamera.WorldToViewportPoint(targetPoint)
            : new Vector3(0.5f, 0.5f, 1f);

        if (!isHoldingEnemy && !HasLineOfSight(origin, candidateCollider, candidate))
            return false;
        if (!CanCurrentShotReachTarget(origin, candidate, candidateCollider, isHoldingEnemy))
            return false;

        float screenDistance = viewportPoint.z > 0f
            ? Vector2.Distance(new Vector2(viewportPoint.x, viewportPoint.y), new Vector2(0.5f, 0.5f))
            : 1f;

        target = new TargetCandidate
        {
            direction = targetDirection,
            viewportPoint = viewportPoint,
            distance = distance,
            angle = angle,
            screenDistance = screenDistance
        };

        return true;
    }

    bool CanCurrentShotReachTarget(Vector3 origin, Transform candidate, Collider candidateCollider, bool isHoldingEnemy)
    {
        if (isHoldingEnemy)
            return CanHeldEnemyThrowReachTarget(origin, candidate, candidateCollider);

        return CanGrappleReachTarget(origin, candidate, candidateCollider);
    }

    bool CanGrappleReachTarget(Vector3 origin, Transform candidate, Collider candidateCollider)
    {
        if (grappleProjectilePrefab == null || candidate == null || candidateCollider == null)
            return false;

        Vector3 targetPoint = GetTargetRangePoint(candidate, candidateCollider, origin);
        Vector3 toTarget = targetPoint - origin;
        float distance = toTarget.magnitude;
        if (distance < 0.001f || distance > Mathf.Max(0.01f, grappleProjectilePrefab.maxRange))
            return false;

        Vector3 direction = toTarget / distance;
        float radius = Mathf.Max(0.01f, grappleProjectilePrefab.hitRadius);
        RaycastHit[] hits = Physics.SphereCastAll(origin, radius, direction, distance, enemyLayerMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
            return false;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null)
                continue;

            Transform hitTarget = GetCandidateTarget(hit.collider);
            if (hitTarget == candidate || hit.collider == candidateCollider || TargetingUtility.RaycastHitBelongsTo(hit, candidate))
                return true;

            if (hitTarget != null)
                return false;
        }

        return false;
    }

    bool CanHeldEnemyThrowReachTarget(Vector3 origin, Transform candidate, Collider candidateCollider)
    {
        if (candidate == null || candidateCollider == null)
            return false;

        Vector3 targetPoint = GetPredictedThrowTargetPoint(origin, candidate, candidateCollider);
        if (!TryComputeBallisticVelocity(origin, targetPoint, Mathf.Max(0.1f, throwForce), out Vector3 velocity))
            return false;

        return ThrowPathReachesTarget(origin, velocity, targetPoint, candidate, candidateCollider);
    }

    bool ThrowPathReachesTarget(Vector3 origin, Vector3 velocity, Vector3 targetPoint, Transform target, Collider targetCollider)
    {
        float horizontalSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
        Vector3 horizontalDelta = new Vector3(targetPoint.x - origin.x, 0f, targetPoint.z - origin.z);
        float flightTime = horizontalSpeed > 0.001f
            ? horizontalDelta.magnitude / horizontalSpeed
            : Vector3.Distance(origin, targetPoint) / Mathf.Max(0.1f, velocity.magnitude);
        flightTime = Mathf.Clamp(flightTime + 0.15f, 0.1f, 3f);

        int steps = Mathf.Max(3, throwReachSteps);
        float radius = Mathf.Max(0.05f, throwReachRadius);
        Vector3 previous = origin;

        for (int i = 1; i <= steps; i++)
        {
            float t = flightTime * i / steps;
            Vector3 current = EvaluateThrowPosition(origin, velocity, t);
            Vector3 segment = current - previous;
            float segmentDistance = segment.magnitude;

            if (targetCollider != null && Vector3.Distance(targetCollider.ClosestPoint(current), current) <= radius)
                return true;

            if (segmentDistance > 0.001f)
            {
                RaycastHit[] hits = Physics.SphereCastAll(
                    previous,
                    radius,
                    segment / segmentDistance,
                    segmentDistance,
                    visibilityMask,
                    QueryTriggerInteraction.Ignore);

                if (hits != null && hits.Length > 0)
                {
                    Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                    for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
                    {
                        RaycastHit hit = hits[hitIndex];
                        if (hit.collider == null)
                            continue;

                        if (hit.collider == targetCollider || TargetingUtility.RaycastHitBelongsTo(hit, target))
                            return true;

                        if (ShouldIgnoreVisibilityHit(hit.collider))
                            continue;

                        return false;
                    }
                }
            }

            previous = current;
        }

        return false;
    }

    Vector3 EvaluateThrowPosition(Vector3 origin, Vector3 velocity, float time)
    {
        return origin + velocity * time + 0.5f * Physics.gravity * time * time;
    }

    bool HasLineOfSight(Vector3 origin, Collider targetCollider, Transform targetTransform)
    {
        Vector3 targetPoint = targetCollider.bounds.center;
        Vector3 toTarget = targetPoint - origin;
        float distance = toTarget.magnitude;
        if (distance < 0.001f) return false;

        Vector3 dir = toTarget / distance;
        RaycastHit[] hits = Physics.RaycastAll(origin, dir, distance, visibilityMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return true;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null)
                continue;

            if (hit.collider == targetCollider || TargetingUtility.RaycastHitBelongsTo(hit, targetTransform))
                return true;

            if (ShouldIgnoreVisibilityHit(hit.collider))
                continue;

            return false;
        }

        return true;
    }

    bool ShouldIgnoreVisibilityHit(Collider hitCollider)
    {
        if (hitCollider == null)
            return true;

        Transform hitTransform = hitCollider.attachedRigidbody != null
            ? hitCollider.attachedRigidbody.transform
            : hitCollider.transform;

        if (hitTransform == transform || hitTransform.IsChildOf(transform))
            return true;

        if (carRigidbody != null &&
            (hitTransform == carRigidbody.transform || hitTransform.IsChildOf(carRigidbody.transform)))
        {
            return true;
        }

        Transform heldEnemy = activeProjectile != null ? activeProjectile.HeldEnemyTransform : null;
        if (heldEnemy != null && (hitTransform == heldEnemy || hitTransform.IsChildOf(heldEnemy)))
            return true;

        return false;
    }

    Transform GetRecentlyThrownTargetToIgnore()
    {
        if (recentlyThrownEnemy == null)
            return null;

        if (Time.unscaledTime - throwStartTime > throwTrackingDuration)
        {
            recentlyThrownEnemy = null;
            return null;
        }

        if (recentlyThrownEnemy.linearVelocity.sqrMagnitude > throwFlyingSpeedThreshold * throwFlyingSpeedThreshold)
            return recentlyThrownEnemy.transform;

        recentlyThrownEnemy = null;
        return null;
    }

    void OnFirePerformed(InputAction.CallbackContext _)
    {
        OnFirePressed();
    }

    void OnFirePressed()
    {
        ResolveReferenceCache();
        UpdateLockOn();

        if (activeProjectile != null && activeProjectile.IsHoldingEnemy)
        {
            LaunchHeldEnemy();
            return;
        }

        if (!IsAimingStatic) return;
        if (activeProjectile != null) return;
        if (fireTimer > 0f) return;

        fireTimer = fireCooldown;

        Transform hold = CurrentHoldPoint;
        if (hold == null)
            hold = transform;

        GrappleProjectile projectile = Instantiate(grappleProjectilePrefab, hold.position, hold.rotation);
        Vector3 direction = mainCamera != null ? mainCamera.transform.forward : hold.forward;

        if (lockedTarget != null)
        {
            Vector3 toTarget = GetTargetAimPoint(lockedTarget) - hold.position;
            if (toTarget.sqrMagnitude > 0.0001f)
                direction = toTarget.normalized;
        }

        projectile.Initialize(this, direction);
        activeProjectile = projectile;
        PlayGrappleFireAudio();
        FirePerformed?.Invoke();
    }

    void LaunchHeldEnemy()
    {
        Transform throwTarget = lockedTarget;
        Enemy heldEnemy = activeProjectile.HeldEnemyComponent;
        ThrowableVase heldThrowableVase = activeProjectile.HeldThrowableVase;
        Rigidbody enemyRb = activeProjectile.ReleaseEnemy();
        if (enemyRb != null)
        {
            enemyRb.linearVelocity = Vector3.zero;
            Vector3 targetPoint = GetThrowTargetPoint(enemyRb.position, throwTarget);
            Vector3 throwVelocity = ComputeThrowVelocity(enemyRb.position, targetPoint, throwTarget);
            enemyRb.AddForce(throwVelocity, ForceMode.VelocityChange);
            recentlyThrownEnemy = enemyRb;
            throwStartTime = Time.unscaledTime;
            FirePerformed?.Invoke();

            if (heldEnemy != null)
            {
                EnemyThrown?.Invoke();
                heldEnemy.ArmExplosion();
            }
            else if (heldThrowableVase != null)
            {
                heldThrowableVase.OnThrownByGrapple(gameObject);
            }

            AttachThrownTrail(enemyRb);
        }

        lockedTarget = null;

        activeProjectile.DestroySelf();
        activeProjectile = null;
    }

    void AttachThrownTrail(Rigidbody enemyRb)
    {
        if (!enableThrownTrailParticles || enemyRb == null)
            return;

        EnemyThrownTrail trail = enemyRb.GetComponent<EnemyThrownTrail>();
        if (trail == null)
            trail = enemyRb.gameObject.AddComponent<EnemyThrownTrail>();

        float minDuration = Mathf.Max(0f, thrownTrailMinDuration);
        float maxDuration = Mathf.Max(minDuration + 0.05f, throwTrackingDuration);
        float thresholdMultiplier = Mathf.Max(0.1f, thrownTrailSpeedThresholdMultiplier);
        float stopSpeedThreshold = Mathf.Max(0.05f, throwFlyingSpeedThreshold * thresholdMultiplier);
        trail.Play(minDuration, maxDuration, stopSpeedThreshold);
    }

    public void OnProjectileFinished()
    {
        activeProjectile = null;
    }

    public void NotifyEnemyGrappled()
    {
        EnemyGrappled?.Invoke();
    }

    Vector3 GetTargetAimPoint(Transform target)
    {
        return TargetingUtility.GetAimPoint(target);
    }

    Vector3 GetTargetRangePoint(Transform target, Collider targetCollider, Vector3 origin)
    {
        if (targetCollider != null)
            return targetCollider.ClosestPoint(origin);

        return GetTargetAimPoint(target);
    }

    Vector3 GetThrowAimPoint(Vector3 throwOrigin, Transform throwTarget, Collider targetCollider)
    {
        if (targetCollider == null)
            return GetTargetAimPoint(throwTarget);

        Vector3 closest = targetCollider.ClosestPoint(throwOrigin);
        if ((closest - throwOrigin).sqrMagnitude > 0.0001f)
            return closest;

        return GetTargetAimPoint(throwTarget);
    }

    Vector3 GetThrowTargetPoint(Vector3 throwOrigin, Transform throwTarget)
    {
        if (throwTarget != null)
            return GetPredictedThrowTargetPoint(
                throwOrigin,
                throwTarget,
                TargetingUtility.GetBestCollider(throwTarget, includeTriggers: false));

        if (mainCamera == null)
            return throwOrigin + transform.forward * 8f;

        Ray lookRay = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
        {
            int groundMask = 1 << groundLayer;
            if (Physics.Raycast(lookRay, out RaycastHit groundHit, throwTargetMaxDistance, groundMask, QueryTriggerInteraction.Ignore))
                return groundHit.point;
        }

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(lookRay, out float enterDistance))
            return lookRay.GetPoint(Mathf.Min(enterDistance, throwTargetMaxDistance));

        return lookRay.GetPoint(throwTargetMaxDistance);
    }

    Vector3 GetPredictedThrowTargetPoint(Vector3 throwOrigin, Transform throwTarget, Collider targetCollider = null)
    {
        Vector3 targetPoint = GetThrowAimPoint(throwOrigin, throwTarget, targetCollider);
        Vector3 targetVelocity = GetTargetVelocity(throwTarget);
        if (targetVelocity.sqrMagnitude < 0.01f || throwTargetLeadTime <= 0f)
            return targetPoint;

        float speed = Mathf.Max(0.1f, throwForce);
        float distance = Vector3.Distance(throwOrigin, targetPoint);
        float travelTime = Mathf.Min(Mathf.Max(0f, throwTargetLeadTime), distance / speed);
        Vector3 lead = targetVelocity * travelTime;
        float maxLead = Mathf.Max(0f, throwTargetLeadMaxDistance);
        if (lead.sqrMagnitude > maxLead * maxLead)
            lead = lead.normalized * maxLead;

        return targetPoint + lead;
    }

    Vector3 GetTargetVelocity(Transform target)
    {
        if (target == null)
            return Vector3.zero;

        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb != null)
            return targetRb.linearVelocity;

        UnityEngine.AI.NavMeshAgent targetAgent = target.GetComponent<UnityEngine.AI.NavMeshAgent>();
        return targetAgent != null ? targetAgent.velocity : Vector3.zero;
    }

    Vector3 ComputeThrowVelocity(Vector3 origin, Vector3 target, Transform throwTarget)
    {
        float speed = Mathf.Max(0.1f, throwForce);
        if (throwTarget != null && TryComputeBallisticVelocity(origin, target, speed, out Vector3 ballisticVelocity))
            return ballisticVelocity;

        Vector3 direction = ComputeArcDirection(origin, target);
        return direction * speed;
    }

    bool TryComputeBallisticVelocity(Vector3 origin, Vector3 target, float speed, out Vector3 velocity)
    {
        velocity = Vector3.zero;

        Vector3 toTarget = target - origin;
        Vector3 planar = new Vector3(toTarget.x, 0f, toTarget.z);
        float horizontalDistance = planar.magnitude;
        if (horizontalDistance < 0.001f)
        {
            velocity = ComputeArcDirection(origin, target) * speed;
            return true;
        }

        float gravity = Mathf.Abs(Physics.gravity.y);
        if (gravity < 0.001f)
        {
            velocity = ComputeArcDirection(origin, target) * speed;
            return true;
        }

        float verticalDistance = toTarget.y;
        float speedSquared = speed * speed;
        float root = speedSquared * speedSquared -
            gravity * (gravity * horizontalDistance * horizontalDistance + 2f * verticalDistance * speedSquared);
        if (root < 0f)
            return false;

        float sqrtRoot = Mathf.Sqrt(root);
        float lowArcTan = (speedSquared - sqrtRoot) / (gravity * horizontalDistance);
        float highArcTan = (speedSquared + sqrtRoot) / (gravity * horizontalDistance);
        float arcPreference = Mathf.Clamp01(throwArcHeight / 4f);
        float selectedTan = Mathf.Lerp(lowArcTan, highArcTan, arcPreference);
        float angle = Mathf.Atan(selectedTan);

        Vector3 planarDirection = planar / horizontalDistance;
        float horizontalSpeed = Mathf.Cos(angle) * speed;
        float verticalSpeed = Mathf.Sin(angle) * speed;
        velocity = planarDirection * horizontalSpeed + Vector3.up * verticalSpeed;
        return velocity.sqrMagnitude > 0.001f;
    }

    Vector3 ComputeArcDirection(Vector3 origin, Vector3 target)
    {
        Vector3 elevatedTarget = target + Vector3.up * Mathf.Max(0f, throwArcHeight);
        Vector3 toTarget = elevatedTarget - origin;
        if (toTarget.sqrMagnitude < 0.0001f)
            return mainCamera != null ? mainCamera.transform.forward : transform.forward;

        Vector3 direction = toTarget.normalized;
        if (direction.y < 0.05f)
            direction = (direction + Vector3.up * 0.08f).normalized;

        return direction;
    }

    void OnDrawGizmosSelected()
    {
        if (mainCamera == null || grappleProjectilePrefab == null) return;

        Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
        Gizmos.DrawWireSphere(mainCamera.transform.position, lockAcquireRadius);

        if (lockedTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(mainCamera.transform.position, lockedTarget.position);
            Gizmos.DrawWireSphere(lockedTarget.position, 0.35f);
        }
    }

    void EnsureTargetIndicator()
    {
        if (!showTargetIndicator || targetIndicatorRenderer != null)
            return;

        GameObject indicatorObject = new GameObject("TargetIndicator");
        indicatorObject.transform.SetParent(transform, false);

        targetIndicatorFilter = indicatorObject.AddComponent<MeshFilter>();
        targetIndicatorRenderer = indicatorObject.AddComponent<MeshRenderer>();
        targetIndicatorRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        targetIndicatorRenderer.receiveShadows = false;

        targetIndicatorMaterial = new Material(Shader.Find("Sprites/Default"));
        targetIndicatorRenderer.material = targetIndicatorMaterial;
        targetIndicatorRenderer.sortingOrder = 2000;
        UpdateIndicatorConeMeshIfNeeded();
    }

    void UpdateTargetIndicator()
    {
        if (!showTargetIndicator || !IsAimingStatic || lockedTarget == null)
        {
            SetTargetIndicatorVisible(false);
            return;
        }

        EnsureTargetIndicator();
        if (targetIndicatorRenderer == null || targetIndicatorFilter == null)
            return;

        float pulse = Mathf.Sin(Time.unscaledTime * Mathf.Max(0f, targetIndicatorPulseSpeed)) * Mathf.Max(0f, targetIndicatorPulseAmplitude);
        Color indicatorColor = activeProjectile != null && activeProjectile.IsHoldingEnemy
            ? throwTargetIndicatorColor
            : targetIndicatorColor;

        Vector3 targetPoint = GetTargetAimPoint(lockedTarget);
        Bounds targetBounds;
        bool hasBounds = TryGetTargetBounds(lockedTarget, out targetBounds);
        Vector3 visualCenter = hasBounds ? targetBounds.center : targetPoint;
        float hoverHeight = Mathf.Max(0.05f, targetIndicatorHeightOffset + targetIndicatorDistanceFromOrigin + pulse);
        float topY = hasBounds ? targetBounds.max.y : targetPoint.y;
        Vector3 indicatorPosition = new Vector3(visualCenter.x, topY + hoverHeight, visualCenter.z);
        Vector3 direction = Vector3.down;

        targetIndicatorRenderer.transform.SetParent(transform, true);
        targetIndicatorRenderer.transform.position = indicatorPosition;
        targetIndicatorRenderer.transform.rotation = Quaternion.LookRotation(direction, Vector3.forward);
        targetIndicatorMaterial.color = indicatorColor;
        UpdateIndicatorConeMeshIfNeeded();

        SetTargetIndicatorVisible(true);
    }

    bool TryGetTargetBounds(Transform target, out Bounds bounds)
    {
        Collider collider = TargetingUtility.GetBestCollider(target, includeTriggers: false);
        if (collider != null)
        {
            bounds = collider.bounds;
            return true;
        }

        Renderer[] renderers = target != null ? target.GetComponentsInChildren<Renderer>() : null;
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                    continue;

                bounds = renderer.bounds;
                for (int j = i + 1; j < renderers.Length; j++)
                {
                    Renderer extraRenderer = renderers[j];
                    if (extraRenderer != null && extraRenderer.enabled)
                        bounds.Encapsulate(extraRenderer.bounds);
                }

                return true;
            }
        }

        bounds = default;
        return false;
    }

    void UpdateIndicatorConeMeshIfNeeded()
    {
        if (targetIndicatorFilter == null)
            return;

        int segments = Mathf.Max(6, targetIndicatorSegments);
        float coneRadius = Mathf.Max(0.02f, targetIndicatorConeRadius);
        float coneLength = Mathf.Max(0.08f, targetIndicatorConeLength);
        if (segments == lastIndicatorSegmentCount &&
            Mathf.Approximately(coneRadius, lastIndicatorConeRadius) &&
            Mathf.Approximately(coneLength, lastIndicatorConeLength))
        {
            return;
        }

        Mesh mesh = new Mesh();
        mesh.name = "TargetIndicatorCone";

        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 6];

        vertices[0] = new Vector3(0f, 0f, coneLength);
        vertices[1] = Vector3.zero;

        float step = Mathf.PI * 2f / segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = step * i;
            vertices[i + 2] = new Vector3(Mathf.Cos(angle) * coneRadius, Mathf.Sin(angle) * coneRadius, 0f);
        }

        for (int i = 0; i < segments; i++)
        {
            int current = i + 2;
            int next = ((i + 1) % segments) + 2;

            int sideTriIndex = i * 3;
            triangles[sideTriIndex] = 0;
            triangles[sideTriIndex + 1] = current;
            triangles[sideTriIndex + 2] = next;

            int capTriIndex = segments * 3 + i * 3;
            triangles[capTriIndex] = 1;
            triangles[capTriIndex + 1] = next;
            triangles[capTriIndex + 2] = current;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        targetIndicatorFilter.sharedMesh = mesh;
        lastIndicatorSegmentCount = segments;
        lastIndicatorConeRadius = coneRadius;
        lastIndicatorConeLength = coneLength;
    }

    void SetTargetIndicatorVisible(bool visible)
    {
        if (targetIndicatorRenderer == null)
            return;

        targetIndicatorRenderer.enabled = visible;
        if (!visible)
            targetIndicatorRenderer.transform.SetParent(transform, false);
    }

    void EnsureAudioSource()
    {
        if (grappleAudioSource != null)
            return;

        grappleAudioSource = AudioPlaybackUtility.EnsureChildAudioSource(
            transform,
            "GrappleAudio",
            loop: false,
            playOnAwake: false,
            spatialBlend: grappleAudioSpatialBlend,
            minDistance: grappleAudioMinDistance,
            maxDistance: grappleAudioMaxDistance);
    }

    void PlayGrappleFireAudio()
    {
        if (grappleFireSFX == null)
            return;

        EnsureAudioSource();
        if (grappleAudioSource == null)
            return;

        grappleAudioSource.pitch = 1f;
        grappleAudioSource.PlayOneShot(grappleFireSFX, Mathf.Clamp01(grappleFireVolume * GameOptions.SoundEffectsVolume));
    }
}
