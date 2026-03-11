using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class EnemyThrownTrail : MonoBehaviour
{
    Rigidbody cachedRigidbody;
    ParticleSystem trailParticles;
    float minEmitDuration;
    float maxEmitDuration;
    float stopSpeedThreshold;
    float emitStartedAt;
    bool isEmitting;

    void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
    }

    public void Play(float minDuration, float maxDuration, float speedThreshold)
    {
        minEmitDuration = Mathf.Max(0f, minDuration);
        maxEmitDuration = Mathf.Max(minEmitDuration + 0.05f, maxDuration);
        stopSpeedThreshold = Mathf.Max(0.01f, speedThreshold);

        EnsureTrailParticles();
        if (trailParticles == null)
            return;

        emitStartedAt = Time.unscaledTime;
        isEmitting = true;
        enabled = true;

        ParticleSystem.EmissionModule emission = trailParticles.emission;
        emission.enabled = true;
        emission.rateOverTime = 42f;
        if (!trailParticles.isPlaying)
            trailParticles.Play(true);
    }

    void Update()
    {
        if (!isEmitting || trailParticles == null || cachedRigidbody == null)
            return;

        float elapsed = Time.unscaledTime - emitStartedAt;
        if (elapsed < minEmitDuration)
            return;

        float stopSpeedSqr = stopSpeedThreshold * stopSpeedThreshold;
        bool belowStopSpeed = cachedRigidbody.linearVelocity.sqrMagnitude <= stopSpeedSqr;
        if (elapsed >= maxEmitDuration || belowStopSpeed)
            StopTrail();
    }

    void OnDisable()
    {
        if (isEmitting)
            StopTrail();
    }

    void EnsureTrailParticles()
    {
        if (trailParticles != null)
            return;

        Transform existing = transform.Find("ThrownTrailParticles");
        if (existing != null)
            trailParticles = existing.GetComponent<ParticleSystem>();
        if (trailParticles != null)
            return;

        trailParticles = RuntimeParticleFactory.CreateEnemyThrownTrail(transform, "ThrownTrailParticles");
    }

    void StopTrail()
    {
        isEmitting = false;

        if (trailParticles == null)
        {
            enabled = false;
            return;
        }

        ParticleSystem.EmissionModule emission = trailParticles.emission;
        emission.enabled = false;
        trailParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        enabled = false;
    }
}
