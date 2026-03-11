using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GrappleController : MonoBehaviour
{
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
    public float throwLockSuppressTime = 0.35f;
    public float throwTrackingDuration = 2f;
    public float throwFlyingSpeedThreshold = 2.5f;
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
    public float targetIndicatorLineWidth = 0.06f;
    public float targetIndicatorRadiusPadding = 0.2f;
    public float targetIndicatorHeightOffset = 0.25f;
    public float targetIndicatorPulseAmplitude = 0.12f;
    public float targetIndicatorPulseSpeed = 6f;
    public int targetIndicatorSegments = 28;
    public float targetIndicatorConeLength = 0.95f;
    public float targetIndicatorConeRadius = 0.18f;
    public float targetIndicatorDistanceFromOrigin = 1.1f;

    public static bool IsAimingStatic { get; private set; }

    GrappleProjectile activeProjectile;
    float fireTimer;
    float lockSuppressedUntil;
    float throwStartTime;
    Rigidbody recentlyThrownEnemy;
    Transform lockedTarget;
    MeshRenderer targetIndicatorRenderer;
    MeshFilter targetIndicatorFilter;
    Material targetIndicatorMaterial;
    int lastIndicatorSegmentCount = -1;
    float lastIndicatorConeRadius = -1f;
    float lastIndicatorConeLength = -1f;
    float nextTargetSwitchTime;

    public Transform LockedTarget => lockedTarget;
    public bool IsAiming => IsAimingStatic;
    public event Action<bool> AimStateChanged;
    public event Action FirePerformed;
    public event Action EnemyGrappled;
    public event Action EnemyThrown;

    public Transform CurrentHoldPoint => IsAimingStatic ? cameraHoldPoint : carHoldPoint;

    void OnEnable()
    {
        aimAction?.action.Enable();
        fireAction?.action.Enable();
        if (fireAction != null)
            fireAction.action.performed += OnFirePerformed;

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

    void Update()
    {
        HandleAimingState();
        UpdateLockOn();
        UpdateTargetIndicator();
        if (fireTimer > 0f) fireTimer -= Time.unscaledDeltaTime;
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
        if (!IsAimingStatic || mainCamera == null || grappleProjectilePrefab == null)
        {
            lockedTarget = null;
            return;
        }

        if (IsThrowLockActive())
        {
            lockedTarget = null;
            return;
        }

        bool isHoldingEnemy = activeProjectile != null && activeProjectile.IsHoldingEnemy;
        Transform hold = CurrentHoldPoint != null ? CurrentHoldPoint : mainCamera.transform;
        Vector3 origin = hold.position;
        Transform heldEnemy = activeProjectile != null ? activeProjectile.HeldEnemyTransform : null;
        float maxRange = GetLockRange(isHoldingEnemy);

        if (IsTargetLockable(lockedTarget, origin, maxRange, heldEnemy))
            return;

        lockedTarget = FindBestLockTarget(origin, maxRange, heldEnemy);
    }

    float GetLockRange(bool isHoldingEnemy)
    {
        if (isHoldingEnemy)
            return Mathf.Max(0.1f, holdEnemyLockRange);

        float grappleRange = grappleProjectilePrefab != null ? grappleProjectilePrefab.maxRange : 0f;
        return Mathf.Max(grappleRange, lockAcquireRadius);
    }

    Transform FindBestLockTarget(Vector3 origin, float maxRange, Transform excludedTarget)
    {
        Collider[] hits = Physics.OverlapSphere(origin, maxRange, enemyLayerMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
            return null;

        HashSet<Transform> seen = new HashSet<Transform>();
        Vector3 cameraForward = mainCamera.transform.forward;
        Transform bestTarget = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Transform candidate = GetCandidateTarget(hits[i]);
            if (candidate == null || !seen.Add(candidate))
                continue;
            if (!IsTargetLockable(candidate, origin, maxRange, excludedTarget))
                continue;

            Vector3 toTarget = GetTargetAimPoint(candidate) - origin;
            float distance = toTarget.magnitude;
            if (distance < 0.001f)
                continue;

            // Prefer nearer targets, with a slight bias toward the camera's forward direction.
            float score = (Vector3.Dot(cameraForward, toTarget / distance) * 10f) - distance;
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }

    public void HandleTargetSwitchInput(Vector2 lookInput)
    {
        if (!IsAimingStatic || mainCamera == null || lockedTarget == null || grappleProjectilePrefab == null)
            return;
        if (Time.unscaledTime < nextTargetSwitchTime)
            return;
        if (lookInput.sqrMagnitude < targetSwitchInputThreshold * targetSwitchInputThreshold)
            return;

        bool isHoldingEnemy = activeProjectile != null && activeProjectile.IsHoldingEnemy;
        Transform heldEnemy = activeProjectile != null ? activeProjectile.HeldEnemyTransform : null;
        Transform hold = CurrentHoldPoint != null ? CurrentHoldPoint : mainCamera.transform;
        Vector3 origin = hold.position;
        float maxRange = GetLockRange(isHoldingEnemy);

        if (!IsTargetLockable(lockedTarget, origin, maxRange, heldEnemy))
            return;

        Vector2 inputDir = lookInput.normalized;
        Transform next = FindDirectionalTarget(origin, maxRange, heldEnemy, inputDir);
        if (next == null)
            return;

        lockedTarget = next;
        nextTargetSwitchTime = Time.unscaledTime + Mathf.Max(0f, targetSwitchCooldown);
    }

    Transform FindDirectionalTarget(Vector3 origin, float maxRange, Transform excludedTarget, Vector2 inputDir)
    {
        Collider[] hits = Physics.OverlapSphere(origin, maxRange, enemyLayerMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
            return null;

        Vector3 currentCameraSpace = GetCameraSpaceAimPoint(lockedTarget);
        HashSet<Transform> seen = new HashSet<Transform>();
        Transform bestTarget = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Transform candidate = GetCandidateTarget(hits[i]);
            if (candidate == null || candidate == lockedTarget || !seen.Add(candidate))
                continue;
            if (!IsTargetLockable(candidate, origin, maxRange, excludedTarget))
                continue;

            Vector3 candidateCameraSpace = GetCameraSpaceAimPoint(candidate);
            Vector2 toCandidate = new Vector2(
                candidateCameraSpace.x - currentCameraSpace.x,
                candidateCameraSpace.y - currentCameraSpace.y
            );
            if (toCandidate.sqrMagnitude < 0.0001f)
                continue;

            Vector2 candidateDir = toCandidate.normalized;
            float alignment = Vector2.Dot(inputDir, candidateDir);
            if (alignment < targetSwitchMinDot)
                continue;

            float distance = Vector3.Distance(origin, GetTargetAimPoint(candidate));
            float score = alignment * 100f - distance;
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

        Transform candidate = c.attachedRigidbody != null ? c.attachedRigidbody.transform : c.transform;
        if (candidate == null)
            return null;
        if (!candidate.CompareTag("Enemy") && !c.CompareTag("Enemy"))
            return null;

        return candidate;
    }

    bool IsTargetLockable(Transform candidate, Vector3 origin, float maxRange, Transform excludedTarget)
    {
        if (candidate == null || candidate == excludedTarget)
            return false;
        if ((enemyLayerMask.value & (1 << candidate.gameObject.layer)) == 0)
            return false;

        Vector3 targetPoint = GetTargetAimPoint(candidate);
        Vector3 toTarget = targetPoint - origin;
        float distance = toTarget.magnitude;
        if (distance < 0.001f || distance > maxRange)
            return false;

        Collider candidateCollider = candidate.GetComponentInChildren<Collider>();
        if (candidateCollider != null && !HasLineOfSight(origin, candidateCollider, candidate))
            return false;

        return true;
    }

    bool HasLineOfSight(Vector3 origin, Collider targetCollider, Transform targetTransform)
    {
        Vector3 targetPoint = targetCollider.bounds.center;
        Vector3 toTarget = targetPoint - origin;
        float distance = toTarget.magnitude;
        if (distance < 0.001f) return false;

        Vector3 dir = toTarget / distance;
        if (Physics.Raycast(origin, dir, out RaycastHit hit, distance, visibilityMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider == targetCollider) return true;
            if (hit.rigidbody != null && hit.rigidbody.transform == targetTransform) return true;
            return false;
        }

        return true;
    }

    bool IsThrowLockActive()
    {
        if (Time.unscaledTime < lockSuppressedUntil)
            return true;

        if (recentlyThrownEnemy == null)
            return false;

        if (Time.unscaledTime - throwStartTime > throwTrackingDuration)
        {
            recentlyThrownEnemy = null;
            return false;
        }

        if (recentlyThrownEnemy.linearVelocity.sqrMagnitude > throwFlyingSpeedThreshold * throwFlyingSpeedThreshold)
            return true;

        recentlyThrownEnemy = null;
        return false;
    }

    void OnFirePerformed(InputAction.CallbackContext _)
    {
        OnFirePressed();
    }

    void OnFirePressed()
    {
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
        GrappleProjectile projectile = Instantiate(grappleProjectilePrefab, hold.position, hold.rotation);
        Vector3 direction = mainCamera.transform.forward;

        if (lockedTarget != null)
        {
            Vector3 toTarget = lockedTarget.position - hold.position;
            if (toTarget.sqrMagnitude > 0.0001f)
                direction = toTarget.normalized;
        }

        projectile.Initialize(this, direction);
        activeProjectile = projectile;
        FirePerformed?.Invoke();
    }

    void LaunchHeldEnemy()
    {
        Rigidbody enemyRb = activeProjectile.ReleaseEnemy();
        if (enemyRb != null)
        {
            enemyRb.linearVelocity = Vector3.zero;
            Vector3 targetPoint = GetThrowTargetPoint(enemyRb.position);
            Vector3 direction = ComputeArcDirection(enemyRb.position, targetPoint);
            enemyRb.AddForce(direction * Mathf.Max(0.1f, throwForce), ForceMode.VelocityChange);
            recentlyThrownEnemy = enemyRb;
            throwStartTime = Time.unscaledTime;
            FirePerformed?.Invoke();
            EnemyThrown?.Invoke();

            Enemy enemy = enemyRb.GetComponent<Enemy>();
            if (enemy != null)
                enemy.ArmExplosion();

            AttachThrownTrail(enemyRb);
        }

        lockSuppressedUntil = Time.unscaledTime + throwLockSuppressTime;
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
        if (target == null) return Vector3.zero;

        Collider c = target.GetComponentInChildren<Collider>();
        if (c != null)
            return c.bounds.center;

        return target.position;
    }

    Vector3 GetCameraSpaceAimPoint(Transform target)
    {
        if (mainCamera == null || target == null)
            return Vector3.zero;

        return mainCamera.transform.InverseTransformPoint(GetTargetAimPoint(target));
    }

    Vector3 GetThrowTargetPoint(Vector3 throwOrigin)
    {
        if (lockedTarget != null)
            return GetTargetAimPoint(lockedTarget);

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
        Vector3 targetPoint = GetTargetAimPoint(lockedTarget);
        float hoverHeight = Mathf.Max(0.05f, targetIndicatorHeightOffset + targetIndicatorDistanceFromOrigin + pulse);
        Vector3 indicatorPosition = targetPoint + Vector3.up * hoverHeight;
        Vector3 direction = Vector3.down;

        targetIndicatorRenderer.transform.SetParent(transform, true);
        targetIndicatorRenderer.transform.position = indicatorPosition;
        targetIndicatorRenderer.transform.rotation = Quaternion.LookRotation(direction, Vector3.forward);
        targetIndicatorMaterial.color = targetIndicatorColor;
        UpdateIndicatorConeMeshIfNeeded();

        SetTargetIndicatorVisible(true);
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
}
