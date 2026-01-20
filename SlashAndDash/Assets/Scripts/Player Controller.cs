using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Car Movement Settings")]
    public float accelerationForce = 2000f;
    public float maxSpeed = 25f;
    public float turnSpeed = 150f;           // Degrees per second
    public float driftFactorMin = 0.7f;      // More drift on sharp turns
    public float driftFactorMax = 0.98f;     // Less drift when going straight
    public float driftSpeedMultiplier = 0.8f; // Slow down while drifting

    [Header("Drift Boost Settings")]
    public float driftBoostAmount = 15f;     // Extra speed during boost
    public float driftBoostDuration = 1f;    // Duration of boost in seconds

    [Header("Jump Settings")]
    public float jumpForce = 10f;            // Upward velocity applied
    public float groundCheckDistance = 0.2f; // Distance for ground raycast
    public LayerMask groundLayer;            // Layers considered as ground

    [Header("Debug Settings")]
    public bool showDebug = true;

    private Rigidbody rb;
    private Vector2 moveInput;
    private PlayerInputActions controls;

    private bool isDrifting = false;
    private bool boostActive = false;
    private float boostTimer = 0f;

    private string debugText = "";

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.linearDamping = 0.5f;
        rb.angularDamping = 0.5f;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Initialize input
        controls = new PlayerInputActions();
        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += ctx => moveInput = Vector2.zero;
        controls.Player.Jump.performed += ctx => Jump();
    }

    private void OnEnable() => controls.Player.Enable();
    private void OnDisable() => controls.Player.Disable();

    private void FixedUpdate()
    {
        HandleAcceleration();
        HandleSteering();
        ApplyDrift();
        HandleBoost();
        UpdateDebugText();
    }

    private void HandleAcceleration()
    {
        float speedMultiplier = isDrifting ? driftSpeedMultiplier : 1f;

        Vector3 force = transform.forward * moveInput.y * accelerationForce * Time.fixedDeltaTime * speedMultiplier;
        rb.AddForce(force);

        // Max speed clamp (boost temporarily increases it)
        float currentMaxSpeed = boostActive ? maxSpeed + driftBoostAmount : maxSpeed;

        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (flatVel.magnitude > currentMaxSpeed)
        {
            flatVel = flatVel.normalized * currentMaxSpeed;
            rb.linearVelocity = new Vector3(flatVel.x, rb.linearVelocity.y, flatVel.z);
        }
    }

    private void HandleSteering()
    {
        float turnAmount = moveInput.x * turnSpeed * Time.fixedDeltaTime;
        transform.Rotate(0f, turnAmount, 0f);
    }

    private void ApplyDrift()
    {
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);

        bool wasDrifting = isDrifting;
        isDrifting = Mathf.Abs(moveInput.x) > 0.1f && moveInput.y > 0.1f;

        if (wasDrifting && !isDrifting)
        {
            boostActive = true;
            boostTimer = driftBoostDuration;
        }

        float inputMagnitude = Mathf.Abs(moveInput.x);
        float driftFactor = Mathf.Lerp(driftFactorMin, driftFactorMax, 1f - inputMagnitude);
        localVel.x *= driftFactor;

        rb.linearVelocity = transform.TransformDirection(localVel);
    }

    private void HandleBoost()
    {
        if (!boostActive) return;

        boostTimer -= Time.fixedDeltaTime;
        if (boostTimer <= 0f)
        {
            boostActive = false;
            boostTimer = 0f;
        }
    }

    private void Jump()
    {
        if (IsGrounded())
        {
            // Reset vertical velocity for consistent jumps
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
        }
    }

    private bool IsGrounded()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return false;

        // Raycast from slightly above the bottom of the collider
        Vector3 origin = col.bounds.center - new Vector3(0, col.bounds.extents.y - 0.05f, 0);

        // Draw debug line in scene view
        if (showDebug)
            Debug.DrawRay(origin, Vector3.down * groundCheckDistance, Color.red, 0.1f);

        return Physics.Raycast(origin, Vector3.down, groundCheckDistance, groundLayer);
    }

    private void UpdateDebugText()
    {
        if (!showDebug) return;

        debugText =
            $"--- INPUT ---\n" +
            $"Move Input: X={moveInput.x:F2}, Y={moveInput.y:F2}\n\n" +

            $"--- DRIFT ---\n" +
            $"IsDrifting: {isDrifting}\n" +
            $"DriftFactorMin: {driftFactorMin:F2}\n" +
            $"DriftFactorMax: {driftFactorMax:F2}\n" +
            $"DriftSpeedMultiplier: {driftSpeedMultiplier:F2}\n\n" +

            $"--- BOOST ---\n" +
            $"BoostActive: {boostActive}\n" +
            $"BoostTimeLeft: {boostTimer:F2}\n" +
            $"DriftBoostAmount: {driftBoostAmount:F2}\n" +
            $"DriftBoostDuration: {driftBoostDuration:F2}\n\n" +

            $"--- SPEED ---\n" +
            $"Linear Velocity: {rb.linearVelocity.magnitude:F2}\n" +
            $"MaxSpeed: {maxSpeed}\n" +
            $"CurrentMaxSpeed: {(boostActive ? maxSpeed + driftBoostAmount : maxSpeed):F2}\n\n" +

            $"--- JUMP ---\n" +
            $"IsGrounded: {IsGrounded()}\n" +
            $"JumpForce: {jumpForce}\n" +
            $"GroundCheckDistance: {groundCheckDistance}";
    }

    private void OnGUI()
    {
        if (!showDebug) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 16;
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(10, 10, 600, 500), debugText, style);
    }
}
