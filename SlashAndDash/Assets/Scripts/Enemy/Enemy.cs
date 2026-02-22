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

[RequireComponent(typeof(Rigidbody), typeof(NavMeshAgent))]
public class Enemy : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] float maxHealth = 50f;
    [SerializeField] bool destroyOnDeath = true;
    [SerializeField] bool logDamageEvents;

    [Header("Health Bar")]
    [SerializeField] bool showHealthBar = true;
    [SerializeField] Vector3 healthBarOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] Vector2 healthBarSize = new Vector2(1.4f, 0.16f);
    [SerializeField] Color healthBarBackgroundColor = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField] Color healthBarFillColor = new Color(0.25f, 0.95f, 0.35f, 1f);

    [Header("Explosion")]
    public float explosionRadius = 4.0f;
    public float explosionDamage = 35f;
    public LayerMask explosionDamageMask = ~0;

    [Header("Effects")]
    public GameObject explosionVFX;
    public AudioClip explosionSFX;

    Rigidbody rb;
    NavMeshAgent agent;
    StateMachine aiStateMachine;
    bool armed;
    bool isDead;
    float currentHealth;
    Camera mainCamera;
    Transform healthBarRoot;
    Transform healthBarFillTransform;
    SpriteRenderer healthBarBackgroundRenderer;
    SpriteRenderer healthBarFillRenderer;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsAlive => !isDead;

    static Sprite solidSprite;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        agent = GetComponent<NavMeshAgent>();
        aiStateMachine = GetComponent<StateMachine>();
        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = maxHealth;
        mainCamera = Camera.main;

        // NavMeshAgent drives movement while AI is active.
        if (agent != null)
            rb.isKinematic = true;

        if (showHealthBar)
        {
            EnsureHealthBar();
            RefreshHealthBar();
        }
    }

    void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = Application.isPlaying ? Mathf.Clamp(currentHealth, 0f, maxHealth) : maxHealth;
        explosionRadius = Mathf.Max(0f, explosionRadius);
        explosionDamage = Mathf.Max(0f, explosionDamage);
        healthBarSize.x = Mathf.Max(0.1f, healthBarSize.x);
        healthBarSize.y = Mathf.Max(0.03f, healthBarSize.y);
    }

    public bool TakeDamage(float amount, GameObject source = null)
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

    void LateUpdate()
    {
        if (!showHealthBar || healthBarRoot == null)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;

        healthBarRoot.position = transform.position + healthBarOffset;
        if (mainCamera != null)
            healthBarRoot.forward = mainCamera.transform.forward;
    }

    public void ArmExplosion()
    {
        if (isDead)
            return;

        armed = true;
        if (aiStateMachine != null)
            aiStateMachine.SetTransitionLock(true);

        // Disable nav movement when the enemy is launched/thrown.
        if (agent != null)
            agent.enabled = false;
    }

    public void OnCapturedByGrapple()
    {
        if (isDead)
            return;

        armed = false;

        if (aiStateMachine != null)
            aiStateMachine.SetTransitionLock(true);

        if (agent != null && agent.enabled)
            agent.enabled = false;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!armed || isDead)
            return;

        // Explode on contact with ground
        if (collision.collider.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            Explode();
            return;
        }

        // Explode on contact with another enemy
        Enemy otherEnemy = collision.collider.GetComponentInParent<Enemy>();
        if (otherEnemy != null && otherEnemy != this)
        {
            Explode();
        }
    }

    void Explode()
    {
        Vector3 pos = transform.position;

        Collider[] hits = Physics.OverlapSphere(pos, explosionRadius, explosionDamageMask, QueryTriggerInteraction.Collide);
        HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();

        for (int i = 0; i < hits.Length; i++)
        {
            IDamageable target = DamageUtility.FindDamageable(hits[i]);
            if (target == null || ReferenceEquals(target, this) || !target.IsAlive)
                continue;

            // Explosion should not damage the player.
            if (target is CarController)
                continue;

            if (!damagedTargets.Add(target))
                continue;

            target.TakeDamage(explosionDamage, gameObject);
        }

        if (explosionVFX != null)
            Instantiate(explosionVFX, pos, Quaternion.identity);

        if (explosionSFX != null)
            AudioSource.PlayClipAtPoint(explosionSFX, pos);

        Destroy(gameObject);
    }

    void Die()
    {
        if (isDead)
            return;

        isDead = true;
        armed = false;

        if (aiStateMachine != null)
            aiStateMachine.SetTransitionLock(true);

        if (agent != null && agent.enabled)
            agent.enabled = false;

        if (destroyOnDeath)
            Destroy(gameObject);
    }

    void EnsureHealthBar()
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

    void RefreshHealthBar()
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
