using Action = System.Action;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class CarController : MonoBehaviour, IDamageable
{
    private const float EffectiveRamMinSpeed = 12f;
    private const int DefaultWheelCount = 4;

    [Header("Movement")]
    [SerializeField] private float accelerationForce = 42f;
    [SerializeField] private float maxSpeed = 32f;
    [SerializeField] private float turnSpeed = 150f;
    [SerializeField] private float reverseMaxSpeed = 8f;
    [SerializeField] private float frontDriveBias = 0.35f;
    [SerializeField] private float maxSteerAngle = 30f;
    [SerializeField] private float highSpeedSteerAngle = 14f;
    [SerializeField] private float steerResponse = 8f;
    [SerializeField] private float yawAssist = 4.8f;

    [Header("Boost")]
    [SerializeField, HideInInspector] private float driftBoostAmount = 12f;
    [SerializeField] private float driftBoostDuration = 1.2f;
    [SerializeField] private int maxBoostStacks = 3;
    [SerializeField] private float boostSpeedPerStack = 12f;
    [SerializeField] private float boostImpulsePerStack = 10f;
    [SerializeField] private float boostAccelerationMultiplier = 1.45f;
    [SerializeField, HideInInspector] private float minBoostStackDuration = 0.2f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 9f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Stability")]
    [SerializeField, HideInInspector] private Vector3 centerOfMassOffset = new Vector3(0f, 0f, -0.08f);
    [SerializeField, HideInInspector] private float groundAngularDamping = 2.5f;
    [SerializeField, HideInInspector] private float airAngularDamping = 0.65f;
    [SerializeField, HideInInspector] private float rampPitchDamping = 0.9f;
    [SerializeField, HideInInspector] private float groundedDownforce = 4f;
    [SerializeField] private float groundUprightStrength = 16f;
    [SerializeField] private float groundUprightDamping = 3.5f;
    [SerializeField] private float airUprightStrength = 3.5f;
    [SerializeField] private float airUprightDamping = 0.9f;
    [SerializeField] private float antiRollStrength = 6500f;

    [Header("Gravity / Airtime")]
    [SerializeField, HideInInspector] private float airGravityMultiplier = 0.85f;
    [SerializeField, HideInInspector] private float fallGravityMultiplier = 1.15f;

    [Header("Drift")]
    [SerializeField, HideInInspector] private float driftSideForce = 10f;
    [SerializeField, HideInInspector] private float driftGripRecovery = 4f;
    [SerializeField] private float minDriftSpeed = 5f;
    [SerializeField] private float driftSteerMultiplier = 1.05f;
    [SerializeField, HideInInspector] private float lowSpeedDriftSideForceMultiplier = 1.15f;
    [SerializeField, HideInInspector] private float minDriftYawFactor = 0.2f;
    [SerializeField, HideInInspector] private float driftLateralGripMultiplier = 0.95f;
    [SerializeField, HideInInspector] private float driftFrontGripMultiplier = 1f;
    [SerializeField, HideInInspector] private float driftRearGripMultiplier = 0.65f;
    [SerializeField, HideInInspector] private float driftYawTorqueMultiplier = 0.9f;
    [SerializeField] private float driftChargeTime = 1f;
    [SerializeField] private float maxDriftBoost = 12f;

    [Header("Legacy Drift Tuning")]
    [SerializeField, HideInInspector] private float driftYawTorque = 4.5f;
    [SerializeField, HideInInspector] private float lateralSlipThreshold = 3.5f;

    [Header("Runtime Particles")]
    [SerializeField] private bool enableRuntimeParticles = true;
    [SerializeField] private Vector3 particleAnchorOffset = new Vector3(0f, 0.2f, -1.35f);
    [SerializeField] private float drivingParticleMinSpeed = 2f;
    [SerializeField] private float drivingParticleMaxRate = 34f;
    [SerializeField] private float driftSparkleMaxRate = 24f;
    [SerializeField] private Color drivingDustColor = new Color(0.78f, 0.73f, 0.64f, 0.62f);
    [SerializeField] private Color boostDustColor = new Color(1f, 0.83f, 0.3f, 0.72f);

    [Header("Audio")]
    [SerializeField] private AudioClip boostEnterSFX;
    [SerializeField] private float boostEnterVolume = 1f;
    [SerializeField] private AudioClip dashSFX;
    [SerializeField] private float dashVolume = 1f;
    [SerializeField] private AudioClip engineIdleLoopSFX;
    [SerializeField] private float engineIdleVolume = 0.65f;
    [SerializeField] private float engineIdlePitch = 1f;
    [SerializeField] private AudioClip engineLoopSFX;
    [SerializeField] private float engineLoopVolume = 0.9f;
    [SerializeField] private float engineMinPitch = 0.8f;
    [SerializeField] private float engineMaxPitch = 1.5f;
    [SerializeField] private float engineRunningFullVolumeSpeed = 7f;
    [SerializeField] private float engineAudioFadeSpeed = 6f;
    [SerializeField] private AudioClip jumpSFX;
    [SerializeField] private float jumpVolume = 1f;
    [SerializeField] private AudioClip terrainThumpSFX;
    [SerializeField] private float terrainThumpVolume = 1f;
    [SerializeField] private float terrainCollisionSoundFullVolumeImpact = 22f;
    [SerializeField] private float terrainCollisionSoundMinVolumeScale = 0.35f;
    [SerializeField] private float terrainCollisionSoundMaxVolumeScale = 2f;
    [SerializeField] private AudioClip wheelRollingLoopSFX;
    [SerializeField] private float wheelRollingVolume = 0.8f;
    [SerializeField] private float wheelRollingMinSpeed = 1.5f;
    [SerializeField] private float wheelRollingMinPitch = 0.8f;
    [SerializeField] private float wheelRollingMaxPitch = 1.3f;
    [SerializeField] private float audioSpatialBlend = 1f;
    [SerializeField] private float audioMinDistance = 1f;
    [SerializeField] private float audioMaxDistance = 24f;
    [SerializeField] private float engineInputThreshold = 0.05f;
    [SerializeField] private float terrainCollisionSoundMinImpact = 4f;
    [SerializeField] private float terrainCollisionSoundCooldown = 0.18f;

    [Header("Air Dash")]
    [SerializeField] private float airDashForce = 28f;
    [SerializeField, HideInInspector] private float airDashUpForce = 4f;
    [SerializeField, HideInInspector] private float airDashCooldown = 0.15f;
    [SerializeField, HideInInspector] private float airDashForwardCarry = 0.35f;

    [Header("Suspension Geometry")]
    [Tooltip("Stable wheel rest positions in the player's local space. These do not depend on collider size.")]
    [SerializeField] private Vector3[] wheelLocalPositions =
    {
        new Vector3(-0.9f, 0.01f, 1.6f),
        new Vector3(0.96f, 0.01f, 1.6f),
        new Vector3(-0.9f, 0.01f, -1.619f),
        new Vector3(0.96f, 0.01f, -1.62f)
    };
    [Tooltip("Optional legacy/reference wheel transforms. Physics only uses them as a fallback if local wheel points are missing.")]
    [SerializeField] private Transform[] wheelTransforms;

    [Header("Suspension Tuning")]
    [Tooltip("Spring rest length from the wheel anchor to the wheel center.")]
    [SerializeField] private float suspensionDistance = 0.9f;
    [Tooltip("Spring force per meter of compression.")]
    [SerializeField] private float suspensionStiffness = 32000f;
    [Tooltip("Damping applied along the spring direction.")]
    [SerializeField] private float suspensionDamping = 4500f;
    [Tooltip("Maximum upward force a single wheel spring can apply.")]
    [SerializeField] private float suspensionMaxForcePerWheel = 22000f;
    [Tooltip("0 uses car up for spring force, 1 uses ground normal.")]
    [SerializeField] private float suspensionNormalBlend = 0.65f;
    [Tooltip("Extra ray length below the wheel so suspension probes visibly reach past the car bottom.")]
    [SerializeField] private float suspensionProbeSlack = 0.25f;

    [Header("Wheel Visuals")]
    [SerializeField] private Transform[] wheelVisuals;
    [SerializeField] private float wheelVisualRadius = 0.47f;
    [SerializeField] private Vector3 wheelSpinAxis = Vector3.right;
    [SerializeField] private float wheelSpinSpeedMultiplier = 1f;
    [SerializeField] private bool spinOnlyWhenGrounded = true;

    [Header("Tires")]
    [SerializeField, HideInInspector] private float tireGrip = 28f;
    [SerializeField, HideInInspector] private AnimationCurve gripCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.65f);
    [SerializeField, HideInInspector] private float frontTireGrip = 1.05f;
    [SerializeField, HideInInspector] private float rearTireGrip = 1f;
    [SerializeField, HideInInspector] private float tireGripSpeedFalloff = 0.2f;
    [SerializeField] private float maxLateralAcceleration = 42f;

    [Header("Rolling Resistance / Braking")]
    [SerializeField, HideInInspector] private float frontRollingResistance = 0.45f;
    [SerializeField, HideInInspector] private float rearRollingResistance = 0.38f;
    [SerializeField, HideInInspector] private float frontCoastDrag = 0.55f;
    [SerializeField, HideInInspector] private float rearCoastDrag = 0.45f;
    [SerializeField, HideInInspector] private float brakeForce = 38f;
    [SerializeField, HideInInspector] private float driftRearRollingResistanceMultiplier = 0.45f;

    [Header("Hill / Slope")]
    [SerializeField, HideInInspector] private float downhillAcceleration = 8f;
    [SerializeField] private float maxDriveSlopeAngle = 72f;
    [SerializeField, HideInInspector] private float steepSlopeSlideForce = 12f;
    [SerializeField, HideInInspector] private float leaveGroundForwardBoost = 2f;
    [SerializeField, HideInInspector] private float leaveGroundUpBoost = 3.5f;
    [SerializeField, HideInInspector] private float boostLeaveGroundUpBoost = 5.5f;
    [SerializeField, HideInInspector] private float rampClimbAssist = 9f;
    [SerializeField, HideInInspector] private float boostRampClimbAssist = 16f;
    [SerializeField, HideInInspector] private float rampClimbAssistMinSlope = 8f;
    [SerializeField, HideInInspector] private float slopeSampleDistance = 1f;
    [SerializeField, HideInInspector] private float minSlopeAngleToAffect = 1f;

    [Header("Debug")]
    [SerializeField, HideInInspector] private bool showSuspensionRays = true;
    [SerializeField, HideInInspector] private bool showSurfaceNormals = true;
    [Tooltip("Draw wheel mount, contact, spring, lateral grip, drive, and rolling resistance gizmos in the Scene view.")]
    [SerializeField] private bool showWheelPhysicsGizmos = true;
    [Tooltip("Radius used for wheel wireframe gizmos.")]
    [SerializeField] private float wheelGizmoRadius = 0.28f;
    [Tooltip("Scene-view vector scale for per-wheel acceleration/force arrows.")]
    [SerializeField] private float forceGizmoScale = 0.035f;
    [Tooltip("Wheel gizmo color when the suspension ray is grounded.")]
    [SerializeField] private Color wheelContactColor = new Color(0.2f, 0.9f, 1f, 1f);
    [Tooltip("Wheel gizmo color when the suspension ray is not grounded.")]
    [SerializeField] private Color wheelAirColor = new Color(0.45f, 0.45f, 0.45f, 1f);
    [Tooltip("Spring force arrow color.")]
    [SerializeField] private Color springForceColor = new Color(0.2f, 1f, 0.25f, 1f);
    [Tooltip("Lateral tire force arrow color.")]
    [SerializeField] private Color lateralForceColor = new Color(1f, 0.25f, 0.9f, 1f);
    [Tooltip("Drive/brake force arrow color.")]
    [SerializeField] private Color driveForceColor = new Color(1f, 0.75f, 0.15f, 1f);
    [Tooltip("Wheel forward direction arrow color.")]
    [SerializeField] private Color wheelForwardColor = new Color(0.15f, 0.55f, 1f, 1f);
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
    [SerializeField, HideInInspector] private bool logDamageEvents;

    [Header("Checkpoint Respawn")]
    [SerializeField] private bool respawnWhenBelowOutOfBoundsY = true;
    [SerializeField] private float outOfBoundsY = 100f;
    [SerializeField] private float outOfBoundsRespawnDamage = 10f;

    [Header("Airtime Trick")]
    [SerializeField] private bool enableAirtimeTrick = true;
    [SerializeField] private float trickMinAirTime = 0.8f;
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

    [Header("Vehicle Dimensions & Mass")]
    [SerializeField] private float vehicleMass = 900f;
    [SerializeField] private float wheelBase = 3.22f;
    [SerializeField] private float trackWidth = 1.86f;
    [SerializeField] private float comHeight = 0.55f;
    [Range(0f, 1f)] [SerializeField] private float frontWeightRatio = 0.52f;
    [SerializeField] private bool autoCalculatePhysics;

    [Header("Legacy Auto-Calc Scale Knobs")]
    [SerializeField] private float suspensionStiffnessScale = 1f;
    [SerializeField] private float suspensionDampingScale = 1f;
    [SerializeField] private float gripScale = 1f;
    [SerializeField] private float rollingResistanceScale = 1f;

    struct WheelState
    {
        public bool grounded;
        public bool isFront;
        public bool isLeft;
        public Vector3 localPosition;
        public Vector3 wheelWorldPosition;
        public Vector3 rayOrigin;
        public Vector3 hitPoint;
        public Vector3 normal;
        public Vector3 springDirection;
        public Vector3 wheelForward;
        public Vector3 wheelRight;
        public float compression;
        public float springLength;
        public float springForce;
        public float forwardSpeed;
        public float lateralSpeed;
        public float lateralAcceleration;
        public float driveAcceleration;
        public float rollingAcceleration;
    }

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
    private bool maxBoostStackLockout;
    private float driftCharge;
    private float driftTimer;
    private float currentSteerAngle;

    private bool airDashUsed;
    private float airDashTimer;
    private RaycastHit groundHit;
    private float slopeAngle;
    private float lastSampledSlopeAngle;
    private Vector3 lastFrontSample = Vector3.zero;
    private Vector3 lastBackSample = Vector3.zero;
    private bool lastSampleHadHits;

    private readonly Dictionary<int, float> recentEnemyImpactTimeById = new Dictionary<int, float>();
    private float currentHealth;
    private bool isDead;
    private bool noClipActive;
    private bool noClipAscendInput;
    private bool noClipDescendInput;
    private float regenPausedUntil;
    private bool isRegenerating;
    private Vector3 checkpointRespawnPosition;
    private Quaternion checkpointRespawnRotation = Quaternion.identity;
    private bool hasCheckpointRespawnPose;
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
    private AudioSource vehicleOneShotSource;
    private AudioSource engineIdleLoopSource;
    private AudioSource engineLoopSource;
    private AudioSource wheelRollingLoopSource;

    private WheelState[] wheelStates;
    private float[] wheelSpinAngles;
    private Quaternion[] wheelVisualBaseRotations;
    private Transform[] wheelVisualCacheRefs;
    private int frontGroundedCount;
    private int rearGroundedCount;
    private int groundedWheelCount;
    private Vector3 averageGroundNormal = Vector3.up;

    private float lastBoostWallBounceTime = -999f;
    private float terrainThumpPlayableAt = -999f;

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
    public float SpeedRatio => Mathf.Clamp01(CurrentPlanarSpeed() / Mathf.Max(0.01f, GetMaxSpeedAtFullBoostStacks()));
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
        EnsureWheelLocalPositions();
        EnsureWheelStateCache();
        ApplyRigidbodySettings();
        EnsureControls();

        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = maxHealth;
        SetCheckpointRespawnPose(transform.position, transform.rotation);
        SetupRamHitbox();
        UpdateRamHitboxSize();
        SetupRuntimeParticles();
        SetupAudioSources();
    }

    private void Reset()
    {
        wheelBase = 3.22f;
        trackWidth = 1.86f;
        wheelLocalPositions = CreateDefaultWheelLocalPositions();
        vehicleMass = 900f;
        suspensionDistance = 0.9f;
        suspensionStiffness = 32000f;
        suspensionDamping = 4500f;
        tireGrip = 28f;
        frontRollingResistance = 0.45f;
        rearRollingResistance = 0.38f;
        frontCoastDrag = 0.55f;
        rearCoastDrag = 0.45f;
        airGravityMultiplier = 0.85f;
        fallGravityMultiplier = 1.15f;
    }

    private void OnValidate()
    {
        ClampInspectorValues();
        EnsureWheelLocalPositions();
    }

    private void OnEnable()
    {
        EnsureControls();
        controls.Player.Enable();
    }

    private void OnDisable()
    {
        if (noClipActive)
            SetNoClipActive(false);

        noClipAscendInput = false;
        noClipDescendInput = false;
        currentBoostStacks = 0;
        boostTimer = 0f;
        boostActive = false;
        maxBoostStackLockout = false;
        EndDrift(false);
        StopRuntimeParticles();
        StopAllVehicleAudio();

        if (controls != null)
            controls.Player.Disable();
    }

    private void OnDestroy()
    {
        if (controls != null)
            controls.Dispose();
    }

    private void FixedUpdate()
    {
        if (GameState.NoClip != noClipActive)
            SetNoClipActive(GameState.NoClip);

        HandlePassiveRegen();

        if (noClipActive)
        {
            HandleNoClipMovement();
            StopAllVehicleAudio();
            return;
        }

        if (HandleOutOfBoundsRespawn())
            return;

        wasGrounded = isGrounded;
        SampleWheelContacts();
        slopeAngle = isGrounded ? Vector3.Angle(averageGroundNormal, Vector3.up) : 0f;

        HandleAirtimeTrick();

        rb.angularDamping = isGrounded ? GetEffectiveGroundAngularDamping() : GetEffectiveAirAngularDamping();

        if (isGrounded)
            airDashUsed = false;

        if (wasGrounded && !isGrounded)
        {
            rb.AddForce(ProjectOnPlaneSafe(transform.forward, Vector3.up) * GetEffectiveLeaveGroundBoost(), ForceMode.VelocityChange);
            rb.AddForce(Vector3.up * GetEffectiveLeaveGroundUpBoost(), ForceMode.VelocityChange);
        }

        UpdateDriftState();
        HandleBoost();

        if (alwaysBoostDebug)
        {
            while (currentBoostStacks < GetBoostStackCap())
                TryAddBoostStack();
        }

        UpdateRamHitboxSize();
        UpdateRuntimeParticles();

        if (isGrounded)
        {
            ApplySuspensionAndTireForces();
            ApplyAntiRoll();
            ApplyGroundStability();
            ApplySlopeForces();
            ApplyRampClimbAssist();
            ApplySteepSlopeSlide();
            DampRampPitch();
        }
        else
        {
            ApplyAirGravity();
            ApplyAirUpright();
        }

        ApplySteeringYawAssist();
        ClampPlanarSpeed();
        UpdateWheelVisuals();
        UpdateVehicleAudio();
    }

    void EnsureControls()
    {
        if (controls != null)
            return;

        controls = new PlayerInputActions();
        controls.Player.Move.performed += c => moveInput = c.ReadValue<Vector2>();
        controls.Player.Move.canceled += _ => moveInput = Vector2.zero;
        controls.Player.Jump.started += _ => noClipAscendInput = true;
        controls.Player.Jump.canceled += _ => noClipAscendInput = false;
        controls.Player.Jump.performed += _ => OnJumpPerformed();
        controls.Player.Dash.started += _ => noClipDescendInput = true;
        controls.Player.Dash.canceled += _ => noClipDescendInput = false;
        controls.Player.Dash.performed += _ => OnDashPerformed();
    }

    void ApplyRigidbodySettings()
    {
        if (rb == null)
            return;

        rb.mass = GetEffectiveVehicleMass();
        float localGroundY = GetAverageWheelLocalY() - GetSuspensionWheelRadius();
        float localCenterZ = GetSuspensionCenterLocalZ();
        float wheelbase = GetEffectiveWheelBase();
        rb.centerOfMass = new Vector3(
            centerOfMassOffset.x,
            localGroundY + Mathf.Max(0.05f, comHeight) + centerOfMassOffset.y,
            localCenterZ + (frontWeightRatio - 0.5f) * wheelbase + centerOfMassOffset.z);
        rb.useGravity = true;
        rb.linearDamping = 0.08f;
        rb.angularDamping = GetEffectiveGroundAngularDamping();
        rb.maxAngularVelocity = 50f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    void ClampInspectorValues()
    {
        maxSpeed = Mathf.Max(1f, maxSpeed);
        accelerationForce = Mathf.Max(0f, accelerationForce);
        reverseMaxSpeed = Mathf.Max(1f, reverseMaxSpeed);
        frontDriveBias = Mathf.Clamp01(frontDriveBias);
        maxSteerAngle = Mathf.Clamp(maxSteerAngle, 1f, 60f);
        highSpeedSteerAngle = Mathf.Clamp(highSpeedSteerAngle, 1f, maxSteerAngle);
        steerResponse = Mathf.Max(0.1f, steerResponse);
        yawAssist = Mathf.Max(0f, yawAssist);

        maxBoostStacks = maxBoostStacks <= 1 ? 3 : Mathf.Clamp(maxBoostStacks, 1, 3);
        boostSpeedPerStack = Mathf.Max(0f, boostSpeedPerStack);
        boostImpulsePerStack = Mathf.Max(0f, boostImpulsePerStack);
        boostAccelerationMultiplier = Mathf.Max(1f, boostAccelerationMultiplier);
        minBoostStackDuration = Mathf.Clamp(minBoostStackDuration, 0.01f, 5f);
        driftBoostDuration = Mathf.Max(minBoostStackDuration, driftBoostDuration);

        jumpForce = Mathf.Max(0f, jumpForce);
        groundAngularDamping = Mathf.Max(0f, groundAngularDamping);
        airAngularDamping = Mathf.Max(0f, airAngularDamping);
        rampPitchDamping = Mathf.Clamp01(rampPitchDamping);
        groundedDownforce = Mathf.Max(0f, groundedDownforce);
        groundUprightStrength = Mathf.Max(0f, groundUprightStrength);
        groundUprightDamping = Mathf.Max(0f, groundUprightDamping);
        airUprightStrength = Mathf.Max(0f, airUprightStrength);
        airUprightDamping = Mathf.Max(0f, airUprightDamping);
        antiRollStrength = Mathf.Max(0f, antiRollStrength);
        airGravityMultiplier = Mathf.Clamp(airGravityMultiplier, 0.2f, 2.5f);
        fallGravityMultiplier = Mathf.Clamp(fallGravityMultiplier, 0.2f, 3f);

        driftSideForce = Mathf.Max(0f, driftSideForce);
        driftGripRecovery = Mathf.Max(0f, driftGripRecovery);
        minDriftSpeed = Mathf.Max(0f, minDriftSpeed);
        driftSteerMultiplier = Mathf.Max(0.1f, driftSteerMultiplier);
        lowSpeedDriftSideForceMultiplier = Mathf.Max(1f, lowSpeedDriftSideForceMultiplier);
        minDriftYawFactor = Mathf.Clamp01(minDriftYawFactor);
        driftLateralGripMultiplier = Mathf.Clamp(driftLateralGripMultiplier, 0.05f, 2f);
        driftFrontGripMultiplier = Mathf.Clamp(driftFrontGripMultiplier, 0.05f, 2f);
        driftRearGripMultiplier = Mathf.Clamp(driftRearGripMultiplier, 0.05f, 2f);
        driftYawTorqueMultiplier = Mathf.Max(0f, driftYawTorqueMultiplier);
        driftChargeTime = Mathf.Max(0f, driftChargeTime);
        maxDriftBoost = Mathf.Max(0f, maxDriftBoost);
        lateralSlipThreshold = Mathf.Max(0.1f, lateralSlipThreshold);

        suspensionDistance = Mathf.Max(0.05f, suspensionDistance);
        suspensionStiffness = Mathf.Max(1f, suspensionStiffness);
        suspensionDamping = Mathf.Max(0f, suspensionDamping);
        suspensionMaxForcePerWheel = Mathf.Max(1f, suspensionMaxForcePerWheel);
        suspensionNormalBlend = Mathf.Clamp01(suspensionNormalBlend);
        suspensionProbeSlack = Mathf.Max(0f, suspensionProbeSlack);
        wheelVisualRadius = Mathf.Max(0.01f, wheelVisualRadius);
        wheelSpinSpeedMultiplier = Mathf.Max(0f, wheelSpinSpeedMultiplier);
        if (wheelSpinAxis.sqrMagnitude < 0.0001f)
            wheelSpinAxis = Vector3.right;
        wheelGizmoRadius = Mathf.Max(0.01f, wheelGizmoRadius);
        forceGizmoScale = Mathf.Max(0.001f, forceGizmoScale);

        tireGrip = Mathf.Max(0.1f, tireGrip);
        frontTireGrip = Mathf.Max(0.01f, frontTireGrip);
        rearTireGrip = Mathf.Max(0.01f, rearTireGrip);
        tireGripSpeedFalloff = Mathf.Max(0f, tireGripSpeedFalloff);
        maxLateralAcceleration = Mathf.Max(1f, maxLateralAcceleration);
        frontRollingResistance = Mathf.Max(0f, frontRollingResistance);
        rearRollingResistance = Mathf.Max(0f, rearRollingResistance);
        frontCoastDrag = Mathf.Max(0f, frontCoastDrag);
        rearCoastDrag = Mathf.Max(0f, rearCoastDrag);
        brakeForce = Mathf.Max(0f, brakeForce);
        driftRearRollingResistanceMultiplier = Mathf.Clamp01(driftRearRollingResistanceMultiplier);

        downhillAcceleration = Mathf.Max(0f, downhillAcceleration);
        maxDriveSlopeAngle = Mathf.Clamp(maxDriveSlopeAngle, 1f, 89f);
        steepSlopeSlideForce = Mathf.Max(0f, steepSlopeSlideForce);
        leaveGroundForwardBoost = Mathf.Max(0f, leaveGroundForwardBoost);
        leaveGroundUpBoost = Mathf.Max(0f, leaveGroundUpBoost);
        boostLeaveGroundUpBoost = Mathf.Max(0f, boostLeaveGroundUpBoost);
        rampClimbAssist = Mathf.Max(0f, rampClimbAssist);
        boostRampClimbAssist = Mathf.Max(0f, boostRampClimbAssist);
        rampClimbAssistMinSlope = Mathf.Max(0f, rampClimbAssistMinSlope);
        slopeSampleDistance = Mathf.Max(0.1f, slopeSampleDistance);
        minSlopeAngleToAffect = Mathf.Max(0f, minSlopeAngleToAffect);

        boostEnterVolume = Mathf.Clamp01(boostEnterVolume);
        dashVolume = Mathf.Clamp01(dashVolume);
        jumpVolume = Mathf.Clamp01(jumpVolume);
        terrainThumpVolume = Mathf.Clamp01(terrainThumpVolume);
        terrainCollisionSoundMinImpact = Mathf.Max(0f, terrainCollisionSoundMinImpact);
        terrainCollisionSoundFullVolumeImpact = Mathf.Max(terrainCollisionSoundMinImpact + 0.01f, terrainCollisionSoundFullVolumeImpact);
        terrainCollisionSoundMinVolumeScale = Mathf.Clamp(terrainCollisionSoundMinVolumeScale, 0f, 2f);
        terrainCollisionSoundMaxVolumeScale = Mathf.Clamp(terrainCollisionSoundMaxVolumeScale, terrainCollisionSoundMinVolumeScale, 2f);
        engineIdleVolume = Mathf.Clamp01(engineIdleVolume);
        engineLoopVolume = Mathf.Clamp01(engineLoopVolume);
        wheelRollingVolume = Mathf.Clamp01(wheelRollingVolume);
        engineIdlePitch = Mathf.Clamp(engineIdlePitch, 0.1f, 3f);
        engineMinPitch = Mathf.Clamp(engineMinPitch, 0.1f, 3f);
        engineMaxPitch = Mathf.Clamp(engineMaxPitch, engineMinPitch, 3f);
        engineRunningFullVolumeSpeed = Mathf.Max(0.01f, engineRunningFullVolumeSpeed);
        engineAudioFadeSpeed = Mathf.Max(0f, engineAudioFadeSpeed);
        wheelRollingMinSpeed = Mathf.Max(0f, wheelRollingMinSpeed);
        wheelRollingMinPitch = Mathf.Clamp(wheelRollingMinPitch, 0.1f, 3f);
        wheelRollingMaxPitch = Mathf.Clamp(wheelRollingMaxPitch, wheelRollingMinPitch, 3f);
        audioSpatialBlend = Mathf.Clamp01(audioSpatialBlend);
        audioMinDistance = Mathf.Max(0.01f, audioMinDistance);
        audioMaxDistance = Mathf.Max(audioMinDistance, audioMaxDistance);
        engineInputThreshold = Mathf.Clamp01(engineInputThreshold);
        terrainCollisionSoundCooldown = Mathf.Max(0f, terrainCollisionSoundCooldown);

        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = Application.isPlaying ? Mathf.Clamp(currentHealth, 0f, maxHealth) : maxHealth;
        regenDelaySeconds = Mathf.Max(0f, regenDelaySeconds);
        regenPerSecond = Mathf.Max(0f, regenPerSecond);
        outOfBoundsY = Mathf.Max(0f, outOfBoundsY);
        outOfBoundsRespawnDamage = Mathf.Max(0f, outOfBoundsRespawnDamage);
        trickMinAirTime = Mathf.Max(0.01f, trickMinAirTime);
        trickLandingGraceSeconds = Mathf.Max(0.01f, trickLandingGraceSeconds);
        trickBoostReward = Mathf.Max(0f, trickBoostReward);
        trickCooldownSeconds = Mathf.Max(0f, trickCooldownSeconds);

        collisionPushCurvePeakSpeed = Mathf.Max(0.01f, collisionPushCurvePeakSpeed);
        collisionSidePushWeight = Mathf.Max(0f, collisionSidePushWeight);
        collisionForwardPushWeight = Mathf.Max(0f, collisionForwardPushWeight);
        collisionRandomYawDegrees = Mathf.Clamp(collisionRandomYawDegrees, 0f, 90f);
        boostCollisionPushMultiplier = Mathf.Max(1f, boostCollisionPushMultiplier);
        boostCollisionMinPushStrength = Mathf.Max(0f, boostCollisionMinPushStrength);
        collisionImpactCooldown = Mathf.Max(0f, collisionImpactCooldown);
        boostWallBounceSpeedScale = Mathf.Max(0f, boostWallBounceSpeedScale);
        boostWallBounceMinSpeed = Mathf.Max(0f, boostWallBounceMinSpeed);
        boostWallBounceCooldown = Mathf.Max(0f, boostWallBounceCooldown);

        ramHitboxBaseSize.x = Mathf.Max(0.05f, ramHitboxBaseSize.x);
        ramHitboxBaseSize.y = Mathf.Max(0.05f, ramHitboxBaseSize.y);
        ramHitboxBaseSize.z = Mathf.Max(0.05f, ramHitboxBaseSize.z);
        ramHitboxMaxWidth = Mathf.Max(ramHitboxBaseSize.x, ramHitboxMaxWidth);
        ramHitboxMaxForwardExtension = Mathf.Max(0f, ramHitboxMaxForwardExtension);
        ramHitboxForwardExtensionPerExtraSpeed = Mathf.Max(0f, ramHitboxForwardExtensionPerExtraSpeed);
        nonBoostRamForwardSpeedLossPercent = Mathf.Clamp01(nonBoostRamForwardSpeedLossPercent);
        nonBoostRamForwardSpeedLossMax = Mathf.Max(0f, nonBoostRamForwardSpeedLossMax);

        vehicleMass = Mathf.Max(1f, vehicleMass);
        wheelBase = Mathf.Max(0.5f, wheelBase);
        trackWidth = Mathf.Max(0.5f, trackWidth);
        comHeight = Mathf.Max(0.05f, comHeight);
        frontWeightRatio = Mathf.Clamp01(frontWeightRatio);
        suspensionStiffnessScale = Mathf.Max(0.01f, suspensionStiffnessScale);
        suspensionDampingScale = Mathf.Max(0.01f, suspensionDampingScale);
        gripScale = Mathf.Max(0.01f, gripScale);
        rollingResistanceScale = Mathf.Max(0.01f, rollingResistanceScale);
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
        Debug.Log(name + " died.", this);
        RespawnAtCheckpoint(0f, restoreFullHealth: true, clampDamageToOneHealth: false);
    }

    bool HandleOutOfBoundsRespawn()
    {
        if (!respawnWhenBelowOutOfBoundsY || isDead)
            return false;
        if (transform.position.y >= outOfBoundsY)
            return false;
        if (hasCheckpointRespawnPose && checkpointRespawnPosition.y < outOfBoundsY)
            return false;

        RespawnAtCheckpoint(outOfBoundsRespawnDamage, restoreFullHealth: false, clampDamageToOneHealth: true);
        return true;
    }

    public void SetCheckpointRespawnPose(Vector3 position, Quaternion rotation)
    {
        checkpointRespawnPosition = position;
        checkpointRespawnRotation = rotation;
        hasCheckpointRespawnPose = true;
    }

    public void RespawnAtCheckpoint(float damage = 0f, bool restoreFullHealth = false, bool clampDamageToOneHealth = false)
    {
        if (!hasCheckpointRespawnPose)
            SetCheckpointRespawnPose(transform.position, transform.rotation);

        ResetForRespawn();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = checkpointRespawnPosition;
            rb.rotation = checkpointRespawnRotation;
        }
        else
        {
            transform.SetPositionAndRotation(checkpointRespawnPosition, checkpointRespawnRotation);
        }

        if (restoreFullHealth)
            currentHealth = maxHealth;
        else
            ApplyRespawnDamage(damage, clampDamageToOneHealth);

        isDead = false;
        isRegenerating = false;
    }

    void ApplyRespawnDamage(float damage, bool clampDamageToOneHealth)
    {
        if (damage <= 0f || GameState.GodMode)
            return;

        float minimumHealth = clampDamageToOneHealth ? 1f : 0f;
        currentHealth = Mathf.Max(minimumHealth, currentHealth - damage);
        regenPausedUntil = Time.time + regenDelaySeconds;
    }

    void ResetForRespawn()
    {
        EndDrift(false);
        trickInAir = false;
        trickCandidateReady = false;
        trickAirTimer = 0f;
        lastTrickSucceeded = false;
        airDashUsed = false;
        airDashTimer = 0f;
        currentSteerAngle = 0f;
        currentBoostStacks = 0;
        boostTimer = 0f;
        boostActive = false;
        maxBoostStackLockout = false;
        StopRuntimeParticles();
        StopAllVehicleAudio();
    }

    void OnJumpPerformed()
    {
        if (!noClipActive && Jump())
        {
            PlayVehicleOneShot(jumpSFX, jumpVolume);
            JumpPerformed?.Invoke();
        }
    }

    void OnDashPerformed()
    {
        if (!noClipActive && !isGrounded)
        {
            TryAirDash();
            PlayVehicleOneShot(dashSFX, dashVolume);
            DashPerformed?.Invoke();
        }
    }

    void HandlePassiveRegen()
    {
        isRegenerating = false;
        if (!enablePassiveRegen || isDead || GameState.IsGameOver)
            return;
        if (currentHealth >= maxHealth)
            return;
        if (Time.time < regenPausedUntil)
            return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + regenPerSecond * Time.fixedDeltaTime);
        isRegenerating = currentHealth < maxHealth;
    }

    void HandleAirtimeTrick()
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

        if (trickCooldownTimer > 0f)
            trickCooldownTimer = Mathf.Max(0f, trickCooldownTimer - Time.fixedDeltaTime);

        if (!isGrounded)
        {
            if (!trickInAir)
            {
                trickInAir = true;
                trickCandidateReady = false;
                trickAirTimer = 0f;
            }

            trickAirTimer += Time.fixedDeltaTime;
            trickCandidateReady = trickAirTimer >= trickMinAirTime;
            return;
        }

        if (!trickInAir)
            return;

        if (trickCandidateReady && trickCooldownTimer <= 0f)
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

    void ApplyTrickBoostReward()
    {
        if (trickBoostReward <= 0f)
            return;

        ApplyBaseDriftBoost();
    }

    void ApplyBaseDriftBoost()
    {
        if (IsMaxBoostStackLockoutActive())
            return;

        driftBoostAmount = boostSpeedPerStack;
        bool wasBoostActive = boostActive && currentBoostStacks > 0 && boostTimer > 0f;
        int previousBoostStacks = currentBoostStacks;
        currentBoostStacks = Mathf.Max(1, currentBoostStacks);
        boostTimer = GetConfiguredBoostStackDuration();
        boostActive = true;
        if (currentBoostStacks >= GetBoostStackCap())
            maxBoostStackLockout = true;

        ApplyBoostImpulse(0.7f);

        if (currentBoostStacks != previousBoostStacks)
            BoostStackGained?.Invoke();

        if (!wasBoostActive)
        {
            PlayVehicleOneShot(boostEnterSFX, boostEnterVolume);
            BoostActivated?.Invoke();
        }
    }

    void SetNoClipActive(bool active)
    {
        noClipActive = active;
        noClipAscendInput = false;
        noClipDescendInput = false;
        EndDrift(false);

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

    void HandleNoClipMovement()
    {
        Vector3 planarMove = transform.forward * moveInput.y + transform.right * moveInput.x;
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

    bool TryProbeWheelContact(int wheelIndex, Vector3 springUp, float restLength, float wheelRadius, out Vector3 local, out Vector3 wheelWorld, out Vector3 rayOrigin, out RaycastHit hit, out float springLength)
    {
        local = GetWheelLocalPosition(wheelIndex);
        wheelWorld = GetWheelWorldPosition(wheelIndex, local);
        Vector3 probeUp = springUp.sqrMagnitude > 0.0001f ? springUp.normalized : transform.up;
        rayOrigin = wheelWorld + probeUp * restLength;

        Vector3 springDown = -probeUp;
        float rayLength = restLength + wheelRadius + Mathf.Max(0f, suspensionProbeSlack);
        float sphereRadius = Mathf.Max(0.01f, wheelRadius);

        bool sphereHit = Physics.SphereCast(rayOrigin, sphereRadius, springDown, out hit, rayLength, groundLayer, QueryTriggerInteraction.Ignore);
        bool rayHit = !sphereHit && Physics.Raycast(rayOrigin, springDown, out hit, rayLength, groundLayer, QueryTriggerInteraction.Ignore);

        if (!sphereHit && !rayHit)
        {
            springLength = restLength;
            return false;
        }

        springLength = sphereHit
            ? Mathf.Clamp(hit.distance, 0f, restLength)
            : Mathf.Clamp(hit.distance - wheelRadius, 0f, restLength);
        return true;
    }

    void SampleWheelContacts()
    {
        EnsureWheelStateCache();
        groundedWheelCount = 0;
        frontGroundedCount = 0;
        rearGroundedCount = 0;
        averageGroundNormal = Vector3.zero;

        float restLength = GetSuspensionRestLength();
        float wheelRadius = GetSuspensionWheelRadius();
        Vector3 carUp = transform.up.sqrMagnitude > 0.0001f ? transform.up.normalized : Vector3.up;

        for (int i = 0; i < wheelStates.Length; i++)
        {
            Vector3 local;
            Vector3 wheelWorld;
            Vector3 origin;
            RaycastHit hit;
            float springLength;

            Vector3 wheelUp = carUp;

            bool grounded = TryProbeWheelContact(i, wheelUp, restLength, wheelRadius, out local, out wheelWorld, out origin, out hit, out springLength);

            WheelState state = wheelStates[i];
            state.localPosition = local;
            Vector3 springDirection = wheelUp;
            Vector3 currentWheelWorld = grounded ? origin - springDirection * springLength : wheelWorld;
            state.wheelWorldPosition = currentWheelWorld;
            state.rayOrigin = origin;
            state.isFront = local.z >= GetSuspensionCenterLocalZ();
            state.isLeft = local.x < 0f;
            state.grounded = grounded;
            state.normal = grounded ? hit.normal : Vector3.up;
            state.springDirection = springDirection;
            state.wheelForward = ProjectOnPlaneSafe(transform.forward, grounded ? state.normal : Vector3.up);
            state.wheelRight = ProjectOnPlaneSafe(transform.right, grounded ? state.normal : Vector3.up);
            state.compression = grounded ? Mathf.Clamp01((restLength - springLength) / restLength) : 0f;
            state.springLength = springLength;
            state.hitPoint = grounded ? hit.point : origin - springDirection * (restLength + wheelRadius + Mathf.Max(0f, suspensionProbeSlack));
            state.springForce = 0f;
            state.forwardSpeed = 0f;
            state.lateralSpeed = 0f;
            state.lateralAcceleration = 0f;
            state.driveAcceleration = 0f;
            state.rollingAcceleration = 0f;

            if (grounded)
            {
                groundedWheelCount++;
                if (state.isFront)
                    frontGroundedCount++;
                else
                    rearGroundedCount++;

                float contactWeight = Mathf.Lerp(0.35f, 1f, state.compression);
                averageGroundNormal += hit.normal * contactWeight;
            }

            wheelStates[i] = state;
        }

        isGrounded = groundedWheelCount > 0;
        if (isGrounded)
        {
            averageGroundNormal.Normalize();
            groundHit.normal = averageGroundNormal;
        }
        else
        {
            averageGroundNormal = Vector3.up;
            groundHit.normal = Vector3.zero;
        }
    }

    void ApplySuspensionAndTireForces()
    {
        if (rb == null || wheelStates == null || wheelStates.Length == 0)
            return;

        UpdateSteerAngle();

        float restLength = GetSuspensionRestLength();
        float stiffness = GetEffectiveSuspensionStiffness();
        float damping = GetEffectiveSuspensionDamping();
        float maxSpringForce = GetEffectiveSuspensionMaxForce();
        float planarSpeedRatio = Mathf.Clamp01(CurrentPlanarSpeed() / Mathf.Max(0.01f, GetCurrentAllowedSpeed()));

        for (int i = 0; i < wheelStates.Length; i++)
        {
            WheelState state = wheelStates[i];
            if (!state.grounded)
                continue;

            Vector3 springDir = Vector3.Slerp(transform.up, state.normal, suspensionNormalBlend);
            if (springDir.sqrMagnitude < 0.0001f)
                springDir = state.normal;
            springDir.Normalize();

            Vector3 forcePoint = state.rayOrigin;
            Vector3 pointVelocity = rb.GetPointVelocity(forcePoint);
            float springOffset = restLength - state.springLength;
            float springVelocity = Vector3.Dot(pointVelocity, springDir);
            float springForce = springOffset * stiffness;
            float damperForce = -springVelocity * damping;
            float netSpringForce = Mathf.Clamp(springForce + damperForce, 0f, maxSpringForce);
            rb.AddForceAtPosition(springDir * netSpringForce, forcePoint, ForceMode.Force);
            state.springDirection = springDir;
            state.springForce = netSpringForce;

            Vector3 wheelForward = GetWheelForwardDirection(state);
            Vector3 wheelRight = Vector3.Cross(springDir, wheelForward);
            if (wheelRight.sqrMagnitude < 0.0001f)
                wheelRight = ProjectOnPlaneSafe(transform.right, state.normal);
            else
                wheelRight.Normalize();

            pointVelocity = rb.GetPointVelocity(GetTireForcePoint(state));
            float forwardSpeed = Vector3.Dot(pointVelocity, wheelForward);
            float lateralSpeed = Vector3.Dot(pointVelocity, wheelRight);
            state.wheelForward = wheelForward;
            state.wheelRight = wheelRight;
            state.forwardSpeed = forwardSpeed;
            state.lateralSpeed = lateralSpeed;
            state.lateralAcceleration = ApplyTireGrip(state, wheelRight, lateralSpeed, planarSpeedRatio);
            state.driveAcceleration = ApplyDriveAndBrake(state, wheelForward, forwardSpeed, planarSpeedRatio, out float rollingAcceleration);
            state.rollingAcceleration = rollingAcceleration;

            wheelStates[i] = state;
        }

        if (isGrounded && groundedDownforce > 0f)
            rb.AddForce(-averageGroundNormal * GetEffectiveDownforce(), ForceMode.Acceleration);
    }

    float ApplyTireGrip(WheelState state, Vector3 wheelRight, float lateralSpeed, float planarSpeedRatio)
    {
        float grip = GetEffectiveTireGrip();
        grip *= state.isFront ? Mathf.Max(0.01f, frontTireGrip) : Mathf.Max(0.01f, rearTireGrip);
        grip *= gripCurve != null ? Mathf.Max(0f, gripCurve.Evaluate(planarSpeedRatio)) : 1f;
        grip *= Mathf.Lerp(0.55f, 1f, state.compression);
        grip /= 1f + CurrentPlanarSpeed() * GetEffectiveTireGripSpeedFalloff();

        if (isDrifting)
        {
            grip *= driftLateralGripMultiplier;
            grip *= state.isFront ? driftFrontGripMultiplier : driftRearGripMultiplier;
        }

        int divisor = Mathf.Max(1, groundedWheelCount);
        float lateralAcceleration = Mathf.Clamp(-lateralSpeed * grip / divisor, -maxLateralAcceleration, maxLateralAcceleration);
        rb.AddForceAtPosition(wheelRight * lateralAcceleration, GetTireForcePoint(state), ForceMode.Acceleration);
        return lateralAcceleration;
    }

    float ApplyDriveAndBrake(WheelState state, Vector3 wheelForward, float forwardSpeed, float planarSpeedRatio, out float rollingAcceleration)
    {
        float throttle = Mathf.Clamp(moveInput.y, -1f, 1f);
        float absThrottle = Mathf.Abs(throttle);
        float driveAcceleration = 0f;
        float appliedDriveAcceleration = 0f;
        float allowedSpeed = GetCurrentAllowedSpeed();
        float forwardPlanarSpeed = Vector3.Dot(rb.linearVelocity, ProjectOnPlaneSafe(transform.forward, Vector3.up));

        if (throttle > 0.01f)
        {
            if (forwardPlanarSpeed < allowedSpeed)
            {
                float falloff = Mathf.Lerp(1f, 0.28f, Mathf.Clamp01(forwardPlanarSpeed / Mathf.Max(0.01f, allowedSpeed)));
                driveAcceleration = GetEffectiveAccelerationForce() * throttle * falloff;
                if (boostActive)
                    driveAcceleration *= boostAccelerationMultiplier;
            }
        }
        else if (throttle < -0.01f)
        {
            if (forwardPlanarSpeed > 0.5f)
                driveAcceleration = -GetEffectiveBrakeForce() * absThrottle;
            else if (forwardPlanarSpeed > -reverseMaxSpeed)
                driveAcceleration = GetEffectiveAccelerationForce() * throttle * 0.45f;
        }

        if (Mathf.Abs(driveAcceleration) > 0.001f && CanDriveOnCurrentSlope())
        {
            float driveShare = GetDriveShare(state);
            appliedDriveAcceleration = driveAcceleration * driveShare;
            rb.AddForceAtPosition(wheelForward * appliedDriveAcceleration, GetTireForcePoint(state), ForceMode.Acceleration);
        }

        float rollingResistance = state.isFront ? GetEffectiveFrontRollingResistance() : GetEffectiveRearRollingResistance();
        float coastDrag = state.isFront ? GetEffectiveFrontCoastDrag() : GetEffectiveRearCoastDrag();
        if (isDrifting && !state.isFront)
            rollingResistance *= driftRearRollingResistanceMultiplier;

        float dragAcceleration = -forwardSpeed * rollingResistance;
        if (absThrottle < 0.01f)
            dragAcceleration += -forwardSpeed * coastDrag;

        dragAcceleration /= Mathf.Max(1, groundedWheelCount);
        rb.AddForceAtPosition(wheelForward * dragAcceleration, GetTireForcePoint(state), ForceMode.Acceleration);
        rollingAcceleration = dragAcceleration;
        return appliedDriveAcceleration;
    }

    Vector3 GetTireForcePoint(WheelState state)
    {
        return state.wheelWorldPosition;
    }

    float GetDriveShare(WheelState state)
    {
        float frontBias = Mathf.Clamp01(frontDriveBias);
        if (state.isFront)
            return frontGroundedCount > 0 ? frontBias / frontGroundedCount : 0f;

        float rearBias = 1f - frontBias;
        return rearGroundedCount > 0 ? rearBias / rearGroundedCount : 0f;
    }

    void UpdateSteerAngle()
    {
        float steerInput = GetEffectiveSteerInput();
        float speedRatio = Mathf.Clamp01(CurrentPlanarSpeed() / Mathf.Max(0.01f, GetCurrentAllowedSpeed()));
        float availableSteer = Mathf.Lerp(maxSteerAngle, highSpeedSteerAngle, speedRatio);
        if (isDrifting)
            availableSteer *= driftSteerMultiplier;

        float targetSteer = steerInput * availableSteer;
        float response = 1f - Mathf.Exp(-steerResponse * Time.fixedDeltaTime);
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetSteer, response);
    }

    float GetEffectiveSteerInput()
    {
        float steerInput = Mathf.Clamp(moveInput.x, -1f, 1f);
        return IsBackingUp() ? -steerInput : steerInput;
    }

    bool IsBackingUp()
    {
        float throttle = Mathf.Clamp(moveInput.y, -1f, 1f);
        if (throttle >= -0.01f)
            return false;

        float forwardPlanarSpeed = Vector3.Dot(rb.linearVelocity, ProjectOnPlaneSafe(transform.forward, Vector3.up));
        return forwardPlanarSpeed <= 0.5f;
    }

    Vector3 GetWheelForwardDirection(WheelState state)
    {
        Vector3 forward = ProjectOnPlaneSafe(transform.forward, state.normal);
        if (state.isFront)
            forward = Quaternion.AngleAxis(currentSteerAngle, state.normal) * forward;

        return ProjectOnPlaneSafe(forward, state.normal);
    }

    void ApplySteeringYawAssist()
    {
        if (!isGrounded || rb == null)
            return;

        float steer = GetEffectiveSteerInput();
        if (Mathf.Abs(steer) < 0.01f)
            return;

        float speedFactor = Mathf.Clamp01(CurrentPlanarSpeed() / Mathf.Max(0.01f, maxSpeed));
        speedFactor = isDrifting ? Mathf.Max(minDriftYawFactor, speedFactor) : speedFactor;
        float torque = isDrifting
            ? GetEffectiveDriftYawTorque() * driftYawTorqueMultiplier
            : GetEffectiveTurnYawAssist();
        rb.AddTorque(averageGroundNormal * steer * torque * speedFactor, ForceMode.Acceleration);

        if (!isDrifting)
            return;

        float lowSpeedAssist = 1f - Mathf.Clamp01(CurrentPlanarSpeed() / Mathf.Max(0.01f, maxSpeed));
        float sideForceMultiplier = Mathf.Lerp(1f, lowSpeedDriftSideForceMultiplier, lowSpeedAssist);
        Vector3 driftDir = ProjectOnPlaneSafe(transform.right * Mathf.Sign(steer), averageGroundNormal);
        rb.AddForce(driftDir * GetEffectiveDriftSideForce() * sideForceMultiplier, ForceMode.Acceleration);
    }

    void ApplyAntiRoll()
    {
        if (antiRollStrength <= 0f || wheelStates == null || wheelStates.Length == 0)
            return;

        ApplyAntiRollForAxle(true);
        ApplyAntiRollForAxle(false);
    }

    void ApplyAntiRollForAxle(bool front)
    {
        int left = -1;
        int right = -1;
        for (int i = 0; i < wheelStates.Length; i++)
        {
            if (wheelStates[i].isFront != front)
                continue;

            if (wheelStates[i].isLeft)
                left = i;
            else
                right = i;
        }

        if (left < 0 || right < 0)
            return;

        float leftTravel = wheelStates[left].grounded ? wheelStates[left].compression : 0f;
        float rightTravel = wheelStates[right].grounded ? wheelStates[right].compression : 0f;
        float antiRollForce = (leftTravel - rightTravel) * antiRollStrength;
        Vector3 forceDir = transform.up;

        if (wheelStates[left].grounded)
            rb.AddForceAtPosition(-forceDir * antiRollForce, wheelStates[left].wheelWorldPosition, ForceMode.Force);
        if (wheelStates[right].grounded)
            rb.AddForceAtPosition(forceDir * antiRollForce, wheelStates[right].wheelWorldPosition, ForceMode.Force);
    }

    void ApplyGroundStability()
    {
        Vector3 desiredUp = Vector3.Slerp(Vector3.up, averageGroundNormal, 0.65f).normalized;
        ApplyUprightTorque(desiredUp, groundUprightStrength, groundUprightDamping);
    }

    void ApplyAirUpright()
    {
        ApplyUprightTorque(Vector3.up, airUprightStrength, airUprightDamping);
    }

    void ApplyUprightTorque(Vector3 desiredUp, float strength, float damping)
    {
        if (rb == null || strength <= 0f)
            return;

        Vector3 currentUp = transform.up;
        Vector3 correctionAxis = Vector3.Cross(currentUp, desiredUp);
        float correctionMagnitude = correctionAxis.magnitude;
        if (correctionMagnitude > 0.0001f)
        {
            correctionAxis /= correctionMagnitude;
            float angle = Vector3.Angle(currentUp, desiredUp) * Mathf.Deg2Rad;
            rb.AddTorque(correctionAxis * angle * strength, ForceMode.Acceleration);
        }

        Vector3 yawVelocity = Vector3.Project(rb.angularVelocity, currentUp);
        Vector3 pitchRollVelocity = rb.angularVelocity - yawVelocity;
        rb.AddTorque(-pitchRollVelocity * damping, ForceMode.Acceleration);
    }

    void DampRampPitch()
    {
        if (slopeAngle < 8f)
            return;

        Vector3 localAngularVelocity = transform.InverseTransformDirection(rb.angularVelocity);
        localAngularVelocity.x *= rampPitchDamping;
        rb.angularVelocity = transform.TransformDirection(localAngularVelocity);
    }

    void ApplyAirGravity()
    {
        if (airDashTimer > 0f)
        {
            airDashTimer -= Time.fixedDeltaTime;
            return;
        }

        float gravityMultiplier = rb.linearVelocity.y < 0f ? GetEffectiveFallGravityMultiplier() : GetEffectiveAirGravityMultiplier();
        rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
    }

    void ApplySlopeForces()
    {
        lastSampleHadHits = false;
        lastFrontSample = lastBackSample = Vector3.zero;
        lastSampledSlopeAngle = 0f;

        if (!isGrounded || averageGroundNormal == Vector3.zero)
            return;

        float halfDistance = slopeSampleDistance;
        Vector3 upOffset = transform.up * (GetSuspensionRestLength() + GetSuspensionWheelRadius() + 0.2f);
        Vector3 frontOrigin = transform.position + transform.forward * halfDistance + upOffset;
        Vector3 backOrigin = transform.position - transform.forward * halfDistance + upOffset;
        float sampleDistance = GetSuspensionRestLength() + GetSuspensionWheelRadius() + 5f;

        if (!Physics.Raycast(frontOrigin, -transform.up, out RaycastHit hitFront, sampleDistance, groundLayer, QueryTriggerInteraction.Ignore))
            return;
        if (!Physics.Raycast(backOrigin, -transform.up, out RaycastHit hitBack, sampleDistance, groundLayer, QueryTriggerInteraction.Ignore))
            return;

        lastSampleHadHits = true;
        lastFrontSample = hitFront.point;
        lastBackSample = hitBack.point;

        float heightDifference = hitFront.point.y - hitBack.point.y;
        float run = halfDistance * 2f;
        float angleDegrees = Mathf.Atan2(heightDifference, run) * Mathf.Rad2Deg;
        lastSampledSlopeAngle = angleDegrees;

        if (Mathf.Abs(angleDegrees) < minSlopeAngleToAffect)
            return;

        if (angleDegrees < 0f)
        {
            float normalized = Mathf.Clamp01(Mathf.Abs(angleDegrees) / 90f);
            Vector3 downhill = ProjectOnPlaneSafe(transform.forward, averageGroundNormal);
            rb.AddForce(downhill * downhillAcceleration * normalized, ForceMode.Acceleration);
        }
    }

    void ApplyRampClimbAssist()
    {
        if (!isGrounded || rb == null || moveInput.y <= 0.05f)
            return;

        float slope = Mathf.Max(slopeAngle, Mathf.Abs(lastSampledSlopeAngle));
        if (slope < rampClimbAssistMinSlope)
            return;

        float slopeRatio = Mathf.Clamp01((slope - rampClimbAssistMinSlope) / Mathf.Max(1f, maxDriveSlopeAngle - rampClimbAssistMinSlope));
        float speedRatio = Mathf.Clamp01(CurrentPlanarSpeed() / Mathf.Max(0.01f, GetCurrentAllowedSpeed()));
        float assist = Mathf.Lerp(rampClimbAssist, boostRampClimbAssist, boostActive ? 1f : 0f);
        assist *= moveInput.y * Mathf.Lerp(0.55f, 1f, speedRatio) * slopeRatio;
        if (assist <= 0f)
            return;

        Vector3 climbDirection = (Vector3.up * 0.65f) + ProjectOnPlaneSafe(transform.forward, Vector3.up) * 0.35f;
        rb.AddForce(climbDirection.normalized * assist, ForceMode.Acceleration);
    }

    void ApplySteepSlopeSlide()
    {
        float slope = Vector3.Angle(averageGroundNormal, Vector3.up);
        if (slope <= maxDriveSlopeAngle)
            return;

        Vector3 slideDir = Vector3.ProjectOnPlane(Vector3.down, averageGroundNormal);
        if (slideDir.sqrMagnitude < 0.0001f)
            return;

        rb.AddForce(slideDir.normalized * steepSlopeSlideForce, ForceMode.Acceleration);
    }

    bool CanDriveOnCurrentSlope()
    {
        return !isGrounded || slopeAngle <= maxDriveSlopeAngle;
    }

    void UpdateDriftState()
    {
        bool shouldDrift = ShouldBuildDriftBoost();
        if (!shouldDrift)
        {
            EndDrift(isGrounded);
            if (isGrounded && rb != null)
                RecoverLateralGrip();
            return;
        }

        if (!isDrifting)
        {
            isDrifting = true;
            DriftStarted?.Invoke();
        }

        driftTimer += Time.fixedDeltaTime;
        if (driftChargeTime <= 0f)
            driftCharge = 1f;
        else
            driftCharge = Mathf.Clamp01(driftCharge + Time.fixedDeltaTime / Mathf.Max(0.0001f, driftChargeTime));
    }

    bool ShouldBuildDriftBoost()
    {
        if (!isGrounded || rb == null)
            return false;

        return CurrentPlanarSpeed() > minDriftSpeed &&
               Mathf.Abs(moveInput.x) > 0.2f &&
               moveInput.y > 0.1f;
    }

    void EndDrift(bool awardBoost)
    {
        if (!isDrifting)
            return;

        isDrifting = false;
        DriftEnded?.Invoke();

        bool completedChargedDrift = driftChargeTime <= 0f
            ? driftTimer > 0f
            : driftTimer >= driftChargeTime && driftCharge >= 1f;
        if (awardBoost && completedChargedDrift)
        {
            driftBoostAmount = boostSpeedPerStack;
            TryAddBoostStack();
        }

        driftCharge = 0f;
        driftTimer = 0f;
    }

    void RecoverLateralGrip()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float speedRatio = Mathf.Clamp01(CurrentPlanarSpeed() / Mathf.Max(0.01f, maxSpeed));
        localVelocity.x = Mathf.Lerp(localVelocity.x, 0f, driftGripRecovery * Time.fixedDeltaTime * speedRatio);
        rb.linearVelocity = transform.TransformDirection(localVelocity);
    }

    void HandleBoost()
    {
        if (currentBoostStacks <= 0)
        {
            currentBoostStacks = 0;
            boostTimer = 0f;
            boostActive = false;
            maxBoostStackLockout = false;
            return;
        }

        boostTimer -= Time.fixedDeltaTime;
        if (boostTimer <= 0f)
        {
            boostTimer = 0f;
            currentBoostStacks = 0;
            boostActive = false;
            maxBoostStackLockout = false;
            return;
        }

        boostActive = true;
    }

    bool TryAddBoostStack()
    {
        if (IsMaxBoostStackLockoutActive())
            return false;

        float stackDuration = GetConfiguredBoostStackDuration();
        int stackCap = GetBoostStackCap();
        bool wasBoostActive = boostActive;
        int previousBoostStacks = currentBoostStacks;

        currentBoostStacks = Mathf.Min(currentBoostStacks + 1, stackCap);
        bool gainedBoostStack = currentBoostStacks > previousBoostStacks;
        if (!gainedBoostStack)
        {
            if (currentBoostStacks >= stackCap)
                maxBoostStackLockout = true;

            return false;
        }

        boostTimer = stackDuration;
        boostActive = true;
        if (currentBoostStacks >= stackCap)
            maxBoostStackLockout = true;

        ApplyBoostImpulse(1f);
        PlayVehicleOneShot(boostEnterSFX, boostEnterVolume);
        BoostStackGained?.Invoke();

        if (!wasBoostActive)
            BoostActivated?.Invoke();

        return true;
    }

    void ApplyBoostImpulse(float multiplier)
    {
        if (rb == null)
            return;

        Vector3 boostDirection = ProjectOnPlaneSafe(transform.forward, Vector3.up);
        if (boostDirection.sqrMagnitude < 0.0001f)
            boostDirection = transform.forward;

        rb.AddForce(boostDirection.normalized * boostImpulsePerStack * Mathf.Max(0f, multiplier), ForceMode.VelocityChange);
    }

    float GetTotalActiveBoostAmount()
    {
        return Mathf.Max(0f, boostSpeedPerStack) * Mathf.Max(0, currentBoostStacks);
    }

    float GetCurrentAllowedSpeed()
    {
        return maxSpeed + GetTotalActiveBoostAmount();
    }

    float GetMaxSpeedAtFullBoostStacks()
    {
        return maxSpeed + Mathf.Max(0f, boostSpeedPerStack) * GetBoostStackCap();
    }

    float GetConfiguredBoostStackDuration()
    {
        float minDuration = Mathf.Clamp(minBoostStackDuration, 0.01f, 5f);
        return Mathf.Max(minDuration, driftBoostDuration);
    }

    int GetBoostStackCap()
    {
        return maxBoostStacks <= 1 ? 3 : Mathf.Clamp(maxBoostStacks, 1, 3);
    }

    bool IsMaxBoostStackLockoutActive()
    {
        int stackCap = GetBoostStackCap();
        if (currentBoostStacks < stackCap || boostTimer <= 0f)
            return false;

        if (boostActive)
            maxBoostStackLockout = true;

        return maxBoostStackLockout;
    }

    void ClampPlanarSpeed()
    {
        if (rb == null)
            return;

        Vector3 velocity = rb.linearVelocity;
        Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);
        float allowedSpeed = GetCurrentAllowedSpeed();
        float hardCap = Mathf.Max(allowedSpeed + 4f, allowedSpeed * 1.18f);
        if (planar.magnitude <= hardCap)
            return;

        planar = planar.normalized * hardCap;
        rb.linearVelocity = new Vector3(planar.x, velocity.y, planar.z);
    }

    bool Jump()
    {
        if (!isGrounded)
            return false;

        Vector3 velocity = rb.linearVelocity;
        if (velocity.y < 0f)
            velocity.y = 0f;
        rb.linearVelocity = velocity;
        rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
        return true;
    }

    void TryAirDash()
    {
        if (isGrounded || airDashUsed)
            return;

        airDashUsed = true;
        airDashTimer = airDashCooldown;
        Vector3 velocity = rb.linearVelocity;
        float forwardSpeed = Vector3.Dot(velocity, transform.forward);
        Vector3 carriedForward = transform.forward * Mathf.Max(0f, forwardSpeed) * airDashForwardCarry;
        rb.linearVelocity = new Vector3(carriedForward.x, Mathf.Max(velocity.y, 0f), carriedForward.z);
        rb.AddForce(transform.forward * airDashForce + Vector3.up * airDashUpForce, ForceMode.VelocityChange);
    }

    void OnCollisionEnter(Collision collision)
    {
        TryPlayTerrainCollisionSound(collision);
        TryBoostWallBounce(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        TryPlayTerrainCollisionSound(collision);
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
        Vector3 planarVelocity = rb.linearVelocity;
        planarVelocity.y = 0f;
        if (planarVelocity.sqrMagnitude < 0.01f)
            return;

        Vector3 planarNormal = contact.normal;
        planarNormal.y = 0f;
        if (planarNormal.sqrMagnitude < 0.0001f)
            return;
        planarNormal.Normalize();

        float intoWall = Vector3.Dot(planarVelocity, planarNormal);
        if (intoWall >= -0.1f)
            return;

        Vector3 reflected = Vector3.Reflect(planarVelocity, planarNormal);
        if (reflected.sqrMagnitude < 0.0001f)
            return;

        float targetSpeed = Mathf.Max(boostWallBounceMinSpeed, planarVelocity.magnitude * boostWallBounceSpeedScale);
        reflected = reflected.normalized * targetSpeed;
        rb.linearVelocity = new Vector3(reflected.x, rb.linearVelocity.y, reflected.z);
        lastBoostWallBounceTime = Time.time;
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
        center.z += forwardExtension * 0.5f;
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
        if (recentEnemyImpactTimeById.TryGetValue(enemyId, out float lastImpactTime) &&
            Time.time - lastImpactTime < collisionImpactCooldown)
        {
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
        Vector3 forward = ProjectOnPlaneSafe(transform.forward, Vector3.up);
        float forwardSpeed = Vector3.Dot(velocity, forward);
        if (forwardSpeed <= 0f)
            return;

        float speedLoss = Mathf.Min(
            forwardSpeed * nonBoostRamForwardSpeedLossPercent,
            nonBoostRamForwardSpeedLossMax);

        rb.linearVelocity = velocity - forward * speedLoss;
    }

    Vector3 GetEnemyImpactPushDirection(Enemy enemy, Vector3 impactOrigin)
    {
        Vector3 toEnemy = enemy.transform.position - impactOrigin;
        toEnemy.y = 0f;

        Vector3 forward = ProjectOnPlaneSafe(transform.forward, Vector3.up);
        Vector3 right = ProjectOnPlaneSafe(transform.right, Vector3.up);

        float sideSign = Mathf.Sign(Vector3.Dot(toEnemy, right));
        if (Mathf.Abs(sideSign) < 0.001f)
            sideSign = Random.value < 0.5f ? -1f : 1f;

        Vector3 sideDirection = right * sideSign;
        Vector3 pushDirection = sideDirection * collisionSidePushWeight + forward * collisionForwardPushWeight;
        if (pushDirection.sqrMagnitude < 0.0001f)
            pushDirection = sideDirection;

        float randomYaw = Random.Range(-collisionRandomYawDegrees, collisionRandomYawDegrees);
        return (Quaternion.AngleAxis(randomYaw, Vector3.up) * pushDirection.normalized).normalized;
    }

    void SetupAudioSources()
    {
        vehicleOneShotSource = AudioPlaybackUtility.EnsureChildAudioSource(
            transform,
            "VehicleOneShotAudio",
            loop: false,
            playOnAwake: false,
            spatialBlend: audioSpatialBlend,
            minDistance: audioMinDistance,
            maxDistance: audioMaxDistance);

        engineIdleLoopSource = AudioPlaybackUtility.EnsureChildAudioSource(
            transform,
            "EngineIdleLoopAudio",
            loop: true,
            playOnAwake: false,
            spatialBlend: audioSpatialBlend,
            minDistance: audioMinDistance,
            maxDistance: audioMaxDistance);

        engineLoopSource = AudioPlaybackUtility.EnsureChildAudioSource(
            transform,
            "EngineLoopAudio",
            loop: true,
            playOnAwake: false,
            spatialBlend: audioSpatialBlend,
            minDistance: audioMinDistance,
            maxDistance: audioMaxDistance);

        wheelRollingLoopSource = AudioPlaybackUtility.EnsureChildAudioSource(
            transform,
            "WheelRollingLoopAudio",
            loop: true,
            playOnAwake: false,
            spatialBlend: audioSpatialBlend,
            minDistance: audioMinDistance,
            maxDistance: audioMaxDistance);
    }

    void UpdateVehicleAudio()
    {
        if (isDead)
        {
            StopAllVehicleAudio();
            return;
        }

        float speedRatio = Mathf.Clamp01(CurrentSpeed / Mathf.Max(0.01f, GetMaxSpeedAtFullBoostStacks()));
        float throttleAmount = Mathf.Abs(moveInput.y);
        bool hasThrottleInput = throttleAmount >= engineInputThreshold;
        float runningBlend = Mathf.Clamp01(CurrentPlanarSpeed() / Mathf.Max(0.01f, engineRunningFullVolumeSpeed));
        if (hasThrottleInput)
            runningBlend = Mathf.Max(runningBlend, Mathf.Lerp(0.25f, 0.7f, throttleAmount));
        if (boostActive)
            runningBlend = Mathf.Max(runningBlend, 0.85f);

        float idleVolume = engineIdleVolume * (1f - runningBlend);
        float runningVolume = engineLoopVolume * runningBlend;
        float enginePitch = Mathf.Lerp(engineMinPitch, engineMaxPitch, speedRatio);
        float wheelPitch = Mathf.Lerp(wheelRollingMinPitch, wheelRollingMaxPitch, speedRatio);

        UpdateLoopSource(engineIdleLoopSource, engineIdleLoopSFX, true, engineIdlePitch, idleVolume, engineAudioFadeSpeed);
        UpdateLoopSource(engineLoopSource, engineLoopSFX, true, enginePitch, runningVolume, engineAudioFadeSpeed);
        UpdateLoopSource(wheelRollingLoopSource, wheelRollingLoopSFX, isGrounded && CurrentSpeed >= wheelRollingMinSpeed, wheelPitch, wheelRollingVolume);
    }

    void UpdateLoopSource(AudioSource source, AudioClip clip, bool shouldPlay, float pitch, float volume, float fadeSpeed = 0f)
    {
        if (source == null)
            return;

        source.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        float targetVolume = Mathf.Clamp01(volume);
        if (fadeSpeed > 0f)
            source.volume = Mathf.MoveTowards(source.volume, targetVolume, fadeSpeed * Time.fixedDeltaTime);
        else
            source.volume = targetVolume;

        if (!shouldPlay || clip == null)
        {
            if (source.isPlaying)
                source.Stop();
            return;
        }

        if (source.clip != clip)
            source.clip = clip;

        if (!source.isPlaying)
            source.Play();
    }

    void StopAllVehicleAudio()
    {
        if (engineIdleLoopSource != null && engineIdleLoopSource.isPlaying)
            engineIdleLoopSource.Stop();
        if (engineLoopSource != null && engineLoopSource.isPlaying)
            engineLoopSource.Stop();
        if (wheelRollingLoopSource != null && wheelRollingLoopSource.isPlaying)
            wheelRollingLoopSource.Stop();
    }

    void PlayVehicleOneShot(AudioClip clip, float volume, float pitch = 1f, float maxVolumeScale = 1f)
    {
        if (clip == null)
            return;

        if (vehicleOneShotSource == null)
            SetupAudioSources();
        if (vehicleOneShotSource == null)
            return;

        vehicleOneShotSource.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        vehicleOneShotSource.PlayOneShot(clip, Mathf.Clamp(volume, 0f, Mathf.Max(0f, maxVolumeScale)));
    }

    void TryPlayTerrainCollisionSound(Collision collision)
    {
        if (collision == null || terrainThumpSFX == null)
            return;
        if (Time.time < terrainThumpPlayableAt)
            return;

        float impact = GetCollisionImpactSpeed(collision);
        if (impact < terrainCollisionSoundMinImpact)
            return;

        float pitch = Random.Range(0.9f, 1.08f);
        terrainThumpPlayableAt = Time.time + GetClipDurationAtPitch(terrainThumpSFX, pitch) + terrainCollisionSoundCooldown;
        PlayVehicleOneShot(terrainThumpSFX, GetTerrainThumpVolume(impact), pitch, terrainCollisionSoundMaxVolumeScale);
    }

    float GetTerrainThumpVolume(float impactSpeed)
    {
        float fullVolumeImpact = Mathf.Max(terrainCollisionSoundMinImpact + 0.01f, terrainCollisionSoundFullVolumeImpact);
        float impactRatio = Mathf.InverseLerp(terrainCollisionSoundMinImpact, fullVolumeImpact, impactSpeed);
        float volumeScale = Mathf.Lerp(terrainCollisionSoundMinVolumeScale, terrainCollisionSoundMaxVolumeScale, impactRatio);
        return terrainThumpVolume * Mathf.Clamp(volumeScale, 0f, 2f);
    }

    static float GetClipDurationAtPitch(AudioClip clip, float pitch)
    {
        if (clip == null)
            return 0f;

        return clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
    }

    float GetCollisionImpactSpeed(Collision collision)
    {
        float impact = collision.relativeVelocity.magnitude;
        if (rb == null || collision.contactCount <= 0)
            return impact;

        Vector3 velocity = rb.linearVelocity;
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            impact = Mathf.Max(impact, Mathf.Abs(Vector3.Dot(velocity, contact.normal)));
        }

        return impact;
    }

    void SetupRuntimeParticles()
    {
        if (!enableRuntimeParticles)
            return;

        Transform existingAnchor = transform.Find("VehicleParticles");
        if (existingAnchor == null)
        {
            GameObject anchorObject = new GameObject("VehicleParticles");
            anchorObject.transform.SetParent(transform, false);
            particleAnchor = anchorObject.transform;
        }
        else
        {
            particleAnchor = existingAnchor;
        }

        UpdateParticleAnchorPose();

        Transform existingDriving = particleAnchor.Find("DrivingDustParticles");
        drivingDustParticles = existingDriving != null ? existingDriving.GetComponent<ParticleSystem>() : null;
        if (drivingDustParticles == null)
            drivingDustParticles = RuntimeParticleFactory.CreateDrivingDust(particleAnchor, "DrivingDustParticles", drivingDustColor);

        Transform existingBoost = particleAnchor.Find("BoostDustParticles");
        boostDustParticles = existingBoost != null ? existingBoost.GetComponent<ParticleSystem>() : null;
        if (boostDustParticles == null)
            boostDustParticles = RuntimeParticleFactory.CreateBoostDust(particleAnchor, "BoostDustParticles", boostDustColor);

        Transform existingSparkle = particleAnchor.Find("DriftSparkleParticles");
        driftSparkleParticles = existingSparkle != null ? existingSparkle.GetComponent<ParticleSystem>() : null;
        if (driftSparkleParticles == null)
            driftSparkleParticles = RuntimeParticleFactory.CreateDriftSparkles(particleAnchor, "DriftSparkleParticles");
    }

    void UpdateRuntimeParticles()
    {
        if (!enableRuntimeParticles || particleAnchor == null)
            return;

        UpdateParticleAnchorPose();
        float speed = CurrentSpeed;
        float speedRatio = Mathf.Clamp01(speed / Mathf.Max(0.01f, GetMaxSpeedAtFullBoostStacks()));
        bool groundedAndMoving = isGrounded && speed >= drivingParticleMinSpeed && moveInput.y > 0.05f;
        float driveRate = Mathf.Lerp(0f, drivingParticleMaxRate, speedRatio);
        float boostRate = Mathf.Lerp(drivingParticleMaxRate * 0.45f, drivingParticleMaxRate * 1.35f, RemainingBoostRatio);
        float driftRate = Mathf.Lerp(driftSparkleMaxRate * 0.35f, driftSparkleMaxRate, speedRatio);

        SetParticleEmission(drivingDustParticles, groundedAndMoving && !boostActive, driveRate);
        SetParticleEmission(boostDustParticles, groundedAndMoving && boostActive, boostRate);
        SetParticleEmission(driftSparkleParticles, isGrounded && isDrifting, driftRate);
    }

    void UpdateParticleAnchorPose()
    {
        if (particleAnchor == null)
            return;

        Vector3 localPosition = particleAnchorOffset;
        if (wheelLocalPositions != null && wheelLocalPositions.Length > 0)
        {
            Vector3 rearCenter = Vector3.zero;
            int rearWheelCount = 0;
            for (int i = 0; i < wheelLocalPositions.Length; i++)
            {
                Vector3 localWheelPosition = wheelLocalPositions[i];
                if (localWheelPosition.z < GetSuspensionCenterLocalZ())
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

    void EnsureWheelStateCache()
    {
        int wheelCount = GetWheelCount();
        if (wheelStates == null || wheelStates.Length != wheelCount)
            wheelStates = new WheelState[wheelCount];
        if (wheelSpinAngles == null || wheelSpinAngles.Length != wheelCount)
            wheelSpinAngles = new float[wheelCount];
        if (wheelVisualBaseRotations == null || wheelVisualBaseRotations.Length != wheelCount)
            wheelVisualBaseRotations = new Quaternion[wheelCount];
        if (wheelVisualCacheRefs == null || wheelVisualCacheRefs.Length != wheelCount)
            wheelVisualCacheRefs = new Transform[wheelCount];

        for (int i = 0; i < wheelCount; i++)
        {
            Transform visual = GetWheelVisual(i);
            if (wheelVisualCacheRefs[i] == visual)
                continue;

            wheelVisualCacheRefs[i] = visual;
            wheelVisualBaseRotations[i] = visual != null ? visual.localRotation : Quaternion.identity;
        }
    }

    void EnsureWheelLocalPositions()
    {
        if (wheelLocalPositions == null || wheelLocalPositions.Length < DefaultWheelCount)
            wheelLocalPositions = CreateDefaultWheelLocalPositions();
    }

    Vector3[] CreateDefaultWheelLocalPositions()
    {
        float halfTrack = Mathf.Max(0.5f, trackWidth * 0.5f);
        float halfWheelBase = Mathf.Max(1f, wheelBase * 0.5f);
        float wheelY = 0.01f;
        return new[]
        {
            new Vector3(-halfTrack, wheelY, halfWheelBase),
            new Vector3(halfTrack, wheelY, halfWheelBase),
            new Vector3(-halfTrack, wheelY, -halfWheelBase),
            new Vector3(halfTrack, wheelY, -halfWheelBase)
        };
    }

    int GetWheelCount()
    {
        if (wheelLocalPositions != null && wheelLocalPositions.Length > 0)
            return wheelLocalPositions.Length;
        if (wheelTransforms != null && wheelTransforms.Length > 0)
            return wheelTransforms.Length;
        return DefaultWheelCount;
    }

    Transform GetWheelTransform(int index)
    {
        if (wheelTransforms != null && index >= 0 && index < wheelTransforms.Length)
            return wheelTransforms[index];
        return null;
    }

    Vector3 GetWheelWorldPosition(int index, Vector3 localFallback)
    {
        return transform.TransformPoint(localFallback);
    }

    Vector3 GetWheelLocalPosition(int index)
    {
        if (wheelLocalPositions != null && index >= 0 && index < wheelLocalPositions.Length)
            return wheelLocalPositions[index];

        Transform wheelTransform = GetWheelTransform(index);
        if (wheelTransform != null)
            return transform.InverseTransformPoint(wheelTransform.position);

        Vector3[] defaults = CreateDefaultWheelLocalPositions();
        return defaults[Mathf.Clamp(index, 0, defaults.Length - 1)];
    }

    void UpdateWheelVisuals()
    {
        if (wheelStates == null)
            return;

        EnsureWheelStateCache();
        Vector3 spinAxis = wheelSpinAxis.sqrMagnitude > 0.0001f ? wheelSpinAxis.normalized : Vector3.right;
        float radius = GetSuspensionWheelRadius();

        for (int i = 0; i < wheelStates.Length; i++)
        {
            Transform visual = GetWheelVisual(i);
            if (visual == null)
                continue;

            WheelState state = wheelStates[i];
            Transform referenceWheel = GetWheelTransform(i);
            bool hasExplicitVisual = wheelVisuals != null && i < wheelVisuals.Length && wheelVisuals[i] != null;
            if (hasExplicitVisual || visual != referenceWheel)
                visual.position = state.wheelWorldPosition;

            if (!spinOnlyWhenGrounded || state.grounded)
            {
                float spinSpeed = state.grounded ? state.forwardSpeed : Vector3.Dot(rb.linearVelocity, transform.forward);
                wheelSpinAngles[i] += spinSpeed / radius * Mathf.Rad2Deg * Time.fixedDeltaTime * wheelSpinSpeedMultiplier;
            }

            Quaternion baseRotation = wheelVisualBaseRotations != null && i < wheelVisualBaseRotations.Length
                ? wheelVisualBaseRotations[i]
                : visual.localRotation;
            Quaternion steerRotation = state.isFront
                ? Quaternion.AngleAxis(currentSteerAngle, Vector3.up)
                : Quaternion.identity;
            visual.localRotation = steerRotation * baseRotation * Quaternion.AngleAxis(wheelSpinAngles[i], spinAxis);
        }
    }

    Transform GetWheelVisual(int index)
    {
        if (wheelVisuals != null && index >= 0 && index < wheelVisuals.Length && wheelVisuals[index] != null)
            return wheelVisuals[index];
        if (wheelTransforms != null && index >= 0 && index < wheelTransforms.Length)
            return wheelTransforms[index];
        return null;
    }

    float GetSuspensionRestLength()
    {
        return Mathf.Max(0.05f, suspensionDistance);
    }

    float GetSuspensionWheelRadius()
    {
        return Mathf.Max(0.05f, wheelVisualRadius);
    }

    float GetSuspensionRayLength()
    {
        return GetSuspensionRestLength() + GetSuspensionWheelRadius() + Mathf.Max(0f, suspensionProbeSlack);
    }

    float GetSuspensionCenterLocalZ()
    {
        int wheelCount = GetWheelCount();
        if (wheelCount <= 0)
            return 0f;

        Vector3 firstWheel = GetWheelLocalPosition(0);
        float minZ = firstWheel.z;
        float maxZ = firstWheel.z;
        for (int i = 1; i < wheelCount; i++)
        {
            Vector3 local = GetWheelLocalPosition(i);
            minZ = Mathf.Min(minZ, local.z);
            maxZ = Mathf.Max(maxZ, local.z);
        }

        return (minZ + maxZ) * 0.5f;
    }

    float GetAverageWheelLocalY()
    {
        int wheelCount = GetWheelCount();
        if (wheelCount <= 0)
            return 0f;

        float total = 0f;
        for (int i = 0; i < wheelCount; i++)
            total += GetWheelLocalPosition(i).y;

        return total / wheelCount;
    }

    float GetMeasuredWheelBase()
    {
        int wheelCount = GetWheelCount();
        if (wheelCount <= 1)
            return Mathf.Max(0.5f, wheelBase);

        Vector3 firstWheel = GetWheelLocalPosition(0);
        float minZ = firstWheel.z;
        float maxZ = firstWheel.z;
        for (int i = 1; i < wheelCount; i++)
        {
            Vector3 local = GetWheelLocalPosition(i);
            minZ = Mathf.Min(minZ, local.z);
            maxZ = Mathf.Max(maxZ, local.z);
        }

        return Mathf.Max(0.5f, maxZ - minZ);
    }

    float CurrentPlanarSpeed()
    {
        if (rb == null)
            return 0f;

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        return velocity.magnitude;
    }

    static Vector3 ProjectOnPlaneSafe(Vector3 vector, Vector3 normal)
    {
        Vector3 projected = Vector3.ProjectOnPlane(vector, normal);
        if (projected.sqrMagnitude < 0.0001f)
        {
            projected = Vector3.ProjectOnPlane(vector, Vector3.up);
            if (projected.sqrMagnitude < 0.0001f)
                projected = Vector3.forward;
        }

        return projected.normalized;
    }

    float GetEffectiveAccelerationForce() => accelerationForce > 100f ? 28f : accelerationForce;
    float GetEffectiveVehicleMass() => vehicleMass < 50f ? 900f : vehicleMass;
    float GetEffectiveWheelBase()
    {
        float measuredWheelBase = GetMeasuredWheelBase();
        if (autoCalculatePhysics || wheelBase < 1f)
            return measuredWheelBase;

        return Mathf.Max(0.5f, wheelBase);
    }
    float GetEffectiveSuspensionStiffness() => suspensionStiffness < 1000f ? 32000f * suspensionStiffnessScale : suspensionStiffness;
    float GetEffectiveSuspensionDamping() => suspensionDamping < 100f ? 4500f * suspensionDampingScale : suspensionDamping;
    float GetEffectiveSuspensionMaxForce() => suspensionMaxForcePerWheel < 1000f ? 18000f : suspensionMaxForcePerWheel;
    float GetEffectiveTireGrip() => (tireGrip < 5f ? 28f : tireGrip) * gripScale;
    float GetEffectiveTireGripSpeedFalloff() => tireGripSpeedFalloff > 1f ? 0.2f : tireGripSpeedFalloff;
    float GetEffectiveFrontRollingResistance() => (frontRollingResistance < 0.05f ? 0.45f : frontRollingResistance) * rollingResistanceScale;
    float GetEffectiveRearRollingResistance() => (rearRollingResistance < 0.05f ? 0.38f : rearRollingResistance) * rollingResistanceScale;
    float GetEffectiveFrontCoastDrag() => (frontCoastDrag < 0.05f ? 0.55f : frontCoastDrag) * rollingResistanceScale;
    float GetEffectiveRearCoastDrag() => (rearCoastDrag < 0.05f ? 0.45f : rearCoastDrag) * rollingResistanceScale;
    float GetEffectiveBrakeForce() => brakeForce < 5f ? 38f : brakeForce;
    float GetEffectiveDriftSideForce() => driftSideForce > 100f ? 10f : driftSideForce;
    float GetEffectiveDriftYawTorque() => driftYawTorque > 25f ? 4.5f : driftYawTorque;
    float GetEffectiveTurnYawAssist() => yawAssist + Mathf.Clamp(turnSpeed * 0.01f, 0f, 3f);
    float GetEffectiveDownforce() => groundedDownforce > 25f ? 8f : groundedDownforce;
    float GetEffectiveLeaveGroundBoost() => leaveGroundForwardBoost > 5f ? 0.75f : leaveGroundForwardBoost;
    float GetEffectiveLeaveGroundUpBoost() => (boostActive ? boostLeaveGroundUpBoost : leaveGroundUpBoost);
    float GetEffectiveAirGravityMultiplier() => airGravityMultiplier > 1.3f ? 0.85f : airGravityMultiplier;
    float GetEffectiveFallGravityMultiplier() => fallGravityMultiplier > 1.6f ? 1.15f : fallGravityMultiplier;
    float GetEffectiveGroundAngularDamping() => groundAngularDamping > 8f ? 2.5f : groundAngularDamping;
    float GetEffectiveAirAngularDamping() => airAngularDamping > 2f ? 0.65f : airAngularDamping;

    private void OnDrawGizmosSelected()
    {
        EnsureWheelLocalPositions();
        float restLength = Mathf.Max(0.05f, suspensionDistance);
        float rayLength = GetSuspensionRayLength();
        float wheelRadius = Mathf.Max(0.01f, wheelGizmoRadius);
        Vector3 springUp = transform.up.sqrMagnitude > 0.0001f ? transform.up : Vector3.up;
        Vector3 springDown = -springUp;

        if (showSuspensionRays || showWheelPhysicsGizmos)
        {
            float physicsWheelRadius = GetSuspensionWheelRadius();
            float gizmoRadius = Mathf.Max(0.01f, wheelGizmoRadius);
            float suspensionRayLength = GetSuspensionRestLength() + physicsWheelRadius + Mathf.Max(0f, suspensionProbeSlack);

            for (int i = 0; i < GetWheelCount(); i++)
            {
                Vector3 local;
                Vector3 wheelPosition;
                Vector3 origin;
                RaycastHit hit;
                float springLength;

                Vector3 wheelUp = springUp;
                bool hitGround = TryProbeWheelContact(i, wheelUp, restLength, physicsWheelRadius, out local, out wheelPosition, out origin, out hit, out springLength);
                Vector3 rayEnd = hitGround ? hit.point : origin + (-wheelUp) * suspensionRayLength;
                Vector3 wheelForward = ProjectOnPlaneSafe(transform.forward, hitGround ? hit.normal : wheelUp);
                Vector3 wheelRight = ProjectOnPlaneSafe(transform.right, hitGround ? hit.normal : wheelUp);

                if (showSuspensionRays)
                {
                    Gizmos.color = hitGround ? Color.yellow : Color.gray;
                    Gizmos.DrawLine(origin, rayEnd);
                    Gizmos.DrawSphere(wheelPosition, debugSphereSize * 0.75f);
                }

                if (showWheelPhysicsGizmos)
                {
                    Gizmos.color = hitGround ? wheelContactColor : wheelAirColor;
                    Gizmos.DrawWireSphere(wheelPosition, gizmoRadius);
                    Gizmos.DrawLine(origin, wheelPosition);
                    DrawGizmoArrow(wheelPosition, wheelForward * physicsWheelRadius * 1.6f, wheelForwardColor);
                    DrawGizmoArrow(wheelPosition, wheelRight * physicsWheelRadius * 1.2f, lateralForceColor);
                }

                if (hitGround)
                {
                    Gizmos.DrawSphere(hit.point, debugSphereSize);
                    if (showSurfaceNormals)
                    {
                        float angle = Vector3.Angle(hit.normal, Vector3.up);
                        Gizmos.color = angle <= maxDriveSlopeAngle ? driveableColor : steepColor;
                        Gizmos.DrawLine(hit.point, hit.point + hit.normal * 0.5f);
                    }
                }
            }
        }

        if (Application.isPlaying && showWheelPhysicsGizmos && wheelStates != null)
        {
            for (int i = 0; i < wheelStates.Length; i++)
            {
                WheelState state = wheelStates[i];
                Vector3 wheelPosition = state.wheelWorldPosition;
                Vector3 contactPosition = state.grounded ? state.hitPoint : wheelPosition;

                Gizmos.color = state.grounded ? wheelContactColor : wheelAirColor;
                Gizmos.DrawWireSphere(wheelPosition, wheelRadius);
                Gizmos.DrawSphere(wheelPosition, debugSphereSize * 0.55f);

                if (!state.grounded)
                    continue;

                Gizmos.DrawLine(state.rayOrigin, state.hitPoint);
                Gizmos.DrawSphere(state.hitPoint, debugSphereSize);

                float compressionOffset = Mathf.Lerp(0.08f, 0.32f, Mathf.Clamp01(state.compression));
                Gizmos.color = springForceColor;
                Gizmos.DrawWireSphere(wheelPosition + state.springDirection * compressionOffset, debugSphereSize * 0.75f);

                float springAcceleration = state.springForce / Mathf.Max(1f, rb != null ? rb.mass : vehicleMass);
                DrawGizmoArrow(contactPosition, state.springDirection * springAcceleration * forceGizmoScale, springForceColor);
                DrawGizmoArrow(contactPosition, state.wheelRight * state.lateralAcceleration * forceGizmoScale, lateralForceColor);
                DrawGizmoArrow(contactPosition, state.wheelForward * state.driveAcceleration * forceGizmoScale, driveForceColor);
                DrawGizmoArrow(contactPosition, state.wheelForward * state.rollingAcceleration * forceGizmoScale, Color.white);
                DrawGizmoArrow(wheelPosition, state.wheelForward * wheelRadius * 1.75f, wheelForwardColor);
            }
        }

        if (Application.isPlaying && groundHit.normal != Vector3.zero && showSurfaceNormals)
        {
            Vector3 pos = transform.position + Vector3.up * 0.5f;
            float angle = Vector3.Angle(groundHit.normal, Vector3.up);
            Gizmos.color = angle <= maxDriveSlopeAngle ? driveableColor : steepColor;
            Gizmos.DrawLine(pos, pos + groundHit.normal);
            Gizmos.DrawSphere(pos + groundHit.normal, debugSphereSize * 0.6f);
        }

        if (Application.isPlaying && lastSampleHadHits)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(lastFrontSample, debugSphereSize * 1.2f);
            Gizmos.DrawSphere(lastBackSample, debugSphereSize * 1.2f);
            float absAngle = Mathf.Abs(lastSampledSlopeAngle);
            Gizmos.color = absAngle <= maxDriveSlopeAngle ? Color.green : Color.red;
            Gizmos.DrawLine(lastFrontSample + Vector3.up * 0.02f, lastBackSample + Vector3.up * 0.02f);
        }
    }

    static void DrawGizmoArrow(Vector3 start, Vector3 vector, Color color)
    {
        if (vector.sqrMagnitude < 0.000001f)
            return;

        Vector3 end = start + vector;
        Vector3 direction = vector.normalized;
        float headLength = Mathf.Min(0.35f, vector.magnitude * 0.25f);
        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
        Vector3 headRight = rotation * Quaternion.Euler(0f, 155f, 0f) * Vector3.forward;
        Vector3 headLeft = rotation * Quaternion.Euler(0f, -155f, 0f) * Vector3.forward;

        Gizmos.color = color;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawLine(end, end + headRight * headLength);
        Gizmos.DrawLine(end, end + headLeft * headLength);
    }
}
