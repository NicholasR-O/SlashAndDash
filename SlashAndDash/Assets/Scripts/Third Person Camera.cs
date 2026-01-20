using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Transform cameraRoot;

    [Header("Camera Settings")]
    public Vector3 offset = new Vector3(0f, 1.8f, -3.5f);
    public float lookSpeed = 1.5f;
    public float autoCenterSpeed = 2f;
    public float lookInputDeadzone = 0.02f;

    [Header("Input")]
    public InputActionReference lookAction;

    float yaw;
    float fixedPitch = 12f;

    void OnEnable() => lookAction?.action.Enable();
    void OnDisable() => lookAction?.action.Disable();

    void Start()
    {
        if (cameraRoot == null) cameraRoot = transform;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        yaw = cameraRoot.eulerAngles.y;
    }

    void LateUpdate()
    {
        if (player == null) return;

        Vector2 look = lookAction != null ? lookAction.action.ReadValue<Vector2>() : Vector2.zero;

        if (Mathf.Abs(look.x) > lookInputDeadzone)
        {
            yaw += look.x * lookSpeed * Time.deltaTime * 120f;
        }
        else
        {
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 vel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                if (vel.sqrMagnitude > 0.01f)
                {
                    float targetYaw = Mathf.Atan2(vel.x, vel.z) * Mathf.Rad2Deg;
                    yaw = Mathf.LerpAngle(yaw, targetYaw, autoCenterSpeed * Time.deltaTime);
                }
            }
        }

        Quaternion rot = Quaternion.Euler(fixedPitch, yaw, 0f);
        cameraRoot.position = player.position + rot * offset;
        cameraRoot.rotation = rot;
    }
}
