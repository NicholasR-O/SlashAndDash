using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public interface IDamageable
{
    float MaxHealth { get; }
    float CurrentHealth { get; }
    bool IsAlive { get; }

    bool TakeDamage(float amount, GameObject source = null);
}

public static class DamageUtility
{
    public static IDamageable FindDamageable(Component source)
    {
        if (source == null)
            return null;

        if (source is Collider collider && collider.attachedRigidbody != null)
        {
            IDamageable onAttachedBody = collider.attachedRigidbody.GetComponent<IDamageable>();
            if (onAttachedBody != null)
                return onAttachedBody;
        }

        IDamageable onSelf = source.GetComponent<IDamageable>();
        if (onSelf != null)
            return onSelf;

        return source.GetComponentInParent<IDamageable>();
    }
}

public static class TargetingUtility
{
    public static Transform FindTaggedTransform(Collider collider, string tag)
    {
        if (collider == null || string.IsNullOrEmpty(tag))
            return null;

        if (collider.CompareTag(tag))
            return collider.attachedRigidbody != null ? collider.attachedRigidbody.transform : collider.transform;

        if (collider.attachedRigidbody != null && collider.attachedRigidbody.CompareTag(tag))
            return collider.attachedRigidbody.transform;

        Transform current = collider.transform.parent;
        while (current != null)
        {
            if (current.CompareTag(tag))
                return current;

            current = current.parent;
        }

        return null;
    }

    public static Collider GetBestCollider(Transform target, bool includeTriggers = true)
    {
        if (target == null)
            return null;

        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        Collider triggerFallback = null;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled)
                continue;

            if (!collider.isTrigger)
                return collider;

            if (includeTriggers && triggerFallback == null)
                triggerFallback = collider;
        }

        return triggerFallback;
    }

    public static Vector3 GetAimPoint(Transform target)
    {
        if (target == null)
            return Vector3.zero;

        Collider collider = GetBestCollider(target);
        return collider != null ? collider.bounds.center : target.position;
    }

    public static float GetColliderDistance(Transform first, Transform second, bool horizontalOnly)
    {
        if (first == null || second == null)
            return float.PositiveInfinity;

        Vector3 firstPoint = first.position;
        Vector3 secondPoint = second.position;
        Collider firstCollider = GetBestCollider(first);
        Collider secondCollider = GetBestCollider(second);

        if (firstCollider != null && secondCollider != null)
        {
            secondPoint = secondCollider.ClosestPoint(firstCollider.bounds.center);
            firstPoint = firstCollider.ClosestPoint(secondPoint);
        }
        else if (firstCollider != null)
        {
            firstPoint = firstCollider.ClosestPoint(second.position);
        }
        else if (secondCollider != null)
        {
            secondPoint = secondCollider.ClosestPoint(first.position);
        }

        Vector3 delta = secondPoint - firstPoint;
        if (horizontalOnly)
            delta.y = 0f;

        return delta.magnitude;
    }

    public static bool RaycastHitBelongsTo(RaycastHit hit, Transform target)
    {
        if (target == null || hit.collider == null)
            return false;

        Transform hitTransform = hit.collider.transform;
        if (hitTransform == target || hitTransform.IsChildOf(target))
            return true;

        if (hit.rigidbody == null)
            return false;

        Transform bodyTransform = hit.rigidbody.transform;
        return bodyTransform == target ||
               bodyTransform.IsChildOf(target) ||
               target.IsChildOf(bodyTransform);
    }
}

public class Enemy : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    [SerializeField] protected float maxHealth = 50f;
    [SerializeField] protected float size = 1f;
    [SerializeField] protected float damage = 10f;
    [SerializeField] protected bool destroyOnDeath = true;
    [SerializeField] protected bool logDamageEvents;

    [Header("Behavior Flags")]
    [SerializeField] protected bool explosionCanHarmPlayer;
    [SerializeField] protected bool canBeGrappled = true;

    [Header("Health Bar")]
    [SerializeField] protected bool showHealthBar = true;
    [SerializeField] protected Vector3 healthBarOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] protected Vector2 healthBarSize = new Vector2(1.4f, 0.16f);
    [SerializeField] protected Color healthBarBackgroundColor = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField] protected Color healthBarFillColor = new Color(0.25f, 0.95f, 0.35f, 1f);

    [Header("Explosion")]
    [SerializeField] protected float explosionRadius = 4.0f;
    [SerializeField] protected float explosionDamage = 35f;
    [SerializeField] protected LayerMask explosionDamageMask = ~0;

    [Header("Effects")]
    [SerializeField] protected GameObject explosionVFX;
    [SerializeField] protected AudioClip explosionSFX;

    [Header("AI")]
    [SerializeField] protected StateMachine aiStateMachine;

    [Header("Car Impact")]
    [SerializeField] protected float carImpactPhysicsDuration = 0.35f;
    [SerializeField] protected float carImpactLift = 1.4f;
    [SerializeField] protected float carImpactLiftFromStrength = 0.12f;
    [SerializeField] protected float maxCarImpactLift = 4.5f;
    [SerializeField] protected float carImpactNavMeshSampleRadius = 3f;

    protected Rigidbody rb;
    protected NavMeshAgent agent;
    protected bool armed;
    protected bool isCapturedByGrapple;
    protected bool isDead;
    protected float currentHealth;
    protected Camera mainCamera;
    protected Transform healthBarRoot;
    protected Transform healthBarFillTransform;
    protected SpriteRenderer healthBarBackgroundRenderer;
    protected SpriteRenderer healthBarFillRenderer;
    protected Coroutine carImpactRoutine;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsAlive => !isDead;
    public bool IsRamDamageImmune => isCapturedByGrapple || armed;
    public float Size => size;
    public float Damage => damage;
    public bool CanBeGrappled => canBeGrappled;
    public bool ExplosionCanHarmPlayer => explosionCanHarmPlayer;
    public float ExplosionRadius => explosionRadius;
    public float ExplosionDamage => explosionDamage;
    public StateMachine StateMachine => aiStateMachine;

    protected bool IsArmed => armed;

    public virtual void OnAttackWindup(float windupDuration, Transform target) { }

    public virtual void OnAttackStrike(Transform target) { }

    protected virtual void OnCarImpactStarted() { }

    protected virtual void OnRecoveredFromCarImpact() { }

    static Sprite solidSprite;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        agent = GetComponent<NavMeshAgent>();
        if (aiStateMachine == null)
            aiStateMachine = GetComponent<StateMachine>();

        maxHealth = Mathf.Max(1f, maxHealth);
        size = Mathf.Max(0.01f, size);
        damage = Mathf.Max(0f, damage);
        currentHealth = maxHealth;
        mainCamera = Camera.main;

        // NavMeshAgent drives movement while AI is active.
        if (agent != null && rb != null)
            rb.isKinematic = true;

        if (showHealthBar)
        {
            EnsureHealthBar();
            RefreshHealthBar();
        }
    }

    protected virtual void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        size = Mathf.Max(0.01f, size);
        damage = Mathf.Max(0f, damage);
        currentHealth = Application.isPlaying ? Mathf.Clamp(currentHealth, 0f, maxHealth) : maxHealth;
        explosionRadius = Mathf.Max(0f, explosionRadius);
        explosionDamage = Mathf.Max(0f, explosionDamage);
        carImpactPhysicsDuration = Mathf.Max(0.01f, carImpactPhysicsDuration);
        carImpactLift = Mathf.Max(0f, carImpactLift);
        carImpactLiftFromStrength = Mathf.Max(0f, carImpactLiftFromStrength);
        maxCarImpactLift = Mathf.Max(carImpactLift, maxCarImpactLift);
        carImpactNavMeshSampleRadius = Mathf.Max(0.1f, carImpactNavMeshSampleRadius);
        healthBarSize.x = Mathf.Max(0.1f, healthBarSize.x);
        healthBarSize.y = Mathf.Max(0.03f, healthBarSize.y);
    }

    public virtual bool TakeDamage(float amount, GameObject source = null)
    {
        if (isDead || amount <= 0f)
            return false;

        currentHealth = Mathf.Max(0f, currentHealth - amount);

        if (logDamageEvents)
            Debug.Log(name + " took " + amount + " damage. HP: " + currentHealth + "/" + maxHealth, this);

        if (currentHealth <= 0f)
            Die();
        else
            RefreshHealthBar();

        return true;
    }

    protected virtual void LateUpdate()
    {
        if (!showHealthBar || healthBarRoot == null)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;

        healthBarRoot.position = transform.position + healthBarOffset;
        if (mainCamera != null)
            healthBarRoot.forward = mainCamera.transform.forward;
    }

    public virtual void ArmExplosion()
    {
        if (isDead)
            return;

        isCapturedByGrapple = false;
        armed = true;
        if (aiStateMachine != null)
            aiStateMachine.SetTransitionLock(true);

        // Disable nav movement when the enemy is launched/thrown.
        if (agent != null)
            agent.enabled = false;
    }

    public virtual void OnCapturedByGrapple()
    {
        if (isDead || !canBeGrappled)
            return;

        isCapturedByGrapple = true;
        armed = false;

        if (aiStateMachine != null)
            aiStateMachine.SetTransitionLock(true);

        if (agent != null && agent.enabled)
            agent.enabled = false;
    }

    public virtual void ApplyCarImpact(Vector3 direction, float strength)
    {
        if (isDead || armed || rb == null || strength <= 0f)
            return;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        if (carImpactRoutine != null)
            StopCoroutine(carImpactRoutine);

        carImpactRoutine = StartCoroutine(HandleCarImpact(direction.normalized, strength));
    }

    protected virtual void Explode()
    {
        if (isDead)
            return;

        Vector3 pos = transform.position;

        Collider[] hits = Physics.OverlapSphere(pos, explosionRadius, explosionDamageMask, QueryTriggerInteraction.Collide);
        HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();

        for (int i = 0; i < hits.Length; i++)
        {
            IDamageable target = DamageUtility.FindDamageable(hits[i]);
            if (target == null || ReferenceEquals(target, this) || !target.IsAlive)
                continue;

            if (!explosionCanHarmPlayer && target is CarController)
                continue;

            if (!damagedTargets.Add(target))
                continue;

            target.TakeDamage(explosionDamage, gameObject);
        }

        if (explosionVFX != null)
            Instantiate(explosionVFX, pos, Quaternion.identity);

        RuntimeParticleFactory.SpawnEnemyExplosionPulse(pos, explosionRadius);

        if (explosionSFX != null)
            AudioPlaybackUtility.PlayDetachedClip(explosionSFX, pos, 1f, 1f, 1f, 1f, 24f);

        Die(forceDestroy: true);
    }

    protected virtual void Die(bool forceDestroy = false)
    {
        if (isDead)
            return;

        isDead = true;
        isCapturedByGrapple = false;
        armed = false;

        if (aiStateMachine != null)
            aiStateMachine.SetTransitionLock(true);

        if (agent != null && agent.enabled)
            agent.enabled = false;

        RefreshHealthBar();

        if (forceDestroy || destroyOnDeath)
            Destroy(gameObject);
    }

    protected virtual void OnDisable()
    {
        if (carImpactRoutine != null)
        {
            StopCoroutine(carImpactRoutine);
            carImpactRoutine = null;
        }
    }

    protected virtual IEnumerator HandleCarImpact(Vector3 direction, float strength)
    {
        if (aiStateMachine != null)
            aiStateMachine.SetTransitionLock(true);

        Quaternion uprightRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        bool canRestoreAgent = agent != null && agent.enabled && agent.isOnNavMesh;

        OnCarImpactStarted();

        if (agent != null && agent.enabled)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
            agent.enabled = false;
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            float lift = carImpactLift + (strength * carImpactLiftFromStrength);
            lift = Mathf.Min(lift, Mathf.Max(carImpactLift, maxCarImpactLift));
            Vector3 impulse = direction * strength + Vector3.up * lift;
            rb.AddForce(impulse, ForceMode.VelocityChange);
        }

        yield return new WaitForSeconds(Mathf.Max(0.01f, carImpactPhysicsDuration));

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (!isDead)
        {
            Vector3 standPosition = transform.position;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, carImpactNavMeshSampleRadius, NavMesh.AllAreas))
                standPosition = navHit.position;

            if (rb != null)
                rb.position = standPosition;
            else
                transform.position = standPosition;

            transform.rotation = uprightRotation;
        }

        if (!isDead && canRestoreAgent && agent != null)
        {
            if (!agent.enabled)
                agent.enabled = true;

            agent.Warp(transform.position);
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.isStopped = false;
        }

        if (rb != null)
            rb.isKinematic = !isDead && agent != null && agent.enabled;

        if (!isDead)
            OnRecoveredFromCarImpact();

        if (aiStateMachine != null && !isDead && !armed && !isCapturedByGrapple)
            aiStateMachine.SetTransitionLock(false);

        carImpactRoutine = null;
    }

    protected virtual void EnsureHealthBar()
    {
        if (healthBarRoot != null)
            return;

        if (solidSprite == null)
            solidSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);

        GameObject root = new GameObject("HealthBar");
        root.transform.SetParent(transform, false);
        healthBarRoot = root.transform;
        healthBarRoot.localPosition = healthBarOffset;

        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(healthBarRoot, false);
        healthBarBackgroundRenderer = bg.AddComponent<SpriteRenderer>();
        healthBarBackgroundRenderer.sprite = solidSprite;
        healthBarBackgroundRenderer.color = healthBarBackgroundColor;
        healthBarBackgroundRenderer.sortingOrder = 1000;

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(healthBarRoot, false);
        healthBarFillTransform = fill.transform;
        healthBarFillRenderer = fill.AddComponent<SpriteRenderer>();
        healthBarFillRenderer.sprite = solidSprite;
        healthBarFillRenderer.color = healthBarFillColor;
        healthBarFillRenderer.sortingOrder = 1001;
    }

    protected virtual void RefreshHealthBar()
    {
        if (!showHealthBar)
            return;

        EnsureHealthBar();
        if (healthBarRoot == null || healthBarFillTransform == null)
            return;

        float width = healthBarSize.x;
        float height = healthBarSize.y;
        float ratio = Mathf.Clamp01(currentHealth / Mathf.Max(1f, maxHealth));

        if (healthBarBackgroundRenderer != null)
            healthBarBackgroundRenderer.transform.localScale = new Vector3(width, height, 1f);

        float fillWidth = Mathf.Max(0.001f, width * ratio);
        healthBarFillTransform.localScale = new Vector3(fillWidth, height * 0.78f, 1f);
        healthBarFillTransform.localPosition = new Vector3(-(width - fillWidth) * 0.5f, 0f, -0.001f);
        healthBarRoot.gameObject.SetActive(!isDead);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
