using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyExplodeOnThrow : MonoBehaviour
{
    [Header("Explosion")]
    public float explosionDelay = 2.0f;
    public float explosionRadius = 4.0f;
    public float explosionForce = 800f;
    public float upwardModifier = 0.6f;

    [Header("Damage / Effects")]
    public GameObject explosionVFX;
    public AudioClip explosionSFX;

    Rigidbody rb;
    bool armed;
    float timer;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (!armed)
            return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            Explode();
        }
    }

    /// <summary>
    /// Called by the grapple when the enemy is thrown.
    /// </summary>
    public void ArmExplosion()
    {
        if (armed)
            return;

        armed = true;
        timer = explosionDelay;
    }

    void Explode()
    {
        Vector3 pos = transform.position;

        // Physics explosion
        Collider[] hits = Physics.OverlapSphere(pos, explosionRadius);
        foreach (var hit in hits)
        {
            Rigidbody hitRb = hit.attachedRigidbody;
            if (hitRb != null && hitRb != rb)
            {
                hitRb.AddExplosionForce(
                    explosionForce,
                    pos,
                    explosionRadius,
                    upwardModifier,
                    ForceMode.Impulse
                );
            }
        }

        // VFX
        if (explosionVFX != null)
        {
            Instantiate(explosionVFX, pos, Quaternion.identity);
        }

        // SFX
        if (explosionSFX != null)
        {
            AudioSource.PlayClipAtPoint(explosionSFX, pos);
        }

        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
