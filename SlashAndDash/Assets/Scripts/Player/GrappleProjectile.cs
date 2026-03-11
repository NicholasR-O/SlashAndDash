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
    Collider projectileCollider;
    LayerMask enemyMask;
    Collider[] heldEnemyColliders;

    enum State { Flying, Returning, HoldingEnemy }
    State state;

    public bool IsHoldingEnemy => state == State.HoldingEnemy;
    public Transform HeldEnemyTransform => attachedEnemy != null ? attachedEnemy.transform : null;

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
            projectileCollider.isTrigger = true;

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
        if (hitRb != null && collider.CompareTag("Enemy"))
            AttachEnemy(hitRb);
    }

    void CheckRange()
    {
        if (Vector3.Distance(startPosition, transform.position) >= maxRange)
            BeginReturn();
    }

    void AttachEnemy(Rigidbody enemy)
    {
        if (state != State.Flying) return;

        attachedEnemy = enemy;
        Enemy enemyComponent = attachedEnemy.GetComponent<Enemy>();
        heldEnemyColliders = attachedEnemy.GetComponentsInChildren<Collider>();

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
        if (controller != null)
            controller.NotifyEnemyGrappled();

        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;

        state = State.HoldingEnemy;
    }

    public Rigidbody ReleaseEnemy()
    {
        if (attachedEnemy == null) return null;

        Rigidbody released = attachedEnemy;
        released.isKinematic = false;
        SetHeldEnemyCollidersEnabled(true);

        attachedEnemy = null;
        heldEnemyColliders = null;
        return released;
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
        Destroy(gameObject);
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
}
