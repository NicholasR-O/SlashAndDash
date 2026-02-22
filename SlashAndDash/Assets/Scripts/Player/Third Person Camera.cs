using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Transform cameraRoot;
    public Camera targetCamera;
    public GrappleController grappleController;

    [Header("Camera Offset")]
    public Vector3 baseOffset = new Vector3(0f, 1.8f, -3.5f);

    [Header("Zoom")]
    public float minDistance = 2.5f;
    public float maxDistance = 6.5f;
    public float zoomSpeed = 4f;

    [Header("Rotation")]
    public float lookSpeed = 1.5f;
    public float fixedPitch = 12f;
    public float minAimPitch = -45f;
    public float maxAimPitch = 60f;
    public float autoCenterSpeed = 3.5f;

    [Header("Recenter Delay")]
    public float recenterDelay = 0.5f;
    float lastLookInputTime;

    [Header("Sensitivity")]
    [Range(0.1f, 3f)]
    public float sensitivity = 1f;

    [Header("Lag")]
    public float positionLag = 10f;
    public float rotationLag = 12f;

    [Header("FOV")]
    public float baseFOV = 70f;
    public float aimFOV = 55f;
    public float boostFOV = 80f;
    public float fovLerpSpeed = 10f;

    [Header("Input")]
    public InputActionReference lookAction;

    float yaw;
    float pitch;
    float currentDistance;
    Vector3 currentVelocity;

    void OnEnable() => lookAction?.action.Enable();
    void OnDisable() => lookAction?.action.Disable();

    void Start()
    {
        if (targetCamera == null || !targetCamera.CompareTag("MainCamera"))
        {
            Debug.LogError("[ThirdPersonCamera] Target Camera must be assigned and tagged MainCamera.");
            enabled = false;
            return;
        }

        if (cameraRoot == null)
            cameraRoot = targetCamera.transform;
        if (grappleController == null && player != null)
            grappleController = player.GetComponent<GrappleController>();

        yaw = cameraRoot.eulerAngles.y;
        pitch = fixedPitch;
        currentDistance = Mathf.Clamp(-baseOffset.z, minDistance, maxDistance);

        targetCamera.fieldOfView = baseFOV;
    }

    void LateUpdate()
    {
        if (player == null) return;
        if (GameState.IsPaused) return;

        Vector2 look = lookAction != null ? lookAction.action.ReadValue<Vector2>() * sensitivity : Vector2.zero;
        float dt = Time.unscaledDeltaTime;

        if (look.sqrMagnitude > 0.001f)
            lastLookInputTime = Time.unscaledTime;

        bool allowRecentering = (Time.unscaledTime - lastLookInputTime) > recenterDelay;

        // Camera rotation
        yaw += look.x * lookSpeed * 120f * dt;
        pitch -= look.y * lookSpeed * 120f * dt;
        pitch = Mathf.Clamp(pitch, fixedPitch, maxAimPitch); // Keep pitch mostly fixed

        bool isAiming = GrappleController.IsAimingStatic;
        Transform lockedTarget = grappleController != null ? grappleController.LockedTarget : null;
        if (isAiming && lockedTarget != null)
        {
            Vector3 toTarget = lockedTarget.position - player.position;
            Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
            if (flat.sqrMagnitude > 0.001f)
            {
                float targetYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
                yaw = Mathf.LerpAngle(yaw, targetYaw, autoCenterSpeed * dt * 2.5f);
            }
        }

        // Auto-center based on player velocity
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null && allowRecentering && !(isAiming && lockedTarget != null))
        {
            Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (flatVel.sqrMagnitude > 0.1f)
            {
                float targetYaw = Mathf.Atan2(flatVel.x, flatVel.z) * Mathf.Rad2Deg;
                yaw = Mathf.LerpAngle(yaw, targetYaw, autoCenterSpeed * dt);
            }
        }

        // Zoom
        currentDistance -= look.y * zoomSpeed * dt;
        currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);

        // Position and rotation
        Quaternion targetRot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 offset = targetRot * new Vector3(baseOffset.x, baseOffset.y, -currentDistance);
        Vector3 targetPos = player.position + offset;

        cameraRoot.position = Vector3.SmoothDamp(
            cameraRoot.position,
            targetPos,
            ref currentVelocity,
            1f / Mathf.Max(0.0001f, positionLag),
            Mathf.Infinity,
            dt
        );
        cameraRoot.rotation = Quaternion.Slerp(cameraRoot.rotation, targetRot, rotationLag * dt);

        // FOV handling
        float targetFOV = baseFOV;

        if (isAiming)
            targetFOV = aimFOV;
        else if (rb != null && rb.linearVelocity.magnitude > 25f)
            targetFOV = boostFOV;

        targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFOV, fovLerpSpeed * dt);
    }
}
