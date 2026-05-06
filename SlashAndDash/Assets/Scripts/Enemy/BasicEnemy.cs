using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody), typeof(NavMeshAgent))]
public class BasicEnemy : Enemy
{
    [Header("Cat Animation")]
    [SerializeField] private bool enableCatHop = true;
    [SerializeField] private Transform modelRoot;
    [SerializeField] private bool allowAnimatingRoot;
    [SerializeField] private bool autoGroundModel = true;
    [SerializeField] private float modelGroundOffset;
    [SerializeField] private float hopHeight = 0.35f;
    [SerializeField] private float hopFrequency = 6f;
    [SerializeField] private float hopSpeedForMax = 5f;
    [SerializeField] private float hopForwardOffset = 0.15f;
    [SerializeField] private float hopSquash = 0.2f;
    [SerializeField] private float hopLeanDegrees = 18f;
    [SerializeField] private float hopExponent = 1.6f;
    [SerializeField] private float hopMinSpeed = 0.2f;

    [Header("Attack Animation")]
    [SerializeField] private bool enableAttackAnim = true;
    [SerializeField] private float attackWindupDistance = 0.25f;
    [SerializeField] private float attackWindupPitch = 12f;
    [SerializeField] private float attackLungeDistance = 0.5f;
    [SerializeField] private float attackLungePitch = 18f;
    [SerializeField] private float attackLungeTime = 0.12f;
    [SerializeField] private float attackRecoverTime = 0.18f;

    [Header("Stand Up Animation")]
    [SerializeField] private bool enableStandUpAnim = true;
    [SerializeField] private float standUpJumpHeight = 0.3f;
    [SerializeField] private float standUpJumpDuration = 0.28f;
    [SerializeField] private float standUpLeanDegrees = 10f;

    [Header("Thrown Alignment")]
    [SerializeField] private bool alignWhenThrown = true;
    [SerializeField] private float thrownAlignSpeed = 12f;
    [SerializeField] private float thrownMinSpeed = 1f;

    [Header("Enemy Audio")]
    [SerializeField] private AudioClip attackSFX;
    [SerializeField] private Vector2 attackPitchRange = new Vector2(0.86f, 1.16f);
    [SerializeField] private float attackVolume = 1f;
    [SerializeField] private AudioClip idleSFX;
    [SerializeField] private Vector2 idlePitchRange = new Vector2(0.84f, 1.18f);
    [SerializeField] private Vector2 idleIntervalRange = new Vector2(2f, 4.5f);
    [SerializeField] private float idleVolume = 0.75f;
    [SerializeField] private int maxSimultaneousIdleMeows = 3;
    [SerializeField] private float enemyAudioSpatialBlend = 1f;
    [SerializeField] private float enemyAudioMinDistance = 1f;
    [SerializeField] private float enemyAudioMaxDistance = 22f;

    private static readonly List<float> activeIdleMeowReleaseTimes = new List<float>();

    private Vector3 modelBaseLocalPos;
    private Quaternion modelBaseLocalRot;
    private Vector3 modelBaseLocalScale;
    private Vector3 attackOffsetLocal;
    private Quaternion attackRotationLocal = Quaternion.identity;
    private Vector3 recoveryOffsetLocal;
    private Quaternion recoveryRotationLocal = Quaternion.identity;
    private Coroutine attackRoutine;
    private Coroutine standUpRoutine;
    private bool hasCachedPose;
    private bool usingRootFallback;
    private float nextIdleSoundAt;

    protected override void Awake()
    {
        base.Awake();
        ResolveModelRoot();
        CacheModelPose();
        ScheduleNextIdleSound(initialDelayOnly: true);
    }

    protected virtual void Update()
    {
        UpdateIdleAudio();
    }

    protected virtual void FixedUpdate()
    {
        AlignThrownBodyToVelocity();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        hopHeight = Mathf.Max(0f, hopHeight);
        hopFrequency = Mathf.Max(0f, hopFrequency);
        hopSpeedForMax = Mathf.Max(0.01f, hopSpeedForMax);
        hopForwardOffset = Mathf.Max(0f, hopForwardOffset);
        hopSquash = Mathf.Clamp(hopSquash, 0f, 0.6f);
        hopLeanDegrees = Mathf.Max(0f, hopLeanDegrees);
        hopExponent = Mathf.Max(0.2f, hopExponent);
        hopMinSpeed = Mathf.Max(0f, hopMinSpeed);

        attackWindupDistance = Mathf.Max(0f, attackWindupDistance);
        attackLungeDistance = Mathf.Max(0f, attackLungeDistance);
        attackLungeTime = Mathf.Max(0.01f, attackLungeTime);
        attackRecoverTime = Mathf.Max(0.01f, attackRecoverTime);

        standUpJumpHeight = Mathf.Max(0f, standUpJumpHeight);
        standUpJumpDuration = Mathf.Max(0.05f, standUpJumpDuration);
        standUpLeanDegrees = Mathf.Max(0f, standUpLeanDegrees);

        thrownAlignSpeed = Mathf.Max(0f, thrownAlignSpeed);
        thrownMinSpeed = Mathf.Max(0f, thrownMinSpeed);

        attackPitchRange.x = Mathf.Clamp(attackPitchRange.x, 0.1f, 3f);
        attackPitchRange.y = Mathf.Clamp(attackPitchRange.y, attackPitchRange.x, 3f);
        idlePitchRange.x = Mathf.Clamp(idlePitchRange.x, 0.1f, 3f);
        idlePitchRange.y = Mathf.Clamp(idlePitchRange.y, idlePitchRange.x, 3f);
        idleIntervalRange.x = Mathf.Max(0.1f, idleIntervalRange.x);
        idleIntervalRange.y = Mathf.Max(idleIntervalRange.x, idleIntervalRange.y);
        attackVolume = Mathf.Clamp01(attackVolume);
        idleVolume = Mathf.Clamp01(idleVolume);
        maxSimultaneousIdleMeows = Mathf.Clamp(maxSimultaneousIdleMeows, 1, 32);
        enemyAudioSpatialBlend = 1f;
        enemyAudioMinDistance = Mathf.Max(0.01f, enemyAudioMinDistance);
        enemyAudioMaxDistance = Mathf.Max(enemyAudioMinDistance, enemyAudioMaxDistance);
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();

        if (modelRoot == null)
            return;

        if (!hasCachedPose)
            CacheModelPose();

        Vector3 velocity = GetMovementVelocity();
        bool thrown = alignWhenThrown && IsArmed && velocity.sqrMagnitude > thrownMinSpeed * thrownMinSpeed;

        ApplyModelPose(velocity, thrown);

        if (thrown)
            AlignModelToVelocity(velocity);
    }

    public override void OnAttackWindup(float windupDuration, Transform target)
    {
        if (!enableAttackAnim || modelRoot == null || IsArmed)
            return;

        if (attackRoutine != null)
            StopCoroutine(attackRoutine);

        float duration = Mathf.Max(0.01f, windupDuration);
        attackRoutine = StartCoroutine(AttackWindupRoutine(duration));
    }

    public override void OnAttackStrike(Transform target)
    {
        if (!enableAttackAnim || modelRoot == null || IsArmed)
        {
            PlayAttackAudio();
            return;
        }

        if (attackRoutine != null)
            StopCoroutine(attackRoutine);

        attackRoutine = StartCoroutine(AttackStrikeRoutine());
        PlayAttackAudio();
    }

    public override void ArmExplosion()
    {
        base.ArmExplosion();
        UnlockThrownRotation();
        StopAttackAnimation();
        StopStandUpAnimation();
    }

    public override void OnCapturedByGrapple()
    {
        base.OnCapturedByGrapple();
        StopAttackAnimation();
        StopStandUpAnimation();
    }

    protected override void OnCarImpactStarted()
    {
        base.OnCarImpactStarted();
        StopAttackAnimation();
        StopStandUpAnimation();
    }

    protected override void OnRecoveredFromCarImpact()
    {
        base.OnRecoveredFromCarImpact();

        if (modelRoot == null || !enableStandUpAnim)
            return;

        if (standUpRoutine != null)
            StopCoroutine(standUpRoutine);

        standUpRoutine = StartCoroutine(StandUpRoutine());
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        StopAttackAnimation();
        StopStandUpAnimation();
        ResetModelPose();
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (!IsAlive || !IsArmed)
            return;

        if (collision == null || collision.collider == null)
            return;

        if (collision.collider.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            Explode();
            return;
        }

        Enemy otherEnemy = collision.collider.GetComponentInParent<Enemy>();
        if (otherEnemy != null && otherEnemy != this)
            Explode();
    }

    protected virtual AudioClip GetAttackAudioClip()
    {
        return attackSFX;
    }

    private void ResolveModelRoot()
    {
        if (modelRoot != null)
        {
            usingRootFallback = modelRoot == transform;
            return;
        }

        Transform found = transform.Find("Model");
        if (found == null && transform.childCount > 0)
            found = transform.GetChild(0);

        if (found != null)
        {
            modelRoot = found;
            usingRootFallback = false;
            return;
        }

        if (allowAnimatingRoot)
        {
            modelRoot = transform;
            usingRootFallback = true;
        }
    }

    private void CacheModelPose()
    {
        if (modelRoot == null)
            return;

        ApplyAutomaticModelGrounding();
        modelBaseLocalPos = modelRoot.localPosition;
        modelBaseLocalRot = modelRoot.localRotation;
        modelBaseLocalScale = modelRoot.localScale;
        hasCachedPose = true;
    }

    private void ResetModelPose()
    {
        if (modelRoot == null || !hasCachedPose)
            return;

        modelRoot.localPosition = modelBaseLocalPos;
        modelRoot.localRotation = modelBaseLocalRot;
        modelRoot.localScale = modelBaseLocalScale;
        attackOffsetLocal = Vector3.zero;
        attackRotationLocal = Quaternion.identity;
        recoveryOffsetLocal = Vector3.zero;
        recoveryRotationLocal = Quaternion.identity;
    }

    private void ApplyAutomaticModelGrounding()
    {
        if (!autoGroundModel || modelRoot == null || usingRootFallback)
            return;

        if (!TryGetModelBounds(out Bounds modelBounds))
            return;

        float targetBottom = transform.position.y + modelGroundOffset;
        float adjustment = targetBottom - modelBounds.min.y;
        if (Mathf.Abs(adjustment) <= 0.0001f)
            return;

        modelRoot.position += Vector3.up * adjustment;
    }

    private bool TryGetModelBounds(out Bounds bounds)
    {
        Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            bounds = renderer.bounds;
            for (int j = i + 1; j < renderers.Length; j++)
            {
                Renderer extraRenderer = renderers[j];
                if (extraRenderer == null || !extraRenderer.enabled)
                    continue;

                bounds.Encapsulate(extraRenderer.bounds);
            }

            return true;
        }

        bounds = default;
        return false;
    }

    private Vector3 GetMovementVelocity()
    {
        if (agent != null && agent.enabled)
            return agent.velocity;

        if (rb != null)
            return rb.linearVelocity;

        return Vector3.zero;
    }

    private void ApplyModelPose(Vector3 velocity, bool thrown)
    {
        Vector3 localPos = modelBaseLocalPos;
        Quaternion localRot = modelBaseLocalRot;
        Vector3 localScale = modelBaseLocalScale;

        if (!thrown)
        {
            localPos += attackOffsetLocal + recoveryOffsetLocal;
            localRot = localRot * attackRotationLocal * recoveryRotationLocal;

            if (enableCatHop && (!usingRootFallback || allowAnimatingRoot))
            {
                float speed = velocity.magnitude;
                if (speed > hopMinSpeed)
                {
                    float speedRatio = Mathf.Clamp01(speed / Mathf.Max(0.01f, hopSpeedForMax));
                    float hopPhase = Time.time * hopFrequency * Mathf.Lerp(0.7f, 1.3f, speedRatio);
                    float hop = Mathf.Abs(Mathf.Sin(hopPhase));
                    hop = Mathf.Pow(hop, hopExponent);

                    float vertical = hopHeight * speedRatio * hop;
                    float forward = hopForwardOffset * speedRatio * hop;

                    localPos += new Vector3(0f, vertical, forward);

                    Vector3 localVelocity = transform.InverseTransformDirection(velocity);
                    float forwardLean = -hopLeanDegrees * Mathf.Clamp(localVelocity.z, -1f, 1f) * speedRatio;
                    float sideLean = hopLeanDegrees * 0.4f * Mathf.Clamp(localVelocity.x, -1f, 1f) * speedRatio;
                    localRot *= Quaternion.Euler(forwardLean, 0f, -sideLean);

                    float squash = hopSquash * hop * speedRatio;
                    Vector3 hopScale = new Vector3(1f + squash * 0.6f, 1f - squash, 1f + squash * 0.6f);
                    localScale = Vector3.Scale(localScale, hopScale);
                }
            }
        }

        modelRoot.localPosition = localPos;
        modelRoot.localRotation = localRot;
        modelRoot.localScale = localScale;
    }

    private void AlignModelToVelocity(Vector3 velocity)
    {
        if (velocity.sqrMagnitude < 0.0001f)
            return;

        Quaternion worldRotation = GetTopFacingVelocityRotation(velocity);
        Quaternion localTarget = Quaternion.Inverse(transform.rotation) * worldRotation;

        modelRoot.localRotation = Quaternion.Slerp(
            modelRoot.localRotation,
            localTarget,
            Mathf.Clamp01(thrownAlignSpeed * Time.deltaTime)
        );
    }

    private void AlignThrownBodyToVelocity()
    {
        if (!alignWhenThrown || !IsArmed || rb == null)
            return;

        Vector3 velocity = rb.linearVelocity;
        if (velocity.sqrMagnitude <= thrownMinSpeed * thrownMinSpeed)
            return;

        Quaternion targetRotation = GetTopFacingVelocityRotation(velocity);
        float alignT = 1f - Mathf.Exp(-Mathf.Max(0f, thrownAlignSpeed) * Time.fixedDeltaTime);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Mathf.Clamp01(alignT)));
    }

    private Quaternion GetTopFacingVelocityRotation(Vector3 velocity)
    {
        Vector3 up = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : transform.up;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, up);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(transform.right, up);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.Cross(up, Vector3.right);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.Cross(up, Vector3.forward);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        return Quaternion.LookRotation(forward.normalized, up);
    }

    private void UnlockThrownRotation()
    {
        if (!alignWhenThrown || rb == null)
            return;

        rb.constraints = RigidbodyConstraints.None;
        rb.angularVelocity = Vector3.zero;
    }

    private IEnumerator AttackWindupRoutine(float duration)
    {
        Vector3 startOffset = attackOffsetLocal;
        Quaternion startRot = attackRotationLocal;
        Vector3 targetOffset = Vector3.back * attackWindupDistance;
        Quaternion targetRot = Quaternion.Euler(attackWindupPitch, 0f, 0f);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float eased = EaseOut(t);
            attackOffsetLocal = Vector3.Lerp(startOffset, targetOffset, eased);
            attackRotationLocal = Quaternion.Slerp(startRot, targetRot, eased);
            yield return null;
        }

        attackOffsetLocal = targetOffset;
        attackRotationLocal = targetRot;
        attackRoutine = null;
    }

    private IEnumerator AttackStrikeRoutine()
    {
        Vector3 startOffset = attackOffsetLocal;
        Quaternion startRot = attackRotationLocal;
        Vector3 lungeOffset = Vector3.forward * attackLungeDistance;
        Quaternion lungeRot = Quaternion.Euler(-attackLungePitch, 0f, 0f);

        float t = 0f;
        float lungeDuration = Mathf.Max(0.01f, attackLungeTime);
        while (t < 1f)
        {
            t += Time.deltaTime / lungeDuration;
            float eased = EaseIn(t);
            attackOffsetLocal = Vector3.Lerp(startOffset, lungeOffset, eased);
            attackRotationLocal = Quaternion.Slerp(startRot, lungeRot, eased);
            yield return null;
        }

        t = 0f;
        Vector3 recoverOffset = Vector3.zero;
        Quaternion recoverRot = Quaternion.identity;
        float recoverDuration = Mathf.Max(0.01f, attackRecoverTime);
        while (t < 1f)
        {
            t += Time.deltaTime / recoverDuration;
            float eased = EaseOut(t);
            attackOffsetLocal = Vector3.Lerp(lungeOffset, recoverOffset, eased);
            attackRotationLocal = Quaternion.Slerp(lungeRot, recoverRot, eased);
            yield return null;
        }

        attackOffsetLocal = recoverOffset;
        attackRotationLocal = recoverRot;
        attackRoutine = null;
    }

    private IEnumerator StandUpRoutine()
    {
        float duration = Mathf.Max(0.05f, standUpJumpDuration);
        float lean = standUpLeanDegrees;
        float height = standUpJumpHeight;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float normalized = Mathf.Clamp01(t);
            float jump = Mathf.Sin(normalized * Mathf.PI);
            recoveryOffsetLocal = Vector3.up * (jump * height);
            recoveryRotationLocal = Quaternion.Euler(-jump * lean, 0f, 0f);
            yield return null;
        }

        recoveryOffsetLocal = Vector3.zero;
        recoveryRotationLocal = Quaternion.identity;
        standUpRoutine = null;
    }

    private void StopAttackAnimation()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        attackOffsetLocal = Vector3.zero;
        attackRotationLocal = Quaternion.identity;
    }

    private void StopStandUpAnimation()
    {
        if (standUpRoutine != null)
        {
            StopCoroutine(standUpRoutine);
            standUpRoutine = null;
        }

        recoveryOffsetLocal = Vector3.zero;
        recoveryRotationLocal = Quaternion.identity;
    }

    private void UpdateIdleAudio()
    {
        if (!CanPlayIdleAudio())
            return;

        if (Time.time < nextIdleSoundAt)
            return;

        PruneActiveIdleMeows();
        if (activeIdleMeowReleaseTimes.Count >= maxSimultaneousIdleMeows)
        {
            ScheduleNextIdleSound(initialDelayOnly: false);
            return;
        }

        float pitch = Random.Range(idlePitchRange.x, idlePitchRange.y);
        AudioPlaybackUtility.PlayDetachedClip(
            idleSFX,
            transform.position,
            idleVolume,
            pitch,
            GetCatSpatialBlend(),
            enemyAudioMinDistance,
            enemyAudioMaxDistance);
        activeIdleMeowReleaseTimes.Add(Time.time + GetClipDurationAtPitch(idleSFX, pitch));

        ScheduleNextIdleSound(initialDelayOnly: false);
    }

    private bool CanPlayIdleAudio()
    {
        if (!IsAlive || idleSFX == null || IsArmed || GameState.DisableAI)
            return false;

        if (attackRoutine != null || standUpRoutine != null)
            return false;

        Vector3 velocity = GetMovementVelocity();
        velocity.y = 0f;
        return velocity.sqrMagnitude <= 0.09f;
    }

    private void ScheduleNextIdleSound(bool initialDelayOnly)
    {
        float minDelay = Mathf.Max(0.1f, idleIntervalRange.x);
        float maxDelay = Mathf.Max(minDelay, idleIntervalRange.y);
        float delay = Random.Range(minDelay, maxDelay);
        if (initialDelayOnly)
            delay = Mathf.Max(delay, 0.5f);

        nextIdleSoundAt = Time.time + delay;
    }

    private void PlayAttackAudio()
    {
        AudioClip clip = GetAttackAudioClip();
        if (clip == null)
            return;

        float pitch = Random.Range(attackPitchRange.x, attackPitchRange.y);
        AudioPlaybackUtility.PlayDetachedClip(
            clip,
            transform.position,
            attackVolume,
            pitch,
            GetCatSpatialBlend(),
            enemyAudioMinDistance,
            enemyAudioMaxDistance);
    }

    private float GetCatSpatialBlend()
    {
        return Mathf.Clamp01(Mathf.Max(1f, enemyAudioSpatialBlend));
    }

    private static void PruneActiveIdleMeows()
    {
        for (int i = activeIdleMeowReleaseTimes.Count - 1; i >= 0; i--)
        {
            if (Time.time >= activeIdleMeowReleaseTimes[i])
                activeIdleMeowReleaseTimes.RemoveAt(i);
        }
    }

    private static float GetClipDurationAtPitch(AudioClip clip, float pitch)
    {
        if (clip == null)
            return 0f;

        return clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
    }

    private static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 2f);
    }

    private static float EaseIn(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t;
    }
}
