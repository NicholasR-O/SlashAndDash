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
    public float holdEnemyLockRange = 40f;
    public float throwLockSuppressTime = 0.35f;
    public float throwTrackingDuration = 2f;
    public float throwFlyingSpeedThreshold = 2.5f;
    public LayerMask visibilityMask = ~0;

    [Header("Input")]
    public InputActionReference aimAction;
    public InputActionReference fireAction;

    public static bool IsAimingStatic { get; private set; }

    GrappleProjectile activeProjectile;
    float fireTimer;
    float lockSuppressedUntil;
    float throwStartTime;
    Rigidbody recentlyThrownEnemy;
    Transform lockedTarget;

    public Transform LockedTarget => lockedTarget;

    public Transform CurrentHoldPoint => IsAimingStatic ? cameraHoldPoint : carHoldPoint;

    void OnEnable()
    {
        aimAction?.action.Enable();
        fireAction?.action.Enable();
        if (fireAction != null)
            fireAction.action.performed += OnFirePerformed;
    }

    void OnDisable()
    {
        if (fireAction != null)
            fireAction.action.performed -= OnFirePerformed;
        aimAction?.action.Disable();
        fireAction?.action.Disable();
    }

    void Update()
    {
        HandleAimingState();
        UpdateLockOn();
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
        float maxRange = isHoldingEnemy ? Mathf.Max(grappleRange, holdEnemyLockRange) : grappleRange;
        float acquireRadius = isHoldingEnemy ? Mathf.Max(lockAcquireRadius, holdEnemyLockRange) : lockAcquireRadius;

        Transform hold = CurrentHoldPoint != null ? CurrentHoldPoint : mainCamera.transform;
        Vector3 origin = hold.position;
        Vector3 forward = mainCamera.transform.forward;

        Collider[] hits = Physics.OverlapSphere(origin, acquireRadius, enemyLayerMask, QueryTriggerInteraction.Collide);
        Transform bestTarget = null;
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

            bool isBeyondGrapple = distance > grappleRange;
            if (isBeyondGrapple && (!isHoldingEnemy || !HasLineOfSight(origin, c, candidate)))
                continue;

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
            enemyRb.AddForce(mainCamera.transform.forward * 45f, ForceMode.VelocityChange);
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
}
