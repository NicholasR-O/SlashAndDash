using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Transform cameraRoot; // usually this.transform
    public Camera targetCamera;  // MUST be the Main Camera

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

    [Header("Aim Zoom")]
    public float aimZoomDistance = 3.5f;

    float yaw;
    float pitch;
    float currentDistance;
    Vector3 currentVelocity;

    void OnEnable() => lookAction.action.Enable();
    void OnDisable() => lookAction.action.Disable();

    void Start()
    {
        // ---------- HARD SAFETY CHECKS ----------
        if (targetCamera == null)
        {
            Debug.LogError("[ThirdPersonCamera] Target Camera is not assigned.");
            enabled = false;
            return;
        }

        if (!targetCamera.CompareTag("MainCamera"))
        {
            Debug.LogError("[ThirdPersonCamera] Target Camera must be tagged MainCamera.");
            enabled = false;
            return;
        }

        if (cameraRoot == null)
            cameraRoot = targetCamera.transform;

        yaw = cameraRoot.eulerAngles.y;
        pitch = fixedPitch;
        currentDistance = -baseOffset.z;

        targetCamera.fieldOfView = baseFOV;
    }

    void LateUpdate()
    {
        if (player == null) return;

        Rigidbody rb = player.GetComponent<Rigidbody>();
        Vector2 look = lookAction.action.ReadValue<Vector2>() * sensitivity;

        float dt = GrappleController.IsAimingStatic
            ? Time.unscaledDeltaTime
            : Time.deltaTime;

        // ---------------- AIM MODE ----------------
        if (GrappleController.IsAimingStatic)
        {
            yaw += look.x * lookSpeed * dt * 120f;
            pitch -= look.y * lookSpeed * dt * 120f;
            pitch = Mathf.Clamp(pitch, minAimPitch, maxAimPitch);

            currentDistance = Mathf.Lerp(
                currentDistance,
                aimZoomDistance,
                8f * Time.unscaledDeltaTime
            );
        }
        else
        {
            yaw += look.x * lookSpeed * dt * 120f;
            pitch = fixedPitch;

            if (rb != null)
            {
                Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                if (flatVel.sqrMagnitude > 0.1f)
                {
                    float targetYaw = Mathf.Atan2(flatVel.x, flatVel.z) * Mathf.Rad2Deg;
                    yaw = Mathf.LerpAngle(yaw, targetYaw, autoCenterSpeed * dt);
                }
            }
        }

        Quaternion targetRot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 offset = targetRot * new Vector3(baseOffset.x, baseOffset.y, -currentDistance);
        Vector3 targetPos = player.position + offset;

        cameraRoot.position = Vector3.SmoothDamp(
            cameraRoot.position,
            targetPos,
            ref currentVelocity,
            1f / positionLag
        );

        cameraRoot.rotation = Quaternion.Slerp(
            cameraRoot.rotation,
            targetRot,
            rotationLag * dt
        );

        // ---------------- FOV ----------------
        float targetFOV = baseFOV;

        if (GrappleController.IsAimingStatic)
            targetFOV = aimFOV;
        else if (rb != null && rb.linearVelocity.magnitude > 20f)
            targetFOV = boostFOV;

        targetCamera.fieldOfView = Mathf.Lerp(
            targetCamera.fieldOfView,
            targetFOV,
            fovLerpSpeed * dt
        );
    }
}
