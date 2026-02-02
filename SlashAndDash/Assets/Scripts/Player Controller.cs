// Full CarController with vehicle-dimension + mass driven auto-calculation
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class CarController : MonoBehaviour
{
    [Header("Movement")]
    public float accelerationForce = 1300f;
    public float maxSpeed = 22f;
    public float turnSpeed = 120f;

    [Header("Boost")]
    public float driftBoostAmount = 12f; // dynamic on drift end
    public float driftBoostDuration = 1.2f;

    [Header("Jump")]
    public float jumpForce = 9f;
    public LayerMask groundLayer;

    [Header("Arcade Stability")]
    public Vector3 centerOfMassOffset = new Vector3(0f, -0.7f, -0.4f);
    public float groundAngularDamping = 10f;
    public float airAngularDamping = 3f;
    public float rampPitchDamping = 0.35f;
    public float groundedDownforce = 35f;

    [Header("Arcade Gravity")]
    public float airGravityMultiplier = 1.6f;
    public float fallGravityMultiplier = 2.2f;

    [Header("Arcade Drift")]
    public float driftSideForce = 2200f;
    public float driftGripRecovery = 3.5f;
    public float minDriftSpeed = 5f;
    public float driftSteerMultiplier = 1.25f;

    [Tooltip("Seconds of continuous drifting to reach full drift boost (1.0 = 1s to full).")]
    public float driftChargeTime = 1f;
    public float maxDriftBoost = 12f;

    [Header("Drift Pivoting (front vs rear balance)")]
    public float frontPivotDistance = 3.0f;
    public float rearPivotDistance = 2.0f;
    public float driftYawTorque = 80f;
    public float lateralSlipThreshold = 2.0f;

    private float driftCharge = 0f;

    [Header("Air Dash")]
    public float airDashForce = 20f;
    public float airDashUpForce = 4f;
    public float airDashCooldown = 0.15f;

    [Header("Fake Wheels / Suspension")]
    [Tooltip("Wheel contact transforms (any order). Place near contact point bottom of each corner).")]
    public Transform[] wheelTransforms;
    [Tooltip("Distance above wheel used as the ray origin; ray length = suspensionDistance * 2")]
    public float suspensionDistance = 0.5f;
    [Tooltip("Spring stiffness. Large numbers ok — we're using ForceMode.Acceleration.")]
    public float suspensionStiffness = 20000f;
    [Tooltip("Suspension damper; reduces oscillation.")]
    public float suspensionDamping = 500f;
    [Tooltip("Clamps per-wheel suspension force.")]
    public float suspensionMaxForcePerWheel = 20000f;

    [Header("Tire Grip (speed-dependent)")]
    [Tooltip("Base lateral grip per wheel (higher = less sliding).")]
    public float tireGrip = 60f;
    [Tooltip("Curve that maps speed ratio (0..1) to grip multiplier. X= speed ratio, Y = multiplier.")]
    public AnimationCurve gripCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.4f);
    [Tooltip("Front/rear multipliers applied to base tireGrip (1 = same as base).")]
    public float frontTireGrip = 1f;
    public float rearTireGrip = 1f;
    [Tooltip("Grip falls off additionally with forward speed multiplier (legacy multiplier kept for quick tuning).")]
    public float tireGripSpeedFalloff = 0.6f;

    [Header("Rolling Resistance / Braking (front/rear)")]
    [Tooltip("Longitudinal rolling resistance (front wheels).")]
    public float frontRollingResistance = 18f;
    [Tooltip("Longitudinal rolling resistance (rear wheels).")]
    public float rearRollingResistance = 18f;
    [Tooltip("Coast drag applied per-wheel when player is not giving throttle (front).")]
    public float frontCoastDrag = 20f;
    [Tooltip("Coast drag applied per-wheel when player is not giving throttle (rear).")]
    public float rearCoastDrag = 20f;
    [Tooltip("Braking force applied per-wheel when the player pulls negative throttle (brake/reverse).")]
    public float brakeForce = 80f;
    [Tooltip("Multiplier applied to rear rolling resistance while drifting; <1 reduces rear resistance to help maintain slide.")]
    public float driftRearRollingResistanceMultiplier = 0.45f;

    [Header("Hill / Slope")]
    [Tooltip("How much extra acceleration you get when going downhill (units of ForceMode.Acceleration). Set to 0 to disable.")]
    public float downhillAcceleration = 12f;

    [Header("Slope Limits")]
    [Tooltip("Maximum slope angle (deg) car can drive up. Set >= 45 for 45° ramps to be climbable.")]
    public float maxDriveSlopeAngle = 60f;
    public float steepSlopeSlideForce = 25f;

    [Header("Leave Ground")]
    public float leaveGroundForwardBoost = 0.25f;

    [Header("Slope Sampling")]
    [Tooltip("How far forwards/back from the car center to sample the ground for slope calculation (meters).")]
    public float slopeSampleDistance = 1.0f;
    [Tooltip("Ignore very small slopes (degrees) so tiny bumps don't count.")]
    public float minSlopeAngleToAffect = 1f;

    [Header("Debug")]
    public bool showSuspensionRays = true;
    public bool showSurfaceNormals = true;
    public Color driveableColor = Color.green;
    public Color steepColor = Color.red;
    public float debugSphereSize = 0.08f;

    // ---------------- Vehicle physical parameters (user inputs) ----------------
    [Header("Vehicle Dimensions & Mass")]
    [Tooltip("Vehicle mass in kilograms.")]
    public float vehicleMass = 1200f;
    [Tooltip("Distance between front and rear axle (meters).")]
    public float wheelBase = 2.6f;
    [Tooltip("Vehicle width (track) in meters.")]
    public float trackWidth = 1.6f;
    [Tooltip("Height of center of mass above the ground in meters (positive).")]
    public float comHeight = 0.5f;
    [Range(0.0f, 1.0f), Tooltip("Fraction of weight on front axle (0..1). 0.5 = even split.")]
    public float frontWeightRatio = 0.5f;

    [Tooltip("When true the script will compute sensible defaults (suspension, grip, rolling resistance, brakes) from the vehicle mass/dimensions.")]
    public bool autoCalculatePhysics = true;

    [Header("Auto-Calc Scale Knobs")]
    [Tooltip("Multiplier applied to computed suspension stiffness (use to bias soft/stiff feel).")]
    public float suspensionStiffnessScale = 3.0f;
    [Tooltip("Multiplier applied to computed suspension damping.")]
    public float suspensionDampingScale = 1.0f;
    [Tooltip("Global scale on computed tire grip.")]
    public float gripScale = 1.0f;
    [Tooltip("Global scale on rolling/brake numbers.")]
    public float rollingResistanceScale = 1.0f;

    // internals
    private Rigidbody rb;
    private Collider col;
    private PlayerInputActions controls;
    private Vector2 moveInput;

    private bool isGrounded;
    private bool wasGrounded;
    private bool isDrifting;
    private bool boostActive;
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
        controls.Player.Jump.performed += _ => Jump();
        controls.Player.Dash.performed += _ => TryAirDash();
    }

    private void OnValidate()
    {
        // keep editor responsive: compute derived stats when inspector is edited
        ComputeDerivedStats();
    }

    private void OnEnable() => controls.Player.Enable();
    private void OnDisable() => controls.Player.Disable();

    private bool CanControl() => isGrounded;

    private void FixedUpdate()
    {
        wasGrounded = isGrounded;
        isGrounded = CheckGrounded();
        slopeAngle = isGrounded ? Vector3.Angle(groundHit.normal, Vector3.up) : 0f;

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
        if (Mathf.Abs(moveInput.x) < 0.01f) return;

        float steerStrength = isDrifting ? driftSteerMultiplier : 1f;
        float turn = moveInput.x * turnSpeed * steerStrength * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn, 0f));
    }

    // ---------------- DRIFT ----------------
    private void HandleDrift()
    {
        if (!isGrounded) { isDrifting = false; return; }

        float speed = rb.linearVelocity.magnitude;
        bool wasDriftingLocal = isDrifting;

        isDrifting = speed > minDriftSpeed && Mathf.Abs(moveInput.x) > 0.2f && moveInput.y > 0.1f;

        if (isDrifting)
        {
            // lateral force to push car sideways
            Vector3 driftDir = transform.right * Mathf.Sign(moveInput.x);
            rb.AddForce(driftDir * driftSideForce * Time.fixedDeltaTime, ForceMode.Acceleration);

            // pivot logic based on front/rear lateral velocities
            float frontLat = GetAverageLateralVelocity(true);
            float rearLat = GetAverageLateralVelocity(false);
            float frontGripFactor = 1f / (1f + Mathf.Abs(frontLat) / Mathf.Max(0.001f, lateralSlipThreshold));
            float rearSlipFactor = Mathf.Clamp01(Mathf.Abs(rearLat) / lateralSlipThreshold);
            float pivotT = Mathf.Clamp01(rearSlipFactor * frontGripFactor);
            float pivotDistance = Mathf.Lerp(-rearPivotDistance, frontPivotDistance, pivotT);

            float speedFactor = Mathf.Clamp01(rb.linearVelocity.magnitude / Mathf.Max(0.1f, maxSpeed));
            float yawAmount = moveInput.x * driftYawTorque * (1f + Mathf.Abs(pivotDistance) / (Mathf.Max(frontPivotDistance, rearPivotDistance) + 0.001f)) * speedFactor;
            rb.AddTorque(Vector3.up * yawAmount * Time.fixedDeltaTime, ForceMode.Acceleration);

            // charge drift boost
            if (driftChargeTime <= 0f) driftCharge = 1f;
            else
            {
                driftCharge += Time.fixedDeltaTime / Mathf.Max(0.0001f, driftChargeTime);
                driftCharge = Mathf.Clamp01(driftCharge);
            }
        }

        if (wasDriftingLocal && !isDrifting)
        {
            boostActive = true;
            boostTimer = driftBoostDuration;
            driftBoostAmount = maxDriftBoost * driftCharge;
            driftCharge = 0f;
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
        if (!boostActive) return;
        boostTimer -= Time.fixedDeltaTime;
        if (boostTimer <= 0f) boostActive = false;
    }

    private void ClampSpeed()
    {
        float allowedSpeed = boostActive ? (maxSpeed + driftBoostAmount) : maxSpeed;
        if (rb.linearVelocity.magnitude > allowedSpeed) rb.linearVelocity = rb.linearVelocity.normalized * allowedSpeed;
    }

    // ---------------- JUMP ----------------
    private void Jump()
    {
        if (!isGrounded) return;
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
    }

    // ---------------- AIR DASH ----------------
    private void TryAirDash()
    {
        if (isGrounded || airDashUsed) return;
        airDashUsed = true;
        airDashTimer = airDashCooldown;
        Vector3 vel = rb.linearVelocity;
        vel = new Vector3(0f, Mathf.Max(vel.y, 0f), 0f);
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
    }
}
