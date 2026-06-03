using UnityEngine;

[AddComponentMenu("Player/Vehicle Squash Stretch")]
public class VehicleSquashStretch : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CarController carController;
    [SerializeField] private Rigidbody carRigidbody;
    [SerializeField] private Transform[] visualRoots;

    [Header("Velocity Shape")]
    [SerializeField] private float forwardStretchAtMaxSpeed = 0.1f;
    [SerializeField] private float forwardSpeedForMaxStretch = 42f;
    [SerializeField] private float accelerationStretch = 0.1f;
    [SerializeField] private float accelerationForMaxStretch = 24f;
    [SerializeField] private float brakingSquash = 0.24f;
    [SerializeField] private float brakingForMaxSquash = 30f;
    [SerializeField] private float lateralStretch = 0.14f;
    [SerializeField] private float lateralSpeedForMaxStretch = 15f;
    [SerializeField] private float upwardStretch = 0.3f;
    [SerializeField] private float downwardStretch = 0.46f;
    [SerializeField] private float verticalSpeedForMaxStretch = 13f;
    [SerializeField, Range(0f, 1f)] private float airborneHorizontalInfluence = 0.45f;
    [SerializeField, Range(0f, 1f)] private float fallingHorizontalInfluence = 0.18f;

    [Header("Action Impulses")]
    [SerializeField] private float jumpImpulseStretch = 0.34f;
    [SerializeField] private float landingImpulseSquash = 0.72f;
    [SerializeField] private float dashImpulseStretch = 0.5f;
    [SerializeField] private float boostImpulseStretch = 0.46f;
    [SerializeField] private float landingSpeedForMaxSquash = 15f;

    [Header("Wobble")]
    [SerializeField] private float wobbleSpringStrength = 185f;
    [SerializeField] private float wobbleDamping = 10.5f;
    [SerializeField] private float eventOffsetDecaySpeed = 9f;
    [SerializeField] private float eventVelocityKick = 5.5f;

    [Header("Sustained Distortion")]
    [SerializeField] private float sustainedDistortionDelay = 0.22f;
    [SerializeField] private float sustainedDistortionRelaxDuration = 0.85f;
    [SerializeField, Range(0f, 1f)] private float sustainedDistortionStrength = 0.58f;
    [SerializeField] private float distortionHoldThreshold = 0.08f;

    [Header("Limits")]
    [SerializeField] private float minScaleMultiplier = 0.46f;
    [SerializeField] private float maxScaleMultiplier = 1.74f;

    private Transform[] cachedRoots;
    private Vector3[] baseScales;
    private Vector3 currentMultiplier = Vector3.one;
    private Vector3 wobbleVelocity;
    private Vector3 eventOffset;
    private Vector3 previousLocalVelocity;
    private bool hasPreviousVelocity;
    private bool wasGrounded;
    private float previousVerticalVelocity;
    private float distortedTimer;

    void Awake()
    {
        ResolveReferences();
        CacheVisualRoots();
        wasGrounded = carController != null && carController.IsGrounded;
        previousLocalVelocity = GetLocalVelocity();
        hasPreviousVelocity = true;
    }

    void OnEnable()
    {
        ResolveReferences();
        Subscribe();
    }

    void OnDisable()
    {
        Unsubscribe();
        ResetVisualScales();
    }

    void OnValidate()
    {
        forwardStretchAtMaxSpeed = Mathf.Max(0f, forwardStretchAtMaxSpeed);
        forwardSpeedForMaxStretch = Mathf.Max(0.01f, forwardSpeedForMaxStretch);
        accelerationStretch = Mathf.Max(0f, accelerationStretch);
        accelerationForMaxStretch = Mathf.Max(0.01f, accelerationForMaxStretch);
        brakingSquash = Mathf.Max(0f, brakingSquash);
        brakingForMaxSquash = Mathf.Max(0.01f, brakingForMaxSquash);
        lateralStretch = Mathf.Max(0f, lateralStretch);
        lateralSpeedForMaxStretch = Mathf.Max(0.01f, lateralSpeedForMaxStretch);
        upwardStretch = Mathf.Max(0f, upwardStretch);
        downwardStretch = Mathf.Max(0f, downwardStretch);
        verticalSpeedForMaxStretch = Mathf.Max(0.01f, verticalSpeedForMaxStretch);
        airborneHorizontalInfluence = Mathf.Clamp01(airborneHorizontalInfluence);
        fallingHorizontalInfluence = Mathf.Clamp01(fallingHorizontalInfluence);
        jumpImpulseStretch = Mathf.Max(0f, jumpImpulseStretch);
        landingImpulseSquash = Mathf.Max(0f, landingImpulseSquash);
        dashImpulseStretch = Mathf.Max(0f, dashImpulseStretch);
        boostImpulseStretch = Mathf.Max(0f, boostImpulseStretch);
        landingSpeedForMaxSquash = Mathf.Max(0.01f, landingSpeedForMaxSquash);
        wobbleSpringStrength = Mathf.Max(0.01f, wobbleSpringStrength);
        wobbleDamping = Mathf.Max(0f, wobbleDamping);
        eventOffsetDecaySpeed = Mathf.Max(0.01f, eventOffsetDecaySpeed);
        eventVelocityKick = Mathf.Max(0f, eventVelocityKick);
        sustainedDistortionDelay = Mathf.Max(0f, sustainedDistortionDelay);
        sustainedDistortionRelaxDuration = Mathf.Max(0.01f, sustainedDistortionRelaxDuration);
        distortionHoldThreshold = Mathf.Max(0f, distortionHoldThreshold);
        minScaleMultiplier = Mathf.Clamp(minScaleMultiplier, 0.1f, 1f);
        maxScaleMultiplier = Mathf.Max(1f, maxScaleMultiplier);
    }

    void LateUpdate()
    {
        if (cachedRoots == null || cachedRoots.Length == 0)
            CacheVisualRoots();

        float deltaTime = Time.deltaTime;
        bool grounded = carController != null && carController.IsGrounded;
        Vector3 localVelocity = GetLocalVelocity();
        Vector3 localAcceleration = hasPreviousVelocity && deltaTime > 0f
            ? (localVelocity - previousLocalVelocity) / deltaTime
            : Vector3.zero;

        if (!wasGrounded && grounded)
        {
            float landingRatio = Mathf.Clamp01(Mathf.Abs(previousVerticalVelocity) / landingSpeedForMaxSquash);
            AddEventDistortion(CreateVerticalSquash(landingImpulseSquash * Mathf.Lerp(0.35f, 1f, landingRatio)));
        }

        Vector3 rawTarget = BuildVelocityTarget(localVelocity, localAcceleration, grounded);
        Vector3 targetMultiplier = ApplySustainedDistortionRelaxation(rawTarget, deltaTime);
        targetMultiplier = AddScaleOffset(targetMultiplier, eventOffset);
        targetMultiplier = ClampMultiplier(targetMultiplier);

        UpdateWobble(targetMultiplier, deltaTime);
        ApplyVisualScale(currentMultiplier);

        eventOffset = Vector3.Lerp(eventOffset, Vector3.zero, 1f - Mathf.Exp(-eventOffsetDecaySpeed * deltaTime));
        previousLocalVelocity = localVelocity;
        hasPreviousVelocity = true;
        wasGrounded = grounded;
        previousVerticalVelocity = carRigidbody != null ? carRigidbody.linearVelocity.y : 0f;
    }

    Vector3 BuildVelocityTarget(Vector3 localVelocity, Vector3 localAcceleration, bool grounded)
    {
        Vector3 target = Vector3.one;
        float verticalSpeed = carRigidbody != null ? carRigidbody.linearVelocity.y : 0f;
        float fallingRatio = !grounded && verticalSpeed < -0.1f
            ? SmoothAmount(Mathf.Clamp01(-verticalSpeed / verticalSpeedForMaxStretch))
            : 0f;
        float horizontalInfluence = grounded
            ? 1f
            : Mathf.Lerp(airborneHorizontalInfluence, fallingHorizontalInfluence, fallingRatio);

        float forwardSpeed = Mathf.Max(0f, localVelocity.z);
        float speedRatio = Mathf.Clamp01(forwardSpeed / forwardSpeedForMaxStretch);
        ApplyForwardStretch(ref target, forwardStretchAtMaxSpeed * SmoothAmount(speedRatio) * horizontalInfluence);

        float accelerationRatio = Mathf.Clamp01(localAcceleration.z / accelerationForMaxStretch);
        if (accelerationRatio > 0f)
            ApplyForwardStretch(ref target, accelerationStretch * SmoothAmount(accelerationRatio) * horizontalInfluence);

        float brakingRatio = Mathf.Clamp01(-localAcceleration.z / brakingForMaxSquash);
        if (brakingRatio > 0f)
            ApplyForwardSquash(ref target, brakingSquash * SmoothAmount(brakingRatio) * horizontalInfluence);

        float lateralRatio = Mathf.Clamp01(Mathf.Abs(localVelocity.x) / lateralSpeedForMaxStretch);
        if (lateralRatio > 0f)
            ApplySideStretch(ref target, lateralStretch * SmoothAmount(lateralRatio) * horizontalInfluence);

        if (!grounded && verticalSpeed > 0.1f)
        {
            float upwardRatio = Mathf.Clamp01(verticalSpeed / verticalSpeedForMaxStretch);
            ApplyVerticalStretch(ref target, upwardStretch * SmoothAmount(upwardRatio));
        }
        else if (!grounded && verticalSpeed < -0.1f)
        {
            float downwardRatio = Mathf.Clamp01(-verticalSpeed / verticalSpeedForMaxStretch);
            ApplyVerticalStretch(ref target, downwardStretch * SmoothAmount(downwardRatio));
        }

        return target;
    }

    Vector3 ApplySustainedDistortionRelaxation(Vector3 rawTarget, float deltaTime)
    {
        float distortion = (rawTarget - Vector3.one).magnitude;
        if (distortion > distortionHoldThreshold)
            distortedTimer += deltaTime;
        else
            distortedTimer = Mathf.MoveTowards(distortedTimer, 0f, deltaTime * 3f);

        float relaxRatio = Mathf.InverseLerp(
            sustainedDistortionDelay,
            sustainedDistortionDelay + sustainedDistortionRelaxDuration,
            distortedTimer);
        float strength = Mathf.Lerp(1f, sustainedDistortionStrength, relaxRatio);
        return Vector3.one + (rawTarget - Vector3.one) * strength;
    }

    void UpdateWobble(Vector3 targetMultiplier, float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        Vector3 displacement = targetMultiplier - currentMultiplier;
        wobbleVelocity += displacement * wobbleSpringStrength * deltaTime;
        wobbleVelocity *= Mathf.Exp(-wobbleDamping * deltaTime);
        currentMultiplier += wobbleVelocity * deltaTime;
        currentMultiplier = ClampMultiplier(currentMultiplier);
    }

    void AddEventDistortion(Vector3 eventMultiplier)
    {
        Vector3 offset = eventMultiplier - Vector3.one;
        eventOffset += offset;
        wobbleVelocity += offset * eventVelocityKick;
    }

    Vector3 GetLocalVelocity()
    {
        if (carRigidbody == null)
            return Vector3.zero;

        return transform.InverseTransformDirection(carRigidbody.linearVelocity);
    }

    void ResolveReferences()
    {
        if (carController == null)
            carController = GetComponent<CarController>();
        if (carRigidbody == null)
            carRigidbody = GetComponent<Rigidbody>();
    }

    void Subscribe()
    {
        if (carController == null)
            return;

        carController.JumpPerformed += OnJumpPerformed;
        carController.DashPerformed += OnDashPerformed;
        carController.BoostActivated += OnBoostActivated;
    }

    void Unsubscribe()
    {
        if (carController == null)
            return;

        carController.JumpPerformed -= OnJumpPerformed;
        carController.DashPerformed -= OnDashPerformed;
        carController.BoostActivated -= OnBoostActivated;
    }

    void CacheVisualRoots()
    {
        cachedRoots = visualRoots;
        if (cachedRoots == null || cachedRoots.Length == 0)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            cachedRoots = new Transform[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                cachedRoots[i] = renderers[i] != null ? renderers[i].transform : null;
        }

        baseScales = new Vector3[cachedRoots.Length];
        for (int i = 0; i < cachedRoots.Length; i++)
            baseScales[i] = cachedRoots[i] != null ? cachedRoots[i].localScale : Vector3.one;
    }

    void ApplyVisualScale(Vector3 multiplier)
    {
        if (cachedRoots == null || baseScales == null)
            return;

        for (int i = 0; i < cachedRoots.Length; i++)
        {
            Transform root = cachedRoots[i];
            if (root == null)
                continue;

            Vector3 baseScale = i < baseScales.Length ? baseScales[i] : Vector3.one;
            root.localScale = new Vector3(
                baseScale.x * multiplier.x,
                baseScale.y * multiplier.y,
                baseScale.z * multiplier.z);
        }
    }

    void ResetVisualScales()
    {
        currentMultiplier = Vector3.one;
        wobbleVelocity = Vector3.zero;
        eventOffset = Vector3.zero;
        distortedTimer = 0f;
        hasPreviousVelocity = false;

        if (cachedRoots == null || baseScales == null)
            return;

        for (int i = 0; i < cachedRoots.Length; i++)
        {
            if (cachedRoots[i] != null && i < baseScales.Length)
                cachedRoots[i].localScale = baseScales[i];
        }
    }

    void OnJumpPerformed()
    {
        float verticalSpeed = carRigidbody != null ? Mathf.Max(0f, carRigidbody.linearVelocity.y) : 0f;
        float amount = jumpImpulseStretch * Mathf.Lerp(0.72f, 1.2f, Mathf.Clamp01(verticalSpeed / verticalSpeedForMaxStretch));
        AddEventDistortion(CreateVerticalStretch(amount));
    }

    void OnDashPerformed()
    {
        float speedRatio = carController != null ? Mathf.Clamp01(carController.SpeedRatio) : 0.75f;
        AddEventDistortion(CreateForwardStretch(dashImpulseStretch * Mathf.Lerp(0.75f, 1.25f, speedRatio)));
    }

    void OnBoostActivated()
    {
        float speedRatio = carController != null ? Mathf.Clamp01(carController.SpeedRatio) : 0.65f;
        AddEventDistortion(CreateForwardStretch(boostImpulseStretch * Mathf.Lerp(0.65f, 1.15f, speedRatio)));
    }

    static float SmoothAmount(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    static Vector3 AddScaleOffset(Vector3 multiplier, Vector3 offset)
    {
        return new Vector3(
            multiplier.x + offset.x,
            multiplier.y + offset.y,
            multiplier.z + offset.z);
    }

    static Vector3 CreateVerticalStretch(float amount)
    {
        return new Vector3(
            1f - amount * 0.52f,
            1f + amount,
            1f - amount * 0.36f);
    }

    static Vector3 CreateVerticalSquash(float amount)
    {
        return new Vector3(
            1f + amount * 0.74f,
            1f - amount,
            1f + amount * 0.56f);
    }

    static Vector3 CreateForwardStretch(float amount)
    {
        return new Vector3(
            1f - amount * 0.42f,
            1f - amount * 0.26f,
            1f + amount);
    }

    static void ApplyVerticalStretch(ref Vector3 multiplier, float amount)
    {
        multiplier.x *= 1f - amount * 0.48f;
        multiplier.y *= 1f + amount;
        multiplier.z *= 1f - amount * 0.34f;
    }

    static void ApplyForwardStretch(ref Vector3 multiplier, float amount)
    {
        multiplier.x *= 1f - amount * 0.36f;
        multiplier.y *= 1f - amount * 0.22f;
        multiplier.z *= 1f + amount;
    }

    static void ApplyForwardSquash(ref Vector3 multiplier, float amount)
    {
        multiplier.x *= 1f + amount * 0.42f;
        multiplier.y *= 1f + amount * 0.2f;
        multiplier.z *= 1f - amount;
    }

    static void ApplySideStretch(ref Vector3 multiplier, float amount)
    {
        multiplier.x *= 1f + amount;
        multiplier.y *= 1f - amount * 0.22f;
        multiplier.z *= 1f - amount * 0.28f;
    }

    Vector3 ClampMultiplier(Vector3 multiplier)
    {
        return new Vector3(
            Mathf.Clamp(multiplier.x, minScaleMultiplier, maxScaleMultiplier),
            Mathf.Clamp(multiplier.y, minScaleMultiplier, maxScaleMultiplier),
            Mathf.Clamp(multiplier.z, minScaleMultiplier, maxScaleMultiplier));
    }
}
