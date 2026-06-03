using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;

[AddComponentMenu("Game/Scene Load Trigger")]
[RequireComponent(typeof(Collider))]
public class SceneLoadTrigger : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string sceneName;
    [SerializeField] private int buildIndex = -1;
    [SerializeField] private LoadSceneMode loadMode = LoadSceneMode.Single;
    [SerializeField] private bool useFadeTransition = true;
    [SerializeField] private float fadeDuration = 0.75f;

    [Header("Cinematic")]
    [SerializeField] private bool playCinematicBeforeLoad;
    [SerializeField] private CarCinematicController cinematicController;
    [FormerlySerializedAs("cinematicDrivePath")]
    [SerializeField] private Transform[] carDrivePoints;
    [SerializeField] private Transform[] cameraMovePoints;
    [FormerlySerializedAs("cinematicCameraLookTarget")]
    [SerializeField] private Transform cameraAimPoint;
    [SerializeField] private float cutsceneDurationBeforeFade = 4f;

    [Header("Transition Music")]
    [SerializeField] private bool fadeMusicDuringCinematic = true;

    [Header("Exit Gate")]
    [SerializeField] private bool closeExitGateOnTrigger;
    [SerializeField] private StoneGateCloser exitGateToClose;
    [SerializeField] private float exitGateCloseDuration = 1.5f;

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool requireTag = true;
    [SerializeField] private bool disableAfterTrigger = true;

    private bool triggered;

#if UNITY_EDITOR
    [Header("Editor")]
    [SerializeField] private UnityEditor.SceneAsset sceneAsset;
#endif

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered)
            return;

        if (requireTag && !string.IsNullOrWhiteSpace(playerTag) && !other.CompareTag(playerTag))
            return;

        triggered = true;
        CloseExitGate();

        if (disableAfterTrigger)
            gameObject.SetActive(false);

        if (playCinematicBeforeLoad && TryPlayCinematicBeforeLoad(other))
            return;

        LoadTargetScene();
    }

    private bool TryPlayCinematicBeforeLoad(Collider other)
    {
        CarCinematicController controller = cinematicController;
        if (controller == null)
        {
            CarController player = GetPlayerFromCollider(other);
            if (player != null)
                controller = player.GetComponent<CarCinematicController>();
        }

        if (controller == null)
        {
            Debug.LogWarning("[SceneLoadTrigger] Cinematic before load is enabled, but no CarCinematicController was found.", this);
            return false;
        }

        if (fadeMusicDuringCinematic)
            BackgroundMusicPlayer.BeginSceneTransitionMusicForActivePlayer(GetTargetSceneName(), cutsceneDurationBeforeFade);

        controller.PlayCinematicForSceneTransition(
            carDrivePoints,
            cameraMovePoints,
            cameraAimPoint,
            cutsceneDurationBeforeFade,
            LoadTargetScene);
        return true;
    }

    private void LoadTargetScene()
    {
        if (buildIndex >= 0)
        {
            if (useFadeTransition)
                SceneTransitionFader.LoadScene(buildIndex, loadMode, fadeDuration);
            else
                SceneManager.LoadScene(buildIndex, loadMode);
            return;
        }

        if (!string.IsNullOrWhiteSpace(sceneName))
        {
            if (useFadeTransition)
                SceneTransitionFader.LoadScene(sceneName, loadMode, fadeDuration);
            else
                SceneManager.LoadScene(sceneName, loadMode);
            return;
        }

        Debug.LogWarning("[SceneLoadTrigger] No scene assigned. Set buildIndex or sceneName.", this);
    }

    private string GetTargetSceneName()
    {
        if (!string.IsNullOrWhiteSpace(sceneName))
            return sceneName;

        if (buildIndex < 0)
            return string.Empty;

        string scenePath = SceneUtility.GetScenePathByBuildIndex(buildIndex);
        return string.IsNullOrWhiteSpace(scenePath)
            ? string.Empty
            : System.IO.Path.GetFileNameWithoutExtension(scenePath);
    }

    private void CloseExitGate()
    {
        if (!closeExitGateOnTrigger)
            return;

        StoneGateCloser gate = exitGateToClose;
        if (gate == null)
            gate = FindClosestExitGate();

        if (gate == null)
        {
            Debug.LogWarning("[SceneLoadTrigger] Exit gate close is enabled, but no StoneGateCloser was found.", this);
            return;
        }

        gate.Close(exitGateCloseDuration);
    }

    private StoneGateCloser FindClosestExitGate()
    {
        StoneGateCloser[] gates = FindObjectsByType<StoneGateCloser>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        StoneGateCloser closest = null;
        float closestSqrDistance = float.PositiveInfinity;
        Vector3 triggerPosition = transform.position;

        for (int i = 0; i < gates.Length; i++)
        {
            StoneGateCloser gate = gates[i];
            if (gate == null)
                continue;

            float sqrDistance = (gate.transform.position - triggerPosition).sqrMagnitude;
            if (sqrDistance >= closestSqrDistance)
                continue;

            closest = gate;
            closestSqrDistance = sqrDistance;
        }

        return closest;
    }

    private static CarController GetPlayerFromCollider(Collider entrant)
    {
        if (entrant == null)
            return null;

        if (entrant.attachedRigidbody != null)
        {
            CarController attachedPlayer = entrant.attachedRigidbody.GetComponentInParent<CarController>();
            if (attachedPlayer != null)
                return attachedPlayer;
        }

        return entrant.GetComponentInParent<CarController>();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        fadeDuration = Mathf.Max(0f, fadeDuration);
        cutsceneDurationBeforeFade = Mathf.Max(0f, cutsceneDurationBeforeFade);
        exitGateCloseDuration = Mathf.Max(0f, exitGateCloseDuration);

        if (sceneAsset != null)
            sceneName = sceneAsset.name;
    }

    private void OnDrawGizmosSelected()
    {
        DrawRouteGizmos(carDrivePoints, new Color(0.25f, 0.8f, 1f, 0.85f), transform.position, 0.45f);
        DrawRouteGizmos(cameraMovePoints, new Color(0.25f, 1f, 0.45f, 0.85f), transform.position, 0.35f);

        if (cameraAimPoint == null)
            return;

        Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.85f);
        Gizmos.DrawWireSphere(cameraAimPoint.position, 0.55f);
    }

    private static void DrawRouteGizmos(Transform[] points, Color color, Vector3 startPosition, float size)
    {
        if (points == null || points.Length == 0)
            return;

        Gizmos.color = color;
        Vector3 previous = startPosition;
        for (int i = 0; i < points.Length; i++)
        {
            Transform point = points[i];
            if (point == null)
                continue;

            Gizmos.DrawSphere(point.position, size);
            Gizmos.DrawLine(previous, point.position);
            previous = point.position;
        }
    }
#endif
}
