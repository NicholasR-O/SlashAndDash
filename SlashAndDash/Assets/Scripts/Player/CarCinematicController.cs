using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Player/Car Cinematic Controller")]
[RequireComponent(typeof(CarController), typeof(Rigidbody))]
public class CarCinematicController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CarController carController;
    [SerializeField] private Rigidbody carRigidbody;
    [SerializeField] private ThirdPersonCamera followCamera;

    [Header("Default Route")]
    [SerializeField] private Transform[] drivePath;
    [SerializeField] private Transform[] cameraMovePath;
    [SerializeField] private float fallbackDriveDistance = 22f;

    [Header("Camera")]
    [SerializeField] private Transform cameraLookTarget;
    [SerializeField] private bool freezeCameraDuringCinematic = true;
    [SerializeField] private bool resumeCameraFollowOnComplete = true;
    [SerializeField] private float cameraPanSpeed = 1.5f;
    [SerializeField] private float cameraPanMaxDegreesPerSecond = 85f;
    [SerializeField] private Vector3 fallbackLookOffset = new Vector3(0f, 1.5f, 10f);

    [Header("Driving")]
    [SerializeField, Range(0f, 1f)] private float throttle = 0.85f;
    [SerializeField] private float waypointReachDistance = 2f;
    [SerializeField] private float finalReachDistance = 2.75f;
    [SerializeField] private float slowDownDistance = 8f;
    [SerializeField, Range(0f, 1f)] private float minimumFinalThrottle = 0.22f;
    [SerializeField] private float steeringAngleForFullInput = 55f;
    [SerializeField] private float hardTurnAngle = 75f;
    [SerializeField, Range(0f, 1f)] private float hardTurnThrottleMultiplier = 0.45f;
    [SerializeField] private float maxDriveTime = 12f;

    [Header("Completion")]
    [SerializeField] private bool restorePlayerControlOnComplete = true;
    [SerializeField] private float completionHoldSeconds = 0.15f;

    private readonly List<Vector3> carRoutePoints = new List<Vector3>();
    private readonly List<Vector3> cameraRoutePoints = new List<Vector3>();
    private Coroutine cinematicRoutine;
    private Transform cachedCameraRoot;

    public bool IsPlaying => cinematicRoutine != null;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        fallbackDriveDistance = Mathf.Max(0.1f, fallbackDriveDistance);
        waypointReachDistance = Mathf.Max(0.1f, waypointReachDistance);
        finalReachDistance = Mathf.Max(0.1f, finalReachDistance);
        slowDownDistance = Mathf.Max(finalReachDistance, slowDownDistance);
        steeringAngleForFullInput = Mathf.Max(1f, steeringAngleForFullInput);
        hardTurnAngle = Mathf.Max(1f, hardTurnAngle);
        maxDriveTime = Mathf.Max(0.1f, maxDriveTime);
        completionHoldSeconds = Mathf.Max(0f, completionHoldSeconds);
        cameraPanSpeed = Mathf.Max(0f, cameraPanSpeed);
        cameraPanMaxDegreesPerSecond = Mathf.Max(0f, cameraPanMaxDegreesPerSecond);
    }

    private void OnDisable()
    {
        StopCinematic(restorePlayerControl: true, resumeCameraFollow: true);
    }

    public void PlayDefaultCinematic()
    {
        PlayCinematic(drivePath, cameraMovePath, cameraLookTarget);
    }

    public Coroutine PlayCinematic(Transform[] route, Transform lookTarget, Action onComplete = null)
    {
        return PlayCinematic(route, null, lookTarget, onComplete);
    }

    public Coroutine PlayCinematic(Transform[] route, Transform[] cameraRoute, Transform lookTarget, Action onComplete = null)
    {
        return StartCinematic(
            route,
            cameraRoute,
            lookTarget,
            -1f,
            restorePlayerControlOnComplete,
            resumeCameraFollowOnComplete,
            onComplete);
    }

    public Coroutine PlayCinematicForSceneTransition(Transform[] route, Transform lookTarget, Action onComplete)
    {
        return PlayCinematicForSceneTransition(route, null, lookTarget, -1f, onComplete);
    }

    public Coroutine PlayCinematicForSceneTransition(
        Transform[] carRoute,
        Transform[] cameraRoute,
        Transform cameraAimTarget,
        float cutsceneDuration,
        Action onComplete)
    {
        float fixedDuration = cutsceneDuration >= 0f ? cutsceneDuration : -1f;
        return StartCinematic(carRoute, cameraRoute, cameraAimTarget, fixedDuration, false, false, onComplete);
    }

    public void StopCinematic(bool restorePlayerControl = true, bool resumeCameraFollow = true)
    {
        if (cinematicRoutine != null)
        {
            StopCoroutine(cinematicRoutine);
            cinematicRoutine = null;
        }

        if (carController != null && carController.CinematicControlActive)
        {
            carController.SetCinematicMoveInput(Vector2.zero);
            if (restorePlayerControl)
                carController.EndCinematicControl();
        }

        if (resumeCameraFollow && followCamera != null)
            followCamera.ResumeFollow();
    }

    private Coroutine StartCinematic(
        Transform[] carRoute,
        Transform[] cameraRoute,
        Transform lookTarget,
        float fixedDuration,
        bool restorePlayerControl,
        bool resumeCameraFollow,
        Action onComplete)
    {
        StopCinematic(restorePlayerControl: true, resumeCameraFollow: true);
        cinematicRoutine = StartCoroutine(CinematicRoutine(
            carRoute,
            cameraRoute,
            lookTarget,
            fixedDuration,
            restorePlayerControl,
            resumeCameraFollow,
            onComplete));
        return cinematicRoutine;
    }

    private IEnumerator CinematicRoutine(
        Transform[] carRoute,
        Transform[] cameraRoute,
        Transform lookTarget,
        float fixedDuration,
        bool restorePlayerControl,
        bool resumeCameraFollow,
        Action onComplete)
    {
        ResolveReferences();

        if (carController == null || carRigidbody == null)
        {
            Debug.LogWarning("[CarCinematicController] Missing CarController or Rigidbody.", this);
            cinematicRoutine = null;
            onComplete?.Invoke();
            yield break;
        }

        BuildCarRoute(carRoute);
        if (carRoutePoints.Count == 0)
        {
            Debug.LogWarning("[CarCinematicController] No cinematic route could be built.", this);
            cinematicRoutine = null;
            onComplete?.Invoke();
            yield break;
        }

        Transform cameraRoot = ResolveCameraRoot();
        Vector3 cameraStartPosition = cameraRoot != null ? cameraRoot.position : Vector3.zero;
        BuildCameraRoute(cameraRoute);

        if (freezeCameraDuringCinematic && followCamera != null)
            followCamera.FreezeFollow();

        carController.BeginCinematicControl();

        int routeIndex = 0;
        float elapsed = 0f;
        bool hasFixedDuration = fixedDuration >= 0f;
        float carTimeLimit = hasFixedDuration ? fixedDuration : maxDriveTime;

        while (hasFixedDuration ? elapsed < fixedDuration : routeIndex < carRoutePoints.Count && elapsed < maxDriveTime)
        {
            if (routeIndex < carRoutePoints.Count && elapsed < carTimeLimit)
            {
                Vector3 target = carRoutePoints[routeIndex];
                Vector3 toTarget = target - carRigidbody.position;
                toTarget.y = 0f;

                float reachDistance = routeIndex == carRoutePoints.Count - 1 ? finalReachDistance : waypointReachDistance;
                if (toTarget.magnitude <= reachDistance)
                {
                    routeIndex++;
                    carController.SetCinematicMoveInput(Vector2.zero);
                }
                else
                {
                    Vector2 input = GetDriveInput(toTarget, routeIndex == carRoutePoints.Count - 1);
                    carController.SetCinematicMoveInput(input);
                }
            }
            else
            {
                carController.SetCinematicMoveInput(Vector2.zero);
            }

            MoveCameraAlongRoute(cameraRoot, cameraStartPosition, elapsed, hasFixedDuration ? fixedDuration : maxDriveTime);
            PanCamera(cameraRoot, lookTarget);

            elapsed += Time.deltaTime;
            yield return null;
        }

        carController.SetCinematicMoveInput(Vector2.zero);
        MoveCameraAlongRoute(cameraRoot, cameraStartPosition, elapsed, hasFixedDuration ? fixedDuration : maxDriveTime);
        PanCamera(cameraRoot, lookTarget);

        if (!hasFixedDuration)
        {
            float holdElapsed = 0f;
            while (holdElapsed < completionHoldSeconds)
            {
                PanCamera(cameraRoot, lookTarget);
                holdElapsed += Time.deltaTime;
                yield return null;
            }
        }

        if (restorePlayerControl)
            carController.EndCinematicControl();

        if (resumeCameraFollow && followCamera != null)
            followCamera.ResumeFollow();

        cinematicRoutine = null;
        onComplete?.Invoke();
    }

    private Vector2 GetDriveInput(Vector3 toTarget, bool finalPoint)
    {
        Vector3 desiredDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = desiredDirection;
        forward.Normalize();

        float signedAngle = Vector3.SignedAngle(forward, desiredDirection, Vector3.up);
        float steer = Mathf.Clamp(signedAngle / steeringAngleForFullInput, -1f, 1f);
        float drive = throttle;

        if (Mathf.Abs(signedAngle) > hardTurnAngle)
            drive *= hardTurnThrottleMultiplier;

        if (finalPoint)
        {
            float slowRatio = Mathf.InverseLerp(finalReachDistance, slowDownDistance, toTarget.magnitude);
            drive *= Mathf.Lerp(minimumFinalThrottle, 1f, slowRatio);
        }

        return new Vector2(steer, Mathf.Clamp01(drive));
    }

    private void PanCamera(Transform cameraRoot, Transform lookTarget)
    {
        if (cameraRoot == null)
            return;

        Vector3 focusPoint = GetCameraFocusPoint(lookTarget);
        Vector3 lookDirection = focusPoint - cameraRoot.position;
        if (lookDirection.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        float deltaTime = Time.unscaledDeltaTime;
        float slerpT = cameraPanSpeed > 0f ? 1f - Mathf.Exp(-cameraPanSpeed * deltaTime) : 1f;
        Quaternion blendedRotation = Quaternion.Slerp(cameraRoot.rotation, targetRotation, Mathf.Clamp01(slerpT));

        if (cameraPanMaxDegreesPerSecond > 0f)
        {
            cameraRoot.rotation = Quaternion.RotateTowards(
                cameraRoot.rotation,
                blendedRotation,
                cameraPanMaxDegreesPerSecond * deltaTime);
            return;
        }

        cameraRoot.rotation = blendedRotation;
    }

    private Vector3 GetCameraFocusPoint(Transform lookTarget)
    {
        if (lookTarget != null)
            return lookTarget.position;

        if (cameraLookTarget != null)
            return cameraLookTarget.position;

        return transform.TransformPoint(fallbackLookOffset);
    }

    private void BuildCarRoute(Transform[] route)
    {
        carRoutePoints.Clear();

        Transform[] sourceRoute = route != null && route.Length > 0 ? route : drivePath;
        if (sourceRoute != null)
        {
            for (int i = 0; i < sourceRoute.Length; i++)
            {
                if (sourceRoute[i] != null)
                    carRoutePoints.Add(sourceRoute[i].position);
            }
        }

        if (carRoutePoints.Count == 0)
            carRoutePoints.Add(transform.position + Flatten(transform.forward, Vector3.forward) * fallbackDriveDistance);
    }

    private void BuildCameraRoute(Transform[] route)
    {
        cameraRoutePoints.Clear();

        Transform[] sourceRoute = route != null && route.Length > 0 ? route : cameraMovePath;
        if (sourceRoute == null)
            return;

        for (int i = 0; i < sourceRoute.Length; i++)
        {
            if (sourceRoute[i] != null)
                cameraRoutePoints.Add(sourceRoute[i].position);
        }
    }

    private void MoveCameraAlongRoute(Transform cameraRoot, Vector3 cameraStartPosition, float elapsed, float duration)
    {
        if (cameraRoot == null || cameraRoutePoints.Count == 0)
            return;

        float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
        cameraRoot.position = EvaluateRoutePosition(cameraStartPosition, cameraRoutePoints, t);
    }

    private static Vector3 EvaluateRoutePosition(Vector3 startPosition, List<Vector3> points, float t)
    {
        if (points == null || points.Count == 0)
            return startPosition;

        if (points.Count == 1)
            return Vector3.Lerp(startPosition, points[0], t);

        float totalDistance = Vector3.Distance(startPosition, points[0]);
        for (int i = 1; i < points.Count; i++)
            totalDistance += Vector3.Distance(points[i - 1], points[i]);

        if (totalDistance <= 0.0001f)
            return points[points.Count - 1];

        float targetDistance = Mathf.Clamp01(t) * totalDistance;
        Vector3 segmentStart = startPosition;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 segmentEnd = points[i];
            float segmentDistance = Vector3.Distance(segmentStart, segmentEnd);
            if (targetDistance <= segmentDistance || i == points.Count - 1)
            {
                float segmentT = segmentDistance > 0.0001f ? targetDistance / segmentDistance : 1f;
                return Vector3.Lerp(segmentStart, segmentEnd, Mathf.Clamp01(segmentT));
            }

            targetDistance -= segmentDistance;
            segmentStart = segmentEnd;
        }

        return points[points.Count - 1];
    }

    private void ResolveReferences()
    {
        if (carController == null)
            carController = GetComponent<CarController>();
        if (carRigidbody == null)
            carRigidbody = GetComponent<Rigidbody>();
        if (followCamera == null)
            followCamera = GetComponent<ThirdPersonCamera>();
        if (followCamera == null)
            followCamera = FindFirstObjectByType<ThirdPersonCamera>();
    }

    private Transform ResolveCameraRoot()
    {
        if (cachedCameraRoot != null)
            return cachedCameraRoot;

        if (followCamera != null)
        {
            if (followCamera.cameraRoot != null)
                cachedCameraRoot = followCamera.cameraRoot;
            else if (followCamera.targetCamera != null)
                cachedCameraRoot = followCamera.targetCamera.transform;
        }

        if (cachedCameraRoot == null && Camera.main != null)
            cachedCameraRoot = Camera.main.transform;

        return cachedCameraRoot;
    }

    private static Vector3 Flatten(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            return direction.normalized;

        fallback.y = 0f;
        if (fallback.sqrMagnitude > 0.0001f)
            return fallback.normalized;

        return Vector3.forward;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.25f, 0.8f, 1f, 0.85f);

        Vector3 previous = transform.position;
        bool drewPoint = false;
        if (drivePath != null)
        {
            for (int i = 0; i < drivePath.Length; i++)
            {
                Transform point = drivePath[i];
                if (point == null)
                    continue;

                Gizmos.DrawSphere(point.position, 0.35f);
                Gizmos.DrawLine(previous, point.position);
                previous = point.position;
                drewPoint = true;
            }
        }

        if (!drewPoint)
        {
            Vector3 fallbackEnd = transform.position + Flatten(transform.forward, Vector3.forward) * Mathf.Max(0.1f, fallbackDriveDistance);
            Gizmos.DrawSphere(fallbackEnd, 0.35f);
            Gizmos.DrawLine(transform.position, fallbackEnd);
        }

        Transform lookTarget = cameraLookTarget;
        if (lookTarget != null)
        {
            Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.85f);
            Gizmos.DrawWireSphere(lookTarget.position, 0.45f);
        }

        if (cameraMovePath != null && cameraMovePath.Length > 0)
        {
            Gizmos.color = new Color(0.25f, 1f, 0.45f, 0.85f);
            previous = cameraMovePath[0] != null ? cameraMovePath[0].position : transform.position;
            for (int i = 0; i < cameraMovePath.Length; i++)
            {
                Transform point = cameraMovePath[i];
                if (point == null)
                    continue;

                Gizmos.DrawCube(point.position, Vector3.one * 0.45f);
                if (i > 0)
                    Gizmos.DrawLine(previous, point.position);
                previous = point.position;
            }
        }
    }
}
