using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class WizardProjectile : MonoBehaviour
{
    private const float SpawnGraceSeconds = 0.08f;
    private const string GroundTag = "Ground";

    [Header("Movement")]
    [SerializeField] float speed = 8f;
    [SerializeField] float maxLifetime = 8f;

    [Header("Homing")]
    [SerializeField] bool homeTowardsTarget = true;
    [SerializeField] float homingTurnSpeed = 75f;
    [SerializeField] float homingStartDelay = 0.1f;

    [Header("Collision")]
    [SerializeField] bool ignoreGround = true;
    [SerializeField] bool avoidSolidObstacles = true;
    [SerializeField] float obstacleProbeDistance = 1.4f;
    [SerializeField] float obstacleAvoidanceStrength = 1.15f;

    [Header("Damage")]
    [SerializeField] float damage = 8f;
    [SerializeField] bool damagePlayer = true;
    [SerializeField] bool damageEnemies;
    [SerializeField] LayerMask hitMask = ~0;
    [SerializeField] bool destroyOnHit = true;

    Rigidbody rb;
    Collider projectileCollider;
    Collider[] projectileColliders;
    Vector3 travelDirection = Vector3.forward;
    Transform target;
    GameObject owner;
    float spawnedAt;
    bool initialized;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        CacheProjectileColliders();
        projectileCollider = GetComponent<Collider>();

        if (projectileCollider != null)
            projectileCollider.isTrigger = true;

        ConfigureGroundCollision();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    void OnValidate()
    {
        speed = Mathf.Max(0.01f, speed);
        maxLifetime = Mathf.Max(0.01f, maxLifetime);
        homingTurnSpeed = Mathf.Max(0f, homingTurnSpeed);
        homingStartDelay = Mathf.Max(0f, homingStartDelay);
        obstacleProbeDistance = Mathf.Max(0f, obstacleProbeDistance);
        obstacleAvoidanceStrength = Mathf.Max(0f, obstacleAvoidanceStrength);
        damage = Mathf.Max(0f, damage);
    }

    public void Initialize(Vector3 direction, float damageAmount, GameObject ownerObject = null, float speedOverride = -1f, float lifetimeOverride = -1f)
    {
        Initialize(direction, damageAmount, ownerObject, null, speedOverride, lifetimeOverride);
    }

    public void Initialize(Vector3 direction, float damageAmount, GameObject ownerObject, Transform targetTransform, float speedOverride = -1f, float lifetimeOverride = -1f)
    {
        travelDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        owner = ownerObject;
        target = targetTransform;

        if (speedOverride > 0f)
            speed = speedOverride;
        if (lifetimeOverride > 0f)
            maxLifetime = lifetimeOverride;

        damage = Mathf.Max(0f, damageAmount);
        spawnedAt = Time.time;
        initialized = true;
        CacheProjectileColliders();
        ConfigureGroundCollision();
        IgnoreOwnerCollisions();
        IgnoreOtherProjectileCollisions();
        UpdateRotation();
        ApplyVelocity();
    }

    public static void DestroyAllActive()
    {
        WizardProjectile[] projectiles = FindObjectsByType<WizardProjectile>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < projectiles.Length; i++)
        {
            WizardProjectile projectile = projectiles[i];
            if (projectile != null)
                projectile.DestroyForCleanup();
        }
    }

    public void DestroyForCleanup()
    {
        initialized = false;
        target = null;

        Collider[] colliders = GetProjectileColliders();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        Destroy(gameObject);
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

        UpdateTravelDirection();
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

        if (ShouldIgnoreCollision(other))
            return;

        if ((hitMask.value & (1 << other.gameObject.layer)) == 0)
            return;

        if (Time.time - spawnedAt <= SpawnGraceSeconds && other.isTrigger)
            return;

        IDamageable damageable = DamageUtility.FindDamageable(other);
        if (damageable == null || !damageable.IsAlive)
        {
            if (other.isTrigger)
                return;

            if (avoidSolidObstacles)
            {
                SteerAwayFrom(other);
                return;
            }

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
        if (owner == null)
            return;

        Collider[] ownColliders = GetProjectileColliders();
        Collider[] ownerColliders = owner.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < ownerColliders.Length; i++)
        {
            Collider ownerCollider = ownerColliders[i];
            if (ownerCollider == null)
                continue;

            for (int j = 0; j < ownColliders.Length; j++)
            {
                Collider ownCollider = ownColliders[j];
                if (ownCollider == null || ownCollider == ownerCollider)
                    continue;

                Physics.IgnoreCollision(ownCollider, ownerCollider, true);
            }
        }
    }

    void IgnoreOtherProjectileCollisions()
    {
        WizardProjectile[] projectiles = FindObjectsByType<WizardProjectile>(FindObjectsSortMode.None);
        for (int i = 0; i < projectiles.Length; i++)
        {
            WizardProjectile otherProjectile = projectiles[i];
            if (otherProjectile == null || otherProjectile == this)
                continue;

            IgnoreCollisionsWith(otherProjectile);
        }
    }

    void IgnoreCollisionsWith(WizardProjectile otherProjectile)
    {
        if (otherProjectile == null)
            return;

        Collider[] ownColliders = GetProjectileColliders();
        Collider[] otherColliders = otherProjectile.GetProjectileColliders();

        for (int i = 0; i < ownColliders.Length; i++)
        {
            Collider ownCollider = ownColliders[i];
            if (ownCollider == null)
                continue;

            for (int j = 0; j < otherColliders.Length; j++)
            {
                Collider otherCollider = otherColliders[j];
                if (otherCollider == null || otherCollider == ownCollider)
                    continue;

                Physics.IgnoreCollision(ownCollider, otherCollider, true);
            }
        }
    }

    void ConfigureGroundCollision()
    {
        if (!ignoreGround)
            return;

        int groundLayer = LayerMask.NameToLayer(GroundTag);
        if (groundLayer < 0)
            return;

        int groundMask = 1 << groundLayer;
        Collider[] ownColliders = GetProjectileColliders();
        for (int i = 0; i < ownColliders.Length; i++)
        {
            Collider ownCollider = ownColliders[i];
            if (ownCollider == null)
                continue;

            LayerMask excludedLayers = ownCollider.excludeLayers;
            excludedLayers.value |= groundMask;
            ownCollider.excludeLayers = excludedLayers;
        }
    }

    Collider[] GetProjectileColliders()
    {
        if (projectileColliders == null || projectileColliders.Length == 0)
            CacheProjectileColliders();

        return projectileColliders;
    }

    void CacheProjectileColliders()
    {
        projectileColliders = GetComponentsInChildren<Collider>(true);
    }

    bool ShouldIgnoreCollision(Collider other)
    {
        if (ignoreGround && IsGround(other))
            return true;

        if (owner != null && ColliderBelongsTo(other, owner.transform))
            return true;

        WizardProjectile otherProjectile = other.GetComponentInParent<WizardProjectile>();
        if (otherProjectile != null)
        {
            if (otherProjectile != this)
                IgnoreCollisionsWith(otherProjectile);

            return true;
        }

        return false;
    }

    bool IsGround(Collider other)
    {
        if (other == null)
            return false;

        int groundLayer = LayerMask.NameToLayer(GroundTag);
        if (groundLayer >= 0 && other.gameObject.layer == groundLayer)
            return true;

        Transform current = other.transform;
        while (current != null)
        {
            if (current.CompareTag(GroundTag))
                return true;

            current = current.parent;
        }

        return false;
    }

    bool ColliderBelongsTo(Collider other, Transform root)
    {
        if (other == null || root == null)
            return false;

        if (TransformBelongsTo(other.transform, root))
            return true;

        return other.attachedRigidbody != null && TransformBelongsTo(other.attachedRigidbody.transform, root);
    }

    bool TransformBelongsTo(Transform candidate, Transform root)
    {
        return candidate != null && root != null && (candidate == root || candidate.IsChildOf(root));
    }

    void UpdateTravelDirection()
    {
        Vector3 desiredDirection = travelDirection;

        if (homeTowardsTarget && target != null && Time.time - spawnedAt >= homingStartDelay)
        {
            Vector3 toTarget = TargetingUtility.GetAimPoint(target) - transform.position;
            if (toTarget.sqrMagnitude > 0.0001f)
                desiredDirection = toTarget.normalized;
        }

        if (avoidSolidObstacles)
            desiredDirection = ApplyObstacleAvoidance(desiredDirection);

        if (desiredDirection.sqrMagnitude < 0.0001f)
            return;

        float turnRadians = Mathf.Deg2Rad * homingTurnSpeed * Time.fixedDeltaTime;
        travelDirection = Vector3.RotateTowards(travelDirection, desiredDirection.normalized, turnRadians, 0f);
        if (travelDirection.sqrMagnitude > 0.0001f)
            travelDirection.Normalize();

        UpdateRotation();
    }

    Vector3 ApplyObstacleAvoidance(Vector3 desiredDirection)
    {
        if (obstacleProbeDistance <= 0f)
            return desiredDirection;

        Vector3 currentDirection = travelDirection.sqrMagnitude > 0.0001f ? travelDirection.normalized : desiredDirection;
        if (currentDirection.sqrMagnitude < 0.0001f)
            return desiredDirection;

        float radius = GetProbeRadius();
        if (!Physics.SphereCast(transform.position, radius, currentDirection, out RaycastHit hit, obstacleProbeDistance, hitMask, QueryTriggerInteraction.Ignore))
            return desiredDirection;

        if (hit.collider == null || ShouldIgnoreCollision(hit.collider) || ColliderBelongsTo(hit.collider, target))
            return desiredDirection;

        Vector3 alongSurface = Vector3.ProjectOnPlane(desiredDirection, hit.normal);
        if (alongSurface.sqrMagnitude < 0.0001f)
            alongSurface = Vector3.Cross(hit.normal, Vector3.up);
        if (alongSurface.sqrMagnitude < 0.0001f)
            alongSurface = Vector3.Cross(hit.normal, Vector3.right);
        if (alongSurface.sqrMagnitude < 0.0001f)
            return desiredDirection;

        Vector3 avoidedDirection = desiredDirection + alongSurface.normalized * obstacleAvoidanceStrength;
        return avoidedDirection.sqrMagnitude > 0.0001f ? avoidedDirection.normalized : desiredDirection;
    }

    float GetProbeRadius()
    {
        if (projectileCollider == null)
            return 0.15f;

        Bounds bounds = projectileCollider.bounds;
        return Mathf.Max(0.05f, Mathf.Min(bounds.extents.x, bounds.extents.y, bounds.extents.z) * 0.9f);
    }

    void SteerAwayFrom(Collider obstacle)
    {
        if (obstacle == null)
            return;

        Vector3 closestPoint = obstacle.ClosestPoint(transform.position);
        Vector3 away = transform.position - closestPoint;
        if (away.sqrMagnitude < 0.0001f)
            away = transform.position - obstacle.bounds.center;
        if (away.sqrMagnitude < 0.0001f)
            return;

        Vector3 steeredDirection = travelDirection + away.normalized * Mathf.Max(0.1f, obstacleAvoidanceStrength);
        if (steeredDirection.sqrMagnitude > 0.0001f)
            travelDirection = steeredDirection.normalized;

        UpdateRotation();
        ApplyVelocity();
    }

    void UpdateRotation()
    {
        if (travelDirection.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(travelDirection, Vector3.up);
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

public static class ProjectileCleanup
{
    public static void ClearAllProjectiles()
    {
        WizardProjectile.DestroyAllActive();
        GrappleProjectile.DestroyAllActive();
    }
}
