using System.Collections;
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
    public Vector3 baseOffset = new Vector3(0f, 1.95f, -3.9f);

    [Header("Zoom")]
    public float minDistance = 2.8f;
    public float maxDistance = 6.4f;
    public float zoomSpeed = 4f;

    [Header("Rotation")]
    public float lookSpeed = 1.5f;
    public float fixedPitch = 12f;
    public float minAimPitch = -45f;
    public float maxAimPitch = 60f;
    public float autoCenterSpeed = 3.5f;
    public float fastAimAssistFullSpeed = 25f;
    public float fastAimAssistMultiplier = 2.25f;
    public float fastAimPitchPull = 0.45f;

    [Header("Recenter Delay")]
    public float recenterDelay = 0.5f;
    float lastLookInputTime;

    [Header("Sensitivity")]
    [Range(0.1f, 3f)]
    public float sensitivity = 1f;

    [Header("Lag")]
    public float positionLag = 10f;
    public float rotationLag = 12f;
    public float followDragFullSpeed = 34f;
    public float followDragMaxOffset = 1.25f;
    public float followDragResponse = 5.5f;

    [Header("FOV")]
    public float baseFOV = 70f;
    public float aimFOV = 55f;
    public float boostFOV = 80f;
    public float fovLerpSpeed = 8f;
    public float speedFOVReferenceSpeed = 26f;
    public float speedFOVAddAtMaxSpeed = 10f;
    public AnimationCurve speedFOVCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public float maxFOV = 90f;
    public float boostFOVPulse = 8f;
    public float boostFOVPulseDuration = 0.42f;
    public AnimationCurve boostFOVPulseCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Input")]
    public InputActionReference lookAction;

    float yaw;
    float pitch;
    float currentDistance;
    Vector3 currentVelocity;
    Vector3 currentFollowDragOffset;
    Rigidbody playerRigidbody;
    CarController carController;
    float boostFOVPulseTimer;
    bool subscribedToPlayerEvents;
    bool followFrozen;
    bool positionLockedLookAtPlayer;

    void OnEnable()
    {
        GameOptions.EnsureLoaded();
        GameOptions.Changed += ApplyUserOptions;
        lookAction?.action.Enable();
        ResolvePlayerReferences();
        SubscribeToPlayerEvents();
        ApplyUserOptions();
    }
    void OnDisable()
    {
        GameOptions.Changed -= ApplyUserOptions;
        lookAction?.action.Disable();
        UnsubscribeFromPlayerEvents();
    }

    void OnValidate()
    {
        minDistance = Mathf.Max(0.01f, minDistance);
        maxDistance = Mathf.Max(minDistance, maxDistance);
        positionLag = Mathf.Max(0.01f, positionLag);
        rotationLag = Mathf.Max(0.01f, rotationLag);
        followDragFullSpeed = Mathf.Max(0.01f, followDragFullSpeed);
        followDragMaxOffset = Mathf.Max(0f, followDragMaxOffset);
        followDragResponse = Mathf.Max(0.01f, followDragResponse);
        fovLerpSpeed = Mathf.Max(0.01f, fovLerpSpeed);
        speedFOVReferenceSpeed = Mathf.Max(0.01f, speedFOVReferenceSpeed);
        speedFOVAddAtMaxSpeed = Mathf.Max(0f, speedFOVAddAtMaxSpeed);
        maxFOV = Mathf.Max(baseFOV, maxFOV);
        boostFOVPulse = Mathf.Max(0f, boostFOVPulse);
        boostFOVPulseDuration = Mathf.Max(0.01f, boostFOVPulseDuration);
    }

    void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null || !targetCamera.CompareTag("MainCamera"))
        {
            Debug.LogError("[ThirdPersonCamera] Target Camera must be assigned and tagged MainCamera.");
            enabled = false;
            return;
        }

        if (cameraRoot == null)
            cameraRoot = targetCamera.transform;
        ResolvePlayerReferences();
        SubscribeToPlayerEvents();
        ApplyUserOptions();

        yaw = cameraRoot.eulerAngles.y;
        pitch = fixedPitch;
        currentDistance = Mathf.Clamp(-baseOffset.z, minDistance, maxDistance);

        targetCamera.fieldOfView = baseFOV;
    }

    void LateUpdate()
    {
        if (player == null) return;
        if (GameState.IsPaused) return;
        if (followFrozen) return;

        float dt = Time.unscaledDeltaTime;
        if (positionLockedLookAtPlayer)
        {
            RotateLockedCameraTowardPlayer(dt);
            return;
        }

        Vector2 look = lookAction != null ? lookAction.action.ReadValue<Vector2>() * sensitivity : Vector2.zero;
        bool isAiming = GrappleController.IsAimingStatic;

        if (look.sqrMagnitude > 0.001f)
            lastLookInputTime = Time.unscaledTime;

        if (grappleController != null)
            grappleController.HandleTargetSwitchInput(isAiming ? look : Vector2.zero);

        bool allowRecentering = (Time.unscaledTime - lastLookInputTime) > recenterDelay;

        // Camera rotation:
        // non-aim -> horizontal orbit only (yaw), keep fixed pitch.
        // aim -> full yaw/pitch camera control.
        yaw += look.x * lookSpeed * 120f * dt;
        if (isAiming)
        {
            pitch -= look.y * lookSpeed * 120f * dt;
            pitch = Mathf.Clamp(pitch, minAimPitch, maxAimPitch);
        }
        else
        {
            pitch = Mathf.Lerp(pitch, fixedPitch, autoCenterSpeed * dt * 1.5f);
        }

        Rigidbody rb = playerRigidbody != null ? playerRigidbody : player.GetComponent<Rigidbody>();
        if (playerRigidbody == null)
            playerRigidbody = rb;
        float speedRatio = 0f;
        if (rb != null)
        {
            Vector3 planarVelocity = rb.linearVelocity;
            planarVelocity.y = 0f;
            speedRatio = Mathf.Clamp01(planarVelocity.magnitude / Mathf.Max(0.01f, fastAimAssistFullSpeed));
        }

        Transform lockedTarget = grappleController != null ? grappleController.LockedTarget : null;
        if (GameOptions.AutoAim && isAiming && lockedTarget != null)
        {
            Vector3 lockedTargetPoint = grappleController != null
                ? grappleController.LockedTargetAimPoint
                : lockedTarget.position;
            Vector3 toTarget = lockedTargetPoint - player.position;
            Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
            if (flat.sqrMagnitude > 0.001f)
            {
                float targetYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
                float aimAssistMultiplier = Mathf.Lerp(1f, Mathf.Max(1f, fastAimAssistMultiplier), speedRatio);
                yaw = Mathf.LerpAngle(yaw, targetYaw, autoCenterSpeed * dt * 2.5f * aimAssistMultiplier);

                if (fastAimPitchPull > 0f && toTarget.sqrMagnitude > 0.001f)
                {
                    float targetPitch = -Mathf.Asin(Mathf.Clamp(toTarget.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
                    targetPitch = Mathf.Clamp(targetPitch, minAimPitch, maxAimPitch);
                    float pitchAssist = Mathf.Lerp(Mathf.Clamp01(fastAimPitchPull), 1f, speedRatio);
                    pitch = Mathf.LerpAngle(
                        pitch,
                        targetPitch,
                        autoCenterSpeed * dt * pitchAssist);
                }
            }
        }

        // Auto-center based on player velocity
        if (rb != null && allowRecentering && !(GameOptions.AutoAim && isAiming && lockedTarget != null))
        {
            Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (flatVel.sqrMagnitude > 0.1f)
            {
                float targetYaw = Mathf.Atan2(flatVel.x, flatVel.z) * Mathf.Rad2Deg;
                float recenterMultiplier = Mathf.Lerp(1f, Mathf.Max(1f, fastAimAssistMultiplier), speedRatio);
                yaw = Mathf.LerpAngle(yaw, targetYaw, autoCenterSpeed * dt * recenterMultiplier);
            }
        }

        // Zoom:
        // non-aim uses vertical look for zoom in/out.
        if (!isAiming)
            currentDistance -= look.y * zoomSpeed * dt;
        currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);

        // Position and rotation
        Quaternion targetRot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 offset = targetRot * new Vector3(baseOffset.x, baseOffset.y, -currentDistance);
        Vector3 targetPos = player.position + offset + GetFollowDragOffset(rb, dt);

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
        {
            targetFOV = aimFOV;
        }
        else if (rb != null)
        {
            Vector3 planarVelocity = rb.linearVelocity;
            planarVelocity.y = 0f;
            float fovSpeedRatio = Mathf.Clamp01(planarVelocity.magnitude / Mathf.Max(0.01f, speedFOVReferenceSpeed));
            float curveValue = speedFOVCurve != null ? speedFOVCurve.Evaluate(fovSpeedRatio) : fovSpeedRatio;
            targetFOV += Mathf.Max(0f, curveValue) * speedFOVAddAtMaxSpeed;
        }

        targetFOV += GetBoostFOVPulse(dt);
        targetFOV = Mathf.Min(targetFOV, Mathf.Max(maxFOV, baseFOV, aimFOV));

        targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFOV, fovLerpSpeed * dt);
    }

    void SubscribeToPlayerEvents()
    {
        if (carController == null || subscribedToPlayerEvents)
            return;

        carController.BoostActivated += OnBoostActivated;
        subscribedToPlayerEvents = true;
    }

    public void ApplyUserOptions()
    {
        GameOptions.EnsureLoaded();
        sensitivity = GameOptions.Sensitivity;
        baseFOV = GameOptions.FieldOfView;

        if (targetCamera != null && !GameState.IsPaused)
            targetCamera.fieldOfView = Mathf.Clamp(baseFOV, 1f, 179f);
    }

    void UnsubscribeFromPlayerEvents()
    {
        if (carController != null && subscribedToPlayerEvents)
            carController.BoostActivated -= OnBoostActivated;

        subscribedToPlayerEvents = false;
    }

    void ResolvePlayerReferences()
    {
        if (player == null)
            return;

        if (grappleController == null)
            grappleController = player.GetComponent<GrappleController>();
        if (playerRigidbody == null)
            playerRigidbody = player.GetComponent<Rigidbody>();
        if (carController == null)
            carController = player.GetComponent<CarController>();
    }

    void OnBoostActivated()
    {
        boostFOVPulseTimer = boostFOVPulseDuration;
    }

    public void FreezeFollow()
    {
        followFrozen = true;
        positionLockedLookAtPlayer = false;
        currentVelocity = Vector3.zero;
        currentFollowDragOffset = Vector3.zero;
    }

    public void LockPositionAndLookAtPlayer()
    {
        ResolvePlayerReferences();
        followFrozen = false;
        positionLockedLookAtPlayer = true;
        currentVelocity = Vector3.zero;
        currentFollowDragOffset = Vector3.zero;
    }

    public void SnapToPlayer()
    {
        ResolvePlayerReferences();
        if (!TryGetPlayerCameraPose(out Vector3 targetPosition, out Quaternion targetRotation))
            return;

        cameraRoot.SetPositionAndRotation(targetPosition, targetRotation);
        currentVelocity = Vector3.zero;
        currentFollowDragOffset = Vector3.zero;
    }

    public void PlaceForSceneIntro(Vector3 spawnPosition, Quaternion spawnRotation, float liftHeight, float driveDistance)
    {
        FreezeFollow();
        if (!TryGetSceneIntroCameraPose(spawnPosition, spawnRotation, 0f, liftHeight, driveDistance, out Vector3 targetPosition, out Quaternion targetRotation))
            return;

        cameraRoot.SetPositionAndRotation(targetPosition, targetRotation);
        currentVelocity = Vector3.zero;
        currentFollowDragOffset = Vector3.zero;
    }

    public void SetSceneIntroCameraProgress(Vector3 spawnPosition, Quaternion spawnRotation, float normalizedProgress, float liftHeight, float driveDistance)
    {
        FreezeFollow();
        if (!TryGetSceneIntroCameraPose(spawnPosition, spawnRotation, normalizedProgress, liftHeight, driveDistance, out Vector3 targetPosition, out Quaternion targetRotation))
            return;

        cameraRoot.SetPositionAndRotation(targetPosition, targetRotation);
        currentVelocity = Vector3.zero;
        currentFollowDragOffset = Vector3.zero;
    }

    public IEnumerator FlyToPlayerWhileFrozen(float duration)
    {
        FreezeFollow();
        ResolvePlayerReferences();
        if (!TryGetPlayerCameraPose(out Vector3 targetPosition, out Quaternion targetRotation))
            yield break;

        Vector3 startPosition = cameraRoot.position;
        Quaternion startRotation = cameraRoot.rotation;
        duration = Mathf.Max(0f, duration);
        if (duration <= 0.001f)
        {
            cameraRoot.SetPositionAndRotation(targetPosition, targetRotation);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            cameraRoot.position = Vector3.LerpUnclamped(startPosition, targetPosition, eased);
            cameraRoot.rotation = Quaternion.SlerpUnclamped(startRotation, targetRotation, eased);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        cameraRoot.SetPositionAndRotation(targetPosition, targetRotation);
        currentVelocity = Vector3.zero;
        currentFollowDragOffset = Vector3.zero;
    }

    bool TryGetSceneIntroCameraPose(Vector3 spawnPosition, Quaternion spawnRotation, float normalizedProgress, float liftHeight, float driveDistance, out Vector3 targetPosition, out Quaternion targetRotation)
    {
        targetPosition = Vector3.zero;
        targetRotation = Quaternion.identity;

        normalizedProgress = Mathf.Clamp01(normalizedProgress);
        liftHeight = Mathf.Max(0f, liftHeight);
        driveDistance = Mathf.Max(0.01f, driveDistance);

        if (!TryGetCameraPoseForPlayerPose(spawnPosition, spawnRotation, 0f, out Vector3 followPosition, out Quaternion followRotation))
            return false;

        Vector3 forward = FlattenDirection(spawnRotation * Vector3.forward, Vector3.forward);
        Vector3 introPosition = spawnPosition + Vector3.up * liftHeight;
        Vector3 lookTarget = spawnPosition - forward * Mathf.Max(2f, driveDistance * 0.8f);
        lookTarget += Vector3.up * Mathf.Max(0.35f, baseOffset.y * 0.25f);

        Vector3 lookDirection = lookTarget - introPosition;
        if (lookDirection.sqrMagnitude < 0.0001f)
            lookDirection = -forward;

        Quaternion introRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        targetPosition = Vector3.LerpUnclamped(introPosition, followPosition, normalizedProgress);
        targetRotation = Quaternion.SlerpUnclamped(introRotation, followRotation, normalizedProgress);
        return true;
    }

    bool TryGetPlayerCameraPose(out Vector3 targetPosition, out Quaternion targetRotation)
    {
        targetPosition = Vector3.zero;
        targetRotation = Quaternion.identity;

        if (player == null)
            return false;

        if (cameraRoot == null && targetCamera != null)
            cameraRoot = targetCamera.transform;
        if (cameraRoot == null)
            return false;

        Rigidbody rb = playerRigidbody != null ? playerRigidbody : player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 planarVelocity = rb.linearVelocity;
            planarVelocity.y = 0f;
            if (planarVelocity.sqrMagnitude > 0.1f)
                yaw = Mathf.Atan2(planarVelocity.x, planarVelocity.z) * Mathf.Rad2Deg;
            else
                yaw = player.eulerAngles.y;
        }
        else
        {
            yaw = player.eulerAngles.y;
        }

        Quaternion playerFacing = Quaternion.Euler(0f, yaw, 0f);
        return TryGetCameraPoseForPlayerPose(player.position, playerFacing, 0f, out targetPosition, out targetRotation);
    }

    bool TryGetCameraPoseForPlayerPose(Vector3 playerPosition, Quaternion playerRotation, float extraHeight, out Vector3 targetPosition, out Quaternion targetRotation)
    {
        targetPosition = Vector3.zero;
        targetRotation = Quaternion.identity;

        if (cameraRoot == null && targetCamera != null)
            cameraRoot = targetCamera.transform;
        if (cameraRoot == null)
            return false;

        yaw = playerRotation.eulerAngles.y;
        pitch = fixedPitch;
        currentDistance = Mathf.Clamp(currentDistance <= 0f ? -baseOffset.z : currentDistance, minDistance, maxDistance);
        targetRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 offset = targetRotation * new Vector3(baseOffset.x, baseOffset.y, -currentDistance);
        targetPosition = playerPosition + offset + Vector3.up * Mathf.Max(0f, extraHeight);
        return true;
    }

    public void ResumeFollow()
    {
        followFrozen = false;
        positionLockedLookAtPlayer = false;
    }

    void RotateLockedCameraTowardPlayer(float deltaTime)
    {
        if (player == null)
            return;

        if (cameraRoot == null && targetCamera != null)
            cameraRoot = targetCamera.transform;
        if (cameraRoot == null)
            return;

        Vector3 lookTarget = player.position + Vector3.up * Mathf.Max(0.1f, baseOffset.y * 0.5f);
        Vector3 lookDirection = lookTarget - cameraRoot.position;
        if (lookDirection.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        cameraRoot.rotation = Quaternion.Slerp(
            cameraRoot.rotation,
            targetRotation,
            Mathf.Clamp01(rotationLag * deltaTime));

        Vector3 euler = cameraRoot.rotation.eulerAngles;
        yaw = euler.y;
        pitch = NormalizePitch(euler.x);
    }

    Vector3 GetFollowDragOffset(Rigidbody rb, float deltaTime)
    {
        Vector3 targetOffset = Vector3.zero;
        if (rb != null && followDragMaxOffset > 0f)
        {
            Vector3 planarVelocity = rb.linearVelocity;
            planarVelocity.y = 0f;
            if (planarVelocity.sqrMagnitude > 0.01f)
            {
                float speedRatio = Mathf.Clamp01(planarVelocity.magnitude / Mathf.Max(0.01f, followDragFullSpeed));
                targetOffset = -planarVelocity.normalized * (followDragMaxOffset * speedRatio);
            }
        }

        float blend = 1f - Mathf.Exp(-followDragResponse * deltaTime);
        currentFollowDragOffset = Vector3.Lerp(currentFollowDragOffset, targetOffset, blend);
        return currentFollowDragOffset;
    }

    float GetBoostFOVPulse(float deltaTime)
    {
        if (boostFOVPulseTimer <= 0f || boostFOVPulse <= 0f)
            return 0f;

        float normalized = 1f - Mathf.Clamp01(boostFOVPulseTimer / Mathf.Max(0.01f, boostFOVPulseDuration));
        float curveValue = boostFOVPulseCurve != null ? boostFOVPulseCurve.Evaluate(normalized) : 1f - normalized;
        boostFOVPulseTimer = Mathf.Max(0f, boostFOVPulseTimer - deltaTime);
        return Mathf.Max(0f, curveValue) * boostFOVPulse;
    }

    static Vector3 FlattenDirection(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            return direction.normalized;

        fallback.y = 0f;
        if (fallback.sqrMagnitude > 0.0001f)
            return fallback.normalized;

        return Vector3.forward;
    }

    static float NormalizePitch(float eulerPitch)
    {
        return eulerPitch > 180f ? eulerPitch - 360f : eulerPitch;
    }
}
