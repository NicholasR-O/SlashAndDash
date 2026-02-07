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

    enum State
    {
        Flying,
        Returning,
        HoldingEnemy
    }

    State state;

    public bool IsHoldingEnemy => state == State.HoldingEnemy;

    public void Initialize(GrappleController owner, Vector3 direction)
    {
        controller = owner;
        fireDirection = direction.normalized;
        startPosition = transform.position;

        rb = GetComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.linearVelocity = fireDirection * speed;

        state = State.Flying;
    }

    void FixedUpdate()
    {
        switch (state)
        {
            case State.Flying:
                CheckForwardHit();
                CheckRange();
                break;

            case State.Returning:
                ReturnToCar();
                break;

            case State.HoldingEnemy:
                HoldAtPoint();
                break;
        }
    }

    // ---------------- HIT DETECTION ----------------

    void CheckForwardHit()
    {
        float stepDistance = rb.linearVelocity.magnitude * Time.fixedDeltaTime;

        if (Physics.SphereCast(
            transform.position,
            hitRadius,
            fireDirection,
            out RaycastHit hit,
            stepDistance,
            ~0,
            QueryTriggerInteraction.Collide
        ))
        {
            HandleHit(hit.collider, hit.rigidbody, hit.point);
        }
    }

    void OnCollisionEnter(Collision col)
    {
        if (state != State.Flying)
            return;

        HandleHit(col.collider, col.rigidbody, col.contacts[0].point);
    }

    void OnTriggerEnter(Collider other)
    {
        if (state != State.Flying)
            return;

        HandleHit(other, other.attachedRigidbody, transform.position);
    }

    void HandleHit(Collider collider, Rigidbody hitRb, Vector3 hitPoint)
    {
        // Enemy
        if (hitRb != null && collider.CompareTag("Enemy"))
        {
            AttachEnemy(hitRb);
        }
        // Environment
        else
        {
            BeginReturn();
        }
    }

    // ---------------- RANGE ----------------

    void CheckRange()
    {
        if (Vector3.Distance(startPosition, transform.position) >= maxRange)
        {
            BeginReturn();
        }
    }

    // ---------------- ENEMY ----------------

    void AttachEnemy(Rigidbody enemy)
    {
        if (state != State.Flying)
            return;

        attachedEnemy = enemy;
        attachedEnemy.isKinematic = true;

        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;

        state = State.HoldingEnemy;
    }

    public Rigidbody ReleaseEnemy()
    {
        if (attachedEnemy == null)
            return null;

        Rigidbody released = attachedEnemy;
        released.isKinematic = false;
        attachedEnemy = null;

        return released;
    }

    // ---------------- RETURN ----------------

    void BeginReturn()
    {
        if (state == State.Returning)
            return;

        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;

        state = State.Returning;
    }

    void ReturnToCar()
    {
        Transform hold = controller.CurrentHoldPoint;

        transform.position = Vector3.MoveTowards(
            transform.position,
            hold.position,
            returnSpeed * Time.fixedDeltaTime
        );

        if (Vector3.Distance(transform.position, hold.position) < 0.15f)
        {
            controller.OnProjectileFinished();
            Destroy(gameObject);
        }
    }

    // ---------------- HOLD ----------------

    void HoldAtPoint()
    {
        Transform hold = controller.CurrentHoldPoint;

        transform.position = hold.position;

        if (attachedEnemy != null)
        {
            attachedEnemy.transform.position = hold.position;
        }
    }

    // ---------------- CLEANUP ----------------

    public void DestroySelf()
    {
        Destroy(gameObject);
    }
}
