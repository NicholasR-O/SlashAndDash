using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Enemy : MonoBehaviour
{
    [Header("Explosion")]
    public float explosionRadius = 4.0f;
    public float explosionForce = 800f;
    public float upwardModifier = 0.6f;

    [Header("Effects")]
    public GameObject explosionVFX;
    public AudioClip explosionSFX;

    Rigidbody rb;
    bool armed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void ArmExplosion()
    {
        armed = true;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!armed) return;

        // explode on contact with ground
        if (collision.collider.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            Explode();
        }
    }

    void Explode()
    {
        Vector3 pos = transform.position;

        Collider[] hits = Physics.OverlapSphere(pos, explosionRadius);
        foreach (var hit in hits)
        {
            Rigidbody hitRb = hit.attachedRigidbody;
            if (hitRb != null && hitRb != rb)
            {
                hitRb.AddExplosionForce(explosionForce, pos, explosionRadius, upwardModifier, ForceMode.Impulse);
            }
        }

        if (explosionVFX != null)
            Instantiate(explosionVFX, pos, Quaternion.identity);

        if (explosionSFX != null)
            AudioSource.PlayClipAtPoint(explosionSFX, pos);

        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
