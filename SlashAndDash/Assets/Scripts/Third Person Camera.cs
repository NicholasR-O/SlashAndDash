using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
public class ThirdPersonCamera : MonoBehaviour
{
    [Header("References")]
    public Transform player;          // Player transform
    public Transform cameraRoot;      // Usually the Main Camera transform (this object)

    [Header("Camera Settings")]
    public Vector3 offset = new Vector3(0f, 1.8f, -3.5f);
    public float followSpeed = 12f;
    public float lookSpeed = 1.5f;     // mouse sensitivity multiplier
    public float pitchMin = -30f;
    public float pitchMax = 60f;

    [Header("Input")]
    public InputActionReference lookAction; // Vector2 (mouse delta)

    float yaw = 0f;
    float pitch = 10f;

    void OnEnable()
    {
        if (lookAction != null) lookAction.action.Enable();
    }
    void OnDisable()
    {
        if (lookAction != null) lookAction.action.Disable();
    }

    void Start()
    {
        if (cameraRoot == null)
            cameraRoot = transform;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Vector3 e = cameraRoot.eulerAngles;
        yaw = e.y;
        pitch = e.x;
    }

    void LateUpdate()
    {
        if (player == null || cameraRoot == null) return;

        Vector2 look = Vector2.zero;
        if (lookAction != null)
            look = lookAction.action.ReadValue<Vector2>();

        yaw += look.x * lookSpeed * Time.deltaTime * 120f;
        pitch -= look.y * lookSpeed * Time.deltaTime * 120f;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPosition = player.position + rotation * offset;

        cameraRoot.position = Vector3.Lerp(
            cameraRoot.position,
            desiredPosition,
            1f - Mathf.Exp(-followSpeed * Time.deltaTime)
        );

        cameraRoot.rotation = rotation;
    }
}
