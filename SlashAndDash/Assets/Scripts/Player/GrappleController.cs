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

    [Header("Throw Targeting")]
    public float throwForce = 45f;
    public float throwArcHeight = 1.4f;
    public float throwTargetMaxDistance = 80f;
    public float holdEnemyLockRange = 40f;
    public float throwLockSuppressTime = 0.35f;
    public float throwTrackingDuration = 2f;
    public float throwFlyingSpeedThreshold = 2.5f;
    public LayerMask visibilityMask = ~0;

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

    public Transform LockedTarget => lockedTarget;

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

        if (aiming)
        {
            Time.timeScale = aimTimeScale;
            Time.fixedDeltaTime = 0.02f * aimTimeScale;
        }
        else
        {
            lockedTarget = null;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
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
        float grappleRange = grappleProjectilePrefab.maxRange;
        Transform hold = CurrentHoldPoint != null ? CurrentHoldPoint : mainCamera.transform;
        Vector3 origin = hold.position;
        Vector3 forward = mainCamera.transform.forward;
        Transform bestTarget = null;
        if (isHoldingEnemy)
        {
            // While holding an enemy, lock to the closest enemy currently in the camera view,
            // regardless of distance, so throw lock stays reliable at long range.
            float bestDistance = float.MaxValue;
            Transform heldEnemy = activeProjectile != null ? activeProjectile.HeldEnemyTransform : null;
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            for (int i = 0; i < enemies.Length; i++)
            {
                Transform candidate = enemies[i].transform;
                if (candidate == null) continue;
                if (candidate == heldEnemy) continue;
                if ((enemyLayerMask.value & (1 << candidate.gameObject.layer)) == 0) continue;

                Vector3 candidatePoint = GetTargetAimPoint(candidate);
                Vector3 viewPos = mainCamera.WorldToViewportPoint(candidatePoint);
                if (viewPos.z <= 0f) continue;
                if (viewPos.x < 0f || viewPos.x > 1f || viewPos.y < 0f || viewPos.y > 1f) continue;

                Collider candidateCollider = candidate.GetComponentInChildren<Collider>();
                if (candidateCollider != null && !HasLineOfSight(origin, candidateCollider, candidate))
                    continue;

                float distance = Vector3.Distance(origin, candidatePoint);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestTarget = candidate;
                }
            }
        }
        else
        {
            float maxRange = Mathf.Max(grappleRange, lockAcquireRadius);
            float acquireRadius = maxRange;
            Collider[] hits = Physics.OverlapSphere(origin, acquireRadius, enemyLayerMask, QueryTriggerInteraction.Collide);
            float bestScore = float.MinValue;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i];
                if (!c.CompareTag("Enemy")) continue;

                Transform candidate = c.attachedRigidbody != null ? c.attachedRigidbody.transform : c.transform;
                Vector3 toTarget = candidate.position - origin;
                float distance = toTarget.magnitude;
                if (distance > maxRange) continue;
                if (distance < 0.001f) continue;

                Vector3 dir = toTarget / distance;
                float angle = Vector3.Angle(forward, dir);
                if (angle > lockMaxAngle) continue;

                // Favor center-screen and closer targets.
                float score = Vector3.Dot(forward, dir) * 100f - distance;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = candidate;
                }
            }
        }

        lockedTarget = bestTarget;
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

            Enemy enemy = enemyRb.GetComponent<Enemy>();
            if (enemy != null)
                enemy.ArmExplosion();
        }

        lockSuppressedUntil = Time.unscaledTime + throwLockSuppressTime;
        lockedTarget = null;

        activeProjectile.DestroySelf();
        activeProjectile = null;
    }

    public void OnProjectileFinished()
    {
        activeProjectile = null;
    }

    Vector3 GetTargetAimPoint(Transform target)
    {
        if (target == null) return Vector3.zero;

        Collider c = target.GetComponentInChildren<Collider>();
        if (c != null)
            return c.bounds.center;

        return target.position;
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
        Transform hold = CurrentHoldPoint != null ? CurrentHoldPoint : transform;
        Vector3 origin = hold.position + Vector3.up * (targetIndicatorHeightOffset + pulse);
        Vector3 targetPoint = GetTargetAimPoint(lockedTarget);
        Vector3 direction = targetPoint - origin;
        if (direction.sqrMagnitude < 0.0001f)
            direction = mainCamera != null ? mainCamera.transform.forward : transform.forward;
        direction.Normalize();

        targetIndicatorRenderer.transform.SetParent(transform, true);
        targetIndicatorRenderer.transform.position = origin + direction * Mathf.Max(0.05f, targetIndicatorDistanceFromOrigin);
        targetIndicatorRenderer.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
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
