using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody), typeof(NavMeshAgent))]
public class WizardEnemy : BasicEnemy
{
    [Header("Wizard Projectile")]
    [SerializeField] private WizardProjectile projectilePrefab;
    [SerializeField] private Transform projectileSpawn;
    [SerializeField] private float projectileSpeed = 8f;
    [SerializeField] private float projectileLifetime = 6f;
    [SerializeField] private float aimHeightOffset = 0.9f;

    [Header("Wizard Audio")]
    [SerializeField] private AudioClip wizardAttackSFX;

    private void Reset()
    {
        maxHealth = 20f;
        damage = 8f;
        size = 0.9f;
    }

    protected override AudioClip GetAttackAudioClip()
    {
        return wizardAttackSFX;
    }

    public bool FireProjectile(Transform target)
    {
        if (projectilePrefab == null || target == null)
            return false;

        Vector3 aimPoint = GetAimPoint(target);
        Vector3 direction = aimPoint - GetAimOrigin();
        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward;

        direction.Normalize();
        Vector3 origin = GetAimOrigin(direction);

        WizardProjectile projectile = Instantiate(
            projectilePrefab,
            origin,
            Quaternion.LookRotation(direction, Vector3.up));

        projectile.Initialize(direction, damage, gameObject, projectileSpeed, projectileLifetime);
        return true;
    }

    private Vector3 GetAimPoint(Transform target)
    {
        Collider targetCollider = target.GetComponentInChildren<Collider>();
        if (targetCollider != null)
            return targetCollider.bounds.center;

        return target.position + Vector3.up * aimHeightOffset;
    }

    private Vector3 GetAimOrigin()
    {
        return GetAimOrigin(transform.forward);
    }

    private Vector3 GetAimOrigin(Vector3 direction)
    {
        if (projectileSpawn != null && projectileSpawn.gameObject.scene.IsValid())
            return projectileSpawn.position;

        Vector3 normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        Bounds ownerBounds = GetOwnerBounds();
        float forwardClearance = Mathf.Max(ownerBounds.extents.x, ownerBounds.extents.z) + GetProjectileRadius() + 0.15f;
        Vector3 baseOrigin = ownerBounds.center;
        baseOrigin.y = Mathf.Max(baseOrigin.y, transform.position.y + aimHeightOffset);
        return baseOrigin + normalizedDirection * forwardClearance;
    }

    private Bounds GetOwnerBounds()
    {
        Collider ownerCollider = GetComponent<Collider>();
        if (ownerCollider != null)
            return ownerCollider.bounds;

        return new Bounds(transform.position + Vector3.up * aimHeightOffset, Vector3.one);
    }

    private float GetProjectileRadius()
    {
        SphereCollider sphereCollider = projectilePrefab.GetComponent<SphereCollider>();
        if (sphereCollider == null)
            return 0.25f;

        Vector3 prefabScale = projectilePrefab.transform.localScale;
        float maxScale = Mathf.Max(Mathf.Abs(prefabScale.x), Mathf.Abs(prefabScale.y), Mathf.Abs(prefabScale.z));
        return sphereCollider.radius * Mathf.Max(0.01f, maxScale);
    }
}
