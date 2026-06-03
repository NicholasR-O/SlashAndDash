using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class ThrowableVase : MonoBehaviour
{
    [SerializeField] float impactDamage = 25f;
    [SerializeField] float minDamageSpeed = 1f;
    [SerializeField] float collisionGraceSeconds = 0.08f;
    [SerializeField] bool canBeGrappled = true;
    [SerializeField] bool destroyOnThrownImpact = true;

    Rigidbody rb;
    bool armed;
    bool hasBroken;
    float thrownAt;
    GameObject throwSource;

    public bool CanBeGrappled => canBeGrappled && !hasBroken;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    void OnValidate()
    {
        impactDamage = Mathf.Max(0f, impactDamage);
        minDamageSpeed = Mathf.Max(0f, minDamageSpeed);
        collisionGraceSeconds = Mathf.Max(0f, collisionGraceSeconds);
    }

    public void OnCapturedByGrapple()
    {
        armed = false;
        throwSource = null;
    }

    public void OnThrownByGrapple(GameObject source)
    {
        armed = true;
        thrownAt = Time.time;
        throwSource = source != null ? source : gameObject;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!armed || hasBroken)
            return;

        if (Time.time - thrownAt < collisionGraceSeconds)
            return;

        TryDamageEnemy(collision);

        if (destroyOnThrownImpact)
            Break();
    }

    void TryDamageEnemy(Collision collision)
    {
        if (collision == null || collision.collider == null)
            return;

        float speed = rb != null ? rb.linearVelocity.magnitude : collision.relativeVelocity.magnitude;
        if (speed < minDamageSpeed)
            return;

        Enemy enemy = collision.collider.GetComponentInParent<Enemy>();
        if (enemy == null || !enemy.IsAlive)
            return;

        enemy.TakeDamage(impactDamage, throwSource);
    }

    void Break()
    {
        if (hasBroken)
            return;

        hasBroken = true;
        Destroy(gameObject);
    }
}
