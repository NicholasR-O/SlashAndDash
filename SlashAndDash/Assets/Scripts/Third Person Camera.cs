using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Transform cameraRoot;

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

    [Header("Lag")]
    public float positionLag = 10f;
    public float rotationLag = 12f;

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
        yaw = cameraRoot.eulerAngles.y;
        pitch = fixedPitch;
        currentDistance = -baseOffset.z;
    }

    void LateUpdate()
    {
        if (player == null) return;

        Rigidbody rb = player.GetComponent<Rigidbody>();
        Vector2 look = lookAction.action.ReadValue<Vector2>();

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

            if (rb != null)
            {
                Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                if (flatVel.sqrMagnitude > 0.1f)
                {
                    float targetYaw = Mathf.Atan2(flatVel.x, flatVel.z) * Mathf.Rad2Deg;
                    yaw = Mathf.LerpAngle(yaw, targetYaw, autoCenterSpeed * dt);
                }
            }

            pitch = fixedPitch;
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
    }
}
