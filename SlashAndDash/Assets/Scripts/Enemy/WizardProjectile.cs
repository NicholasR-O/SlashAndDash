using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class WizardProjectile : MonoBehaviour
{
    private const float SpawnGraceSeconds = 0.08f;

    [Header("Movement")]
    [SerializeField] float speed = 8f;
    [SerializeField] float maxLifetime = 6f;

    [Header("Damage")]
    [SerializeField] float damage = 8f;
    [SerializeField] bool damagePlayer = true;
    [SerializeField] bool damageEnemies;
    [SerializeField] LayerMask hitMask = ~0;
    [SerializeField] bool destroyOnHit = true;

    Rigidbody rb;
    Collider projectileCollider;
    Vector3 travelDirection = Vector3.forward;
    GameObject owner;
    float spawnedAt;
    bool initialized;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        projectileCollider = GetComponent<Collider>();

        if (projectileCollider != null)
            projectileCollider.isTrigger = true;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    public void Initialize(Vector3 direction, float damageAmount, GameObject ownerObject = null, float speedOverride = -1f, float lifetimeOverride = -1f)
    {
        travelDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        owner = ownerObject;

        if (speedOverride > 0f)
            speed = speedOverride;
        if (lifetimeOverride > 0f)
            maxLifetime = lifetimeOverride;

        damage = Mathf.Max(0f, damageAmount);
        spawnedAt = Time.time;
        initialized = true;
        IgnoreOwnerCollisions();
        ApplyVelocity();
    }

    void FixedUpdate()
    {
        if (!initialized)
            return;

        if (Time.time - spawnedAt >= maxLifetime)
        {
            Destroy(gameObject);
            return;
        }

        ApplyVelocity();
    }

    void OnTriggerEnter(Collider other)
    {
        HandleHit(other);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
            return;

        HandleHit(collision.collider);
    }

    void HandleHit(Collider other)
    {
        if (other == null)
            return;

        if ((hitMask.value & (1 << other.gameObject.layer)) == 0)
            return;

        if (owner != null && other.transform.IsChildOf(owner.transform))
            return;

        if (Time.time - spawnedAt <= SpawnGraceSeconds && other.isTrigger)
            return;

        IDamageable damageable = DamageUtility.FindDamageable(other);
        if (damageable == null || !damageable.IsAlive)
        {
            if (other.isTrigger)
                return;

            if (destroyOnHit)
                Destroy(gameObject);
            return;
        }

        if (!damagePlayer && damageable is CarController)
            return;

        if (!damageEnemies && damageable is Enemy)
            return;

        damageable.TakeDamage(damage, owner);

        if (destroyOnHit)
            Destroy(gameObject);
    }

    void IgnoreOwnerCollisions()
    {
        if (projectileCollider == null || owner == null)
            return;

        Collider[] ownerColliders = owner.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < ownerColliders.Length; i++)
        {
            Collider ownerCollider = ownerColliders[i];
            if (ownerCollider == null || ownerCollider == projectileCollider)
                continue;

            Physics.IgnoreCollision(projectileCollider, ownerCollider, true);
        }
    }

    void ApplyVelocity()
    {
        if (rb != null)
        {
            rb.linearVelocity = travelDirection * speed;
            return;
        }

        transform.position += travelDirection * speed * Time.fixedDeltaTime;
    }
}
