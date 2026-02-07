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
    public float launchForce = 45f;

    [Header("Aiming")]
    public float aimTimeScale = 0.35f;
    public float holdPointAimDistance = 1.2f;
    public float holdPointAimSmooth = 12f;

    [Header("Input")]
    public InputActionReference aimAction;
    public InputActionReference fireAction;

    public static bool IsAimingStatic { get; private set; }

    GrappleProjectile activeProjectile;
    float fireTimer;

    Vector3 cameraHoldPointVelocity;

    public Transform CurrentHoldPoint =>
        IsAimingStatic ? cameraHoldPoint : carHoldPoint;

    void OnEnable()
    {
        aimAction?.action.Enable();
        fireAction?.action.Enable();

        fireAction.action.performed += _ => OnFirePressed();
    }

    void OnDisable()
    {
        fireAction.action.performed -= _ => OnFirePressed();

        aimAction?.action.Disable();
        fireAction?.action.Disable();
    }

    void Update()
    {
        HandleAimingState();
        UpdateCameraHoldPoint();

        if (fireTimer > 0f)
            fireTimer -= Time.unscaledDeltaTime;
    }

    // ---------------- AIMING ----------------

    void HandleAimingState()
    {
        bool aiming = aimAction != null && aimAction.action.ReadValue<float>() > 0.1f;

        if (aiming == IsAimingStatic)
            return;

        IsAimingStatic = aiming;

        if (aiming)
        {
            Time.timeScale = aimTimeScale;
            Time.fixedDeltaTime = 0.02f * aimTimeScale;
        }
        else
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }
    }

    void UpdateCameraHoldPoint()
    {
        if (!cameraHoldPoint || !mainCamera)
            return;

        if (IsAimingStatic)
        {
            Vector3 target =
                mainCamera.transform.position +
                mainCamera.transform.forward * holdPointAimDistance;

            cameraHoldPoint.position = Vector3.SmoothDamp(
                cameraHoldPoint.position,
                target,
                ref cameraHoldPointVelocity,
                1f / holdPointAimSmooth,
                Mathf.Infinity,
                Time.unscaledDeltaTime
            );
        }
        else
        {
            cameraHoldPoint.position = carHoldPoint.position;
            cameraHoldPointVelocity = Vector3.zero;
        }
    }

    // ---------------- FIRE LOGIC ----------------

    void OnFirePressed()
    {
        // CASE 1: Holding an enemy → launch it
        if (activeProjectile != null && activeProjectile.IsHoldingEnemy)
        {
            LaunchHeldEnemy();
            return;
        }

        // CASE 2: Fire grapple normally
        TryFire();
    }

    void TryFire()
    {
        if (!IsAimingStatic)
            return;

        if (activeProjectile != null)
            return;

        if (fireTimer > 0f)
            return;

        FireGrapple();
    }

    void FireGrapple()
    {
        fireTimer = fireCooldown;

        Transform hold = CurrentHoldPoint;

        GrappleProjectile projectile = Instantiate(
            grappleProjectilePrefab,
            hold.position,
            Quaternion.identity
        );

        Vector3 direction = mainCamera.transform.forward;

        projectile.Initialize(this, direction);
        activeProjectile = projectile;
    }

    void LaunchHeldEnemy()
    {
        Rigidbody enemyRb = activeProjectile.ReleaseEnemy();

        if (enemyRb != null)
        {
            enemyRb.linearVelocity = Vector3.zero;
            enemyRb.AddForce(mainCamera.transform.forward * launchForce, ForceMode.VelocityChange);

            // Arm the enemy explosion if possible
            EnemyExplodeOnThrow explodeComponent = enemyRb.GetComponent<EnemyExplodeOnThrow>();
            if (explodeComponent != null)
            {
                explodeComponent.ArmExplosion();
            }
        }

        activeProjectile.DestroySelf();
        activeProjectile = null;
    }

    // ---------------- CALLBACK ----------------

    public void OnProjectileFinished()
    {
        activeProjectile = null;
    }
}
