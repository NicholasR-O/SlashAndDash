using Action = System.Action;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class CarController : MonoBehaviour, IDamageable
{
    private const float EffectiveRamMinSpeed = 12f;
    private const float KillerRamCollisionDamage = 1000000f;
    private const int DefaultWheelCount = 4;
    private const int RearWheelParticleEmitterCount = 2;
    private const string ParticleAssetFolder = "Assets/Particles";
    private const string WheelDustParticlePrefabPath = ParticleAssetFolder + "/WheelDustParticles.prefab";
    private const string DriftSparkleParticlePrefabPath = ParticleAssetFolder + "/DriftSparkleParticles.prefab";

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

    [Header("Scene Intro")]
    [SerializeField] private bool playSceneLoadDriveIn = true;
    [SerializeField] private float sceneIntroDriveDistance = 12f;
    [SerializeField] private float sceneIntroDuration = 1.45f;
    [SerializeField] private bool sceneIntroControlLock = true;
    [SerializeField] private float sceneIntroCameraLiftHeight = 3f;
    [SerializeField] private AnimationCurve sceneIntroCameraGlideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Boost")]
    [SerializeField, HideInInspector] private float driftBoostAmount = 12f;
    [SerializeField] private float driftBoostDuration = 1.2f;
    [SerializeField] private int maxBoostStacks = 3;
    [SerializeField] private float boostSpeedPerStack = 12f;
    [SerializeField] private float boostImpulsePerStack = 10f;
    [SerializeField] private float boostAccelerationMultiplier = 1.45f;
    [SerializeField, HideInInspector] private float minBoostStackDuration = 0.2f;
    [SerializeField] private float storedBoostChargeTimePerBar = 1.05f;
    [SerializeField] private float storedBoostBarDifficultyMultiplier = 1.65f;
    [SerializeField] private float storedBoostMinTurnInput = 0.2f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 8.5f;
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
    [SerializeField, HideInInspector] private float airGravityMultiplier = 1.25f;
    [SerializeField, HideInInspector] private float fallGravityMultiplier = 1.75f;

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
    [SerializeField] private Vector3 rearWheelParticleOffset = new Vector3(0f, -0.16f, -0.08f);
    [SerializeField] private float drivingParticleMinSpeed = 2f;
    [SerializeField] private float drivingParticleMaxRate = 34f;
    [SerializeField] private float drivingParticleBoostRateMultiplier = 5.2f;
    [SerializeField] private float driftSparkleMaxRate = 24f;
    [SerializeField] private Color drivingDustColor = new Color(0.78f, 0.73f, 0.64f, 0.62f);
    [SerializeField] private Color boostDustColor = new Color(1f, 0.83f, 0.3f, 0.72f);
    [SerializeField] private ParticleSystem drivingDustParticlesPrefab;
    [SerializeField] private ParticleSystem boostDustParticlesPrefab;
    [SerializeField] private ParticleSystem wheelDustParticlesPrefab;
    [SerializeField] private ParticleSystem driftSparkleParticlesPrefab;
    [SerializeField] private Texture2D drivingDustTexture;
    [SerializeField] private Texture2D boostDustTexture;
    [SerializeField] private Texture2D wheelDustTexture;
    [SerializeField] private Texture2D boostWheelDustTexture;
    [SerializeField] private Texture2D driftSparkleTexture;
    [SerializeField] private Texture2D collisionSparkTexture;
    [SerializeField] private Texture2D dashEffectTexture;

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
    [SerializeField] private bool simulateEngineGearShifts = true;
    [SerializeField] private int simulatedEngineGearCount = 5;
    [SerializeField] private float simulatedEngineGearMinSpeedRatio = 0.08f;
    [SerializeField] private float simulatedEngineGearPitchResponse = 18f;
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
    [SerializeField] private float terrainCollisionPerColliderCooldown = 0.65f;
    [SerializeField, Range(0f, 1f)] private float terrainCollisionGroundNormalThreshold = 0.35f;
    [SerializeField] private ParticleSystem collisionSparkParticlesPrefab;

    [Header("Air Dash")]
    [SerializeField] private float airDashForce = 28f;
    [SerializeField, HideInInspector] private float airDashUpForce = 4f;
    [SerializeField, HideInInspector] private float airDashCooldown = 0.15f;
    [SerializeField, HideInInspector] private float airDashForwardCarry = 0.35f;
    [SerializeField] private float airDashColliderLengthMultiplier = 1.8f;
    [SerializeField] private float airDashDuration = 0.42f;
    [SerializeField] private float airDashMaxSpeedMultiplier = 1.25f;
    [SerializeField] private float dashEchoInterval = 0.055f;
    [SerializeField] private float dashEchoLifetime = 0.55f;
    [SerializeField] private Color dashEchoColor = new Color(0.2f, 0.72f, 1f, 0.82f);
    [SerializeField] private GameObject dashEchoModelPrefab;
    [SerializeField] private Transform[] dashEchoSourceRoots;
    [SerializeField] private bool includeAutoDashEchoSources = true;
    [SerializeField] private bool allowColliderDashEchoFallback = false;

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
    [Tooltip("Minimum upward normal for suspension contacts. This keeps pit walls from being treated like ground.")]
    [SerializeField, Range(0f, 1f)] private float minSuspensionGroundNormalY = 0.25f;

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
    [SerializeField] private bool snapRespawnToGround = true;
    [SerializeField] private float respawnGroundSampleRadius = 8f;
    [SerializeField] private float respawnGroundProbeHeight = 12f;
    [SerializeField] private float respawnGroundProbeDepth = 30f;
    [SerializeField, Range(0f, 1f)] private float respawnGroundMinNormalY = 0.35f;
    [SerializeField] private float respawnGroundClearance = 0.08f;

    [Header("Respawn Visuals")]
    [SerializeField] private bool playRespawnFade = true;
    [SerializeField] private float respawnFadeDuration = 0.35f;
    [SerializeField, Range(0f, 1f)] private float respawnFadeStartAlpha = 0.06f;
    [SerializeField] private bool playRespawnScreenFade = true;
    [SerializeField] private Color respawnScreenFadeColor = Color.black;
    [SerializeField] private float respawnScreenFadeInDuration = 0.65f;
    [SerializeField] private float pitRespawnPreFadeDelay = 1.1f;
    [SerializeField] private float respawnScreenHoldDuration = 1f;
    [SerializeField] private float respawnCameraReturnDuration = 0.45f;
    [SerializeField] private float enemyDeathExplosionRadius = 5f;
    [SerializeField] private float enemyDeathExplosionDelay = 0.55f;
    [SerializeField] private AudioClip enemyDeathExplosionSFX;
    [SerializeField, Range(0f, 1f)] private float enemyDeathExplosionVolume = 1f;
    [SerializeField] private AnimationCurve respawnFadeCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.35f, 0.72f),
        new Keyframe(1f, 1f));

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

    struct RespawnFadeRendererState
    {
        public Renderer renderer;
        public Material[] originalMaterials;
        public Material[] fadeMaterials;
    }

    private Rigidbody rb;
    private Collider col;
    private PlayerInputActions controls;
    private Vector2 moveInput;
    private bool cinematicControlActive;
    private Vector2 cinematicMoveInput;

    private bool isGrounded;
    private bool wasGrounded;
    private bool isDrifting;
    private bool boostActive;
    private int currentBoostStacks;
    private float boostTimer;
    private bool maxBoostStackLockout;
    private float storedBoostCharge;
    private float driftCharge;
    private float driftTimer;
    private float currentSteerAngle;

    private bool airDashUsed;
    private float airDashTimer;
    private bool airDashActive;
    private float airDashRemainingTime;
    private float airDashSpeed;
    private float airDashAcceleration;
    private float nextDashEchoTime;
    private Vector3 airDashDirection;
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
    private bool respawnInProgress;
    private bool respawnPreFadeFallInProgress;
    private bool sceneIntroInProgress;
    private bool sceneIntroOriginalKinematic;
    private ThirdPersonCamera sceneIntroFollowCamera;
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
    private Transform[] rearWheelParticleAnchors;
    private ParticleSystem[] rearWheelDustParticles;
    private int[] rearWheelParticleWheelIndices;
    private int rearWheelDustAppearanceMode = -1;
    private Material drivingDustRuntimeMaterial;
    private Material boostDustRuntimeMaterial;
    private Material wheelDustRuntimeMaterial;
    private Material boostWheelDustRuntimeMaterial;
    private Material driftSparkleRuntimeMaterial;
    private Material collisionSparkRuntimeMaterial;
    private AudioSource vehicleOneShotSource;
    private AudioSource engineIdleLoopSource;
    private AudioSource engineLoopSource;
    private AudioSource wheelRollingLoopSource;
    private float smoothedEnginePitch;
    private Material dashEchoMaterial;

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
    private readonly Dictionary<int, float> terrainThumpPlayableAtByCollider = new Dictionary<int, float>();
    private Coroutine respawnSequenceRoutine;
    private Coroutine enemyDeathSequenceRoutine;
    private Coroutine respawnFadeRoutine;
    private Coroutine respawnScreenFadeRoutine;
    private CanvasGroup respawnScreenFadeGroup;
    private Image respawnScreenFadeImage;
    private readonly List<RespawnFadeRendererState> respawnFadeRendererStates = new List<RespawnFadeRendererState>();
    private readonly List<Renderer> enemyDeathHiddenRenderers = new List<Renderer>();
    private readonly RaycastHit[] respawnGroundHits = new RaycastHit[16];
    private bool killerRamDebug;
    private bool hasDefaultRamCollisionDamage;
    private float defaultRamCollisionDamage;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsAlive => !isDead;
    public bool IsRegenerating => isRegenerating;
    public bool IntroInProgress => sceneIntroInProgress;
    public bool TrickReady => enableAirtimeTrick && !trickInAir && trickCooldownTimer <= 0f;
    public bool TrickInAir => trickInAir;
    public float TrickAirTimeRatio => Mathf.Clamp01(trickAirTimer / Mathf.Max(0.01f, trickMinAirTime));
    public bool TrickOnCooldown => trickCooldownTimer > 0f;
    public bool LastTrickSucceeded => lastTrickSucceeded;
    public float LastTrickSucceededAt => lastTrickSucceededAt;
    public bool HasPerformedTrickThisLife => hasPerformedTrickThisLife;
    public bool CinematicControlActive => cinematicControlActive;
    public bool IsGrounded => isGrounded;
    public bool IsDrifting => isDrifting;
    public Vector2 MoveInput => moveInput;
    public int CurrentBoostStacks => Mathf.Max(0, currentBoostStacks);
    public int BoostStackCap => GetBoostStackCap();
    public float StoredBoostRatio => Mathf.Clamp01(storedBoostCharge / Mathf.Max(1f, GetBoostStackCap()));
    public int StoredBoostFullBars => Mathf.Clamp(Mathf.FloorToInt(storedBoostCharge), 0, GetBoostStackCap());
    public bool CanReleaseStoredBoost => StoredBoostFullBars > 0 && !boostActive;
    public int RamImpactCount => ramImpactCount;
    public bool ShowSuspensionRays { get => showSuspensionRays; set => showSuspensionRays = value; }
    public bool ShowSurfaceNormals { get => showSurfaceNormals; set => showSurfaceNormals = value; }
    public bool AlwaysBoostDebug { get => alwaysBoostDebug; set => alwaysBoostDebug = value; }
    public bool KillerRamDebug
    {
        get => killerRamDebug;
        set
        {
            if (killerRamDebug == value)
                return;

            CaptureDefaultRamCollisionDamage();
            killerRamDebug = value;
            maxCollisionDamage = killerRamDebug ? KillerRamCollisionDamage : defaultRamCollisionDamage;
        }
    }
    public float CurrentSpeed => rb != null ? rb.linearVelocity.magnitude : 0f;
    public float SpeedRatio => Mathf.Clamp01(CurrentPlanarSpeed() / Mathf.Max(0.01f, GetMaxSpeedAtFullBoostStacks()));
    public float RemainingBoostRatio => boostActive
        ? Mathf.Clamp01(boostTimer / Mathf.Max(0.01f, GetConfiguredBoostStackDuration()))
        : StoredBoostRatio;

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
        CaptureDefaultRamCollisionDamage();
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

    private void Start()
    {
        if (playSceneLoadDriveIn && isActiveAndEnabled)
            StartCoroutine(SceneLoadDriveInRoutine());
    }

    void CaptureDefaultRamCollisionDamage()
    {
        if (hasDefaultRamCollisionDamage)
            return;

        defaultRamCollisionDamage = Mathf.Max(0f, maxCollisionDamage);
        hasDefaultRamCollisionDamage = true;
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
        EndCinematicControl();

        if (noClipActive)
            SetNoClipActive(false);

        noClipAscendInput = false;
        noClipDescendInput = false;
        if (sceneIntroInProgress && rb != null)
            rb.isKinematic = sceneIntroOriginalKinematic;
        if (sceneIntroInProgress && sceneIntroFollowCamera != null)
        {
            sceneIntroFollowCamera.ResumeFollow();
            sceneIntroFollowCamera = null;
        }
        sceneIntroInProgress = false;
        airDashActive = false;
        airDashRemainingTime = 0f;
        airDashTimer = 0f;
        airDashAcceleration = 0f;
        currentBoostStacks = 0;
        boostTimer = 0f;
        boostActive = false;
        maxBoostStackLockout = false;
        storedBoostCharge = 0f;
        EndDrift(false);
        StopRuntimeParticles();
        StopAllVehicleAudio();
        StopRespawnSequence(restoreOriginals: true);

        if (controls != null)
            controls.Player.Disable();
    }

    private void OnDestroy()
    {
        if (controls != null)
            controls.Dispose();

        if (dashEchoMaterial != null)
            Destroy(dashEchoMaterial);

        StopRespawnSequence(restoreOriginals: true);
        DestroyRuntimeParticleMaterials();
    }

    private void FixedUpdate()
    {
        if (!cinematicControlActive && GameState.NoClip != noClipActive)
            SetNoClipActive(GameState.NoClip);

        if (sceneIntroInProgress && sceneIntroControlLock)
        {
            moveInput = Vector2.zero;
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            StopRuntimeParticles();
            StopAllVehicleAudio();
            return;
        }

        if (cinematicControlActive)
            moveInput = cinematicMoveInput;

        HandlePassiveRegen();

        if (noClipActive)
        {
            HandleNoClipMovement();
            StopAllVehicleAudio();
            return;
        }

        if (HandleOutOfBoundsRespawn())
            return;

        if (respawnInProgress)
        {
            moveInput = Vector2.zero;
            if (respawnPreFadeFallInProgress)
            {
                StopRuntimeParticles();
                StopAllVehicleAudio();
                return;
            }

            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            StopRuntimeParticles();
            StopAllVehicleAudio();
            return;
        }

        wasGrounded = isGrounded;
        SampleWheelContacts();
        slopeAngle = isGrounded ? Vector3.Angle(averageGroundNormal, Vector3.up) : 0f;

        HandleAirtimeTrick();

        rb.angularDamping = isGrounded ? GetEffectiveGroundAngularDamping() : GetEffectiveAirAngularDamping();

        if (isGrounded)
            airDashUsed = false;

        if (airDashActive)
        {
            HandleAirDashMovement();
            UpdateRuntimeParticles();
            UpdateWheelVisuals();
            UpdateVehicleAudio();
            return;
        }

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
        controls.Player.Move.performed += c =>
        {
            if (!cinematicControlActive)
                moveInput = c.ReadValue<Vector2>();
        };
        controls.Player.Move.canceled += _ =>
        {
            if (!cinematicControlActive)
                moveInput = Vector2.zero;
        };
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
        sceneIntroDriveDistance = Mathf.Max(0f, sceneIntroDriveDistance);
        sceneIntroDuration = Mathf.Max(0.01f, sceneIntroDuration);
        sceneIntroCameraLiftHeight = Mathf.Max(0f, sceneIntroCameraLiftHeight);

        maxBoostStacks = maxBoostStacks <= 1 ? 3 : Mathf.Clamp(maxBoostStacks, 1, 3);
        boostSpeedPerStack = Mathf.Max(0f, boostSpeedPerStack);
        boostImpulsePerStack = Mathf.Max(0f, boostImpulsePerStack);
        boostAccelerationMultiplier = Mathf.Max(1f, boostAccelerationMultiplier);
        minBoostStackDuration = Mathf.Clamp(minBoostStackDuration, 0.01f, 5f);
        driftBoostDuration = Mathf.Max(minBoostStackDuration, driftBoostDuration);
        storedBoostChargeTimePerBar = Mathf.Max(0.05f, storedBoostChargeTimePerBar);
        storedBoostBarDifficultyMultiplier = Mathf.Max(1f, storedBoostBarDifficultyMultiplier);
        storedBoostMinTurnInput = Mathf.Clamp01(storedBoostMinTurnInput);

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
        drivingParticleMinSpeed = Mathf.Max(0f, drivingParticleMinSpeed);
        drivingParticleMaxRate = Mathf.Max(0f, drivingParticleMaxRate);
        drivingParticleBoostRateMultiplier = Mathf.Max(1f, drivingParticleBoostRateMultiplier);
        driftSparkleMaxRate = Mathf.Max(0f, driftSparkleMaxRate);

        suspensionDistance = Mathf.Max(0.05f, suspensionDistance);
        suspensionStiffness = Mathf.Max(1f, suspensionStiffness);
        suspensionDamping = Mathf.Max(0f, suspensionDamping);
        suspensionMaxForcePerWheel = Mathf.Max(1f, suspensionMaxForcePerWheel);
        suspensionNormalBlend = Mathf.Clamp01(suspensionNormalBlend);
        suspensionProbeSlack = Mathf.Max(0f, suspensionProbeSlack);
        minSuspensionGroundNormalY = Mathf.Clamp01(minSuspensionGroundNormalY);
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
        simulatedEngineGearCount = Mathf.Clamp(simulatedEngineGearCount, 1, 8);
        simulatedEngineGearMinSpeedRatio = Mathf.Clamp01(simulatedEngineGearMinSpeedRatio);
        simulatedEngineGearPitchResponse = Mathf.Max(0.1f, simulatedEngineGearPitchResponse);
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
        terrainCollisionPerColliderCooldown = Mathf.Max(0f, terrainCollisionPerColliderCooldown);
        terrainCollisionGroundNormalThreshold = Mathf.Clamp01(terrainCollisionGroundNormalThreshold);
        airDashForce = Mathf.Max(0f, airDashForce);
        airDashUpForce = Mathf.Max(0f, airDashUpForce);
        airDashCooldown = Mathf.Max(0f, airDashCooldown);
        airDashForwardCarry = Mathf.Clamp01(airDashForwardCarry);
        airDashColliderLengthMultiplier = Mathf.Max(0.1f, airDashColliderLengthMultiplier);
        airDashDuration = Mathf.Max(Time.fixedDeltaTime, airDashDuration);
        airDashMaxSpeedMultiplier = Mathf.Max(0.1f, airDashMaxSpeedMultiplier);
        dashEchoInterval = Mathf.Max(0.01f, dashEchoInterval);
        dashEchoLifetime = Mathf.Max(0.05f, dashEchoLifetime);

        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = Application.isPlaying ? Mathf.Clamp(currentHealth, 0f, maxHealth) : maxHealth;
        regenDelaySeconds = Mathf.Max(0f, regenDelaySeconds);
        regenPerSecond = Mathf.Max(0f, regenPerSecond);
        outOfBoundsY = Mathf.Max(0f, outOfBoundsY);
        outOfBoundsRespawnDamage = Mathf.Max(0f, outOfBoundsRespawnDamage);
        respawnGroundSampleRadius = Mathf.Max(0.1f, respawnGroundSampleRadius);
        respawnGroundProbeHeight = Mathf.Max(0.1f, respawnGroundProbeHeight);
        respawnGroundProbeDepth = Mathf.Max(0.1f, respawnGroundProbeDepth);
        respawnGroundClearance = Mathf.Max(0f, respawnGroundClearance);
        respawnFadeDuration = Mathf.Max(0.01f, respawnFadeDuration);
        respawnFadeStartAlpha = Mathf.Clamp01(respawnFadeStartAlpha);
        respawnScreenFadeColor.a = Mathf.Clamp01(respawnScreenFadeColor.a);
        respawnScreenFadeInDuration = Mathf.Max(0.01f, respawnScreenFadeInDuration);
        pitRespawnPreFadeDelay = Mathf.Max(0f, pitRespawnPreFadeDelay);
        respawnScreenHoldDuration = Mathf.Max(1f, respawnScreenHoldDuration);
        respawnCameraReturnDuration = Mathf.Max(0f, respawnCameraReturnDuration);
        enemyDeathExplosionRadius = Mathf.Max(1f, enemyDeathExplosionRadius);
        enemyDeathExplosionDelay = Mathf.Max(0f, enemyDeathExplosionDelay);
        enemyDeathExplosionVolume = Mathf.Clamp01(enemyDeathExplosionVolume);
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
            Die(source);

        return true;
    }

    void Die(GameObject source = null)
    {
        if (isDead)
            return;

        isDead = true;
        Debug.Log(name + " died.", this);

        if (IsEnemyDamageSource(source))
        {
            BeginEnemyDeathRespawn();
            return;
        }

        BeginRespawnAtCheckpoint(0f, restoreFullHealth: true, clampDamageToOneHealth: false, fadeOutFirst: true);
    }

    bool IsEnemyDamageSource(GameObject source)
    {
        if (source == null)
            return false;

        if (source.GetComponentInParent<Enemy>() != null)
            return true;

        return source.GetComponentInParent<WizardProjectile>() != null;
    }

    void BeginEnemyDeathRespawn()
    {
        if (enemyDeathSequenceRoutine != null)
            return;

        enemyDeathSequenceRoutine = StartCoroutine(EnemyDeathRespawnRoutine());
    }

    IEnumerator EnemyDeathRespawnRoutine()
    {
        respawnInProgress = true;
        moveInput = Vector2.zero;
        cinematicMoveInput = Vector2.zero;
        EndDrift(false);
        StopRuntimeParticles();
        StopAllVehicleAudio();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        HideEnemyDeathVisuals();
        RuntimeParticleFactory.SpawnEnemyExplosionPulse(transform.position, enemyDeathExplosionRadius);
        PlayEnemyDeathExplosionSFX();

        if (enemyDeathExplosionDelay > 0f)
            yield return new WaitForSecondsRealtime(enemyDeathExplosionDelay);

        enemyDeathSequenceRoutine = null;
        BeginRespawnAtCheckpoint(0f, restoreFullHealth: true, clampDamageToOneHealth: false, fadeOutFirst: true, keepDeathVisualsHiddenUntilBlack: true);
    }

    void PlayEnemyDeathExplosionSFX()
    {
        if (enemyDeathExplosionSFX == null)
            return;

        AudioPlaybackUtility.PlayDetachedClip(
            enemyDeathExplosionSFX,
            transform.position,
            enemyDeathExplosionVolume,
            1f,
            audioSpatialBlend,
            audioMinDistance,
            audioMaxDistance);
    }

    bool HandleOutOfBoundsRespawn()
    {
        if (!respawnWhenBelowOutOfBoundsY || isDead || respawnInProgress)
            return false;
        if (transform.position.y >= outOfBoundsY)
            return false;
        if (hasCheckpointRespawnPose && checkpointRespawnPosition.y < outOfBoundsY)
            return false;

        BeginRespawnAtCheckpoint(
            outOfBoundsRespawnDamage,
            restoreFullHealth: false,
            clampDamageToOneHealth: true,
            fadeOutFirst: true,
            lockCameraPositionUntilFade: true,
            preFadeDelay: pitRespawnPreFadeDelay);
        return true;
    }

    public void SetCheckpointRespawnPose(Vector3 position, Quaternion rotation)
    {
        checkpointRespawnPosition = position;
        checkpointRespawnRotation = rotation;
        hasCheckpointRespawnPose = true;
    }

    void ResolveGroundedRespawnPose(ref Vector3 position, Quaternion rotation)
    {
        if (!snapRespawnToGround)
            return;

        if (!TryFindRespawnGroundPoint(position, out Vector3 groundPoint))
            return;

        position.y = groundPoint.y + GetRespawnGroundClearance(rotation);
    }

    bool TryFindRespawnGroundPoint(Vector3 position, out Vector3 groundPoint)
    {
        bool hasNavMeshHit = NavMesh.SamplePosition(
            position,
            out NavMeshHit navMeshHit,
            Mathf.Max(0.1f, respawnGroundSampleRadius),
            NavMesh.AllAreas);

        Vector3 probePosition = hasNavMeshHit ? navMeshHit.position : position;
        if (TryRaycastRespawnGround(probePosition, out groundPoint))
            return true;

        if (hasNavMeshHit)
        {
            groundPoint = navMeshHit.position;
            return true;
        }

        groundPoint = position;
        return false;
    }

    bool TryRaycastRespawnGround(Vector3 position, out Vector3 groundPoint)
    {
        Vector3 origin = position + Vector3.up * Mathf.Max(0.1f, respawnGroundProbeHeight);
        float distance = Mathf.Max(0.1f, respawnGroundProbeHeight + respawnGroundProbeDepth);
        int groundMask = groundLayer.value != 0 ? groundLayer.value : Physics.DefaultRaycastLayers;
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            respawnGroundHits,
            distance,
            groundMask,
            QueryTriggerInteraction.Ignore);

        int bestHitIndex = -1;
        float bestDistance = float.PositiveInfinity;
        float minNormalY = Mathf.Clamp01(respawnGroundMinNormalY);
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = respawnGroundHits[i];
            if (hit.collider == null)
                continue;
            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                continue;
            if (hit.normal.y < minNormalY)
                continue;
            if (hit.distance >= bestDistance)
                continue;

            bestDistance = hit.distance;
            bestHitIndex = i;
        }

        if (bestHitIndex >= 0)
        {
            groundPoint = respawnGroundHits[bestHitIndex].point;
            return true;
        }

        groundPoint = position;
        return false;
    }

    float GetRespawnGroundClearance(Quaternion rotation)
    {
        if (col is BoxCollider box && box.transform == transform)
            return GetBoxColliderGroundClearance(box, rotation) + respawnGroundClearance;

        if (col != null)
            return Mathf.Max(0f, transform.position.y - col.bounds.min.y) + respawnGroundClearance;

        return respawnGroundClearance;
    }

    float GetBoxColliderGroundClearance(BoxCollider box, Quaternion rotation)
    {
        Vector3 scale = AbsVector(transform.lossyScale);
        Vector3 center = Vector3.Scale(box.center, scale);
        Vector3 extents = Vector3.Scale(box.size * 0.5f, scale);
        float minOffsetY = float.PositiveInfinity;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 localCorner = center + new Vector3(extents.x * x, extents.y * y, extents.z * z);
                    float offsetY = (rotation * localCorner).y;
                    if (offsetY < minOffsetY)
                        minOffsetY = offsetY;
                }
            }
        }

        return Mathf.Max(0f, -minOffsetY);
    }

    public void RespawnAtCheckpoint(float damage = 0f, bool restoreFullHealth = false, bool clampDamageToOneHealth = false)
    {
        StopRespawnSequence(restoreOriginals: true);
        CompleteRespawnAtCheckpoint(damage, restoreFullHealth, clampDamageToOneHealth, playFadeIn: true);
        ArenaTrigger.ResetActiveArenasForPlayerRespawn();
    }

    public void RespawnFromVoid(float damage = -1f)
    {
        if (isDead || respawnInProgress)
            return;

        float respawnDamage = damage >= 0f ? damage : outOfBoundsRespawnDamage;
        BeginRespawnAtCheckpoint(
            respawnDamage,
            restoreFullHealth: false,
            clampDamageToOneHealth: true,
            fadeOutFirst: true,
            lockCameraPositionUntilFade: true,
            preFadeDelay: pitRespawnPreFadeDelay);
    }

    void BeginRespawnAtCheckpoint(
        float damage,
        bool restoreFullHealth,
        bool clampDamageToOneHealth,
        bool fadeOutFirst,
        bool keepDeathVisualsHiddenUntilBlack = false,
        bool lockCameraPositionUntilFade = false,
        float preFadeDelay = 0f)
    {
        if (respawnSequenceRoutine != null)
            return;

        if (!fadeOutFirst || !playRespawnScreenFade || !isActiveAndEnabled)
        {
            CompleteRespawnAtCheckpoint(damage, restoreFullHealth, clampDamageToOneHealth, playFadeIn: true);
            ArenaTrigger.ResetActiveArenasForPlayerRespawn();
            return;
        }

        respawnSequenceRoutine = StartCoroutine(RespawnAtCheckpointWithScreenFade(
            damage,
            restoreFullHealth,
            clampDamageToOneHealth,
            keepDeathVisualsHiddenUntilBlack,
            lockCameraPositionUntilFade,
            preFadeDelay));
    }

    IEnumerator RespawnAtCheckpointWithScreenFade(
        float damage,
        bool restoreFullHealth,
        bool clampDamageToOneHealth,
        bool keepDeathVisualsHiddenUntilBlack,
        bool lockCameraPositionUntilFade,
        float preFadeDelay)
    {
        respawnInProgress = true;
        respawnPreFadeFallInProgress = false;
        ResetForRespawn(restoreEnemyDeathVisuals: !keepDeathVisualsHiddenUntilBlack);
        ThirdPersonCamera followCamera = FindFirstObjectByType<ThirdPersonCamera>();
        if (followCamera != null)
        {
            if (lockCameraPositionUntilFade)
                followCamera.LockPositionAndLookAtPlayer();
            else
                followCamera.FreezeFollow();
        }

        preFadeDelay = Mathf.Max(0f, preFadeDelay);
        if (preFadeDelay > 0f)
        {
            respawnPreFadeFallInProgress = true;
            yield return new WaitForSecondsRealtime(preFadeDelay);
            respawnPreFadeFallInProgress = false;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        StopRespawnFade(restoreOriginals: true);
        yield return FadeRespawnScreen(0f, 1f, respawnScreenFadeInDuration);

        if (followCamera != null)
            followCamera.FreezeFollow();

        CompleteRespawnAtCheckpoint(damage, restoreFullHealth, clampDamageToOneHealth, playFadeIn: false);
        SetRespawnScreenBlack();
        if (followCamera != null)
        {
            yield return followCamera.FlyToPlayerWhileFrozen(respawnCameraReturnDuration);
            followCamera.ResumeFollow();
            yield return null;
            SetRespawnScreenBlack();
        }

        bool carFadePrepared = PrepareRespawnFadeIn();

        if (respawnScreenHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(respawnScreenHoldDuration);

        ArenaTrigger.ResetActiveArenasForPlayerRespawn();
        yield return FadeRespawnScreen(1f, 0f, respawnScreenFadeInDuration);

        if (carFadePrepared)
            yield return PlayPreparedRespawnFadeIn();
        else
            PlayRespawnFade();

        respawnInProgress = false;
        respawnPreFadeFallInProgress = false;
        respawnSequenceRoutine = null;
    }

    void CompleteRespawnAtCheckpoint(float damage, bool restoreFullHealth, bool clampDamageToOneHealth, bool playFadeIn)
    {
        ProjectileCleanup.ClearAllProjectiles();

        if (!hasCheckpointRespawnPose)
            SetCheckpointRespawnPose(transform.position, transform.rotation);

        ResetForRespawn();
        Vector3 respawnPosition = checkpointRespawnPosition;
        Quaternion respawnRotation = checkpointRespawnRotation;
        ResolveGroundedRespawnPose(ref respawnPosition, respawnRotation);

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = respawnPosition;
            rb.rotation = respawnRotation;
        }
        else
        {
            transform.SetPositionAndRotation(respawnPosition, respawnRotation);
        }

        if (restoreFullHealth)
            currentHealth = maxHealth;
        else
            ApplyRespawnDamage(damage, clampDamageToOneHealth);

        isDead = false;
        isRegenerating = false;
        if (playFadeIn)
            PlayRespawnFade();
    }

    void ApplyRespawnDamage(float damage, bool clampDamageToOneHealth)
    {
        if (damage <= 0f || GameState.GodMode)
            return;

        float minimumHealth = clampDamageToOneHealth ? 1f : 0f;
        currentHealth = Mathf.Max(minimumHealth, currentHealth - damage);
        regenPausedUntil = Time.time + regenDelaySeconds;
    }

    IEnumerator SceneLoadDriveInRoutine()
    {
        if (rb == null || sceneIntroDriveDistance <= 0f || sceneIntroDuration <= 0f)
            yield break;

        sceneIntroInProgress = true;
        moveInput = Vector2.zero;

        Vector3 endPosition = rb.position;
        Quaternion endRotation = rb.rotation;
        Vector3 introDirection = ProjectOnPlaneSafe(endRotation * Vector3.forward, Vector3.up);
        Vector3 startPosition = endPosition - introDirection * sceneIntroDriveDistance;
        sceneIntroOriginalKinematic = rb.isKinematic;
        sceneIntroFollowCamera = FindFirstObjectByType<ThirdPersonCamera>();
        if (sceneIntroFollowCamera != null)
            sceneIntroFollowCamera.PlaceForSceneIntro(endPosition, endRotation, sceneIntroCameraLiftHeight, sceneIntroDriveDistance);

        SetCheckpointRespawnPose(endPosition, endRotation);
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.position = startPosition;
        rb.rotation = endRotation;
        transform.SetPositionAndRotation(startPosition, endRotation);
        if (sceneIntroFollowCamera != null)
            sceneIntroFollowCamera.PlaceForSceneIntro(endPosition, endRotation, sceneIntroCameraLiftHeight, sceneIntroDriveDistance);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, sceneIntroDuration);
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            Vector3 position = Vector3.LerpUnclamped(startPosition, endPosition, eased);
            rb.MovePosition(position);
            rb.MoveRotation(endRotation);
            transform.SetPositionAndRotation(position, endRotation);
            float cameraProgress = sceneIntroCameraGlideCurve != null ? sceneIntroCameraGlideCurve.Evaluate(t) : eased;
            if (sceneIntroFollowCamera != null)
                sceneIntroFollowCamera.SetSceneIntroCameraProgress(endPosition, endRotation, cameraProgress, sceneIntroCameraLiftHeight, sceneIntroDriveDistance);

            elapsed += Time.deltaTime;
            yield return null;
        }

        rb.position = endPosition;
        rb.rotation = endRotation;
        transform.SetPositionAndRotation(endPosition, endRotation);
        rb.isKinematic = sceneIntroOriginalKinematic;
        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        if (sceneIntroFollowCamera != null)
        {
            sceneIntroFollowCamera.SetSceneIntroCameraProgress(endPosition, endRotation, 1f, sceneIntroCameraLiftHeight, sceneIntroDriveDistance);
            sceneIntroFollowCamera.ResumeFollow();
            sceneIntroFollowCamera = null;
        }
        sceneIntroInProgress = false;
        SetCheckpointRespawnPose(endPosition, endRotation);
    }

    void ResetForRespawn(bool restoreEnemyDeathVisuals = true)
    {
        if (restoreEnemyDeathVisuals)
            RestoreEnemyDeathVisuals();

        EndDrift(false);
        trickInAir = false;
        trickCandidateReady = false;
        trickAirTimer = 0f;
        lastTrickSucceeded = false;
        airDashUsed = false;
        airDashTimer = 0f;
        airDashActive = false;
        airDashRemainingTime = 0f;
        airDashSpeed = 0f;
        airDashAcceleration = 0f;
        currentSteerAngle = 0f;
        currentBoostStacks = 0;
        boostTimer = 0f;
        boostActive = false;
        maxBoostStackLockout = false;
        storedBoostCharge = 0f;
        StopRuntimeParticles();
        StopAllVehicleAudio();
    }

    void HideEnemyDeathVisuals()
    {
        RestoreEnemyDeathVisuals();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!ShouldHideEnemyDeathRenderer(renderer))
                continue;

            enemyDeathHiddenRenderers.Add(renderer);
            renderer.enabled = false;
        }
    }

    bool ShouldHideEnemyDeathRenderer(Renderer renderer)
    {
        if (renderer == null || !renderer.enabled)
            return false;

        if (renderer is ParticleSystemRenderer)
            return false;

        if (renderer.gameObject.name == "CheckpointArrowVisual")
            return false;

        return renderer.GetComponentInParent<CarController>() == this;
    }

    void RestoreEnemyDeathVisuals()
    {
        for (int i = 0; i < enemyDeathHiddenRenderers.Count; i++)
        {
            Renderer renderer = enemyDeathHiddenRenderers[i];
            if (renderer != null)
                renderer.enabled = true;
        }

        enemyDeathHiddenRenderers.Clear();
    }

    void SetRespawnScreenBlack()
    {
        EnsureRespawnScreenFade();
        if (respawnScreenFadeGroup == null)
            return;

        StopRespawnScreenFade(clear: false);
        SetRespawnScreenFadeAlpha(1f);
    }

    IEnumerator FadeRespawnScreen(float fromAlpha, float toAlpha, float durationSeconds)
    {
        EnsureRespawnScreenFade();
        if (respawnScreenFadeGroup == null)
            yield break;

        if (respawnScreenFadeRoutine != null)
        {
            StopCoroutine(respawnScreenFadeRoutine);
            respawnScreenFadeRoutine = null;
        }

        respawnScreenFadeRoutine = StartCoroutine(RespawnScreenFadeRoutine(fromAlpha, toAlpha, durationSeconds));
        yield return respawnScreenFadeRoutine;
    }

    IEnumerator RespawnScreenFadeRoutine(float fromAlpha, float toAlpha, float durationSeconds)
    {
        float duration = Mathf.Max(0.01f, durationSeconds);
        float elapsed = 0f;
        SetRespawnScreenFadeAlpha(fromAlpha);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curveT = respawnFadeCurve != null ? Mathf.Clamp01(respawnFadeCurve.Evaluate(t)) : t;
            SetRespawnScreenFadeAlpha(Mathf.Lerp(fromAlpha, toAlpha, curveT));
            yield return null;
        }

        SetRespawnScreenFadeAlpha(toAlpha);
        respawnScreenFadeRoutine = null;
    }

    void EnsureRespawnScreenFade()
    {
        if (respawnScreenFadeGroup != null && respawnScreenFadeImage != null)
            return;

        GameObject canvasObject = new GameObject("RespawnScreenFadeCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        respawnScreenFadeGroup = canvasObject.AddComponent<CanvasGroup>();
        respawnScreenFadeGroup.blocksRaycasts = false;
        respawnScreenFadeGroup.interactable = false;
        respawnScreenFadeGroup.ignoreParentGroups = true;

        GameObject imageObject = new GameObject("RespawnScreenFadeImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        respawnScreenFadeImage = imageObject.GetComponent<Image>();
        respawnScreenFadeImage.raycastTarget = false;
        SetRespawnScreenFadeAlpha(0f);
    }

    void SetRespawnScreenFadeAlpha(float alpha)
    {
        if (respawnScreenFadeGroup == null)
            return;

        Color color = respawnScreenFadeColor;
        float maxAlpha = Mathf.Clamp01(color.a);
        color.a = 1f;
        if (respawnScreenFadeImage != null)
            respawnScreenFadeImage.color = color;

        float effectiveAlpha = Mathf.Clamp01(alpha) * maxAlpha;
        respawnScreenFadeGroup.alpha = effectiveAlpha;
        respawnScreenFadeGroup.gameObject.SetActive(effectiveAlpha > 0.001f);
    }

    void StopRespawnScreenFade(bool clear)
    {
        if (respawnScreenFadeRoutine != null)
        {
            StopCoroutine(respawnScreenFadeRoutine);
            respawnScreenFadeRoutine = null;
        }

        if (clear)
            SetRespawnScreenFadeAlpha(0f);
    }

    void PlayRespawnFade()
    {
        if (!PrepareRespawnFadeIn())
            return;

        StartPreparedRespawnFadeIn();
    }

    bool PrepareRespawnFadeIn()
    {
        if (!playRespawnFade || !isActiveAndEnabled)
            return false;

        StopRespawnFade(restoreOriginals: true);
        CaptureRespawnFadeRenderers();
        if (respawnFadeRendererStates.Count == 0)
            return false;

        SetRespawnFadeAlpha(respawnFadeStartAlpha);
        return true;
    }

    IEnumerator PlayPreparedRespawnFadeIn()
    {
        Coroutine fadeRoutine = StartPreparedRespawnFadeIn();
        if (fadeRoutine != null)
            yield return fadeRoutine;
    }

    Coroutine StartPreparedRespawnFadeIn()
    {
        if (respawnFadeRendererStates.Count == 0)
            return null;

        if (respawnFadeRoutine != null)
            StopCoroutine(respawnFadeRoutine);

        respawnFadeRoutine = StartCoroutine(RespawnFadeRoutine(respawnFadeStartAlpha, 1f, respawnFadeDuration, restoreOriginalsOnComplete: true));
        return respawnFadeRoutine;
    }

    IEnumerator RespawnFadeRoutine(float fromAlpha, float toAlpha, float durationSeconds, bool restoreOriginalsOnComplete)
    {
        float duration = Mathf.Max(0.01f, durationSeconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curveT = respawnFadeCurve != null ? Mathf.Clamp01(respawnFadeCurve.Evaluate(t)) : t;
            SetRespawnFadeAlpha(Mathf.Lerp(fromAlpha, toAlpha, curveT));
            yield return null;
        }

        SetRespawnFadeAlpha(toAlpha);
        respawnFadeRoutine = null;
        if (restoreOriginalsOnComplete)
            RestoreRespawnFadeMaterials();
    }

    void CaptureRespawnFadeRenderers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!ShouldFadeRespawnRenderer(renderer))
                continue;

            Material[] originalMaterials = renderer.sharedMaterials;
            if (originalMaterials == null || originalMaterials.Length == 0)
                continue;

            Material[] fadeMaterials = new Material[originalMaterials.Length];
            bool hasFadeMaterial = false;
            for (int materialIndex = 0; materialIndex < originalMaterials.Length; materialIndex++)
            {
                Material source = originalMaterials[materialIndex];
                if (source == null)
                    continue;

                Material fadeMaterial = new Material(source)
                {
                    name = source.name + " Respawn Fade"
                };

                ForceTransparentMaterial(fadeMaterial);
                SetRespawnFadeMaterialAlpha(fadeMaterial, respawnFadeStartAlpha);
                fadeMaterials[materialIndex] = fadeMaterial;
                hasFadeMaterial = true;
            }

            if (!hasFadeMaterial)
                continue;

            respawnFadeRendererStates.Add(new RespawnFadeRendererState
            {
                renderer = renderer,
                originalMaterials = originalMaterials,
                fadeMaterials = fadeMaterials
            });
            renderer.sharedMaterials = fadeMaterials;
        }
    }

    bool ShouldFadeRespawnRenderer(Renderer renderer)
    {
        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
            return false;

        if (renderer is ParticleSystemRenderer)
            return false;

        if (renderer.gameObject.name == "CheckpointArrowVisual")
            return false;

        return renderer.GetComponentInParent<CarController>() == this;
    }

    void SetRespawnFadeAlpha(float alpha)
    {
        float clampedAlpha = Mathf.Clamp01(alpha);
        for (int i = 0; i < respawnFadeRendererStates.Count; i++)
        {
            Material[] fadeMaterials = respawnFadeRendererStates[i].fadeMaterials;
            if (fadeMaterials == null)
                continue;

            for (int materialIndex = 0; materialIndex < fadeMaterials.Length; materialIndex++)
                SetRespawnFadeMaterialAlpha(fadeMaterials[materialIndex], clampedAlpha);
        }
    }

    static void SetRespawnFadeMaterialAlpha(Material material, float alpha)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
        {
            Color color = material.GetColor("_BaseColor");
            color.a = alpha;
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            Color color = material.GetColor("_Color");
            color.a = alpha;
            material.SetColor("_Color", color);
        }
    }

    void StopRespawnFade(bool restoreOriginals)
    {
        if (respawnFadeRoutine != null)
        {
            StopCoroutine(respawnFadeRoutine);
            respawnFadeRoutine = null;
        }

        if (restoreOriginals)
            RestoreRespawnFadeMaterials();
        else
            DestroyRespawnFadeMaterials();
    }

    void StopRespawnSequence(bool restoreOriginals)
    {
        if (enemyDeathSequenceRoutine != null)
        {
            StopCoroutine(enemyDeathSequenceRoutine);
            enemyDeathSequenceRoutine = null;
        }

        if (respawnSequenceRoutine != null)
        {
            StopCoroutine(respawnSequenceRoutine);
            respawnSequenceRoutine = null;
        }

        respawnInProgress = false;
        respawnPreFadeFallInProgress = false;
        RestoreEnemyDeathVisuals();
        StopRespawnScreenFade(clear: true);
        StopRespawnFade(restoreOriginals);
    }

    void RestoreRespawnFadeMaterials()
    {
        for (int i = 0; i < respawnFadeRendererStates.Count; i++)
        {
            RespawnFadeRendererState state = respawnFadeRendererStates[i];
            if (state.renderer != null && state.originalMaterials != null)
                state.renderer.sharedMaterials = state.originalMaterials;

            DestroyRespawnFadeMaterials(state.fadeMaterials);
        }

        respawnFadeRendererStates.Clear();
    }

    void DestroyRespawnFadeMaterials()
    {
        for (int i = 0; i < respawnFadeRendererStates.Count; i++)
            DestroyRespawnFadeMaterials(respawnFadeRendererStates[i].fadeMaterials);

        respawnFadeRendererStates.Clear();
    }

    static void DestroyRespawnFadeMaterials(Material[] materials)
    {
        if (materials == null)
            return;

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null)
                continue;

            if (Application.isPlaying)
                Destroy(material);
            else
                DestroyImmediate(material);
        }
    }

    void OnJumpPerformed()
    {
        if (cinematicControlActive)
            return;

        if (sceneIntroInProgress && sceneIntroControlLock)
            return;

        if (!noClipActive && Jump())
        {
            PlayVehicleOneShot(jumpSFX, jumpVolume);
            JumpPerformed?.Invoke();
        }
    }

    void OnDashPerformed()
    {
        if (cinematicControlActive)
            return;

        if (sceneIntroInProgress && sceneIntroControlLock)
            return;

        if (!noClipActive && isGrounded && TryReleaseStoredBoost())
            return;

        if (!noClipActive && !isGrounded)
        {
            if (TryAirDash())
            {
                PlayVehicleOneShot(dashSFX, dashVolume);
                DashPerformed?.Invoke();
            }
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

    public void BeginCinematicControl()
    {
        cinematicControlActive = true;
        cinematicMoveInput = Vector2.zero;
        moveInput = Vector2.zero;
        noClipAscendInput = false;
        noClipDescendInput = false;
        EndDrift(false);

        if (noClipActive)
            SetNoClipActive(false);
    }

    public void SetCinematicMoveInput(Vector2 input)
    {
        if (!cinematicControlActive)
            BeginCinematicControl();

        cinematicMoveInput = Vector2.ClampMagnitude(input, 1f);
        moveInput = cinematicMoveInput;
    }

    public void EndCinematicControl()
    {
        cinematicControlActive = false;
        cinematicMoveInput = Vector2.zero;
        moveInput = controls != null && controls.Player.Move.enabled
            ? controls.Player.Move.ReadValue<Vector2>()
            : Vector2.zero;
        noClipAscendInput = false;
        noClipDescendInput = false;
        EndDrift(false);
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

        if (hit.normal.y < minSuspensionGroundNormalY)
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

        ChargeStoredTurnBoost();
    }

    bool ShouldBuildDriftBoost()
    {
        if (!isGrounded || rb == null)
            return false;

        return CurrentPlanarSpeed() > minDriftSpeed &&
               Mathf.Abs(moveInput.x) > storedBoostMinTurnInput &&
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
        driftCharge = 0f;
        driftTimer = 0f;
    }

    void ChargeStoredTurnBoost()
    {
        int stackCap = GetBoostStackCap();
        if (stackCap <= 0 || boostActive)
            return;

        float turnStrength = Mathf.InverseLerp(storedBoostMinTurnInput, 1f, Mathf.Abs(moveInput.x));
        float currentBar = Mathf.Clamp(Mathf.Floor(storedBoostCharge), 0f, stackCap - 1);
        float difficulty = Mathf.Pow(storedBoostBarDifficultyMultiplier, currentBar);
        float chargePerSecond = 1f / Mathf.Max(0.05f, storedBoostChargeTimePerBar * difficulty);
        storedBoostCharge = Mathf.Min(stackCap, storedBoostCharge + chargePerSecond * Mathf.Max(0.15f, turnStrength) * Time.fixedDeltaTime);
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

    bool TryReleaseStoredBoost()
    {
        int stacksToRelease = StoredBoostFullBars;
        if (stacksToRelease <= 0 || boostActive)
            return false;

        int stackCap = GetBoostStackCap();
        currentBoostStacks = Mathf.Clamp(stacksToRelease, 1, stackCap);
        storedBoostCharge = 0f;
        boostTimer = GetConfiguredBoostStackDuration();
        boostActive = true;
        maxBoostStackLockout = currentBoostStacks >= stackCap;

        ApplyBoostImpulse(currentBoostStacks);
        PlayVehicleOneShot(boostEnterSFX, boostEnterVolume);
        BoostStackGained?.Invoke();
        BoostActivated?.Invoke();
        return true;
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

    bool TryAirDash()
    {
        if (isGrounded || airDashUsed || airDashActive)
            return false;

        airDashUsed = true;
        airDashActive = true;
        airDashTimer = airDashCooldown;
        airDashRemainingTime = Mathf.Max(Time.fixedDeltaTime, airDashDuration);
        airDashDirection = ProjectOnPlaneSafe(transform.forward, Vector3.up);
        if (airDashDirection.sqrMagnitude < 0.0001f)
            airDashDirection = transform.forward;
        airDashDirection.Normalize();

        float dashDistance = GetPlayerColliderLength() * airDashColliderLengthMultiplier;
        airDashAcceleration = 2f * dashDistance / (airDashRemainingTime * airDashRemainingTime);
        airDashSpeed = airDashAcceleration * airDashRemainingTime * airDashMaxSpeedMultiplier;
        nextDashEchoTime = Time.time;

        EndDrift(false);
        Vector3 velocity = rb.linearVelocity;
        Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
        float forwardSpeed = Mathf.Max(0f, Vector3.Dot(planarVelocity, airDashDirection));
        rb.linearVelocity = airDashDirection * forwardSpeed;
        rb.angularVelocity = Vector3.zero;
        SpawnDashEcho();
        return true;
    }

    void HandleAirDashMovement()
    {
        if (rb == null)
            return;

        if (Time.time >= nextDashEchoTime)
        {
            SpawnDashEcho();
            nextDashEchoTime = Time.time + dashEchoInterval;
        }

        rb.AddForce(airDashDirection * airDashAcceleration, ForceMode.Acceleration);
        rb.angularVelocity = Vector3.zero;
        LimitAirDashSpeed();

        airDashRemainingTime -= Time.fixedDeltaTime;
        if (airDashRemainingTime > 0f)
            return;

        airDashActive = false;
        airDashTimer = 0f;
        airDashAcceleration = 0f;
    }

    void LimitAirDashSpeed()
    {
        Vector3 velocity = rb.linearVelocity;
        Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
        float forwardSpeed = Vector3.Dot(planarVelocity, airDashDirection);
        if (forwardSpeed <= airDashSpeed)
            return;

        Vector3 excessForwardVelocity = airDashDirection * (forwardSpeed - airDashSpeed);
        rb.linearVelocity = velocity - excessForwardVelocity;
    }

    float GetPlayerColliderLength()
    {
        if (col is BoxCollider box)
            return Mathf.Max(0.05f, box.size.z * Mathf.Abs(transform.lossyScale.z));

        if (col is CapsuleCollider capsule)
        {
            Vector3 scale = transform.lossyScale;
            float axisScale = capsule.direction == 0
                ? Mathf.Abs(scale.x)
                : capsule.direction == 1
                    ? Mathf.Abs(scale.y)
                    : Mathf.Abs(scale.z);
            return Mathf.Max(0.05f, capsule.height * axisScale);
        }

        if (col is SphereCollider sphere)
            return Mathf.Max(0.05f, sphere.radius * 2f * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)));

        if (col != null)
            return Mathf.Max(0.05f, Vector3.Dot(col.bounds.size, AbsVector(transform.forward)));

        return Mathf.Max(0.05f, GetEffectiveWheelBase());
    }

    static Vector3 AbsVector(Vector3 vector)
    {
        return new Vector3(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z));
    }

    void SpawnDashEcho()
    {
        SpawnDashParticleEffect();

        List<Renderer> sourceRenderers = GetDashEchoSourceRenderers();
        GameObject echoRoot = new GameObject("DashEcho");
        echoRoot.transform.SetPositionAndRotation(transform.position, transform.rotation);
        Material echoMaterial = CreateDashEchoMaterialInstance();
        List<Mesh> bakedMeshes = new List<Mesh>();
        int echoCount = 0;

        if (sourceRenderers != null)
        {
            for (int i = 0; i < sourceRenderers.Count; i++)
            {
                Renderer sourceRenderer = sourceRenderers[i];
                if (!IsValidDashEchoRenderer(sourceRenderer))
                    continue;

                Mesh echoMesh = GetDashEchoMesh(sourceRenderer, bakedMeshes);
                if (echoMesh == null)
                    continue;

                GameObject echoObject = new GameObject(sourceRenderer.gameObject.name + " Echo");
                Transform echoTransform = echoObject.transform;
                echoTransform.SetParent(echoRoot.transform, worldPositionStays: true);
                echoTransform.SetPositionAndRotation(sourceRenderer.transform.position, sourceRenderer.transform.rotation);
                echoTransform.localScale = sourceRenderer.transform.lossyScale;

                MeshFilter echoFilter = echoObject.AddComponent<MeshFilter>();
                echoFilter.sharedMesh = echoMesh;

                MeshRenderer echoRenderer = echoObject.AddComponent<MeshRenderer>();
                int materialCount = Mathf.Max(1, sourceRenderer.sharedMaterials != null ? sourceRenderer.sharedMaterials.Length : 1);
                Material[] materials = new Material[materialCount];
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                    materials[materialIndex] = echoMaterial;

                echoRenderer.sharedMaterials = materials;
                echoRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                echoRenderer.receiveShadows = false;
                echoCount++;
            }
        }

        if (echoCount == 0)
            echoCount = CreatePrefabDashEcho(echoRoot.transform, echoMaterial);

        if (echoCount == 0 && allowColliderDashEchoFallback)
            echoCount = CreateColliderDashEcho(echoRoot.transform, echoMaterial);

        RemoveDashEchoCollision(echoRoot);

        if (echoCount > 0)
            StartCoroutine(FadeDashEcho(echoRoot, echoMaterial, dashEchoLifetime, bakedMeshes));
        else
            DestroyDashEchoImmediately(echoRoot, echoMaterial, bakedMeshes);
    }

    void SpawnDashParticleEffect()
    {
        Vector3 direction = airDashDirection.sqrMagnitude > 0.0001f
            ? airDashDirection.normalized
            : ProjectOnPlaneSafe(transform.forward, Vector3.up);
        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward;

        direction.Normalize();
        float backOffset = Mathf.Max(0.35f, GetPlayerColliderLength() * 0.45f);
        Vector3 position = transform.position - direction * backOffset + transform.up * 0.35f;
        RuntimeParticleFactory.SpawnDashBurst(position, direction, dashEchoColor, dashEffectTexture);
    }

    List<Renderer> GetDashEchoSourceRenderers()
    {
        List<Renderer> sourceRenderers = new List<Renderer>();
        HashSet<Renderer> seenRenderers = new HashSet<Renderer>();

        if (dashEchoSourceRoots != null)
        {
            for (int i = 0; i < dashEchoSourceRoots.Length; i++)
                CollectDashEchoRenderers(dashEchoSourceRoots[i], sourceRenderers, seenRenderers);
        }

        if (includeAutoDashEchoSources)
            CollectDashEchoRenderers(transform, sourceRenderers, seenRenderers);

        return sourceRenderers;
    }

    void CollectDashEchoRenderers(Transform root, List<Renderer> sourceRenderers, HashSet<Renderer> seenRenderers)
    {
        if (root == null || sourceRenderers == null || seenRenderers == null)
            return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || seenRenderers.Contains(renderer))
                continue;

            seenRenderers.Add(renderer);
            sourceRenderers.Add(renderer);
        }
    }

    bool IsValidDashEchoRenderer(Renderer sourceRenderer)
    {
        if (sourceRenderer == null || !sourceRenderer.enabled)
            return false;
        if (!sourceRenderer.gameObject.activeInHierarchy)
            return false;
        if (sourceRenderer.GetComponentInParent<CheckpointArrowIndicator>() != null)
            return false;
        if (sourceRenderer.GetComponentInParent<ParticleSystem>() != null)
            return false;

        return true;
    }

    int CreateColliderDashEcho(Transform echoRoot, Material echoMaterial)
    {
        if (col == null || echoRoot == null)
            return 0;

        GameObject fallback = new GameObject("Bounds Dash Echo");
        fallback.name = "Collider Dash Echo";
        Transform fallbackTransform = fallback.transform;
        fallbackTransform.SetParent(echoRoot, worldPositionStays: false);

        MeshFilter filter = fallback.AddComponent<MeshFilter>();
        filter.sharedMesh = GetDashEchoCubeMesh();

        if (col is BoxCollider box)
        {
            fallbackTransform.SetPositionAndRotation(transform.TransformPoint(box.center), transform.rotation);
            Vector3 scale = transform.lossyScale;
            fallbackTransform.localScale = new Vector3(
                Mathf.Abs(box.size.x * scale.x),
                Mathf.Abs(box.size.y * scale.y),
                Mathf.Abs(box.size.z * scale.z));
        }
        else
        {
            fallbackTransform.SetPositionAndRotation(col.bounds.center, transform.rotation);
            fallbackTransform.localScale = col.bounds.size;
        }

        MeshRenderer renderer = fallback.AddComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = echoMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        return 1;
    }

    int CreatePrefabDashEcho(Transform echoRoot, Material echoMaterial)
    {
        if (dashEchoModelPrefab == null || echoRoot == null)
            return 0;

        GameObject modelInstance = Instantiate(dashEchoModelPrefab, echoRoot);
        modelInstance.name = dashEchoModelPrefab.name + " Dash Echo";
        modelInstance.transform.SetPositionAndRotation(transform.position, transform.rotation);
        RemoveDashEchoCollision(modelInstance);
        RemoveDashEchoParticles(modelInstance);

        Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(includeInactive: true);
        int rendererCount = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!IsValidDashEchoRenderer(renderer))
                continue;

            int materialCount = Mathf.Max(1, renderer.sharedMaterials != null ? renderer.sharedMaterials.Length : 1);
            Material[] materials = new Material[materialCount];
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                materials[materialIndex] = echoMaterial;

            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            rendererCount++;
        }

        return rendererCount;
    }

    static Mesh GetDashEchoCubeMesh()
    {
        return Resources.GetBuiltinResource<Mesh>("Cube.fbx");
    }

    static void RemoveDashEchoCollision(GameObject root)
    {
        if (root == null)
            return;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(includeInactive: true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
                continue;

            colliders[i].enabled = false;
            Destroy(colliders[i]);
        }

        Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(includeInactive: true);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] == null)
                continue;

            bodies[i].detectCollisions = false;
            bodies[i].isKinematic = true;
            Destroy(bodies[i]);
        }
    }

    static void RemoveDashEchoParticles(GameObject root)
    {
        if (root == null)
            return;

        ParticleSystem[] particles = root.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] == null)
                continue;

            particles[i].gameObject.SetActive(false);
            Destroy(particles[i].gameObject);
        }
    }

    Mesh GetDashEchoMesh(Renderer sourceRenderer, List<Mesh> bakedMeshes)
    {
        if (sourceRenderer is MeshRenderer)
        {
            MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
            return sourceFilter != null ? sourceFilter.sharedMesh : null;
        }

        if (sourceRenderer is SkinnedMeshRenderer skinnedRenderer)
        {
            Mesh bakedMesh = new Mesh
            {
                name = skinnedRenderer.name + " Dash Echo Mesh"
            };
            skinnedRenderer.BakeMesh(bakedMesh);
            bakedMeshes?.Add(bakedMesh);
            return bakedMesh;
        }

        return null;
    }

    Material CreateDashEchoMaterialInstance()
    {
        Material material = new Material(GetDashEchoMaterialTemplate())
        {
            name = "Dash Echo Material Instance"
        };

        ApplyDashEchoAlpha(material, dashEchoColor.a);
        return material;
    }

    Material GetDashEchoMaterialTemplate()
    {
        if (dashEchoMaterial != null)
            return dashEchoMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");

        dashEchoMaterial = new Material(shader)
        {
            name = "Dash Echo Material"
        };

        ForceTransparentMaterial(dashEchoMaterial);
        ApplyDashEchoAlpha(dashEchoMaterial, dashEchoColor.a);
        return dashEchoMaterial;
    }

    IEnumerator FadeDashEcho(GameObject echoRoot, Material echoMaterial, float lifetime, List<Mesh> bakedMeshes)
    {
        float duration = Mathf.Max(0.01f, lifetime);
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float alpha = dashEchoColor.a * (1f - Mathf.Clamp01(timer / duration));
            ApplyDashEchoAlpha(echoMaterial, alpha);
            yield return null;
        }

        if (echoRoot != null)
            Destroy(echoRoot);
        if (echoMaterial != null)
            Destroy(echoMaterial);
        DestroyBakedDashEchoMeshes(bakedMeshes);
    }

    static void DestroyBakedDashEchoMeshes(List<Mesh> bakedMeshes)
    {
        if (bakedMeshes == null)
            return;

        for (int i = 0; i < bakedMeshes.Count; i++)
        {
            if (bakedMeshes[i] != null)
                Destroy(bakedMeshes[i]);
        }
    }

    static void DestroyDashEchoImmediately(GameObject echoRoot, Material echoMaterial, List<Mesh> bakedMeshes)
    {
        if (echoRoot != null)
            Destroy(echoRoot);
        if (echoMaterial != null)
            Destroy(echoMaterial);
        DestroyBakedDashEchoMeshes(bakedMeshes);
    }

    void ApplyDashEchoAlpha(Material material, float alpha)
    {
        if (material == null)
            return;

        Color color = new Color(dashEchoColor.r, dashEchoColor.g, dashEchoColor.b, Mathf.Clamp01(alpha));
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", color * 1.8f);
    }

    static void ForceTransparentMaterial(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_SurfaceType"))
            material.SetFloat("_SurfaceType", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", 2f);
        if (material.HasProperty("_ZTest"))
            material.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_EMISSION");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    struct CollisionImpactInfo
    {
        public Collider otherCollider;
        public ContactPoint contact;
        public float impactSpeed;
    }

    void OnCollisionEnter(Collision collision)
    {
        TryPlayTerrainCollisionFeedback(collision);
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
        float enginePitch = GetSimulatedEnginePitch(speedRatio, runningBlend);
        float wheelPitch = Mathf.Lerp(wheelRollingMinPitch, wheelRollingMaxPitch, speedRatio);

        UpdateLoopSource(engineIdleLoopSource, engineIdleLoopSFX, true, engineIdlePitch, idleVolume, engineAudioFadeSpeed);
        UpdateLoopSource(engineLoopSource, engineLoopSFX, true, enginePitch, runningVolume, engineAudioFadeSpeed);
        UpdateLoopSource(wheelRollingLoopSource, wheelRollingLoopSFX, isGrounded && CurrentSpeed >= wheelRollingMinSpeed, wheelPitch, wheelRollingVolume);
    }

    float GetSimulatedEnginePitch(float speedRatio, float runningBlend)
    {
        float targetPitch = Mathf.Lerp(engineMinPitch, engineMaxPitch, speedRatio);
        if (simulateEngineGearShifts && speedRatio >= simulatedEngineGearMinSpeedRatio && runningBlend > 0.05f)
        {
            int gearCount = Mathf.Max(1, simulatedEngineGearCount);
            float scaledSpeed = Mathf.Min(speedRatio * gearCount, gearCount - 0.001f);
            float gearPhase = scaledSpeed - Mathf.Floor(scaledSpeed);
            float revCurve = Mathf.SmoothStep(0f, 1f, gearPhase);
            targetPitch = Mathf.Lerp(engineMinPitch, engineMaxPitch, revCurve);
        }

        if (smoothedEnginePitch <= 0.01f)
            smoothedEnginePitch = targetPitch;

        float blend = 1f - Mathf.Exp(-simulatedEngineGearPitchResponse * Time.fixedDeltaTime);
        smoothedEnginePitch = Mathf.Lerp(smoothedEnginePitch, targetPitch, blend);
        return smoothedEnginePitch;
    }

    void UpdateLoopSource(AudioSource source, AudioClip clip, bool shouldPlay, float pitch, float volume, float fadeSpeed = 0f)
    {
        if (source == null)
            return;

        source.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        float targetVolume = Mathf.Clamp01(volume * GameOptions.SoundEffectsVolume);
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
        vehicleOneShotSource.PlayOneShot(clip, Mathf.Clamp(volume * GameOptions.SoundEffectsVolume, 0f, Mathf.Max(0f, maxVolumeScale)));
    }

    void TryPlayTerrainCollisionFeedback(Collision collision)
    {
        if (!TryGetAcceptedTerrainImpact(collision, out CollisionImpactInfo impactInfo))
            return;

        float pitch = Random.Range(0.9f, 1.08f);
        terrainThumpPlayableAt = Time.time + GetClipDurationAtPitch(terrainThumpSFX, pitch) + terrainCollisionSoundCooldown;

        if (impactInfo.otherCollider != null)
            terrainThumpPlayableAtByCollider[impactInfo.otherCollider.GetInstanceID()] = Time.time + terrainCollisionPerColliderCooldown;

        PlayVehicleOneShot(terrainThumpSFX, GetTerrainThumpVolume(impactInfo.impactSpeed), pitch, terrainCollisionSoundMaxVolumeScale);
        SpawnCollisionSparks(impactInfo.contact, impactInfo.impactSpeed);
    }

    bool TryGetAcceptedTerrainImpact(Collision collision, out CollisionImpactInfo impactInfo)
    {
        impactInfo = default;
        if (collision == null || terrainThumpSFX == null)
            return false;
        if (Time.time < terrainThumpPlayableAt)
            return false;

        Collider other = collision.collider;
        if (IsIgnoredCollisionFeedbackCollider(other))
            return false;
        if (terrainThumpPlayableAtByCollider.TryGetValue(other.GetInstanceID(), out float colliderPlayableAt) && Time.time < colliderPlayableAt)
            return false;
        if (collision.contactCount <= 0)
            return false;

        float impact = GetStrongestContactNormalImpact(collision, out ContactPoint strongestContact);
        if (impact < terrainCollisionSoundMinImpact)
            return false;

        impactInfo.otherCollider = other;
        impactInfo.contact = strongestContact;
        impactInfo.impactSpeed = impact;
        return true;
    }

    bool IsIgnoredCollisionFeedbackCollider(Collider other)
    {
        if (other == null || other.isTrigger)
            return true;
        if (other.transform.IsChildOf(transform))
            return true;
        if (other.GetComponentInParent<ParticleSystem>() != null)
            return true;

        Transform current = other.transform;
        while (current != null)
        {
            if (current.name.Contains("DashEcho"))
                return true;

            current = current.parent;
        }

        return false;
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

    float GetStrongestContactNormalImpact(Collision collision, out ContactPoint strongestContact)
    {
        strongestContact = collision.GetContact(0);
        float strongestImpact = 0f;
        Vector3 velocity = rb != null ? rb.linearVelocity : collision.relativeVelocity;
        Vector3 relativeVelocity = collision.relativeVelocity;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            if (contact.normal.y >= terrainCollisionGroundNormalThreshold)
                continue;

            float ownImpact = Mathf.Abs(Vector3.Dot(velocity, contact.normal));
            float relativeImpact = Mathf.Abs(Vector3.Dot(relativeVelocity, contact.normal));
            float contactImpact = Mathf.Max(ownImpact, relativeImpact);
            if (contactImpact <= strongestImpact)
                continue;

            strongestImpact = contactImpact;
            strongestContact = contact;
        }

        return strongestImpact;
    }

    void SpawnCollisionSparks(ContactPoint contact, float impactSpeed)
    {
        Vector3 normal = contact.normal.sqrMagnitude > 0.0001f ? contact.normal.normalized : Vector3.up;
        Vector3 position = contact.point + normal * 0.035f;

        if (collisionSparkParticlesPrefab == null)
        {
            RuntimeParticleFactory.SpawnCollisionSparks(position, normal, impactSpeed, collisionSparkTexture);
            return;
        }

        Quaternion rotation = Quaternion.LookRotation(normal, Vector3.up);
        ParticleSystem particles = Instantiate(collisionSparkParticlesPrefab, position, rotation);
        ApplyParticleTexture(particles, collisionSparkTexture, ref collisionSparkRuntimeMaterial, "Collision Spark Particle Material");
        particles.Play(true);
        Destroy(particles.gameObject, GetParticleSystemLifetime(particles));
    }

    static float GetParticleSystemLifetime(ParticleSystem particles)
    {
        if (particles == null)
            return 0.25f;

        ParticleSystem.MainModule main = particles.main;
        return Mathf.Max(0.25f, main.duration + main.startLifetime.constantMax + 0.25f);
    }

    void SetupRuntimeParticles()
    {
        if (!enableRuntimeParticles)
            return;

        string anchorName = name + "_VehicleParticles_" + GetInstanceID();
        GameObject existingAnchorObject = GameObject.Find(anchorName);
        if (existingAnchorObject == null)
        {
            GameObject anchorObject = new GameObject(anchorName);
            particleAnchor = anchorObject.transform;
        }
        else
        {
            particleAnchor = existingAnchorObject.transform;
        }

        particleAnchor.SetParent(null, true);
        UpdateParticleAnchorPose();

        SetupRearWheelDustParticles();

        Transform existingDriving = particleAnchor.Find("DrivingDustParticles");
        drivingDustParticles = existingDriving != null ? existingDriving.GetComponent<ParticleSystem>() : null;
        if (drivingDustParticles == null)
            drivingDustParticles = InstantiateParticlePrefabOrNull(drivingDustParticlesPrefab, ParticleAssetFolder + "/DrivingDustParticles.prefab", particleAnchor, "DrivingDustParticles");
        ConfigureDebrisParticles(drivingDustParticles, boosted: false);
        ApplyParticleTexture(drivingDustParticles, drivingDustTexture, ref drivingDustRuntimeMaterial, "Driving Dust Particle Material");
        SetParticleEmission(drivingDustParticles, false, 0f);

        Transform existingBoost = particleAnchor.Find("BoostDustParticles");
        boostDustParticles = existingBoost != null ? existingBoost.GetComponent<ParticleSystem>() : null;
        if (boostDustParticles == null)
            boostDustParticles = InstantiateParticlePrefabOrNull(boostDustParticlesPrefab, ParticleAssetFolder + "/BoostDustParticles.prefab", particleAnchor, "BoostDustParticles");
        ConfigureDebrisParticles(boostDustParticles, boosted: true);
        ApplyParticleTexture(boostDustParticles, boostDustTexture, ref boostDustRuntimeMaterial, "Boost Dust Particle Material");
        SetParticleEmission(boostDustParticles, false, 0f);

        Transform existingSparkle = particleAnchor.Find("DriftSparkleParticles");
        driftSparkleParticles = existingSparkle != null ? existingSparkle.GetComponent<ParticleSystem>() : null;
        if (driftSparkleParticles == null)
            driftSparkleParticles = InstantiateParticlePrefabOrNull(driftSparkleParticlesPrefab, DriftSparkleParticlePrefabPath, particleAnchor, "DriftSparkleParticles");
        if (driftSparkleParticles == null)
            driftSparkleParticles = RuntimeParticleFactory.CreateDriftSparkles(particleAnchor, "DriftSparkleParticles");
        ApplyParticleTexture(driftSparkleParticles, driftSparkleTexture, ref driftSparkleRuntimeMaterial, "Drift Sparkle Particle Material");
    }

    void UpdateRuntimeParticles()
    {
        if (!enableRuntimeParticles || particleAnchor == null)
            return;

        UpdateParticleAnchorPose();
        UpdateRearWheelParticleAnchors();
        float speed = CurrentSpeed;
        float speedRatio = Mathf.Clamp01(speed / Mathf.Max(0.01f, GetMaxSpeedAtFullBoostStacks()));
        bool groundedAndMoving = isGrounded && speed >= drivingParticleMinSpeed && (moveInput.y > 0.05f || boostActive);
        float driveRate = Mathf.Lerp(drivingParticleMaxRate * 0.55f, drivingParticleMaxRate * 2.4f, speedRatio);
        float boostRate = drivingParticleMaxRate * drivingParticleBoostRateMultiplier * Mathf.Lerp(0.85f, 1.25f, speedRatio);
        float driftRate = Mathf.Lerp(driftSparkleMaxRate * 0.35f, driftSparkleMaxRate, speedRatio);

        SetParticleEmission(drivingDustParticles, false, 0f);
        SetParticleEmission(boostDustParticles, false, 0f);
        UpdateRearWheelDustEmission(groundedAndMoving, boostActive ? boostRate : driveRate);
        SetParticleEmission(driftSparkleParticles, isGrounded && isDrifting, driftRate);
    }

    void SetupRearWheelDustParticles()
    {
        rearWheelParticleAnchors = new Transform[RearWheelParticleEmitterCount];
        rearWheelDustParticles = new ParticleSystem[RearWheelParticleEmitterCount];
        rearWheelParticleWheelIndices = GetRearWheelParticleIndices();
        rearWheelDustAppearanceMode = -1;

        for (int i = 0; i < RearWheelParticleEmitterCount; i++)
        {
            string sideName = i == 0 ? "RearLeft" : "RearRight";
            string anchorName = sideName + "WheelParticles";
            Transform anchor = particleAnchor.Find(anchorName);
            if (anchor == null)
            {
                GameObject anchorObject = new GameObject(anchorName);
                anchorObject.transform.SetParent(particleAnchor, true);
                anchor = anchorObject.transform;
            }

            rearWheelParticleAnchors[i] = anchor;

            string particleName = sideName + "WheelDustParticles";
            Transform existingParticles = anchor.Find(particleName);
            ParticleSystem particles = existingParticles != null ? existingParticles.GetComponent<ParticleSystem>() : null;
            if (particles == null)
                particles = InstantiateParticlePrefabOrNull(wheelDustParticlesPrefab, WheelDustParticlePrefabPath, anchor, particleName);
            if (particles == null)
                particles = RuntimeParticleFactory.CreateWheelDust(anchor, particleName, drivingDustColor);
            ConfigureDebrisParticles(particles, boosted: false);

            rearWheelDustParticles[i] = particles;
        }

        UpdateRearWheelParticleAnchors();
        ApplyRearWheelDustAppearance(boosted: false);
    }

    void UpdateRearWheelParticleAnchors()
    {
        if (rearWheelParticleAnchors == null || rearWheelParticleWheelIndices == null)
            return;

        for (int i = 0; i < rearWheelParticleAnchors.Length; i++)
        {
            Transform anchor = rearWheelParticleAnchors[i];
            if (anchor == null)
                continue;

            int wheelIndex = rearWheelParticleWheelIndices[Mathf.Min(i, rearWheelParticleWheelIndices.Length - 1)];
            Vector3 wheelWorldPosition = GetRearWheelParticleWorldPosition(wheelIndex);
            anchor.position = wheelWorldPosition + transform.TransformVector(rearWheelParticleOffset);
            anchor.rotation = GetParticleDebrisRotation();
        }
    }

    ParticleSystem InstantiateParticlePrefabOrNull(ParticleSystem prefab, string assetPath, Transform parent, string instanceName)
    {
        if (prefab != null)
        {
            GameObject runtimeInstance = Instantiate(prefab.gameObject);
            runtimeInstance.name = instanceName;
            runtimeInstance.transform.SetParent(parent, worldPositionStays: true);
            runtimeInstance.transform.localPosition = Vector3.zero;
            runtimeInstance.transform.rotation = Quaternion.identity;
            return runtimeInstance.GetComponent<ParticleSystem>();
        }

#if UNITY_EDITOR
        ParticleSystem editorPrefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>(assetPath);
        if (editorPrefab == null)
            return null;

        GameObject instance = PrefabUtility.InstantiatePrefab(editorPrefab.gameObject) as GameObject;
        if (instance == null)
            instance = Instantiate(editorPrefab.gameObject);

        instance.name = instanceName;
        instance.transform.SetParent(parent, worldPositionStays: true);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;
        return instance.GetComponent<ParticleSystem>();
#else
        return null;
#endif
    }

    void UpdateRearWheelDustEmission(bool active, float rate)
    {
        if (rearWheelDustParticles == null)
            return;

        ApplyRearWheelDustAppearance(boostActive);

        for (int i = 0; i < rearWheelDustParticles.Length; i++)
        {
            int wheelIndex = rearWheelParticleWheelIndices != null && i < rearWheelParticleWheelIndices.Length
                ? rearWheelParticleWheelIndices[i]
                : i;
            bool wheelGrounded = IsWheelGrounded(wheelIndex);
            SetParticleEmission(rearWheelDustParticles[i], active && wheelGrounded, rate);
        }
    }

    void ApplyRearWheelDustAppearance(bool boosted)
    {
        int appearanceMode = boosted ? 1 : 0;
        if (rearWheelDustAppearanceMode == appearanceMode || rearWheelDustParticles == null)
            return;

        rearWheelDustAppearanceMode = appearanceMode;
        Color color = boosted ? boostDustColor : drivingDustColor;
        for (int i = 0; i < rearWheelDustParticles.Length; i++)
            ApplyWheelDustAppearance(rearWheelDustParticles[i], color, boosted);
    }

    void ApplyWheelDustAppearance(ParticleSystem particles, Color color, bool boosted)
    {
        if (particles == null)
            return;

        ConfigureDebrisParticles(particles, boosted);

        ParticleSystem.MainModule main = particles.main;
        main.startLifetime = boosted
            ? new ParticleSystem.MinMaxCurve(0.14f, 0.28f)
            : new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
        main.startSize = boosted
            ? new ParticleSystem.MinMaxCurve(0.34f, 0.68f)
            : new ParticleSystem.MinMaxCurve(0.18f, 0.36f);
        main.startSpeed = boosted
            ? new ParticleSystem.MinMaxCurve(1.1f, 2.2f)
            : new ParticleSystem.MinMaxCurve(0.55f, 1.35f);
        main.startColor = new ParticleSystem.MinMaxGradient(color);
        main.maxParticles = boosted ? 1400 : 900;
        main.gravityModifier = boosted ? 0.85f : 0.62f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = boosted ? 10f : 15f;
        shape.radius = boosted ? 0.16f : 0.11f;
        shape.rotation = Vector3.zero;

        ParticleSystem.InheritVelocityModule inheritVelocity = particles.inheritVelocity;
        inheritVelocity.enabled = true;
        inheritVelocity.mode = ParticleSystemInheritVelocityMode.Initial;
        inheritVelocity.curve = new ParticleSystem.MinMaxCurve(boosted ? 0.18f : 0.08f);

        Texture2D selectedTexture = boosted && boostWheelDustTexture != null ? boostWheelDustTexture : wheelDustTexture;
        if (boosted && boostWheelDustTexture != null)
            ApplyParticleTexture(particles, boostWheelDustTexture, ref boostWheelDustRuntimeMaterial, "Boost Wheel Dust Particle Material", forceMaterial: true);
        else
            ApplyParticleTexture(particles, selectedTexture, ref wheelDustRuntimeMaterial, "Wheel Dust Particle Material", forceMaterial: boostWheelDustTexture != null);
    }

    static void ConfigureDebrisParticles(ParticleSystem particles, bool boosted)
    {
        if (particles == null)
            return;

        ParticleSystem.MainModule main = particles.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = boosted ? 0.85f : 0.62f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = boosted ? 10f : 15f;
        shape.radius = boosted ? 0.18f : 0.12f;
        shape.rotation = Vector3.zero;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = boosted
            ? new ParticleSystem.MinMaxCurve(-0.75f, 0.75f)
            : new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);
        velocity.y = boosted
            ? new ParticleSystem.MinMaxCurve(-0.22f, 0.16f)
            : new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
        velocity.z = boosted
            ? new ParticleSystem.MinMaxCurve(3.8f, 7.2f)
            : new ParticleSystem.MinMaxCurve(1.7f, 3.6f);
        velocity.speedModifier = new ParticleSystem.MinMaxCurve(1f, 1f);

        ParticleSystem.InheritVelocityModule inheritVelocity = particles.inheritVelocity;
        inheritVelocity.enabled = true;
        inheritVelocity.mode = ParticleSystemInheritVelocityMode.Initial;
        inheritVelocity.curve = new ParticleSystem.MinMaxCurve(boosted ? 0.18f : 0.08f);
    }

    Quaternion GetParticleDebrisRotation()
    {
        Vector3 direction = GetParticleDebrisDirection();
        if (direction.sqrMagnitude < 0.0001f)
            return Quaternion.identity;

        return Quaternion.LookRotation(direction, Vector3.up);
    }

    Vector3 GetParticleDebrisDirection()
    {
        Vector3 planarVelocity = rb != null ? rb.linearVelocity : Vector3.zero;
        planarVelocity.y = 0f;
        if (planarVelocity.sqrMagnitude > 0.25f)
            return -planarVelocity.normalized;

        Vector3 planarForward = transform.forward;
        planarForward.y = 0f;
        if (planarForward.sqrMagnitude > 0.0001f)
            return -planarForward.normalized;

        return Vector3.back;
    }

    void ApplyParticleTexture(ParticleSystem particles, Texture texture, ref Material material, string materialName, bool forceMaterial = false)
    {
        if (particles == null)
            return;
        if (texture == null && !forceMaterial)
            return;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
            return;

        if (material == null)
            material = CreateParticleTextureMaterial(materialName);
        if (material == null)
            return;

        SetParticleMaterialTexture(material, texture);
        renderer.sharedMaterial = material;
    }

    Material CreateParticleTextureMaterial(string materialName)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return null;

        Material material = new Material(shader)
        {
            name = materialName
        };

        ForceTransparentMaterial(material);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);

        return material;
    }

    static void SetParticleMaterialTexture(Material material, Texture texture)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);
    }

    void DestroyRuntimeParticleMaterials()
    {
        DestroyRuntimeMaterial(ref drivingDustRuntimeMaterial);
        DestroyRuntimeMaterial(ref boostDustRuntimeMaterial);
        DestroyRuntimeMaterial(ref wheelDustRuntimeMaterial);
        DestroyRuntimeMaterial(ref boostWheelDustRuntimeMaterial);
        DestroyRuntimeMaterial(ref driftSparkleRuntimeMaterial);
        DestroyRuntimeMaterial(ref collisionSparkRuntimeMaterial);
    }

    static void DestroyRuntimeMaterial(ref Material material)
    {
        if (material == null)
            return;

        Destroy(material);
        material = null;
    }

    Vector3 GetRearWheelParticleWorldPosition(int wheelIndex)
    {
        if (wheelIndex >= 0 && wheelStates != null && wheelIndex < wheelStates.Length)
            return wheelStates[wheelIndex].wheelWorldPosition;

        return transform.TransformPoint(GetWheelLocalPosition(Mathf.Max(0, wheelIndex)));
    }

    bool IsWheelGrounded(int wheelIndex)
    {
        if (wheelIndex < 0 || wheelStates == null || wheelIndex >= wheelStates.Length)
            return isGrounded;

        return wheelStates[wheelIndex].grounded;
    }

    int[] GetRearWheelParticleIndices()
    {
        int leftRearIndex = -1;
        int rightRearIndex = -1;
        int fallbackRearIndex = -1;
        float centerZ = GetSuspensionCenterLocalZ();
        int wheelCount = GetWheelCount();

        for (int i = 0; i < wheelCount; i++)
        {
            Vector3 local = GetWheelLocalPosition(i);
            if (local.z >= centerZ)
                continue;

            fallbackRearIndex = i;
            if (local.x < 0f)
                leftRearIndex = i;
            else
                rightRearIndex = i;
        }

        if (leftRearIndex < 0)
            leftRearIndex = fallbackRearIndex >= 0 ? fallbackRearIndex : Mathf.Max(0, wheelCount - 2);
        if (rightRearIndex < 0)
            rightRearIndex = fallbackRearIndex >= 0 && fallbackRearIndex != leftRearIndex ? fallbackRearIndex : Mathf.Max(0, wheelCount - 1);

        return new[] { leftRearIndex, rightRearIndex };
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

        particleAnchor.position = transform.TransformPoint(localPosition);
        particleAnchor.rotation = GetParticleDebrisRotation();
    }

    void StopRuntimeParticles()
    {
        SetParticleEmission(drivingDustParticles, false, 0f);
        SetParticleEmission(boostDustParticles, false, 0f);
        SetParticleEmission(driftSparkleParticles, false, 0f);

        if (rearWheelDustParticles == null)
            return;

        for (int i = 0; i < rearWheelDustParticles.Length; i++)
            SetParticleEmission(rearWheelDustParticles[i], false, 0f);
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
    float GetEffectiveFallGravityMultiplier() => Mathf.Max(1f, fallGravityMultiplier);
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
