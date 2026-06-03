using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody), typeof(NavMeshAgent))]
public class BasicEnemy : Enemy
{
    [Header("Slime Animation")]
    [SerializeField] private bool enableSlimeMotion = true;
    [SerializeField] private Transform modelRoot;
    [SerializeField] private bool allowAnimatingRoot;
    [SerializeField] private bool autoGroundModel = true;
    [SerializeField] private float modelGroundOffset;
    [SerializeField] private float idleSquish = 0.04f;
    [SerializeField] private float idleSquishFrequency = 1.8f;
    [SerializeField] private float hopHeight = 0.35f;
    [SerializeField] private float hopFrequency = 6f;
    [SerializeField] private float hopSpeedForMax = 5f;
    [SerializeField] private float hopForwardOffset = 0.15f;
    [SerializeField] private float hopSquash = 0.2f;
    [SerializeField] private float hopStretch = 0.1f;
    [SerializeField] private float hopLeanDegrees = 18f;
    [SerializeField] private float hopExponent = 1.6f;
    [SerializeField] private float hopMinSpeed = 0.2f;
    [SerializeField] private bool useSteppedAnimation = true;
    [SerializeField, Min(1)] private int movementPoseSteps = 5;
    [SerializeField, Min(1)] private int attackPoseSteps = 4;
    [SerializeField, Min(1)] private int recoveryPoseSteps = 5;

    [Header("Attack Animation")]
    [SerializeField] private bool enableAttackAnim = true;
    [SerializeField] private float attackWindupDistance = 0.25f;
    [SerializeField] private float attackWindupPitch = 12f;
    [SerializeField] private float attackLungeDistance = 0.5f;
    [SerializeField] private float attackLungePitch = 18f;
    [SerializeField] private float attackLungeTime = 0.12f;
    [SerializeField] private float attackRecoverTime = 0.18f;
    [SerializeField] private float attackWindupSquash = 0.18f;
    [SerializeField] private float attackLungeStretch = 0.22f;

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
    [SerializeField] private bool playIdleSoundsWhileChasing = true;
    [SerializeField] private Vector2 idlePitchRange = new Vector2(0.84f, 1.18f);
    [SerializeField] private Vector2 idleIntervalRange = new Vector2(2f, 4.5f);
    [SerializeField] private float idleVolume = 0.75f;
    [SerializeField] private int maxSimultaneousIdleSounds = 3;
    [SerializeField] private AudioClip hitSFX;
    [SerializeField] private Vector2 hitPitchRange = new Vector2(0.9f, 1.12f);
    [SerializeField] private float hitVolume = 0.9f;
    [SerializeField] private float enemyAudioSpatialBlend = 1f;
    [SerializeField] private float enemyAudioMinDistance = 1f;
    [SerializeField] private float enemyAudioMaxDistance = 32f;

    private const float MinimumEnemyAudioMaxDistance = 32f;

    private static readonly List<float> activeIdleSoundReleaseTimes = new List<float>();

    private Vector3 modelBaseLocalPos;
    private Quaternion modelBaseLocalRot;
    private Vector3 modelBaseLocalScale;
    private Vector3 attackOffsetLocal;
    private Quaternion attackRotationLocal = Quaternion.identity;
    private Vector3 attackScaleLocal = Vector3.one;
    private Vector3 recoveryOffsetLocal;
    private Quaternion recoveryRotationLocal = Quaternion.identity;
    private Vector3 recoveryScaleLocal = Vector3.one;
    private Vector3 unscaledModelLocalScale;
    private Coroutine attackRoutine;
    private Coroutine standUpRoutine;
    private bool hasCachedPose;
    private bool hasUnscaledModelLocalScale;
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
        idleSquish = Mathf.Clamp(idleSquish, 0f, 0.25f);
        idleSquishFrequency = Mathf.Max(0f, idleSquishFrequency);
        hopHeight = Mathf.Max(0f, hopHeight);
        hopFrequency = Mathf.Max(0f, hopFrequency);
        hopSpeedForMax = Mathf.Max(0.01f, hopSpeedForMax);
        hopForwardOffset = Mathf.Max(0f, hopForwardOffset);
        hopSquash = Mathf.Clamp(hopSquash, 0f, 0.6f);
        hopStretch = Mathf.Clamp(hopStretch, 0f, 0.6f);
        hopLeanDegrees = Mathf.Max(0f, hopLeanDegrees);
        hopExponent = Mathf.Max(0.2f, hopExponent);
        hopMinSpeed = Mathf.Max(0f, hopMinSpeed);
        movementPoseSteps = Mathf.Max(1, movementPoseSteps);
        attackPoseSteps = Mathf.Max(1, attackPoseSteps);
        recoveryPoseSteps = Mathf.Max(1, recoveryPoseSteps);

        attackWindupDistance = Mathf.Max(0f, attackWindupDistance);
        attackLungeDistance = Mathf.Max(0f, attackLungeDistance);
        attackLungeTime = Mathf.Max(0.01f, attackLungeTime);
        attackRecoverTime = Mathf.Max(0.01f, attackRecoverTime);
        attackWindupSquash = Mathf.Clamp(attackWindupSquash, 0f, 0.6f);
        attackLungeStretch = Mathf.Clamp(attackLungeStretch, 0f, 0.6f);

        standUpJumpHeight = Mathf.Max(0f, standUpJumpHeight);
        standUpJumpDuration = Mathf.Max(0.05f, standUpJumpDuration);
        standUpLeanDegrees = Mathf.Max(0f, standUpLeanDegrees);

        thrownAlignSpeed = Mathf.Max(0f, thrownAlignSpeed);
        thrownMinSpeed = Mathf.Max(0f, thrownMinSpeed);

        attackPitchRange.x = Mathf.Clamp(attackPitchRange.x, 0.1f, 3f);
        attackPitchRange.y = Mathf.Clamp(attackPitchRange.y, attackPitchRange.x, 3f);
        idlePitchRange.x = Mathf.Clamp(idlePitchRange.x, 0.1f, 3f);
        idlePitchRange.y = Mathf.Clamp(idlePitchRange.y, idlePitchRange.x, 3f);
        hitPitchRange.x = Mathf.Clamp(hitPitchRange.x, 0.1f, 3f);
        hitPitchRange.y = Mathf.Clamp(hitPitchRange.y, hitPitchRange.x, 3f);
        idleIntervalRange.x = Mathf.Max(0.1f, idleIntervalRange.x);
        idleIntervalRange.y = Mathf.Max(idleIntervalRange.x, idleIntervalRange.y);
        attackVolume = Mathf.Clamp01(attackVolume);
        idleVolume = Mathf.Clamp01(idleVolume);
        hitVolume = Mathf.Clamp01(hitVolume);
        maxSimultaneousIdleSounds = Mathf.Clamp(maxSimultaneousIdleSounds, 1, 32);
        enemyAudioSpatialBlend = Mathf.Clamp01(enemyAudioSpatialBlend);
        enemyAudioMinDistance = Mathf.Max(0.01f, enemyAudioMinDistance);
        enemyAudioMaxDistance = Mathf.Max(enemyAudioMinDistance, enemyAudioMaxDistance);

        if (Application.isPlaying && hasCachedPose)
            ApplyConfiguredSizeToModel();
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

    protected override void OnDamageTaken(float amount, GameObject source)
    {
        base.OnDamageTaken(amount, source);
        PlayHitAudio();
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

        ApplyConfiguredSizeToModel();
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
        attackScaleLocal = Vector3.one;
        recoveryOffsetLocal = Vector3.zero;
        recoveryRotationLocal = Quaternion.identity;
        recoveryScaleLocal = Vector3.one;
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

    private void ApplyConfiguredSizeToModel()
    {
        if (modelRoot == null)
            return;

        if (!hasUnscaledModelLocalScale)
        {
            unscaledModelLocalScale = modelRoot.localScale;
            hasUnscaledModelLocalScale = true;
        }

        modelRoot.localScale = unscaledModelLocalScale * SizeScale;
        modelBaseLocalScale = modelRoot.localScale;
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
        Vector3 slimeScale = Vector3.one;

        if (!thrown)
        {
            localPos += attackOffsetLocal + recoveryOffsetLocal;
            localRot = localRot * attackRotationLocal * recoveryRotationLocal;

            if (enableSlimeMotion && (!usingRootFallback || allowAnimatingRoot))
            {
                Vector3 horizontalVelocity = velocity;
                horizontalVelocity.y = 0f;
                float speed = horizontalVelocity.magnitude;
                if (speed > hopMinSpeed)
                {
                    float speedRatio = Mathf.Clamp01(speed / Mathf.Max(0.01f, hopSpeedForMax));
                    float hopPhase = Time.time * hopFrequency * Mathf.Lerp(0.7f, 1.3f, speedRatio);
                    float hop = GetPoseStep(Mathf.Abs(Mathf.Sin(hopPhase)), movementPoseSteps);
                    hop = Mathf.Pow(hop, hopExponent);
                    float contact = 1f - hop;

                    float vertical = hopHeight * speedRatio * hop;
                    float forward = hopForwardOffset * speedRatio * hop;

                    localPos += new Vector3(0f, vertical, forward);

                    Vector3 localVelocity = transform.InverseTransformDirection(horizontalVelocity.normalized);
                    float wobble = Mathf.Sin(hopPhase * 2f) * hopLeanDegrees * 0.18f * speedRatio;
                    float forwardLean = -hopLeanDegrees * Mathf.Clamp(localVelocity.z, -1f, 1f) * speedRatio * Mathf.Lerp(0.35f, 1f, hop);
                    float sideLean = hopLeanDegrees * 0.45f * Mathf.Clamp(localVelocity.x, -1f, 1f) * speedRatio;
                    localRot *= Quaternion.Euler(forwardLean + wobble, 0f, -sideLean);

                    float squash = hopSquash * contact * speedRatio;
                    float stretch = hopStretch * hop * speedRatio;
                    Vector3 hopScale = new Vector3(
                        1f + squash * 0.55f - stretch * 0.2f,
                        1f - squash + stretch,
                        1f + squash * 0.55f - stretch * 0.2f);
                    slimeScale = Vector3.Scale(slimeScale, hopScale);
                }
                else if (idleSquish > 0f && idleSquishFrequency > 0f)
                {
                    float pulse = (Mathf.Sin(Time.time * idleSquishFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
                    pulse = GetPoseStep(pulse, movementPoseSteps);
                    slimeScale = Vector3.Scale(slimeScale, MakeSquashScale(idleSquish * pulse));
                }
            }
        }

        Vector3 localScale = modelBaseLocalScale;
        localScale = Vector3.Scale(localScale, slimeScale);
        localScale = Vector3.Scale(localScale, attackScaleLocal);
        localScale = Vector3.Scale(localScale, recoveryScaleLocal);

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
            GetPoseStep(Mathf.Clamp01(thrownAlignSpeed * Time.deltaTime), movementPoseSteps)
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
        Vector3 startScale = attackScaleLocal;
        Vector3 targetOffset = Vector3.back * attackWindupDistance;
        Quaternion targetRot = Quaternion.Euler(attackWindupPitch, 0f, 0f);
        Vector3 targetScale = MakeSquashScale(attackWindupSquash);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float eased = EaseOut(t);
            eased = GetPoseStep(eased, attackPoseSteps);
            attackOffsetLocal = Vector3.Lerp(startOffset, targetOffset, eased);
            attackRotationLocal = Quaternion.Slerp(startRot, targetRot, eased);
            attackScaleLocal = Vector3.Lerp(startScale, targetScale, eased);
            yield return null;
        }

        attackOffsetLocal = targetOffset;
        attackRotationLocal = targetRot;
        attackScaleLocal = targetScale;
        attackRoutine = null;
    }

    private IEnumerator AttackStrikeRoutine()
    {
        Vector3 startOffset = attackOffsetLocal;
        Quaternion startRot = attackRotationLocal;
        Vector3 startScale = attackScaleLocal;
        Vector3 lungeOffset = Vector3.forward * attackLungeDistance;
        Quaternion lungeRot = Quaternion.Euler(-attackLungePitch, 0f, 0f);
        Vector3 lungeScale = MakeLungeScale(attackLungeStretch);

        float t = 0f;
        float lungeDuration = Mathf.Max(0.01f, attackLungeTime);
        while (t < 1f)
        {
            t += Time.deltaTime / lungeDuration;
            float eased = EaseIn(t);
            eased = GetPoseStep(eased, attackPoseSteps);
            attackOffsetLocal = Vector3.Lerp(startOffset, lungeOffset, eased);
            attackRotationLocal = Quaternion.Slerp(startRot, lungeRot, eased);
            attackScaleLocal = Vector3.Lerp(startScale, lungeScale, eased);
            yield return null;
        }

        t = 0f;
        Vector3 recoverOffset = Vector3.zero;
        Quaternion recoverRot = Quaternion.identity;
        Vector3 recoverScale = Vector3.one;
        float recoverDuration = Mathf.Max(0.01f, attackRecoverTime);
        while (t < 1f)
        {
            t += Time.deltaTime / recoverDuration;
            float eased = EaseOut(t);
            eased = GetPoseStep(eased, recoveryPoseSteps);
            attackOffsetLocal = Vector3.Lerp(lungeOffset, recoverOffset, eased);
            attackRotationLocal = Quaternion.Slerp(lungeRot, recoverRot, eased);
            attackScaleLocal = Vector3.Lerp(lungeScale, recoverScale, eased);
            yield return null;
        }

        attackOffsetLocal = recoverOffset;
        attackRotationLocal = recoverRot;
        attackScaleLocal = recoverScale;
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
            float jump = GetPoseStep(Mathf.Sin(normalized * Mathf.PI), recoveryPoseSteps);
            recoveryOffsetLocal = Vector3.up * (jump * height);
            recoveryRotationLocal = Quaternion.Euler(-jump * lean, 0f, 0f);
            recoveryScaleLocal = Vector3.Lerp(Vector3.one, MakeStretchScale(0.14f), jump);
            yield return null;
        }

        recoveryOffsetLocal = Vector3.zero;
        recoveryRotationLocal = Quaternion.identity;
        recoveryScaleLocal = Vector3.one;
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
        attackScaleLocal = Vector3.one;
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
        recoveryScaleLocal = Vector3.one;
    }

    private void UpdateIdleAudio()
    {
        if (!CanPlayIdleAudio())
            return;

        if (Time.time < nextIdleSoundAt)
            return;

        PruneActiveIdleSounds();
        if (activeIdleSoundReleaseTimes.Count >= maxSimultaneousIdleSounds)
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
            GetEnemySpatialBlend(),
            enemyAudioMinDistance,
            GetEnemyAudioMaxDistance());
        activeIdleSoundReleaseTimes.Add(Time.time + GetClipDurationAtPitch(idleSFX, pitch));

        ScheduleNextIdleSound(initialDelayOnly: false);
    }

    private bool CanPlayIdleAudio()
    {
        if (!IsAlive || idleSFX == null || IsArmed || GameState.DisableAI)
            return false;

        if (attackRoutine != null || standUpRoutine != null)
            return false;

        if (!playIdleSoundsWhileChasing)
        {
            Vector3 velocity = GetMovementVelocity();
            velocity.y = 0f;
            return velocity.sqrMagnitude <= 0.09f;
        }

        return IsChasingPlayer();
    }

    private bool IsChasingPlayer()
    {
        if (StateMachine == null)
            return false;

        EnemyAIState currentState = StateMachine.CurrentState;
        if (currentState is ChaseState chaseState)
            return chaseState.Target != null;
        if (currentState is KeepDistanceState keepDistanceState)
            return keepDistanceState.Target != null;

        return false;
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
            GetEnemySpatialBlend(),
            enemyAudioMinDistance,
            GetEnemyAudioMaxDistance());
    }

    private void PlayHitAudio()
    {
        if (hitSFX == null)
            return;

        float pitch = Random.Range(hitPitchRange.x, hitPitchRange.y);
        AudioPlaybackUtility.PlayDetachedClip(
            hitSFX,
            transform.position,
            hitVolume,
            pitch,
            GetEnemySpatialBlend(),
            enemyAudioMinDistance,
            GetEnemyAudioMaxDistance());
    }

    private float GetEnemySpatialBlend()
    {
        return Mathf.Clamp01(enemyAudioSpatialBlend);
    }

    private float GetEnemyAudioMaxDistance()
    {
        float minDistance = Mathf.Max(0.01f, enemyAudioMinDistance);
        return Mathf.Max(minDistance, enemyAudioMaxDistance, MinimumEnemyAudioMaxDistance);
    }

    private static void PruneActiveIdleSounds()
    {
        for (int i = activeIdleSoundReleaseTimes.Count - 1; i >= 0; i--)
        {
            if (Time.time >= activeIdleSoundReleaseTimes[i])
                activeIdleSoundReleaseTimes.RemoveAt(i);
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

    private float GetPoseStep(float t, int steps)
    {
        t = Mathf.Clamp01(t);
        if (!useSteppedAnimation)
            return t;

        steps = Mathf.Max(1, steps);
        return Mathf.Clamp01(Mathf.Round(t * steps) / steps);
    }

    private static Vector3 MakeSquashScale(float amount)
    {
        amount = Mathf.Clamp(amount, 0f, 0.8f);
        return new Vector3(1f + amount * 0.55f, 1f - amount, 1f + amount * 0.55f);
    }

    private static Vector3 MakeStretchScale(float amount)
    {
        amount = Mathf.Clamp(amount, 0f, 0.8f);
        return new Vector3(1f - amount * 0.35f, 1f + amount, 1f - amount * 0.35f);
    }

    private static Vector3 MakeLungeScale(float amount)
    {
        amount = Mathf.Clamp(amount, 0f, 0.8f);
        return new Vector3(1f - amount * 0.35f, 1f + amount * 0.12f, 1f + amount);
    }

    private static float EaseIn(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t;
    }
}
