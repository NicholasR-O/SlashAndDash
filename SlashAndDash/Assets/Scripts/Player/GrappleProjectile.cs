using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class GrappleProjectile : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 35f;
    public float maxRange = 10f;
    public float returnSpeed = 30f;
    public float hitRadius = 0.25f;

    Rigidbody rb;
    GrappleController controller;
    Vector3 fireDirection;
    Vector3 startPosition;
    Rigidbody attachedEnemy;
    Enemy attachedEnemyComponent;
    ThrowableVase attachedThrowableVase;
    Collider projectileCollider;
    Collider[] carColliders;
    LayerMask enemyMask;
    Collider[] heldEnemyColliders;

    enum State { Flying, Returning, HoldingEnemy }
    State state;

    public bool IsHoldingEnemy => state == State.HoldingEnemy;
    public Transform HeldEnemyTransform => attachedEnemy != null ? attachedEnemy.transform : null;
    public Enemy HeldEnemyComponent => attachedEnemyComponent;
    public ThrowableVase HeldThrowableVase => attachedThrowableVase;

    public void Initialize(GrappleController owner, Vector3 direction)
    {
        controller = owner;
        fireDirection = direction.normalized;
        startPosition = transform.position;
        enemyMask = owner != null ? owner.enemyLayerMask : ~0;

        rb = GetComponent<Rigidbody>();
        projectileCollider = GetComponent<Collider>();
        rb.isKinematic = false;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.linearVelocity = fireDirection * speed;

        if (projectileCollider != null)
        {
            projectileCollider.isTrigger = true;
            IgnoreProjectileCarCollisions(true);
        }

        CacheCarColliders();
        IgnoreProjectileCarCollisions(true);

        state = State.Flying;
    }

    void FixedUpdate()
    {
        switch (state)
        {
            case State.Flying:
                CheckForwardHit();
                if (state != State.Flying)
                    break;
                CheckRange();
                break;

            case State.Returning:
                ReturnToHoldPoint();
                break;

            case State.HoldingEnemy:
                HoldAtCurrentPoint();
                break;
        }
    }

    void CheckForwardHit()
    {
        float stepDistance = rb.linearVelocity.magnitude * Time.fixedDeltaTime;

        if (Physics.SphereCast(transform.position, hitRadius, fireDirection,
            out RaycastHit hit, stepDistance, enemyMask, QueryTriggerInteraction.Collide))
        {
            HandleHit(hit.collider, hit.rigidbody);
        }
    }

    void OnCollisionEnter(Collision col)
    {
        if (state != State.Flying) return;
        HandleHit(col.collider, col.rigidbody);
    }

    void OnTriggerEnter(Collider other)
    {
        if (state != State.Flying) return;
        HandleHit(other, other.attachedRigidbody);
    }

    void HandleHit(Collider collider, Rigidbody hitRb)
    {
        ThrowableVase throwableVase = ResolveThrowableVase(collider, hitRb);
        if (throwableVase != null)
        {
            Rigidbody vaseBody = throwableVase.GetComponent<Rigidbody>();
            if (vaseBody != null)
                AttachEnemy(vaseBody, null, throwableVase);
            return;
        }

        Enemy enemy = ResolveEnemy(collider, hitRb);
        if (enemy == null)
            return;

        Rigidbody enemyBody = enemy.GetComponent<Rigidbody>();
        if (enemyBody == null)
            return;

        AttachEnemy(enemyBody, enemy, null);
    }

    void CheckRange()
    {
        if (Vector3.Distance(startPosition, transform.position) >= maxRange)
            BeginReturn();
    }

    Enemy ResolveEnemy(Collider collider, Rigidbody hitRb)
    {
        if (hitRb != null)
        {
            Enemy attachedEnemyComponent = hitRb.GetComponent<Enemy>();
            if (attachedEnemyComponent != null)
                return attachedEnemyComponent;
        }

        return collider != null ? collider.GetComponentInParent<Enemy>() : null;
    }

    ThrowableVase ResolveThrowableVase(Collider collider, Rigidbody hitRb)
    {
        if (hitRb != null)
        {
            ThrowableVase attachedThrowable = hitRb.GetComponent<ThrowableVase>();
            if (attachedThrowable != null)
                return attachedThrowable;
        }

        return collider != null ? collider.GetComponentInParent<ThrowableVase>() : null;
    }

    void AttachEnemy(Rigidbody enemy, Enemy enemyComponent, ThrowableVase throwableVase)
    {
        if (state != State.Flying) return;

        if (enemy == null)
            return;

        if (enemyComponent != null && !enemyComponent.CanBeGrappled)
        {
            BeginReturn();
            return;
        }

        if (throwableVase != null && !throwableVase.CanBeGrappled)
        {
            BeginReturn();
            return;
        }

        attachedEnemy = enemy;
        attachedEnemyComponent = enemyComponent;
        attachedThrowableVase = throwableVase;
        heldEnemyColliders = attachedEnemy.GetComponentsInChildren<Collider>();
        IgnoreHeldEnemyCarCollisions(true);

        // Make sure it's dynamic before modifying velocity
        if (attachedEnemy.isKinematic)
            attachedEnemy.isKinematic = false;

        attachedEnemy.linearVelocity = Vector3.zero;
        attachedEnemy.angularVelocity = Vector3.zero;
        attachedEnemy.Sleep();

        attachedEnemy.isKinematic = true;
        SetHeldEnemyCollidersEnabled(false);

        if (enemyComponent != null)
            enemyComponent.OnCapturedByGrapple();
        if (throwableVase != null)
            throwableVase.OnCapturedByGrapple();
        if (controller != null && enemyComponent != null)
            controller.NotifyEnemyGrappled();

        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;

        state = State.HoldingEnemy;
    }

    public Rigidbody ReleaseEnemy()
    {
        if (attachedEnemy == null) return null;

        Rigidbody released = attachedEnemy;
        Collider[] releasedColliders = heldEnemyColliders;
        released.isKinematic = false;
        SetHeldEnemyCollidersEnabled(true);
        IgnoreCollisionSet(releasedColliders, carColliders, true);

        attachedEnemy = null;
        attachedEnemyComponent = null;
        attachedThrowableVase = null;
        heldEnemyColliders = null;
        return released;
    }

    public static void DestroyAllActive()
    {
        GrappleProjectile[] projectiles = FindObjectsByType<GrappleProjectile>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < projectiles.Length; i++)
        {
            GrappleProjectile projectile = projectiles[i];
            if (projectile != null)
                projectile.DestroySelf();
        }
    }

    void BeginReturn()
    {
        if (state == State.Returning) return;

        if (rb != null && !rb.isKinematic)
            rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;
        state = State.Returning;
    }

    void ReturnToHoldPoint()
    {
        Transform hold = controller.CurrentHoldPoint;

        transform.position = Vector3.MoveTowards(
            transform.position,
            hold.position,
            returnSpeed * Time.fixedDeltaTime
        );

        transform.rotation = hold.rotation;

        if (Vector3.Distance(transform.position, hold.position) < 0.15f)
        {
            controller.OnProjectileFinished();
            Destroy(gameObject);
        }
    }

    void HoldAtCurrentPoint()
    {
        Transform hold = controller != null && controller.carHoldPoint != null
            ? controller.carHoldPoint
            : controller.CurrentHoldPoint;

        transform.position = hold.position;
        transform.rotation = hold.rotation;

        if (attachedEnemy != null)
        {
            attachedEnemy.transform.position = hold.position;
            attachedEnemy.transform.rotation = hold.rotation;
        }
    }

    public void DestroySelf()
    {
        ReleaseEnemy();
        if (controller != null)
            controller.OnProjectileFinished();

        if (projectileCollider != null)
            projectileCollider.enabled = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        ReleaseEnemy();
        if (controller != null)
            controller.OnProjectileFinished();

        IgnoreProjectileCarCollisions(false);
    }

    void CacheCarColliders()
    {
        Rigidbody carRb = controller != null ? controller.carRigidbody : null;
        if (carRb == null && controller != null)
            carRb = controller.GetComponentInParent<Rigidbody>();

        carColliders = carRb != null ? carRb.GetComponentsInChildren<Collider>(true) : null;
    }

    void IgnoreProjectileCarCollisions(bool ignore)
    {
        if (projectileCollider == null || carColliders == null)
            return;

        for (int i = 0; i < carColliders.Length; i++)
        {
            Collider carCollider = carColliders[i];
            if (carCollider != null && carCollider != projectileCollider)
                Physics.IgnoreCollision(projectileCollider, carCollider, ignore);
        }
    }

    void IgnoreHeldEnemyCarCollisions(bool ignore)
    {
        IgnoreCollisionSet(heldEnemyColliders, carColliders, ignore);
    }

    void SetHeldEnemyCollidersEnabled(bool enabled)
    {
        if (heldEnemyColliders == null) return;

        for (int i = 0; i < heldEnemyColliders.Length; i++)
        {
            if (heldEnemyColliders[i] != null)
                heldEnemyColliders[i].enabled = enabled;
        }
    }

    static void IgnoreCollisionSet(Collider[] first, Collider[] second, bool ignore)
    {
        if (first == null || second == null)
            return;

        for (int i = 0; i < first.Length; i++)
        {
            Collider a = first[i];
            if (a == null)
                continue;

            for (int j = 0; j < second.Length; j++)
            {
                Collider b = second[j];
                if (b != null && b != a)
                    Physics.IgnoreCollision(a, b, ignore);
            }
        }
    }
}
