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
    public float driftBoostAmount = 12f;
    public float driftBoostDuration = 1.2f;

    [Header("Jump")]
    public float jumpForce = 9f;
    public float groundCheckDistance = 0.45f;
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
    public float driftChargeRate = 1f; // units per second, how fast boost fills
    public float maxDriftBoost = 12f; // max boost that can be gained
    private float driftCharge = 0f; // current drift charge



    [Header("Air Dash")]
    public float airDashForce = 20f;
    public float airDashUpForce = 4f;
    public float airDashCooldown = 0.15f;

    [Header("Fake Wheels / Suspension")]
    public Transform[] wheelTransforms; // 4 corners of the car
    public float suspensionDistance = 0.5f;
    public float suspensionStiffness = 10f;
    public float suspensionDamping = 5f;

    private Rigidbody rb;
    private Collider col;
    private PlayerInputActions controls;
    private Vector2 moveInput;

    private bool isGrounded;
    private bool isDrifting;
    private bool boostActive;
    private float boostTimer;

    private bool airDashUsed;
    private float airDashTimer;

    private RaycastHit groundHit;
    private float slopeAngle;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        rb.useGravity = true;
        rb.centerOfMass += centerOfMassOffset;
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

    private void OnEnable() => controls.Player.Enable();
    private void OnDisable() => controls.Player.Disable();

    private bool CanControl() => isGrounded;

    private void FixedUpdate()
    {
        isGrounded = CheckGrounded();
        slopeAngle = isGrounded ? Vector3.Angle(groundHit.normal, Vector3.up) : 0f;

        rb.angularDamping = isGrounded ? groundAngularDamping : airAngularDamping;

        if (isGrounded)
            airDashUsed = false;

        // Only allow ground movement
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
            ApplySuspension(); // NEW: fake wheels suspension
            DampRampPitch();
        }
        else
        {
            ApplyArcadeGravity();
            AlignUprightInAir();
        }
    }

    // ---------------- MOVEMENT ----------------
    private void HandleAcceleration()
    {
        if (Mathf.Abs(moveInput.y) < 0.01f) return;

        float speedRatio = rb.linearVelocity.magnitude / maxSpeed;
        float accelFalloff = Mathf.Lerp(1f, 0.35f, speedRatio);

        Vector3 forwardDir = Vector3.ProjectOnPlane(transform.forward, groundHit.normal).normalized;

        rb.AddForce(forwardDir * moveInput.y * accelerationForce * accelFalloff, ForceMode.Acceleration);
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
    if (!isGrounded)
    {
        isDrifting = false;
        return;
    }

    float speed = rb.linearVelocity.magnitude;
    bool wasDrifting = isDrifting;

    isDrifting = speed > minDriftSpeed && Mathf.Abs(moveInput.x) > 0.2f && moveInput.y > 0.1f;

    if (isDrifting)
    {
        // Drift side force
        Vector3 driftDir = transform.right * Mathf.Sign(moveInput.x);
        rb.AddForce(driftDir * driftSideForce * Time.fixedDeltaTime, ForceMode.Acceleration);

        // Increase drift charge over time, clamped to 1
        driftCharge += driftChargeRate * Time.fixedDeltaTime;
        driftCharge = Mathf.Clamp01(driftCharge);
    }

    if (wasDrifting && !isDrifting)
    {
        // Apply boost proportional to charge
        boostActive = true;
        boostTimer = driftBoostDuration;
        driftBoostAmount = maxDriftBoost * driftCharge;

        driftCharge = 0f; // reset charge
    }

    if (!isDrifting)
    {
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        localVel.x = Mathf.Lerp(localVel.x, 0f, driftGripRecovery * Time.fixedDeltaTime * Mathf.Clamp01(speed / maxSpeed));
        rb.linearVelocity = transform.TransformDirection(localVel);
    }
}


    // ---------------- BOOST ----------------
    private void HandleBoost()
    {
        if (!boostActive) return;

        boostTimer -= Time.fixedDeltaTime;
        if (boostTimer <= 0f)
            boostActive = false;
    }

    private void ClampSpeed()
    {
        float allowedSpeed = boostActive ? maxSpeed + driftBoostAmount : maxSpeed;

        if (rb.linearVelocity.magnitude > allowedSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * allowedSpeed;
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
        if (isGrounded || airDashUsed)
            return;

        airDashUsed = true;
        airDashTimer = airDashCooldown;

        Vector3 vel = rb.linearVelocity;
        vel = new Vector3(0f, Mathf.Max(vel.y, 0f), 0f);
        rb.linearVelocity = vel;

        rb.AddForce(transform.forward * airDashForce + Vector3.up * airDashUpForce, ForceMode.VelocityChange);
    }

    // ---------------- ARCADE HELPERS ----------------
    private void ApplyArcadeDownforce()
    {
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
        if (airDashTimer > 0f)
        {
            airDashTimer -= Time.fixedDeltaTime;
            return;
        }

        float gravityMult = rb.linearVelocity.y < 0f ? fallGravityMultiplier : airGravityMultiplier;
        rb.AddForce(Physics.gravity * (gravityMult - 1f), ForceMode.Acceleration);
    }

    private void AlignUprightInAir()
    {
        Quaternion target = Quaternion.Euler(0f, rb.rotation.eulerAngles.y, 0f);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, target, 4f * Time.fixedDeltaTime));
    }

    // ---------------- SUSPENSION ----------------
    private void ApplySuspension()
    {
        if (wheelTransforms.Length == 0) return;

        Vector3 averagePos = Vector3.zero;
        Vector3 averageNormal = Vector3.zero;

        foreach (Transform wheel in wheelTransforms)
        {
            Ray ray = new Ray(wheel.position + Vector3.up * suspensionDistance, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, suspensionDistance * 2f, groundLayer))
            {
                float suspensionOffset = suspensionDistance - (hit.distance - suspensionDistance);
                averagePos += wheel.position + Vector3.up * suspensionOffset;
                averageNormal += hit.normal;
            }
            else
            {
                averagePos += wheel.position + Vector3.down * suspensionDistance;
                averageNormal += Vector3.up;
            }
        }

        averagePos /= wheelTransforms.Length;
        averageNormal.Normalize();

        // Smoothly move the car to the average wheel height
        Vector3 targetPos = new Vector3(rb.position.x, averagePos.y, rb.position.z);
        rb.position = Vector3.Lerp(rb.position, targetPos, suspensionStiffness * Time.fixedDeltaTime);

        // Align the car to the average normal
        Quaternion targetRot = Quaternion.FromToRotation(transform.up, averageNormal) * rb.rotation;
        rb.rotation = Quaternion.Slerp(rb.rotation, targetRot, suspensionDamping * Time.fixedDeltaTime);
    }

    // ---------------- GROUND CHECK ----------------
    private bool CheckGrounded()
    {
        if (wheelTransforms.Length == 0) return false;

        bool grounded = false;
        Vector3 averageNormal = Vector3.zero;
        int hitCount = 0;

        foreach (Transform wheel in wheelTransforms)
        {
            Ray ray = new Ray(wheel.position + Vector3.up * suspensionDistance, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, suspensionDistance * 2f, groundLayer))
            {
                grounded = true; // if at least one wheel hits, car is grounded
                averageNormal += hit.normal;
                hitCount++;
            }
        }

        if (hitCount > 0)
            groundHit.normal = averageNormal.normalized;

        return grounded;
    }

}
