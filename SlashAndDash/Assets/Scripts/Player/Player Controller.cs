// Full CarController with vehicle-dimension + mass driven auto-calculation
using Action = System.Action;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class CarController : MonoBehaviour, IDamageable
{
    private const float EffectiveRamMinSpeed = 12f;

    [Header("Movement")]
    [SerializeField] private float accelerationForce = 1300f;
    [SerializeField] private float maxSpeed = 22f;
    [SerializeField] private float turnSpeed = 120f;

    [Header("Boost")]
    [SerializeField, HideInInspector] private float driftBoostAmount = 12f; // dynamic on drift end
    [SerializeField] private float driftBoostDuration = 1.2f;
    [SerializeField] private int maxBoostStacks = 3;
    [SerializeField] private float boostSpeedPerStack = 10f;
    [SerializeField, HideInInspector] private float minBoostStackDuration = 0.2f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 9f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Arcade Stability")]
    [SerializeField, HideInInspector] private Vector3 centerOfMassOffset = new Vector3(0f, -0.7f, -0.4f);
    [SerializeField, HideInInspector] private float groundAngularDamping = 10f;
    [SerializeField, HideInInspector] private float airAngularDamping = 3f;
    [SerializeField, HideInInspector] private float rampPitchDamping = 0.35f;
    [SerializeField, HideInInspector] private float groundedDownforce = 35f;

    [Header("Arcade Gravity")]
    [SerializeField, HideInInspector] private float airGravityMultiplier = 1.6f;
    [SerializeField, HideInInspector] private float fallGravityMultiplier = 2.2f;

    [Header("Arcade Drift")]
    [SerializeField, HideInInspector] private float driftSideForce = 2200f;
    [SerializeField, HideInInspector] private float driftGripRecovery = 3.5f;
    [SerializeField] private float minDriftSpeed = 5f;
    [SerializeField] private float driftSteerMultiplier = 1.45f;
    [Tooltip("Extra side-force multiplier applied at lower speeds so drift is still noticeable.")]
    [SerializeField, HideInInspector] private float lowSpeedDriftSideForceMultiplier = 1.6f;
    [Tooltip("Minimum yaw factor while drifting so low-speed drift rotation remains visible.")]
    [SerializeField, HideInInspector] private float minDriftYawFactor = 0.35f;
    [Tooltip("Overall lateral tire grip multiplier while drifting (<1 = more slide).")]
    [SerializeField, HideInInspector] private float driftLateralGripMultiplier = 0.6f;
    [Tooltip("Front wheel lateral grip multiplier while drifting.")]
    [SerializeField, HideInInspector] private float driftFrontGripMultiplier = 0.8f;
    [Tooltip("Rear wheel lateral grip multiplier while drifting.")]
    [SerializeField, HideInInspector] private float driftRearGripMultiplier = 0.5f;
    [Tooltip("Extra yaw torque multiplier while drifting to take larger turns.")]
    [SerializeField, HideInInspector] private float driftYawTorqueMultiplier = 1.35f;

    [Tooltip("Seconds of continuous drifting required before drift boost can trigger.")]
    [SerializeField] private float driftChargeTime = 1f;
    [SerializeField] private float maxDriftBoost = 12f;

    [Header("Drift Pivoting (front vs rear balance)")]
    [SerializeField, HideInInspector] private float frontPivotDistance = 3.0f;
    [SerializeField, HideInInspector] private float rearPivotDistance = 2.0f;
    [SerializeField, HideInInspector] private float driftYawTorque = 80f;
    [SerializeField, HideInInspector] private float lateralSlipThreshold = 2.0f;

    private float driftCharge = 0f;
    private float driftTimer = 0f;

    [Header("Runtime Particles")]
    [SerializeField] private bool enableRuntimeParticles = true;
    [SerializeField] private Vector3 particleAnchorOffset = new Vector3(0f, 0.2f, -1.35f);
    [SerializeField] private float drivingParticleMinSpeed = 2f;
    [SerializeField] private float drivingParticleMaxRate = 34f;
    [SerializeField] private float driftSparkleMaxRate = 24f;
    [SerializeField] private Color drivingDustColor = new Color(0.78f, 0.73f, 0.64f, 0.62f);
    [SerializeField] private Color boostDustColor = new Color(1f, 0.83f, 0.3f, 0.72f);

    [Header("Air Dash")]
    [SerializeField] private float airDashForce = 28f;
    [SerializeField, HideInInspector] private float airDashUpForce = 4f;
    [SerializeField, HideInInspector] private float airDashCooldown = 0.15f;
    [Tooltip("How much existing forward velocity is carried into the air dash.")]
    [SerializeField, HideInInspector] private float airDashForwardCarry = 0.35f;

    [Header("Fake Wheels / Suspension")]
    [Tooltip("Wheel contact transforms (any order). Place near contact point bottom of each corner).")]
    [SerializeField] private Transform[] wheelTransforms;
    [Tooltip("Distance above wheel used as the ray origin; ray length = suspensionDistance * 2")]
    [SerializeField] private float suspensionDistance = 0.5f;
    [Tooltip("Spring stiffness. Large numbers ok — we're using ForceMode.Acceleration.")]
    [SerializeField, HideInInspector] private float suspensionStiffness = 20000f;
    [Tooltip("Suspension damper; reduces oscillation.")]
    [SerializeField, HideInInspector] private float suspensionDamping = 500f;
    [Tooltip("Clamps per-wheel suspension force.")]
    [SerializeField, HideInInspector] private float suspensionMaxForcePerWheel = 20000f;

    [Header("Tire Grip (speed-dependent)")]
    [Tooltip("Base lateral grip per wheel (higher = less sliding).")]
    [SerializeField, HideInInspector] private float tireGrip = 60f;
    [Tooltip("Curve that maps speed ratio (0..1) to grip multiplier. X= speed ratio, Y = multiplier.")]
    [SerializeField, HideInInspector] private AnimationCurve gripCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.4f);
    [Tooltip("Front/rear multipliers applied to base tireGrip (1 = same as base).")]
    [SerializeField, HideInInspector] private float frontTireGrip = 1f;
    [SerializeField, HideInInspector] private float rearTireGrip = 1f;
    [Tooltip("Grip falls off additionally with forward speed multiplier (legacy multiplier kept for quick tuning).")]
    [SerializeField, HideInInspector] private float tireGripSpeedFalloff = 0.6f;

    [Header("Rolling Resistance / Braking (front/rear)")]
    [Tooltip("Longitudinal rolling resistance (front wheels).")]
    [SerializeField, HideInInspector] private float frontRollingResistance = 18f;
    [Tooltip("Longitudinal rolling resistance (rear wheels).")]
    [SerializeField, HideInInspector] private float rearRollingResistance = 18f;
    [Tooltip("Coast drag applied per-wheel when player is not giving throttle (front).")]
    [SerializeField, HideInInspector] private float frontCoastDrag = 20f;
    [Tooltip("Coast drag applied per-wheel when player is not giving throttle (rear).")]
    [SerializeField, HideInInspector] private float rearCoastDrag = 20f;
    [Tooltip("Braking force applied per-wheel when the player pulls negative throttle (brake/reverse).")]
    [SerializeField, HideInInspector] private float brakeForce = 80f;
    [Tooltip("Multiplier applied to rear rolling resistance while drifting; <1 reduces rear resistance to help maintain slide.")]
    [SerializeField, HideInInspector] private float driftRearRollingResistanceMultiplier = 0.45f;

    [Header("Hill / Slope")]
    [Tooltip("How much extra acceleration you get when going downhill (units of ForceMode.Acceleration). Set to 0 to disable.")]
    [SerializeField, HideInInspector] private float downhillAcceleration = 12f;

    [Header("Slope Limits")]
    [Tooltip("Maximum slope angle (deg) car can drive up. Set >= 45 for 45° ramps to be climbable.")]
    [SerializeField] private float maxDriveSlopeAngle = 60f;
    [SerializeField, HideInInspector] private float steepSlopeSlideForce = 25f;

    [Header("Leave Ground")]
    [SerializeField, HideInInspector] private float leaveGroundForwardBoost = 0.25f;

    [Header("Slope Sampling")]
    [Tooltip("How far forwards/back from the car center to sample the ground for slope calculation (meters).")]
    [SerializeField, HideInInspector] private float slopeSampleDistance = 1.0f;
    [Tooltip("Ignore very small slopes (degrees) so tiny bumps don't count.")]
    [SerializeField, HideInInspector] private float minSlopeAngleToAffect = 1f;

#if false // Auto-steer temporarily disabled for build stabilization.
    [Header("Auto Steer Assist")]
    [Tooltip("Automatically nudges steering away from upcoming non-drivable surfaces.")]
    [SerializeField] private bool autoSteerEnabled = true;
    [Tooltip("Assist starts ramping in above this forward speed.")]
    [SerializeField] private float autoSteerMinSpeed = 10f;
    [Tooltip("Assist reaches full strength at this forward speed.")]
    [SerializeField] private float autoSteerFullSpeed = 28f;
    [Tooltip("Forward probe distance at minimum assist speed.")]
    [SerializeField] private float autoSteerMinLookAhead = 2.5f;
    [Tooltip("Forward probe distance at full assist speed.")]
    [SerializeField] private float autoSteerMaxLookAhead = 8f;
    [Tooltip("Radius for each forward auto-steer probe.")]
    [SerializeField] private float autoSteerProbeRadius = 0.45f;
    [Tooltip("Maximum steering input that auto-steer can add.")]
    [SerializeField, Range(0f, 1f)] private float autoSteerMaxInput = 0.55f;
    [Tooltip("How quickly auto-steer input can change.")]
    [SerializeField] private float autoSteerResponse = 6f;
    [Tooltip("How many FixedUpdate frames ahead to predict when checking if the car will hit a wall.")]
    [SerializeField, Min(1)] private int autoSteerPredictionFrames = 8;
    [Tooltip("If player steer magnitude is above this, auto-steer is disabled.")]
    [SerializeField, Range(0f, 1f)] private float autoSteerPlayerTurnDeadzone = 0.08f;
    [Tooltip("Extra climbable slope angle (deg) considered driveable at high speed for auto-steer wall checks.")]
    [SerializeField, Range(0f, 30f)] private float autoSteerSpeedSlopeBonus = 12f;
    [Tooltip("Layers considered for auto-steer obstacle probes.")]
    [SerializeField] private LayerMask autoSteerLayerMask = ~0;
    [Tooltip("Draw auto-steer prediction and wall-avoidance gizmos while selected.")]
    [SerializeField] private bool showAutoSteerGizmos = true;
#endif

    [Header("Debug")]
    [SerializeField, HideInInspector] private bool showSuspensionRays = true;
    [SerializeField, HideInInspector] private bool showSurfaceNormals = true;
    [SerializeField, HideInInspector] private Color driveableColor = Color.green;
    [SerializeField, HideInInspector] private Color steepColor = Color.red;
    [SerializeField, HideInInspector] private float debugSphereSize = 0.08f;
    [SerializeField, HideInInspector] private bool alwaysBoostDebug = false;
    [SerializeField, HideInInspector] private float noClipMoveSpeed = 22f;
    [SerializeField, HideInInspector] private float noClipVerticalSpeed = 14f;

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool enablePassiveRegen = true;
    [SerializeField] private float regenDelaySeconds = 4f;
    [SerializeField] private float regenPerSecond = 8f;
    [SerializeField, HideInInspector] private bool disableControllerOnDeath = true;
    [SerializeField, HideInInspector] private bool logDamageEvents;

    [Header("Fall Recovery")]
    [SerializeField] private float freeFallRecoveryDelaySeconds = 1f;
    [SerializeField] private float freeFallRecoveryDamage = 10f;

    [Header("Airtime Trick")]
    [SerializeField] private bool enableAirtimeTrick = true;
    [SerializeField] private float trickMinAirTime = 0.45f;
    [SerializeField] private float trickLandingGraceSeconds = 0.25f;
    [SerializeField] private float trickBoostReward = 1f;
    [SerializeField] private float trickCooldownSeconds = 1f;

    [Header("Enemy Collision Impact")]
    [SerializeField] private float minCollisionImpactSpeed = 12f;
    [SerializeField] private float maxCollisionDamage = 10f;
    [SerializeField, HideInInspector] private float maxCollisionPushStrength = 14f;
    [SerializeField, HideInInspector] private float collisionPushCurvePeakSpeed = 30f;
    [SerializeField, HideInInspector] private AnimationCurve collisionPushBySpeed = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.35f, 0.12f),
        new Keyframe(0.65f, 0.78f),
        new Keyframe(1f, 1f));
    [SerializeField, HideInInspector] private float collisionSidePushWeight = 1.35f;
    [SerializeField, HideInInspector] private float collisionForwardPushWeight = 0.2f;
    [SerializeField, HideInInspector] private float collisionRandomYawDegrees = 18f;
    [SerializeField, HideInInspector] private float boostCollisionPushMultiplier = 2.6f;
    [SerializeField, HideInInspector] private float boostCollisionMinPushStrength = 24f;
    [SerializeField, HideInInspector] private float collisionImpactCooldown = 0.2f;
    [Header("Boost Wall Bounce")]
    [SerializeField] private float boostWallBounceSpeedScale = 0.35f;
    [SerializeField] private float boostWallBounceMinSpeed = 6f;
    [SerializeField] private float boostWallBounceCooldown = 0.12f;
    [Header("Ram Hitbox")]
    [SerializeField] private bool autoCreateRamHitbox = true;
    [SerializeField] private BoxCollider ramHitbox;
    [SerializeField] private Vector3 ramHitboxCenter = new Vector3(0f, 0.8f, 1.15f);
    [SerializeField] private Vector3 ramHitboxBaseSize = new Vector3(1.4f, 1.15f, 2.1f);
    [SerializeField] private float ramHitboxMaxWidth = 3.5f;
    [SerializeField, HideInInspector] private AnimationCurve ramHitboxWidthBySpeed = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private float ramHitboxMaxForwardExtension = 2.5f;
    [SerializeField, HideInInspector] private AnimationCurve ramHitboxForwardBySpeed = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField, HideInInspector] private float ramHitboxForwardExtensionPerExtraSpeed = 0.12f;
    [SerializeField, HideInInspector] private float nonBoostRamForwardSpeedLossPercent = 0.015f;
    [SerializeField, HideInInspector] private float nonBoostRamForwardSpeedLossMax = 0.75f;

    // ---------------- Vehicle physical parameters (user inputs) ----------------
    [Header("Vehicle Dimensions & Mass")]
    [Tooltip("Vehicle mass in kilograms.")]
    [SerializeField] private float vehicleMass = 1200f;
    [Tooltip("Distance between front and rear axle (meters).")]
    [SerializeField] private float wheelBase = 2.6f;
    [Tooltip("Vehicle width (track) in meters.")]
    [SerializeField] private float trackWidth = 1.6f;
    [Tooltip("Height of center of mass above the ground in meters (positive).")]
    [SerializeField] private float comHeight = 0.5f;
    [Range(0.0f, 1.0f), Tooltip("Fraction of weight on front axle (0..1). 0.5 = even split.")]
    [SerializeField] private float frontWeightRatio = 0.5f;

    [Tooltip("When true the script will compute sensible defaults (suspension, grip, rolling resistance, brakes) from the vehicle mass/dimensions.")]
    [SerializeField] private bool autoCalculatePhysics = true;

    [Header("Auto-Calc Scale Knobs")]
    [Tooltip("Multiplier applied to computed suspension stiffness (use to bias soft/stiff feel).")]
    [SerializeField] private float suspensionStiffnessScale = 3.0f;
    [Tooltip("Multiplier applied to computed suspension damping.")]
    [SerializeField] private float suspensionDampingScale = 1.0f;
    [Tooltip("Global scale on computed tire grip.")]
    [SerializeField] private float gripScale = 1.0f;
    [Tooltip("Global scale on rolling/brake numbers.")]
    [SerializeField] private float rollingResistanceScale = 1.0f;

    // internals
    private Rigidbody rb;
    private Collider col;
    private PlayerInputActions controls;
    private Vector2 moveInput;

    private bool isGrounded;
    private bool wasGrounded;
    private bool isDrifting;
    private bool boostActive;
    private int currentBoostStacks;
    private float boostTimer;

    private bool airDashUsed;
    private float airDashTimer;

    private RaycastHit groundHit;
    private float slopeAngle;

    // slope sample debug
    private float lastSampledSlopeAngle = 0f;
    private Vector3 lastFrontSample = Vector3.zero;
    private Vector3 lastBackSample = Vector3.zero;
    private bool lastSampleHadHits = false;
    private readonly Dictionary<int, float> recentEnemyImpactTimeById = new Dictionary<int, float>();
    private float currentHealth;
    private bool isDead;
    private bool noClipActive;
    private bool noClipAscendInput;
    private bool noClipDescendInput;
#if false // Auto-steer temporarily disabled for build stabilization.
    private float autoSteerInput;
    private int autoSteerPerpendicularWallId;
    private float autoSteerPerpendicularTurnSign = 1f;
    private readonly RaycastHit[] autoSteerProbeHits = new RaycastHit[16];
    private bool autoSteerDebugActive;
    private bool autoSteerDebugSuppressedByPlayer;
    private bool autoSteerDebugSuppressedBySpeed;
    private Vector3 autoSteerDebugOrigin;
    private Vector3 autoSteerDebugDirection;
    private float autoSteerDebugLookAhead;
    private bool autoSteerDebugHasWallHit;
    private Vector3 autoSteerDebugWallPoint;
    private Vector3 autoSteerDebugWallNormal;
    private float autoSteerDebugDanger;
    private float autoSteerDebugSteerSign;
#endif
    private float regenPausedUntil;
    private bool isRegenerating;
    private float freeFallStartedAt = -1f;
    private Vector3 lastGroundedPosition;
    private Quaternion lastGroundedRotation = Quaternion.identity;
    private bool hasLastGroundedPose;
    private bool trickInAir;
    private bool trickCandidateReady;
    private float trickAirTimer;
    private float trickCooldownTimer;
    private bool lastTrickSucceeded;
    private float lastTrickSucceededAt = -999f;
    private bool hasPerformedTrickThisLife;
    private int ramImpactCount;
    private Transform particleAnchor;
    private ParticleSystem drivingDustParticles;
    private ParticleSystem boostDustParticles;
    private ParticleSystem driftSparkleParticles;
    private float lastBoostWallBounceTime = -999f;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsAlive => !isDead;
    public bool IsRegenerating => isRegenerating;
    public bool TrickReady => enableAirtimeTrick && !trickInAir && trickCooldownTimer <= 0f;
    public bool TrickInAir => trickInAir;
    public float TrickAirTimeRatio => Mathf.Clamp01(trickAirTimer / Mathf.Max(0.01f, trickMinAirTime));
    public bool TrickOnCooldown => trickCooldownTimer > 0f;
    public bool LastTrickSucceeded => lastTrickSucceeded;
    public float LastTrickSucceededAt => lastTrickSucceededAt;
    public bool HasPerformedTrickThisLife => hasPerformedTrickThisLife;
    public bool IsGrounded => isGrounded;
    public bool IsDrifting => isDrifting;
    public Vector2 MoveInput => moveInput;
    public int CurrentBoostStacks => Mathf.Max(0, currentBoostStacks);
    public int BoostStackCap => GetBoostStackCap();
    public int RamImpactCount => ramImpactCount;
    public bool ShowSuspensionRays { get => showSuspensionRays; set => showSuspensionRays = value; }
    public bool ShowSurfaceNormals { get => showSurfaceNormals; set => showSurfaceNormals = value; }
    public bool AlwaysBoostDebug { get => alwaysBoostDebug; set => alwaysBoostDebug = value; }
    public float CurrentSpeed => rb != null ? rb.linearVelocity.magnitude : 0f;
    public float SpeedRatio => Mathf.Clamp01(CurrentSpeed / Mathf.Max(0.01f, maxSpeed));
    public float RemainingBoostRatio => boostActive
        ? Mathf.Clamp01(boostTimer / Mathf.Max(0.01f, GetConfiguredBoostStackDuration()))
        : 0f;

    public event Action JumpPerformed;
    public event Action DashPerformed;
    public event Action DriftStarted;
    public event Action DriftEnded;
    public event Action BoostActivated;
    public event Action BoostStackGained;
    public event Action EnemyRamImpact;
    public event Action TrickLandedSuccessfully;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        // apply computed values if desired
        ComputeDerivedStats();

        // apply mass and center of mass (ComputeDerivedStats already sets rb.mass & rb.centerOfMass)
        rb.useGravity = true;
        rb.linearDamping = 0.5f;
        rb.angularDamping = groundAngularDamping;
        rb.maxAngularVelocity = 100f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        controls = new PlayerInputActions();
        controls.Player.Move.performed += c => moveInput = c.ReadValue<Vector2>();
        controls.Player.Move.canceled += _ => moveInput = Vector2.zero;
        controls.Player.Jump.started += _ => noClipAscendInput = true;
        controls.Player.Jump.canceled += _ => noClipAscendInput = false;
        controls.Player.Jump.performed += _ => OnJumpPerformed();
        controls.Player.Dash.started += _ => noClipDescendInput = true;
        controls.Player.Dash.canceled += _ => noClipDescendInput = false;
        controls.Player.Dash.performed += _ => OnDashPerformed();

        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = maxHealth;
        CacheGroundedPose();
        SetupRamHitbox();
        UpdateRamHitboxSize();
        SetupRuntimeParticles();
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = Application.isPlaying ? Mathf.Clamp(currentHealth, 0f, maxHealth) : maxHealth;
        collisionPushCurvePeakSpeed = Mathf.Max(0.01f, collisionPushCurvePeakSpeed);
        collisionSidePushWeight = Mathf.Max(0f, collisionSidePushWeight);
        collisionForwardPushWeight = Mathf.Max(0f, collisionForwardPushWeight);
        collisionRandomYawDegrees = Mathf.Clamp(collisionRandomYawDegrees, 0f, 90f);
        boostCollisionPushMultiplier = Mathf.Max(1f, boostCollisionPushMultiplier);
        boostCollisionMinPushStrength = Mathf.Max(0f, boostCollisionMinPushStrength);
        maxBoostStacks = maxBoostStacks <= 1 ? 3 : Mathf.Clamp(maxBoostStacks, 1, 3);
        boostSpeedPerStack = Mathf.Max(0f, boostSpeedPerStack);
        minBoostStackDuration = Mathf.Clamp(minBoostStackDuration, 0.01f, 5f);
        driftBoostDuration = Mathf.Max(minBoostStackDuration, driftBoostDuration);
        ramHitboxBaseSize.x = Mathf.Max(0.05f, ramHitboxBaseSize.x);
        ramHitboxBaseSize.y = Mathf.Max(0.05f, ramHitboxBaseSize.y);
        ramHitboxBaseSize.z = Mathf.Max(0.05f, ramHitboxBaseSize.z);
        ramHitboxMaxWidth = Mathf.Max(ramHitboxBaseSize.x, ramHitboxMaxWidth);
        ramHitboxMaxForwardExtension = Mathf.Max(0f, ramHitboxMaxForwardExtension);
        ramHitboxForwardExtensionPerExtraSpeed = Mathf.Max(0f, ramHitboxForwardExtensionPerExtraSpeed);
        nonBoostRamForwardSpeedLossPercent = Mathf.Clamp01(nonBoostRamForwardSpeedLossPercent);
        nonBoostRamForwardSpeedLossMax = Mathf.Max(0f, nonBoostRamForwardSpeedLossMax);
#if false // Auto-steer temporarily disabled for build stabilization.
        autoSteerMinSpeed = Mathf.Max(0f, autoSteerMinSpeed);
        autoSteerFullSpeed = Mathf.Max(autoSteerMinSpeed + 0.01f, autoSteerFullSpeed);
        autoSteerMinLookAhead = Mathf.Max(0.5f, autoSteerMinLookAhead);
        autoSteerMaxLookAhead = Mathf.Max(autoSteerMinLookAhead, autoSteerMaxLookAhead);
        autoSteerProbeRadius = Mathf.Max(0.05f, autoSteerProbeRadius);
        autoSteerMaxInput = Mathf.Clamp01(autoSteerMaxInput);
        autoSteerResponse = Mathf.Max(0.1f, autoSteerResponse);
        autoSteerPredictionFrames = Mathf.Max(1, autoSteerPredictionFrames);
        autoSteerPlayerTurnDeadzone = Mathf.Clamp01(autoSteerPlayerTurnDeadzone);
        autoSteerSpeedSlopeBonus = Mathf.Clamp(autoSteerSpeedSlopeBonus, 0f, 30f);
#endif
        regenDelaySeconds = Mathf.Max(0f, regenDelaySeconds);
        regenPerSecond = Mathf.Max(0f, regenPerSecond);
        trickMinAirTime = Mathf.Max(0.01f, trickMinAirTime);
        trickLandingGraceSeconds = Mathf.Max(0.01f, trickLandingGraceSeconds);
        trickBoostReward = Mathf.Max(0f, trickBoostReward);
        trickCooldownSeconds = Mathf.Max(0f, trickCooldownSeconds);
        drivingParticleMinSpeed = Mathf.Max(0f, drivingParticleMinSpeed);
        drivingParticleMaxRate = Mathf.Max(1f, drivingParticleMaxRate);
        driftSparkleMaxRate = Mathf.Max(1f, driftSparkleMaxRate);

        // keep editor responsive: compute derived stats when inspector is edited
        ComputeDerivedStats();
        UpdateRamHitboxSize();
    }

    private void OnEnable() => controls.Player.Enable();

    private void OnDisable()
    {
        if (noClipActive)
            SetNoClipActive(false);

        noClipAscendInput = false;
        noClipDescendInput = false;
        freeFallStartedAt = -1f;
        currentBoostStacks = 0;
        boostTimer = 0f;
        boostActive = false;
#if false // Auto-steer temporarily disabled for build stabilization.
        autoSteerInput = 0f;
        autoSteerPerpendicularWallId = 0;
        ClearAutoSteerDebug();
#endif
        StopRuntimeParticles();
        controls.Player.Disable();
    }

    public bool TakeDamage(float amount, GameObject source = null)
    {
        if (isDead || amount <= 0f || GameState.GodMode)
            return false;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        regenPausedUntil = Time.time + regenDelaySeconds;
        isRegenerating = false;

        if (logDamageEvents)
            Debug.Log(name + " took " + amount + " damage. HP: " + currentHealth + "/" + maxHealth, this);

        if (currentHealth <= 0f)
            Die();

        return true;
    }

    void Die()
    {
        if (isDead)
            return;

        isDead = true;
        GameState.SetGameOver();
        Debug.Log(name + " died.", this);

        if (disableControllerOnDeath)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
#if false // Auto-steer temporarily disabled for build stabilization.
            autoSteerInput = 0f;
            autoSteerPerpendicularWallId = 0;
            ClearAutoSteerDebug();
#endif
            enabled = false;
        }
    }

    private bool CanControl() => isGrounded;

    private void FixedUpdate()
    {
        if (GameState.NoClip != noClipActive)
            SetNoClipActive(GameState.NoClip);

        HandlePassiveRegen();

        if (noClipActive)
        {
            freeFallStartedAt = -1f;
            HandleNoClipMovement();
            return;
        }

        wasGrounded = isGrounded;
        isGrounded = CheckGrounded();
        slopeAngle = isGrounded ? Vector3.Angle(groundHit.normal, Vector3.up) : 0f;
        HandleAirtimeTrick();
        HandleFreeFallRecovery();

        rb.angularDamping = isGrounded ? groundAngularDamping : airAngularDamping;

        if (isGrounded) airDashUsed = false;

        if (wasGrounded && !isGrounded)
            rb.AddForce(transform.forward * leaveGroundForwardBoost, ForceMode.VelocityChange);

        if (CanControl())
        {
            HandleSteering();
            HandleAcceleration();
            HandleDrift();
        }

        HandleBoost();
        if (alwaysBoostDebug)
        {
            while (currentBoostStacks < GetBoostStackCap())
                AddBoostStack();
        }
        UpdateRamHitboxSize();
        UpdateRuntimeParticles();
        ClampSpeed();

        if (isGrounded)
        {
            ApplyArcadeDownforce();
            ApplySuspensionAndTireForces();
            ApplySlopeForces(); // downhill assist only
            ApplySteepSlopeSlide();
            DampRampPitch();
        }
        else
        {
            ApplyArcadeGravity();
            AlignUprightInAir();
        }

        MaintainBoostSpeedAtCap();
    }

    private void HandleFreeFallRecovery()
    {
        if (isDead || GameState.IsGameOver)
            return;

        if (isGrounded)
        {
            freeFallStartedAt = -1f;
            CacheGroundedPose();
            return;
        }

        if (freeFallStartedAt < 0f)
            freeFallStartedAt = Time.time;

        float requiredDelay = Mathf.Max(0f, freeFallRecoveryDelaySeconds);
        if (Time.time - freeFallStartedAt < requiredDelay)
            return;

        TriggerFreeFallRecovery();
    }

    private void TriggerFreeFallRecovery()
    {
        freeFallStartedAt = -1f;

        if (!hasLastGroundedPose)
            return;

        // Recovery is a fail-safe reposition, not a valid trick landing.
        trickInAir = false;
        trickCandidateReady = false;
        trickAirTimer = 0f;
        lastTrickSucceeded = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = lastGroundedPosition;
            rb.rotation = lastGroundedRotation;
        }
        else
        {
            transform.SetPositionAndRotation(lastGroundedPosition, lastGroundedRotation);
        }

        if (freeFallRecoveryDamage > 0f)
            TakeDamage(freeFallRecoveryDamage);
    }

    private void CacheGroundedPose()
    {
        lastGroundedPosition = transform.position;
        lastGroundedRotation = transform.rotation;
        hasLastGroundedPose = true;
    }

    private void OnJumpPerformed()
    {
        if (!noClipActive && Jump())
            JumpPerformed?.Invoke();
    }

    private void OnDashPerformed()
    {
        if (!noClipActive)
        {
            TryAirDash();
            DashPerformed?.Invoke();
        }
    }

    public bool RestoreHealth(float amount, bool allowOverheal = false)
    {
        if (isDead || amount <= 0f)
            return false;

        float clampedMax = allowOverheal ? maxHealth + amount : maxHealth;
        float target = Mathf.Min(currentHealth + amount, clampedMax);
        if (target <= currentHealth)
            return false;

        currentHealth = target;
        return true;
    }

    private void HandlePassiveRegen()
    {
        isRegenerating = false;
        if (!enablePassiveRegen || isDead || GameState.IsGameOver)
            return;
        if (currentHealth >= maxHealth)
            return;
        if (Time.time < regenPausedUntil)
            return;

        float healAmount = regenPerSecond * Time.fixedDeltaTime;
        if (healAmount <= 0f)
            return;

        isRegenerating = RestoreHealth(healAmount);
    }

    private void HandleAirtimeTrick()
    {
        if (lastTrickSucceeded && Time.time - lastTrickSucceededAt > trickLandingGraceSeconds)
            lastTrickSucceeded = false;

        if (!enableAirtimeTrick || isDead || GameState.IsGameOver)
        {
            trickInAir = false;
            trickCandidateReady = false;
            trickAirTimer = 0f;
            return;
        }

        trickCooldownTimer = Mathf.Max(0f, trickCooldownTimer - Time.fixedDeltaTime);

        if (!isGrounded)
        {
            if (!trickInAir)
            {
                trickInAir = true;
                trickCandidateReady = false;
                trickAirTimer = 0f;
                return;
            }

            trickAirTimer += Time.fixedDeltaTime;
            trickCandidateReady = trickAirTimer >= trickMinAirTime;
            return;
        }

        if (!trickInAir)
            return;

        bool success = trickCandidateReady && trickCooldownTimer <= 0f;
        if (success)
        {
            ApplyTrickBoostReward();
            trickCooldownTimer = trickCooldownSeconds;
            hasPerformedTrickThisLife = true;
            lastTrickSucceeded = true;
            lastTrickSucceededAt = Time.time;
            TrickLandedSuccessfully?.Invoke();
        }
        else
        {
            lastTrickSucceeded = false;
        }

        trickInAir = false;
        trickCandidateReady = false;
        trickAirTimer = 0f;
    }

    private void ApplyTrickBoostReward()
    {
        if (trickBoostReward <= 0f)
            return;

        // Airtime reward should match a single fresh drift boost (no stacking).
        ApplyBaseDriftBoost();
    }

    private void ApplyBaseDriftBoost()
    {
        driftBoostAmount = boostSpeedPerStack;

        bool wasBoostActive = boostActive && currentBoostStacks > 0 && boostTimer > 0f;
        int previousBoostStacks = currentBoostStacks;
        currentBoostStacks = 1;
        boostTimer = GetConfiguredBoostStackDuration();
        boostActive = true;

        if (currentBoostStacks != previousBoostStacks)
            BoostStackGained?.Invoke();

        if (!wasBoostActive)
            BoostActivated?.Invoke();
    }

    private void SetNoClipActive(bool active)
    {
        noClipActive = active;
        noClipAscendInput = false;
        noClipDescendInput = false;
#if false // Auto-steer temporarily disabled for build stabilization.
        autoSteerInput = 0f;
        autoSteerPerpendicularWallId = 0;
        ClearAutoSteerDebug();
#endif

        if (rb == null)
            return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = active;
        rb.useGravity = !active;

        if (col != null)
            col.enabled = !active;
        if (ramHitbox != null)
            ramHitbox.enabled = !active;
    }

    private void HandleNoClipMovement()
    {
        Vector3 forward = transform.forward * moveInput.y;
        Vector3 right = transform.right * moveInput.x;
        Vector3 planarMove = forward + right;
        if (planarMove.sqrMagnitude > 1f)
            planarMove.Normalize();

        float verticalInput = 0f;
        if (noClipAscendInput)
            verticalInput += 1f;
        if (noClipDescendInput)
            verticalInput -= 1f;

        Vector3 velocity = planarMove * noClipMoveSpeed + Vector3.up * (verticalInput * noClipVerticalSpeed);
        rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
    }

    // ---------------- Derived stats calculation ----------------
    private void ComputeDerivedStats()
    {
        if (!autoCalculatePhysics)
            return;

        if (rb == null) rb = GetComponent<Rigidbody>();
        int wheelCount = Mathf.Max(1, (wheelTransforms != null ? wheelTransforms.Length : 4));

        // Mass
        rb.mass = Mathf.Max(1f, vehicleMass);

        // center of mass: set relative to object center
        // positive z is forward. If frontWeightRatio > 0.5, shift COM forward
        float longitudinalOffset = (frontWeightRatio - 0.5f) * wheelBase;
        Vector3 calculatedCOM = new Vector3(0f, -Mathf.Max(0.01f, comHeight), longitudinalOffset);
        // add manual small tweak offset
        rb.centerOfMass = calculatedCOM + centerOfMassOffset;

        // Per-wheel mass & normal
        float perWheelMass = vehicleMass / (float)wheelCount;
        float g = Physics.gravity.magnitude;
        float perWheelNormal = perWheelMass * g; // N

        // Suspension stiffness: base spring that supports per-wheel normal over travel (N/m),
        // then scaled by suspensionStiffnessScale to match expected "game" stiffness numbers.
        float baseSpring = perWheelNormal / Mathf.Max(0.01f, suspensionDistance); // N/m
        suspensionStiffness = baseSpring * Mathf.Max(0.0001f, suspensionStiffnessScale);

        // Damping: approximated from critical damping c = 2*sqrt(k*m)
        float c = 2f * Mathf.Sqrt(Mathf.Max(0.0001f, suspensionStiffness) * perWheelMass);
        suspensionDamping = c * Mathf.Max(0.0001f, suspensionDampingScale);

        // Tire grip: derive from typical friction coefficient (mu ~ 0.8) and per-wheel normal.
        // Convert to the script's "grip" number by dividing by a reference speed (so lateralForce ~= mu*normal at low speeds).
        float mu = 0.9f; // typical rubber-on-tarmac friction estimate for lateral grip baseline
        float referenceSpeed = Mathf.Max(5f, maxSpeed); // avoid divide-by-zero, tie to vehicle top speed
        float computedGrip = (mu * perWheelNormal) / referenceSpeed;
        tireGrip = computedGrip * Mathf.Max(0.0001f, gripScale);

        // Rolling resistance & coast drag (scaled by vehicleMass so big cars feel heavier)
        float massFactor = vehicleMass / 1200f;
        frontRollingResistance = 18f * massFactor * Mathf.Max(0.0001f, rollingResistanceScale);
        rearRollingResistance  = 18f * massFactor * Mathf.Max(0.0001f, rollingResistanceScale);
        frontCoastDrag = 20f * massFactor * Mathf.Max(0.0001f, rollingResistanceScale);
        rearCoastDrag  = 20f * massFactor * Mathf.Max(0.0001f, rollingResistanceScale);

        // Brake force baseline
        brakeForce = 80f * massFactor * Mathf.Max(0.0001f, rollingResistanceScale);

        // Keep sane clamps (protect against crazy inputs)
        suspensionStiffness = Mathf.Clamp(suspensionStiffness, 100f, 1e6f);
        suspensionDamping = Mathf.Clamp(suspensionDamping, 0f, 1e5f);
        tireGrip = Mathf.Clamp(tireGrip, 1f, 2000f);
        frontRollingResistance = Mathf.Clamp(frontRollingResistance, 0.1f, 500f);
        rearRollingResistance = Mathf.Clamp(rearRollingResistance, 0.1f, 500f);
        frontCoastDrag = Mathf.Clamp(frontCoastDrag, 0f, 500f);
        rearCoastDrag = Mathf.Clamp(rearCoastDrag, 0f, 500f);
        brakeForce = Mathf.Clamp(brakeForce, 10f, 1000f);

        // Debug log a short summary so you can see results at runtime
        if (Application.isPlaying)
        {
            Debug.Log($"[CarController] Derived stats: mass={rb.mass:F0}kg, COM={rb.centerOfMass}, suspensionK={suspensionStiffness:F0} N/m, damping={suspensionDamping:F1}, tireGrip={tireGrip:F1}, frontRR={frontRollingResistance:F1}, rearRR={rearRollingResistance:F1}");
        }
    }

    // ---------------- MOVEMENT ----------------
    private void HandleAcceleration()
    {
        if (Mathf.Abs(moveInput.y) < 0.01f) return;

        if (isGrounded)
        {
            float slope = Vector3.Angle(groundHit.normal, Vector3.up);

            // Disallow driving up slopes steeper than allowed
            if (slope > maxDriveSlopeAngle)
                return;

            Vector3 forwardDir = Vector3.ProjectOnPlane(transform.forward, groundHit.normal).normalized;

            float speedRatio = rb.linearVelocity.magnitude / maxSpeed;
            float accelFalloff = Mathf.Lerp(1f, 0.35f, speedRatio);

            rb.AddForce(forwardDir * moveInput.y * accelerationForce * accelFalloff, ForceMode.Acceleration);
        }
        else
        {
            rb.AddForce(transform.forward * moveInput.y * accelerationForce * 0.15f, ForceMode.Acceleration);
        }
    }

    private void HandleSteering()
    {
        // Auto-steer disabled: use only direct player steering input.
        float steerInput = Mathf.Clamp(moveInput.x, -1f, 1f);
        if (Mathf.Abs(steerInput) < 0.01f) return;

        float steerStrength = isDrifting ? driftSteerMultiplier : 1f;
        float turn = steerInput * turnSpeed * steerStrength * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn, 0f));
    }

#if false // Auto-steer temporarily disabled for build stabilization.
    private float ComputeAutoSteerInput(float playerSteerInput)
    {
        ClearAutoSteerDebug();

        if (!autoSteerEnabled || rb == null)
            return MoveAutoSteerTowards(0f);

        Vector3 planarVelocity = rb.linearVelocity;
        planarVelocity.y = 0f;
        float planarSpeed = planarVelocity.magnitude;

        autoSteerDebugActive = true;
        if (planarSpeed > 0.001f)
            autoSteerDebugDirection = planarVelocity / planarSpeed;

        if (Mathf.Abs(playerSteerInput) >= autoSteerPlayerTurnDeadzone)
        {
            autoSteerDebugSuppressedByPlayer = true;
            return MoveAutoSteerTowards(0f);
        }

        if (planarSpeed <= autoSteerMinSpeed)
        {
            autoSteerDebugSuppressedBySpeed = true;
            return MoveAutoSteerTowards(0f);
        }

        Vector3 travelDirection = planarVelocity / Mathf.Max(0.0001f, planarSpeed);
        float predictedDistance = planarSpeed * Time.fixedDeltaTime * autoSteerPredictionFrames;
        float lookAhead = Mathf.Clamp(predictedDistance, autoSteerMinLookAhead, autoSteerMaxLookAhead);

        if (!TryGetUpcomingAutoSteerWallHit(travelDirection, planarSpeed, lookAhead, out RaycastHit wallHit, out float danger))
        {
            autoSteerPerpendicularWallId = 0;
            return MoveAutoSteerTowards(0f);
        }

        float steerSign = GetAutoSteerDirectionFromWall(travelDirection, wallHit);
        autoSteerDebugSteerSign = steerSign;
        if (Mathf.Abs(steerSign) <= 0.001f)
            return MoveAutoSteerTowards(0f);

        float speedFactor = Mathf.InverseLerp(autoSteerMinSpeed, autoSteerFullSpeed, planarSpeed);
        float targetAssist = steerSign * danger * speedFactor * autoSteerMaxInput;
        return MoveAutoSteerTowards(targetAssist);
    }

    private float MoveAutoSteerTowards(float target)
    {
        autoSteerInput = Mathf.MoveTowards(autoSteerInput, target, autoSteerResponse * Time.fixedDeltaTime);
        return autoSteerInput;
    }

    private bool TryGetUpcomingAutoSteerWallHit(
        Vector3 travelDirection,
        float travelSpeed,
        float lookAheadDistance,
        out RaycastHit nearestWallHit,
        out float danger)
    {
        nearestWallHit = default;
        danger = 0f;

        float originHeight = col != null ? Mathf.Max(0.25f, col.bounds.extents.y * 0.4f) : 0.35f;
        float forwardOffset = col != null ? Mathf.Max(0.25f, col.bounds.extents.z * 0.3f) : 0.4f;
        Vector3 origin = rb.worldCenterOfMass + Vector3.up * originHeight + travelDirection * forwardOffset;
        autoSteerDebugOrigin = origin;
        autoSteerDebugDirection = travelDirection;
        autoSteerDebugLookAhead = lookAheadDistance;
        autoSteerDebugHasWallHit = false;
        autoSteerDebugDanger = 0f;

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            autoSteerProbeRadius,
            travelDirection,
            autoSteerProbeHits,
            lookAheadDistance,
            autoSteerLayerMask,
            QueryTriggerInteraction.Ignore);

        if (hitCount <= 0)
            return false;

        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = autoSteerProbeHits[i];
            if (!IsAutoSteerWall(hit, travelDirection, travelSpeed))
                continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestWallHit = hit;
            }
        }

        if (float.IsPositiveInfinity(nearestDistance))
            return false;

        danger = 1f - Mathf.Clamp01(nearestDistance / Mathf.Max(0.01f, lookAheadDistance));
        autoSteerDebugHasWallHit = true;
        autoSteerDebugWallPoint = nearestWallHit.point;
        autoSteerDebugWallNormal = nearestWallHit.normal;
        autoSteerDebugDanger = danger;
        return danger > 0f;
    }

    private float GetAutoSteerDirectionFromWall(Vector3 travelDirection, RaycastHit wallHit)
    {
        Vector3 wallNormal = wallHit.normal;
        wallNormal.y = 0f;
        if (wallNormal.sqrMagnitude < 0.0001f)
            return 0f;

        wallNormal.Normalize();
        float intoWall = Vector3.Dot(travelDirection, -wallNormal);
        if (intoWall <= 0.05f)
            return 0f;

        Vector3 wallTangent = Vector3.Cross(wallNormal, Vector3.up);
        if (wallTangent.sqrMagnitude < 0.0001f)
            return 0f;
        wallTangent.Normalize();

        float tangentComponent = Vector3.Dot(travelDirection, wallTangent);
        if (Mathf.Abs(tangentComponent) > 0.03f)
        {
            autoSteerPerpendicularWallId = 0;
            return Mathf.Sign(tangentComponent);
        }

        int wallId = wallHit.collider != null ? wallHit.collider.GetInstanceID() : 0;
        if (wallId != autoSteerPerpendicularWallId)
        {
            autoSteerPerpendicularWallId = wallId;
            autoSteerPerpendicularTurnSign = Random.value < 0.5f ? -1f : 1f;
        }

        return autoSteerPerpendicularTurnSign;
    }

    private bool IsAutoSteerWall(RaycastHit hit, Vector3 probeDirection, float travelSpeed)
    {
        if (hit.collider == null)
            return false;

        if (hit.collider.transform.IsChildOf(transform))
            return false;

        Rigidbody hitBody = hit.collider.attachedRigidbody;
        if (hitBody != null && hitBody != rb && !hitBody.isKinematic)
            return false;

        float driveableSlopeLimit = GetAutoSteerDriveableSlopeLimit(travelSpeed);
        float slope = Vector3.Angle(hit.normal, Vector3.up);
        if (slope <= driveableSlopeLimit)
            return false;

        return Vector3.Dot(-hit.normal, probeDirection) > 0.05f;
    }

    private float GetAutoSteerDriveableSlopeLimit(float speed)
    {
        float speedFactor = Mathf.InverseLerp(autoSteerMinSpeed, autoSteerFullSpeed, speed);
        float bonus = autoSteerSpeedSlopeBonus * speedFactor;
        return Mathf.Clamp(maxDriveSlopeAngle + bonus, 0f, 89f);
    }

    private void ClearAutoSteerDebug()
    {
        autoSteerDebugActive = false;
        autoSteerDebugSuppressedByPlayer = false;
        autoSteerDebugSuppressedBySpeed = false;
        autoSteerDebugOrigin = Vector3.zero;
        autoSteerDebugDirection = Vector3.zero;
        autoSteerDebugLookAhead = 0f;
        autoSteerDebugHasWallHit = false;
        autoSteerDebugWallPoint = Vector3.zero;
        autoSteerDebugWallNormal = Vector3.zero;
        autoSteerDebugDanger = 0f;
        autoSteerDebugSteerSign = 0f;
    }
#endif

    // ---------------- DRIFT ----------------
    private void HandleDrift()
    {
        if (!isGrounded)
        {
            if (isDrifting)
            {
                isDrifting = false;
                DriftEnded?.Invoke();
            }

            return;
        }

        float speed = rb.linearVelocity.magnitude;
        bool wasDriftingLocal = isDrifting;

        isDrifting = speed > minDriftSpeed && Mathf.Abs(moveInput.x) > 0.2f && moveInput.y > 0.1f;

        if (!wasDriftingLocal && isDrifting)
            DriftStarted?.Invoke();

        if (isDrifting)
        {
            // lateral force to push car sideways
            Vector3 driftDir = transform.right * Mathf.Sign(moveInput.x);
            float speedRatio = Mathf.Clamp01(speed / Mathf.Max(0.0001f, maxSpeed));
            float lowSpeedAssist = 1f - speedRatio;
            float sideForceMultiplier = Mathf.Lerp(1f, lowSpeedDriftSideForceMultiplier, lowSpeedAssist);
            rb.AddForce(driftDir * driftSideForce * sideForceMultiplier * Time.fixedDeltaTime, ForceMode.Acceleration);

            // pivot logic based on front/rear lateral velocities
            float frontLat = GetAverageLateralVelocity(true);
            float rearLat = GetAverageLateralVelocity(false);
            float frontGripFactor = 1f / (1f + Mathf.Abs(frontLat) / Mathf.Max(0.001f, lateralSlipThreshold));
            float rearSlipFactor = Mathf.Clamp01(Mathf.Abs(rearLat) / lateralSlipThreshold);
            float pivotT = Mathf.Clamp01(rearSlipFactor * frontGripFactor);
            float pivotDistance = Mathf.Lerp(-rearPivotDistance, frontPivotDistance, pivotT);

            float speedFactor = Mathf.Clamp01(rb.linearVelocity.magnitude / Mathf.Max(0.1f, maxSpeed));
            speedFactor = Mathf.Max(minDriftYawFactor, speedFactor);
            float yawAmount = moveInput.x * driftYawTorque * driftYawTorqueMultiplier * (1f + Mathf.Abs(pivotDistance) / (Mathf.Max(frontPivotDistance, rearPivotDistance) + 0.001f)) * speedFactor;
            rb.AddTorque(Vector3.up * yawAmount * Time.fixedDeltaTime, ForceMode.Acceleration);

            // charge drift boost
            driftTimer += Time.fixedDeltaTime;
            if (driftChargeTime <= 0f) driftCharge = 1f;
            else
            {
                driftCharge += Time.fixedDeltaTime / Mathf.Max(0.0001f, driftChargeTime);
                driftCharge = Mathf.Clamp01(driftCharge);
            }
        }

        if (wasDriftingLocal && !isDrifting)
        {
            DriftEnded?.Invoke();
            bool completedChargedDrift = driftTimer >= driftChargeTime && driftCharge > 0f;
            bool completedStackingDrift = boostActive && driftTimer > 0.05f;

            if (completedChargedDrift || completedStackingDrift)
            {
                driftBoostAmount = boostSpeedPerStack;
                AddBoostStack();
            }
            driftCharge = 0f;
            driftTimer = 0f;
        }

        if (!isDrifting)
        {
            Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
            localVel.x = Mathf.Lerp(localVel.x, 0f, driftGripRecovery * Time.fixedDeltaTime * Mathf.Clamp01(speed / maxSpeed));
            rb.linearVelocity = transform.TransformDirection(localVel);
        }
    }

    private float GetAverageLateralVelocity(bool front)
    {
        if (wheelTransforms == null || wheelTransforms.Length == 0) return 0f;
        float sum = 0f; int count = 0;
        foreach (Transform w in wheelTransforms)
        {
            bool isFront = transform.InverseTransformPoint(w.position).z > 0f;
            if (front != isFront) continue;
            Vector3 pointVel = rb.GetPointVelocity(w.position);
            Vector3 local = transform.InverseTransformDirection(pointVel);
            sum += local.x; count++;
        }
        if (count == 0) return 0f;
        return sum / count;
    }

    // ---------------- BOOST ----------------
    private void HandleBoost()
    {
        if (currentBoostStacks <= 0)
        {
            currentBoostStacks = 0;
            boostTimer = 0f;
            boostActive = false;
            return;
        }

        boostTimer -= Time.fixedDeltaTime;
        if (boostTimer <= 0f)
        {
            boostTimer = 0f;
            currentBoostStacks = 0;
            boostActive = false;
            return;
        }

        boostActive = true;
    }

    private void ClampSpeed()
    {
        float allowedSpeed = maxSpeed + GetTotalActiveBoostAmount();
        if (rb.linearVelocity.magnitude > allowedSpeed) rb.linearVelocity = rb.linearVelocity.normalized * allowedSpeed;
    }

    private void MaintainBoostSpeedAtCap()
    {
        if (!boostActive || rb == null)
            return;

        float targetPlanarSpeed = maxSpeed + GetTotalActiveBoostAmount();
        if (targetPlanarSpeed <= 0f)
            return;

        Vector3 velocity = rb.linearVelocity;
        Vector3 planarDirection = transform.forward;
        planarDirection.y = 0f;
        if (planarDirection.sqrMagnitude < 0.0001f)
            planarDirection = Vector3.forward;
        else
            planarDirection.Normalize();

        Vector3 boostedPlanarVelocity = planarDirection * targetPlanarSpeed;
        rb.linearVelocity = new Vector3(boostedPlanarVelocity.x, velocity.y, boostedPlanarVelocity.z);
    }

    // ---------------- JUMP ----------------
    private bool Jump()
    {
        if (!isGrounded) return false;
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
        return true;
    }

    // ---------------- AIR DASH ----------------
    private void TryAirDash()
    {
        if (isGrounded || airDashUsed) return;
        airDashUsed = true;
        airDashTimer = airDashCooldown;
        Vector3 vel = rb.linearVelocity;
        float forwardSpeed = Vector3.Dot(vel, transform.forward);
        Vector3 carriedForward = transform.forward * Mathf.Max(0f, forwardSpeed) * airDashForwardCarry;
        vel = new Vector3(carriedForward.x, Mathf.Max(vel.y, 0f), carriedForward.z);
        rb.linearVelocity = vel;
        rb.AddForce(transform.forward * airDashForce + Vector3.up * airDashUpForce, ForceMode.VelocityChange);
    }

    // ---------------- ARCADE ----------------
    private void ApplyArcadeDownforce()
    {
        float slope = Vector3.Angle(groundHit.normal, Vector3.up);
        if (slope > maxDriveSlopeAngle) return;
        rb.AddForce(-groundHit.normal * groundedDownforce, ForceMode.Acceleration);
    }

    private void DampRampPitch()
    {
        if (slopeAngle < 8f) return;
        Vector3 angVel = rb.angularVelocity;
        angVel.x *= rampPitchDamping;
        rb.angularVelocity = angVel;
    }

    private void ApplyArcadeGravity()
    {
        if (airDashTimer > 0f) { airDashTimer -= Time.fixedDeltaTime; return; }
        float gravityMult = rb.linearVelocity.y < 0f ? fallGravityMultiplier : airGravityMultiplier;
        rb.AddForce(Physics.gravity * (gravityMult - 1f), ForceMode.Acceleration);
    }

    private void AlignUprightInAir()
    {
        Quaternion target = Quaternion.Euler(0f, rb.rotation.eulerAngles.y, 0f);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, target, 4f * Time.fixedDeltaTime));
    }

    // ---------------- SUSPENSION + TIRE FORCES ----------------
    private void ApplySuspensionAndTireForces()
    {
        if (wheelTransforms == null || wheelTransforms.Length == 0) return;

        Vector3 averageNormal = Vector3.zero;
        int hitCount = 0;
        float rayLength = suspensionDistance * 2f;

        for (int i = 0; i < wheelTransforms.Length; i++)
        {
            Transform wheel = wheelTransforms[i];
            Vector3 origin = wheel.position + Vector3.up * suspensionDistance;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayLength, groundLayer))
            {
                averageNormal += hit.normal;
                hitCount++;

                // compression 0..1
                float compression = Mathf.Clamp01((rayLength - hit.distance) / rayLength);

                // spring & damper (vertical)
                float springForce = compression * suspensionStiffness;
                float wheelPointVelUp = Vector3.Dot(rb.GetPointVelocity(wheel.position), hit.normal);
                float damperForce = wheelPointVelUp * suspensionDamping;
                float netForce = Mathf.Clamp(springForce - damperForce, -suspensionMaxForcePerWheel, suspensionMaxForcePerWheel);

                // Apply vertical spring force at wheel point
                rb.AddForceAtPosition(hit.normal * netForce, wheel.position, ForceMode.Acceleration);

                // --- LATERAL TIRE FRICTION (speed-dependent curve + front/rear multipliers) ---
                Vector3 lateralDir = Vector3.ProjectOnPlane(transform.right, hit.normal).normalized;
                float lateralVel = Vector3.Dot(rb.GetPointVelocity(wheel.position), lateralDir);

                // compute speed ratio (0..1)
                float speedRatio = Mathf.Clamp01(rb.linearVelocity.magnitude / Mathf.Max(0.0001f, maxSpeed));
                // evaluate grip curve (designer curve) and combine with base grip
                float curveGripMult = (gripCurve != null) ? gripCurve.Evaluate(speedRatio) : 1f;
                bool isFront = transform.InverseTransformPoint(wheel.position).z > 0f;
                float wheelGripMult = isFront ? frontTireGrip : rearTireGrip;

                float grip = tireGrip * wheelGripMult * curveGripMult * (0.5f + 0.5f * compression);
                // legacy speed falloff factor (keeps quick tweak available)
                float legacyFalloff = 1f / (1f + (rb.linearVelocity.magnitude * tireGripSpeedFalloff));
                grip *= legacyFalloff;
                if (isDrifting)
                {
                    grip *= driftLateralGripMultiplier;
                    grip *= isFront ? driftFrontGripMultiplier : driftRearGripMultiplier;
                }

                float lateralForce = -lateralVel * grip;
                rb.AddForceAtPosition(lateralDir * lateralForce, wheel.position, ForceMode.Acceleration);

                // --- LONGITUDINAL (FORWARD) TIRE FRICTION / ROLLING RESISTANCE ---
                Vector3 forwardDir = Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;
                float forwardVel = Vector3.Dot(rb.GetPointVelocity(wheel.position), forwardDir);

                // select front/rear rolling and coast drag
                float rollingResistance = isFront ? frontRollingResistance : rearRollingResistance;
                float coastDrag = isFront ? frontCoastDrag : rearCoastDrag;

                // dynamic reduction of rear rolling while drifting (helps sustain slide)
                if (isDrifting && !isFront)
                    rollingResistance *= driftRearRollingResistanceMultiplier;

                // base rolling resistance (opposes forward velocity)
                float rolling = -forwardVel * rollingResistance;

                // extra coast drag when player is not giving throttle
                if (Mathf.Abs(moveInput.y) < 0.01f)
                {
                    rolling += -forwardVel * coastDrag;
                }

                // braking when player pulls negative (brake / reverse)
                if (moveInput.y < -0.01f)
                {
                    float braking = -Mathf.Sign(forwardVel) * brakeForce * Mathf.Abs(moveInput.y);
                    rolling += braking;
                }

                // scale by compression so unloaded wheels don't massively affect braking
                rolling *= (0.5f + 0.5f * compression);

                rb.AddForceAtPosition(forwardDir * rolling, wheel.position, ForceMode.Acceleration);
            }
        }

        if (hitCount > 0)
        {
            averageNormal.Normalize();
            groundHit.normal = averageNormal;
        }
    }

    // ---------------- SLOPE FORCES (ONLY DOWNHILL ASSIST NOW) ----------------
    private void ApplySlopeForces()
    {
        lastSampleHadHits = false;
        lastFrontSample = lastBackSample = Vector3.zero;
        lastSampledSlopeAngle = 0f;

        if (wheelTransforms == null || wheelTransforms.Length == 0) return;
        if (groundHit.normal == Vector3.zero) return;

        float halfDist = slopeSampleDistance;
        Vector3 frontOrigin = transform.position + transform.forward * halfDist + Vector3.up * (suspensionDistance + 0.2f);
        Vector3 backOrigin = transform.position - transform.forward * halfDist + Vector3.up * (suspensionDistance + 0.2f);

        if (!Physics.Raycast(frontOrigin, Vector3.down, out RaycastHit hitF, (suspensionDistance + 5f), groundLayer)) return;
        if (!Physics.Raycast(backOrigin, Vector3.down, out RaycastHit hitB, (suspensionDistance + 5f), groundLayer)) return;

        lastSampleHadHits = true;
        lastFrontSample = hitF.point;
        lastBackSample = hitB.point;

        float heightDiff = hitF.point.y - hitB.point.y; // positive => front higher (uphill)
        float run = halfDist * 2f;
        float angleDeg = Mathf.Atan2(heightDiff, run) * Mathf.Rad2Deg;
        lastSampledSlopeAngle = angleDeg;

        if (Mathf.Abs(angleDeg) < minSlopeAngleToAffect) return;

        Vector3 forwardPlane = Vector3.ProjectOnPlane(transform.forward, groundHit.normal).normalized;

        // only apply downhill assist (negative angle => forward is downhill)
        if (angleDeg < 0f)
        {
            float normalized = Mathf.Clamp01(Mathf.Abs(angleDeg) / 90f);
            float forceMag = downhillAcceleration * normalized;
            rb.AddForce(forwardPlane * forceMag, ForceMode.Acceleration);
        }
    }

    // ---------------- STEEP SLOPE SLIDE ----------------
    private void ApplySteepSlopeSlide()
    {
        float slope = Vector3.Angle(groundHit.normal, Vector3.up);
        if (slope <= maxDriveSlopeAngle) return;
        Vector3 slideDir = Vector3.ProjectOnPlane(Vector3.down, groundHit.normal).normalized;
        rb.AddForce(slideDir * steepSlopeSlideForce, ForceMode.Acceleration);
    }

    void OnCollisionEnter(Collision collision)
    {
        TryBoostWallBounce(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        TryBoostWallBounce(collision);
    }

    void TryBoostWallBounce(Collision collision)
    {
        if (!boostActive || rb == null || collision == null)
            return;
        if (Time.time - lastBoostWallBounceTime < boostWallBounceCooldown)
            return;

        Collider other = collision.collider;
        if (other == null || other.isTrigger)
            return;
        if (other.transform.IsChildOf(transform))
            return;
        if (other.GetComponentInParent<Enemy>() != null)
            return;
        if (((1 << other.gameObject.layer) & groundLayer.value) != 0)
            return;
        if (collision.contactCount == 0)
            return;

        ContactPoint contact = collision.GetContact(0);
        Vector3 normal = contact.normal;

        Vector3 planarVelocity = rb.linearVelocity;
        planarVelocity.y = 0f;
        if (planarVelocity.sqrMagnitude < 0.01f)
            return;

        Vector3 planarNormal = normal;
        planarNormal.y = 0f;
        if (planarNormal.sqrMagnitude < 0.0001f)
            return;
        planarNormal.Normalize();

        float intoWall = Vector3.Dot(planarVelocity, planarNormal);
        if (intoWall >= -0.1f)
            return;

        Vector3 reflected = Vector3.Reflect(planarVelocity, planarNormal);
        float targetSpeed = Mathf.Max(boostWallBounceMinSpeed, planarVelocity.magnitude * Mathf.Max(0f, boostWallBounceSpeedScale));
        if (reflected.sqrMagnitude < 0.0001f)
            return;

        reflected = reflected.normalized * targetSpeed;
        rb.linearVelocity = new Vector3(reflected.x, rb.linearVelocity.y, reflected.z);
        lastBoostWallBounceTime = Time.time;
    }

    // ---------------- GROUND CHECK (wheel-based) ----------------
    private bool CheckGrounded()
    {
        if (wheelTransforms == null || wheelTransforms.Length == 0) return false;
        bool grounded = false;
        Vector3 averageNormal = Vector3.zero;
        int hitCount = 0;
        float rayLength = suspensionDistance * 2f;

        foreach (Transform wheel in wheelTransforms)
        {
            Vector3 origin = wheel.position + Vector3.up * suspensionDistance;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayLength, groundLayer))
            {
                grounded = true;
                averageNormal += hit.normal;
                hitCount++;
            }
        }

        if (hitCount > 0) groundHit.normal = averageNormal.normalized;
        return grounded;
    }

    void SetupRamHitbox()
    {
        if (ramHitbox != null)
        {
            if (ramHitbox.GetComponent<CarRamHitbox>() == null)
                ramHitbox.gameObject.AddComponent<CarRamHitbox>();
            ramHitbox.isTrigger = true;
            return;
        }

        if (!autoCreateRamHitbox)
            return;

        Transform hitboxTransform = transform.Find("RamHitbox");
        if (hitboxTransform == null)
        {
            GameObject hitboxObject = new GameObject("RamHitbox");
            hitboxObject.transform.SetParent(transform, false);
            hitboxTransform = hitboxObject.transform;
        }

        BoxCollider box = hitboxTransform.GetComponent<BoxCollider>();
        if (box == null)
            box = hitboxTransform.gameObject.AddComponent<BoxCollider>();
        if (hitboxTransform.GetComponent<CarRamHitbox>() == null)
            hitboxTransform.gameObject.AddComponent<CarRamHitbox>();

        box.isTrigger = true;
        ramHitbox = box;
    }

    void SetupRuntimeParticles()
    {
        if (!enableRuntimeParticles)
            return;

        if (particleAnchor == null)
        {
            Transform existingAnchor = transform.Find("RuntimeParticles");
            if (existingAnchor == null)
            {
                GameObject anchorObject = new GameObject("RuntimeParticles");
                anchorObject.transform.SetParent(transform, false);
                existingAnchor = anchorObject.transform;
            }

            particleAnchor = existingAnchor;
        }

        UpdateParticleAnchorPose();

        if (drivingDustParticles == null)
        {
            Transform existingDust = particleAnchor.Find("DrivingDustParticles");
            drivingDustParticles = existingDust != null ? existingDust.GetComponent<ParticleSystem>() : null;
            if (drivingDustParticles == null)
                drivingDustParticles = RuntimeParticleFactory.CreateDrivingDust(particleAnchor, "DrivingDustParticles", drivingDustColor);
        }

        if (boostDustParticles == null)
        {
            Transform existingBoost = particleAnchor.Find("BoostDustParticles");
            boostDustParticles = existingBoost != null ? existingBoost.GetComponent<ParticleSystem>() : null;
            if (boostDustParticles == null)
                boostDustParticles = RuntimeParticleFactory.CreateBoostDust(particleAnchor, "BoostDustParticles", boostDustColor);
        }

        if (driftSparkleParticles == null)
        {
            Transform existingSparkle = particleAnchor.Find("DriftSparkleParticles");
            driftSparkleParticles = existingSparkle != null ? existingSparkle.GetComponent<ParticleSystem>() : null;
            if (driftSparkleParticles == null)
                driftSparkleParticles = RuntimeParticleFactory.CreateDriftSparkles(particleAnchor, "DriftSparkleParticles");
        }

        StopRuntimeParticles();
    }

    void UpdateRuntimeParticles()
    {
        if (!enableRuntimeParticles)
        {
            StopRuntimeParticles();
            return;
        }

        if (particleAnchor == null || drivingDustParticles == null || boostDustParticles == null || driftSparkleParticles == null)
            SetupRuntimeParticles();

        if (particleAnchor == null || rb == null || isDead || noClipActive)
        {
            StopRuntimeParticles();
            return;
        }

        UpdateParticleAnchorPose();

        float speed = rb.linearVelocity.magnitude;
        float maxTrackedSpeed = Mathf.Max(0.01f, GetMaxSpeedAtFullBoostStacks());
        float speedRatio = Mathf.Clamp01(speed / maxTrackedSpeed);
        bool groundedAndMoving = isGrounded && speed >= drivingParticleMinSpeed && moveInput.y > 0.05f;
        bool boostDustActive = groundedAndMoving && boostActive;

        float driveRate = Mathf.Lerp(8f, drivingParticleMaxRate, speedRatio);
        float boostRate = Mathf.Lerp(12f, drivingParticleMaxRate * 1.25f, speedRatio);
        float driftRate = Mathf.Lerp(5f, driftSparkleMaxRate, Mathf.Clamp01(driftCharge));

        SetParticleEmission(drivingDustParticles, groundedAndMoving && !boostDustActive, driveRate);
        SetParticleEmission(boostDustParticles, boostDustActive, boostRate);
        SetParticleEmission(driftSparkleParticles, isGrounded && isDrifting, driftRate);
    }

    void UpdateParticleAnchorPose()
    {
        if (particleAnchor == null)
            return;

        Vector3 localPosition = particleAnchorOffset;
        if (wheelTransforms != null && wheelTransforms.Length > 0)
        {
            Vector3 rearCenter = Vector3.zero;
            int rearWheelCount = 0;

            for (int i = 0; i < wheelTransforms.Length; i++)
            {
                Transform wheel = wheelTransforms[i];
                if (wheel == null)
                    continue;

                Vector3 localWheelPosition = transform.InverseTransformPoint(wheel.position);
                if (localWheelPosition.z < 0f)
                {
                    rearCenter += localWheelPosition;
                    rearWheelCount++;
                }
            }

            if (rearWheelCount > 0)
            {
                rearCenter /= rearWheelCount;
                localPosition = new Vector3(rearCenter.x, rearCenter.y + 0.12f, rearCenter.z - 0.3f);
            }
        }

        particleAnchor.localPosition = localPosition;
        particleAnchor.localRotation = Quaternion.identity;
    }

    void StopRuntimeParticles()
    {
        SetParticleEmission(drivingDustParticles, false, 0f);
        SetParticleEmission(boostDustParticles, false, 0f);
        SetParticleEmission(driftSparkleParticles, false, 0f);
    }

    static void SetParticleEmission(ParticleSystem particles, bool active, float rate)
    {
        if (particles == null)
            return;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = active;
        if (active)
            emission.rateOverTime = Mathf.Max(0f, rate);

        if (active)
        {
            if (!particles.isPlaying)
                particles.Play(true);
            return;
        }

        if (particles.isPlaying)
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    void UpdateRamHitboxSize()
    {
        if (ramHitbox == null)
            return;

        ramHitbox.isTrigger = true;

        float speed = rb != null ? rb.linearVelocity.magnitude : 0f;
        float widthCapSpeed = GetMaxSpeedAtFullBoostStacks();
        float speedRatio = Mathf.Clamp01(speed / Mathf.Max(0.01f, widthCapSpeed));
        float widthRatio = ramHitboxWidthBySpeed != null ? ramHitboxWidthBySpeed.Evaluate(speedRatio) : speedRatio;
        widthRatio = Mathf.Clamp01(widthRatio);
        float forwardRatio = ramHitboxForwardBySpeed != null
            ? ramHitboxForwardBySpeed.Evaluate(Mathf.Clamp01(speed / Mathf.Max(0.01f, maxSpeed)))
            : Mathf.Clamp01(speed / Mathf.Max(0.01f, maxSpeed));
        forwardRatio = Mathf.Clamp01(forwardRatio);
        float forwardExtension = Mathf.Lerp(0f, ramHitboxMaxForwardExtension, forwardRatio);
        if (speed > maxSpeed)
            forwardExtension += (speed - maxSpeed) * ramHitboxForwardExtensionPerExtraSpeed;

        Vector3 size = ramHitboxBaseSize;
        size.x = Mathf.Lerp(ramHitboxBaseSize.x, ramHitboxMaxWidth, widthRatio);
        size.z = ramHitboxBaseSize.z + forwardExtension;
        ramHitbox.size = size;

        Vector3 center = ramHitboxCenter;
        center.z += forwardExtension * 0.5f; // extend only in front of the base hitbox
        ramHitbox.center = center;
    }

    public void TryApplyRamImpact(Collider other, Vector3 impactOrigin)
    {
        if (isDead || noClipActive || other == null)
            return;

        if (other.transform.IsChildOf(transform))
            return;

        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy == null || !enemy.IsAlive)
            return;
        if (enemy.IsRamDamageImmune)
            return;

        float speed = rb != null ? rb.linearVelocity.magnitude : 0f;
        float effectiveMinImpactSpeed = Mathf.Max(minCollisionImpactSpeed, EffectiveRamMinSpeed);
        if (speed < effectiveMinImpactSpeed)
            return;

        int enemyId = enemy.GetInstanceID();
        if (recentEnemyImpactTimeById.TryGetValue(enemyId, out float lastImpactTime))
        {
            if (Time.time - lastImpactTime < collisionImpactCooldown)
                return;
        }

        recentEnemyImpactTimeById[enemyId] = Time.time;

        float damageFullSpeed = Mathf.Max(effectiveMinImpactSpeed + 0.01f, maxSpeed);
        float damageRatio = Mathf.Clamp01((speed - effectiveMinImpactSpeed) / (damageFullSpeed - effectiveMinImpactSpeed));
        float damage = damageRatio * Mathf.Max(0f, maxCollisionDamage);
        if (damage > 0f)
            enemy.TakeDamage(damage, gameObject);

        Vector3 pushDirection = GetEnemyImpactPushDirection(enemy, impactOrigin);

        float pushPeakSpeed = Mathf.Max(effectiveMinImpactSpeed + 0.01f, collisionPushCurvePeakSpeed);
        float pushCurveRatio = Mathf.Clamp01((speed - effectiveMinImpactSpeed) / (pushPeakSpeed - effectiveMinImpactSpeed));
        float pushMultiplier = collisionPushBySpeed != null ? collisionPushBySpeed.Evaluate(pushCurveRatio) : pushCurveRatio;
        float pushStrength = Mathf.Max(0f, pushMultiplier) * Mathf.Max(0f, maxCollisionPushStrength);
        if (boostActive)
            pushStrength = Mathf.Max(pushStrength * boostCollisionPushMultiplier, boostCollisionMinPushStrength);
        enemy.ApplyCarImpact(pushDirection.normalized, pushStrength);
        ramImpactCount++;
        EnemyRamImpact?.Invoke();

        if (!boostActive)
            ApplyNonBoostRamSlowdown();
    }

    void ApplyNonBoostRamSlowdown()
    {
        if (rb == null)
            return;

        Vector3 velocity = rb.linearVelocity;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            return;

        forward.Normalize();
        float forwardSpeed = Vector3.Dot(velocity, forward);
        if (forwardSpeed <= 0f)
            return;

        float speedLoss = Mathf.Min(
            forwardSpeed * nonBoostRamForwardSpeedLossPercent,
            nonBoostRamForwardSpeedLossMax);

        rb.linearVelocity = velocity - forward * speedLoss;
    }

    void AddBoostStack()
    {
        float stackDuration = GetConfiguredBoostStackDuration();
        int stackCap = GetBoostStackCap();
        bool wasBoostActive = boostActive;
        int previousBoostStacks = currentBoostStacks;

        currentBoostStacks = Mathf.Min(currentBoostStacks + 1, stackCap);
        boostTimer = stackDuration;
        boostActive = true;

        if (currentBoostStacks != previousBoostStacks)
            BoostStackGained?.Invoke();

        if (!wasBoostActive)
            BoostActivated?.Invoke();
    }

    float GetTotalActiveBoostAmount()
    {
        return Mathf.Max(0f, boostSpeedPerStack) * Mathf.Max(0, currentBoostStacks);
    }

    float GetMaxSpeedAtFullBoostStacks()
    {
        float perStackBoost = Mathf.Max(0f, boostSpeedPerStack);
        int stackCap = GetBoostStackCap();
        return maxSpeed + perStackBoost * stackCap;
    }

    float GetConfiguredBoostStackDuration()
    {
        float minDuration = Mathf.Clamp(minBoostStackDuration, 0.01f, 5f);
        return Mathf.Max(minDuration, driftBoostDuration);
    }

    int GetBoostStackCap()
    {
        // Older scene data may have serialized this as 1 from an earlier clamp bug.
        return maxBoostStacks <= 1 ? 3 : Mathf.Clamp(maxBoostStacks, 1, 3);
    }

    Vector3 GetEnemyImpactPushDirection(Enemy enemy, Vector3 impactOrigin)
    {
        Vector3 toEnemy = enemy.transform.position - impactOrigin;
        toEnemy.y = 0f;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        else
            forward.Normalize();

        Vector3 right = transform.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.right;
        else
            right.Normalize();

        float sideSign = Mathf.Sign(Vector3.Dot(toEnemy, right));
        if (Mathf.Abs(sideSign) < 0.001f)
            sideSign = Random.value < 0.5f ? -1f : 1f;

        Vector3 sideDirection = right * sideSign;
        Vector3 pushDirection = sideDirection * collisionSidePushWeight + forward * collisionForwardPushWeight;
        if (pushDirection.sqrMagnitude < 0.0001f)
            pushDirection = sideDirection;

        float randomYaw = Random.Range(-collisionRandomYawDegrees, collisionRandomYawDegrees);
        pushDirection = Quaternion.AngleAxis(randomYaw, Vector3.up) * pushDirection.normalized;
        return pushDirection.normalized;
    }

    // ---------------- DEBUG DRAW ----------------
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            if (wheelTransforms != null)
            {
                Gizmos.color = Color.yellow;
                foreach (var w in wheelTransforms) if (w != null) Gizmos.DrawSphere(w.position, debugSphereSize * 0.8f);
            }
            return;
        }

        if (wheelTransforms != null && showSuspensionRays)
        {
            float rayLength = suspensionDistance * 2f;
            foreach (var w in wheelTransforms)
            {
                if (w == null) continue;
                Vector3 origin = w.position + Vector3.up * suspensionDistance;
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayLength, groundLayer))
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(origin, hit.point);
                    Gizmos.DrawSphere(hit.point, debugSphereSize);
                    if (showSurfaceNormals)
                    {
                        float a = Vector3.Angle(hit.normal, Vector3.up);
                        Gizmos.color = a <= maxDriveSlopeAngle ? driveableColor : steepColor;
                        Gizmos.DrawLine(hit.point, hit.point + hit.normal * 0.5f);
                    }
                }
                else
                {
                    Gizmos.color = Color.gray;
                    Gizmos.DrawLine(origin, origin + Vector3.down * rayLength);
                }
            }
        }

        if (groundHit.normal != Vector3.zero && showSurfaceNormals)
        {
            Vector3 pos = transform.position + Vector3.up * 0.5f;
            float a = Vector3.Angle(groundHit.normal, Vector3.up);
            Gizmos.color = a <= maxDriveSlopeAngle ? driveableColor : steepColor;
            Gizmos.DrawLine(pos, pos + groundHit.normal * 1.0f);
            Gizmos.DrawSphere(pos + groundHit.normal * 1.0f, debugSphereSize * 0.6f);
        }

        if (lastSampleHadHits)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(lastFrontSample, debugSphereSize * 1.2f);
            Gizmos.DrawSphere(lastBackSample, debugSphereSize * 1.2f);
            float absAngle = Mathf.Abs(lastSampledSlopeAngle);
            Gizmos.color = absAngle <= maxDriveSlopeAngle ? Color.green : Color.red;
            Gizmos.DrawLine(lastFrontSample + Vector3.up * 0.02f, lastBackSample + Vector3.up * 0.02f);
        }

        // DrawAutoSteerGizmos(); // Auto-steer temporarily disabled for build stabilization.
    }

#if false // Auto-steer temporarily disabled for build stabilization.
    private void DrawAutoSteerGizmos()
    {
        if (!showAutoSteerGizmos || !autoSteerDebugActive)
            return;

        Vector3 direction = autoSteerDebugDirection;
        if (direction.sqrMagnitude < 0.0001f)
            return;
        direction.Normalize();

        Vector3 origin = autoSteerDebugOrigin == Vector3.zero ? rb.worldCenterOfMass : autoSteerDebugOrigin;
        float lookAhead = Mathf.Max(0.05f, autoSteerDebugLookAhead);
        Vector3 end = origin + direction * lookAhead;

        Color pathColor = Color.cyan;
        if (autoSteerDebugSuppressedByPlayer)
            pathColor = Color.magenta;
        else if (autoSteerDebugSuppressedBySpeed)
            pathColor = Color.gray;

        Gizmos.color = pathColor;
        Gizmos.DrawLine(origin, end);
        Gizmos.DrawWireSphere(origin, autoSteerProbeRadius);
        Gizmos.DrawWireSphere(end, autoSteerProbeRadius * 0.65f);

        if (!autoSteerDebugHasWallHit)
            return;

        float markerSize = Mathf.Lerp(debugSphereSize * 1.4f, debugSphereSize * 2.8f, autoSteerDebugDanger);
        Gizmos.color = new Color(1f, 0.45f, 0.2f, 1f);
        Gizmos.DrawSphere(autoSteerDebugWallPoint, markerSize);

        Vector3 wallNormal = autoSteerDebugWallNormal;
        if (wallNormal.sqrMagnitude > 0.0001f)
        {
            wallNormal.Normalize();
            Gizmos.DrawLine(autoSteerDebugWallPoint, autoSteerDebugWallPoint + wallNormal * 1.2f);
        }

        if (Mathf.Abs(autoSteerDebugSteerSign) <= 0.001f)
            return;

        Vector3 flatNormal = autoSteerDebugWallNormal;
        flatNormal.y = 0f;
        if (flatNormal.sqrMagnitude < 0.0001f)
            return;
        flatNormal.Normalize();

        Vector3 tangent = Vector3.Cross(flatNormal, Vector3.up).normalized * Mathf.Sign(autoSteerDebugSteerSign);
        float turnLength = Mathf.Lerp(0.8f, 1.6f, autoSteerDebugDanger);
        Gizmos.color = autoSteerDebugSteerSign < 0f ? Color.green : Color.yellow;
        Gizmos.DrawLine(autoSteerDebugWallPoint, autoSteerDebugWallPoint + tangent * turnLength);
    }
#endif
}
