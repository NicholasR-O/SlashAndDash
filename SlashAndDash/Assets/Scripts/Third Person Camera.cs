using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Transform cameraRoot;

    [Header("Camera Offset")]
    public Vector3 baseOffset = new Vector3(0f, 1.8f, -3.5f);

    [Header("Zoom (Look Y)")]
    public float minDistance = 2.5f;
    public float maxDistance = 6.5f;
    public float zoomSpeed = 4f;
    public bool invertZoomInput = false;

    [Header("Speed Pullback")]
    public float speedPullback = 1.2f;
    public float maxPullback = 2f;

    [Header("Rotation")]
    public float fixedPitch = 12f;
    public float lookSpeed = 1.5f;
    [Tooltip("How fast the camera recenters toward movement direction (higher = faster).")]
    public float autoCenterSpeed = 3.5f; // increased by default
    [Tooltip("Multiplier applied to auto-centering when player is airborne.")]
    public float airAutoCenterMultiplier = 0.35f;
    public float lookInputDeadzone = 0.02f;

    [Header("Lag")]
    public float positionLag = 10f;
    public float rotationLag = 12f;

    [Header("Input")]
    public InputActionReference lookAction;

    float yaw;
    float currentDistance;
    Vector3 currentVelocity;

    void OnEnable() => lookAction?.action.Enable();
    void OnDisable() => lookAction?.action.Disable();

    void Start()
    {
        if (cameraRoot == null)
            cameraRoot = transform;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        yaw = cameraRoot.eulerAngles.y;
        currentDistance = -baseOffset.z; // positive distance
    }

    void LateUpdate()
    {
        if (player == null) return;

        Rigidbody rb = player.GetComponent<Rigidbody>();
        // simpler grounded check for camera: use small vertical velocity threshold
        bool isGrounded = rb != null && Mathf.Abs(rb.linearVelocity.y) < 0.1f;

        Vector2 look = lookAction != null
            ? lookAction.action.ReadValue<Vector2>()
            : Vector2.zero;

        // ---------------- HORIZONTAL LOOK (YAW) ----------------
        bool playerIsActivelyLooking = Mathf.Abs(look.x) > lookInputDeadzone;

        if (playerIsActivelyLooking)
        {
            yaw += look.x * lookSpeed * Time.deltaTime * 120f;
        }
        else if (rb != null)
        {
            Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (flatVel.sqrMagnitude > 0.05f)
            {
                float targetYaw = Mathf.Atan2(flatVel.x, flatVel.z) * Mathf.Rad2Deg;
                float airMult = isGrounded ? 1f : airAutoCenterMultiplier;

                // increase rate of recentering via autoCenterSpeed
                yaw = Mathf.LerpAngle(
                    yaw,
                    targetYaw,
                    autoCenterSpeed * airMult * Time.deltaTime
                );
            }
        }

        // ---------------- VERTICAL LOOK → ZOOM ----------------
        float zoomInput = Mathf.Abs(look.y) > lookInputDeadzone ? look.y : 0f;
        if (invertZoomInput) zoomInput *= -1f;

        currentDistance -= zoomInput * zoomSpeed * Time.deltaTime;
        currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);

        // ---------------- SPEED-BASED PULLBACK ----------------
        float speed = rb != null ? rb.linearVelocity.magnitude : 0f;
        float pullback = Mathf.Clamp(speed * speedPullback * 0.05f, 0f, maxPullback);

        float finalDistance = currentDistance + pullback;

        // ---------------- APPLY TRANSFORM WITH LAG ----------------
        Quaternion targetRot = Quaternion.Euler(fixedPitch, yaw, 0f);

        Vector3 offset =
            new Vector3(
                baseOffset.x,
                baseOffset.y,
                -finalDistance
            );

        Vector3 targetPos = player.position + targetRot * offset;

        cameraRoot.position = Vector3.SmoothDamp(
            cameraRoot.position,
            targetPos,
            ref currentVelocity,
            1f / positionLag
        );

        cameraRoot.rotation = Quaternion.Slerp(
            cameraRoot.rotation,
            targetRot,
            rotationLag * Time.deltaTime
        );
    }
}
