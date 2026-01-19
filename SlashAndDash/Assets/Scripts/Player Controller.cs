using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float acceleration = 10f;      // How fast we correct velocity toward the desired velocity
    public float maxVelocity = 6f;        // Base maximum horizontal speed
    public float weight = 1.5f;           // Higher = harder to change direction (like car weight)

    [Header("Jump")]
    public float jumpForce = 5f;          // Instant vertical velocity set when jumping
    public float groundCheckDistance = 0.15f;
    public LayerMask groundMask;

    [Header("Drift / Boost")]
    [Tooltip("Factor applied to maxVelocity while drifting (ex: 0.6 means you are slower during the drift)")]
    public float driftSlowFactor = 0.6f;
    [Tooltip("How fast boost builds up while drifting (units per second)")]
    public float driftBuildRate = 1f;
    [Tooltip("Maximum extra speed (in world units per second) that can be added by drift boost")]
    public float driftMaxBoost = 3f;
    [Tooltip("How long the temporary drift boost lasts (seconds)")]
    public float driftBoostDuration = 1.5f;

    [Header("References")]
    public Transform cameraTransform;    // Use camera forward/right for movement basis

    [Header("Input (assign via Input Actions or PlayerInput)")]
    public InputActionReference moveAction;   // Vector2
    public InputActionReference lookAction;   // Vector2 (optional for orientation)
    public InputActionReference jumpAction;   // Button
    public InputActionReference driftAction;  // Button

    // runtime
    Rigidbody rb;
    Vector2 moveInput;
    bool jumpPressed;
    bool driftHeld;
    float driftCharge = 0f;      // current stored boost
    bool boosting = false;
    float boostRemaining = 0f;
    float activeBoostAmount = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.freezeRotation = true; // let physics handle position but not rotate the capsule
    }

    void OnEnable()
    {
        if (moveAction != null) moveAction.action.Enable();
        if (jumpAction != null) jumpAction.action.Enable();
        if (driftAction != null) driftAction.action.Enable();
        if (lookAction != null) lookAction.action.Enable();
    }
    void OnDisable()
    {
        if (moveAction != null) moveAction.action.Disable();
        if (jumpAction != null) jumpAction.action.Disable();
        if (driftAction != null) driftAction.action.Disable();
        if (lookAction != null) lookAction.action.Disable();
    }

    void Update()
    {
        if (moveAction != null)
            moveInput = moveAction.action.ReadValue<Vector2>();
        else
            moveInput = Vector2.zero;

        if (jumpAction != null)
            jumpPressed = jumpAction.action.triggered;
        else
            jumpPressed = false;

        if (driftAction != null)
            driftHeld = driftAction.action.ReadValue<float>() > 0.5f;
        else
            driftHeld = false;
    }

    void FixedUpdate()
    {
        // compute movement basis (relative to camera)
        Vector3 basisForward = Vector3.forward;
        Vector3 basisRight = Vector3.right;
        if (cameraTransform != null)
        {
            Vector3 camF = cameraTransform.forward;
            camF.y = 0f;
            if (camF.sqrMagnitude < 0.001f) camF = Vector3.forward;
            basisForward = camF.normalized;
            basisRight = Quaternion.Euler(0, 90, 0) * basisForward;
        }

        Vector3 desiredDir = (basisForward * moveInput.y + basisRight * moveInput.x);
        float inputMag = Mathf.Clamp01(desiredDir.magnitude);
        if (inputMag > 0.001f) desiredDir.Normalize();

        // effective max depending on drift/boost
        float effectiveMax = maxVelocity + (boosting ? activeBoostAmount : 0f);
        if (driftHeld)
        {
            // during drift you are slower (charge builds up)
            effectiveMax = maxVelocity * driftSlowFactor;
        }

        Vector3 desiredVelocity = desiredDir * effectiveMax * inputMag;

        // current horizontal velocity
        Vector3 currentVel = rb.linearVelocity;
        Vector3 currentHoriz = new Vector3(currentVel.x, 0f, currentVel.z);

        // velocity difference that we want to correct
        Vector3 velDiff = desiredVelocity - currentHoriz;

        // acceleration modified by weight: heavier = slower response
        float effectiveAccel = acceleration / Mathf.Max(0.001f, weight);

        // apply acceleration as ForceMode.Acceleration (ignores mass, intuitive tuning)
        Vector3 accelForce = velDiff * effectiveAccel;
        rb.AddForce(accelForce, ForceMode.Acceleration);

        // Jump: preserves horizontal momentum automatically because we set only Y velocity.
        if (jumpPressed && IsGrounded())
        {
            Vector3 newVel = rb.linearVelocity;
            newVel.y = jumpForce;
            rb.linearVelocity = newVel;
        }

        // Drift charge mechanic
        if (driftHeld && inputMag > 0.1f && IsGrounded())
        {
            driftCharge += driftBuildRate * Time.fixedDeltaTime;
            driftCharge = Mathf.Min(driftCharge, driftMaxBoost);
        }

        // if player releases drift (or stops holding), apply boost if any
        if (!driftHeld && driftCharge > 0.001f)
        {
            StartBoost(driftCharge);
            driftCharge = 0f;
        }

        // handle boost timer
        if (boosting)
        {
            boostRemaining -= Time.fixedDeltaTime;
            if (boostRemaining <= 0f)
            {
                boosting = false;
                activeBoostAmount = 0f;
            }
        }
    }

    void StartBoost(float amount)
    {
        activeBoostAmount = Mathf.Clamp(amount, 0f, driftMaxBoost);
        boosting = true;
        boostRemaining = driftBoostDuration;
    }

    bool IsGrounded()
    {
        // simple raycast down - make sure player's pivot is at foot area or tweak distance
        float checkDist = groundCheckDistance + 0.01f;
        return Physics.Raycast(transform.position, Vector3.down, checkDist, groundMask);
    }
}