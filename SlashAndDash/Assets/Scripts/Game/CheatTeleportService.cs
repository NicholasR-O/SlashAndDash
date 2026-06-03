using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum TeleportDestinationKind
{
    Checkpoint,
    Arena
}

public static class CheatTeleportService
{
    public sealed class Destination
    {
        public Destination(string sceneName, TeleportDestinationKind kind, string objectName, Vector3 fallbackPosition, Quaternion fallbackRotation)
        {
            SceneName = sceneName;
            Kind = kind;
            ObjectName = objectName;
            FallbackPosition = fallbackPosition;
            FallbackRotation = fallbackRotation;
        }

        public string SceneName { get; }
        public TeleportDestinationKind Kind { get; }
        public string ObjectName { get; }
        public Vector3 FallbackPosition { get; }
        public Quaternion FallbackRotation { get; }

        public string KindLabel => Kind == TeleportDestinationKind.Arena ? "Arena" : "Checkpoint";
    }

    sealed class Runner : MonoBehaviour
    {
    }

    const float PlayerLookupTimeoutSeconds = 8f;
    const float IntroWaitTimeoutSeconds = 4f;
    const float PositionMatchMaxSqrDistance = 35f * 35f;

    static readonly Destination[] DefaultCatalog =
    {
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint", new Vector3(-308.68225f, 0f, -14.443359f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (8)", new Vector3(218.82285f, -11.471161f, -166.85577f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (12)", new Vector3(0f, 0f, 0f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (17)", new Vector3(-207.69998f, 0f, -162.3999f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (9)", new Vector3(-316.40002f, 0f, 236.90002f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (5)", new Vector3(215.12283f, -9.869019f, -842.3558f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Arena, "Arena Trigger", new Vector3(798.7f, 200f, 843.1f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (13)", new Vector3(-362.8f, 0f, -200f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint", new Vector3(-204.32716f, -11.486389f, -800.2058f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (11)", new Vector3(-308.8f, 0f, -529.69995f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (3)", new Vector3(-119f, 0f, -376.5f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (3)", new Vector3(-484.80005f, 0f, -62f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (3)", new Vector3(181.20001f, 0f, -433.69995f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (4)", new Vector3(-449f, 0f, -121.5f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (15)", new Vector3(-537.1f, 0f, -216.29993f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (1)", new Vector3(-96.47717f, -11.48642f, -447.2558f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (6)", new Vector3(-135.79999f, 0f, -463.69995f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (12)", new Vector3(-290.8f, 0f, -275.69995f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (21)", new Vector3(-55.299988f, 0f, -327f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (20)", new Vector3(53.799988f, 0f, -424.79993f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (10)", new Vector3(-453.5f, 0f, 447.40002f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (1)", new Vector3(-743.5f, 0f, -143.5f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (15)", new Vector3(-728.5f, 0f, 144.1f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (14)", new Vector3(-384.5f, 0f, -157.5f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Arena, "Arena Trigger (3)", new Vector3(2386.1f, 200f, 1316.1f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (16)", new Vector3(-406.5f, 0f, -68.29993f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (11)", new Vector3(-47.3f, 0f, 407.1f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (2)", new Vector3(-85f, 0f, -296.5f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint", new Vector3(99.02527f, -7.5226746f, -821.3378f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (19)", new Vector3(0f, 0f, 0f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (4)", new Vector3(-56.599976f, 0f, -449.19995f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (13)", new Vector3(-198.5f, 0f, 109.5f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (16)", new Vector3(-646.5f, 0f, -230.5f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (6)", new Vector3(-238f, 0f, -41.2f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (10)", new Vector3(432.52286f, -11.48642f, -227.35577f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (7)", new Vector3(-95.61226f, -10.956726f, -162.30865f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (5)", new Vector3(-953f, 0f, -262.5f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (11)", new Vector3(103.62283f, -11.486328f, -196.7558f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (7)", new Vector3(-577.6f, 0f, 206.40002f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (14)", new Vector3(-441.4f, 0f, -299.29993f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (3)", new Vector3(15.861359f, -11.449738f, -757.946f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (12)", new Vector3(0f, 0f, 0f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (2)", new Vector3(38.700012f, 0f, -533.6f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (4)", new Vector3(135.82285f, -11.486389f, -480.4558f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (1)", new Vector3(145.9972f, 0f, -697.865f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (1)", new Vector3(-55f, 0f, -212.5f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (4)", new Vector3(-335.19995f, 0f, -44.5f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (2)", new Vector3(-553.5f, 0f, -379.5f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Arena, "Arena Trigger (2)", new Vector3(1916f, 200f, 376.3f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (13)", new Vector3(358.7228f, -11.486389f, -431.4558f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (7)", new Vector3(0f, 0f, 0f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (8)", new Vector3(-152.79999f, 0f, -755.69995f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (6)", new Vector3(-86.3999f, 9.559021f, -1026.8f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (5)", new Vector3(-330.5f, 0f, -385.5f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (8)", new Vector3(-459.90002f, 0f, 293.59998f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (2)", new Vector3(-95.57716f, -11.464874f, -560.9558f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (9)", new Vector3(244.32285f, -11.486389f, -309.65582f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (5)", new Vector3(-112.79999f, 0f, -618.69995f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (6)", new Vector3(288.35294f, -9.110718f, -696.7322f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint", new Vector3(-936.73553f, 0f, 455.41998f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (18)", new Vector3(-161f, 0f, -44.6f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (10)", new Vector3(-358.8f, 0f, -409.69995f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (9)", new Vector3(-454.8f, 0f, -491.69995f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Checkpoint, "Checkpoint (7)", new Vector3(-177.79999f, 0f, -661.69995f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level1", TeleportDestinationKind.Arena, "Arena Trigger (1)", new Vector3(802.4f, 200f, 1748.5f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Checkpoint, "Checkpoint (1)", new Vector3(-240.52f, 0f, 0f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Checkpoint, "Checkpoint (20)", new Vector3(417f, 49.9f, 2190.6f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Checkpoint, "Checkpoint (22)", new Vector3(421.4f, -2.4f, 2999.1f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Checkpoint, "Checkpoint (17)", new Vector3(-57.66f, 49.9f, 1614.3f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Arena, "Arena Trigger (2)", new Vector3(416.9f, 3.1f, 2898.4f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Checkpoint, "Checkpoint (6)", new Vector3(-418.6f, 0f, 541.3f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Checkpoint, "Checkpoint (19)", new Vector3(417f, 49.9f, 1979f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Checkpoint, "Checkpoint (2)", new Vector3(0.2f, 0f, 118.02f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Checkpoint, "Checkpoint (11)", new Vector3(-713.81f, 54.2f, 959.2f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Checkpoint, "Checkpoint (9)", new Vector3(-354.94f, 0f, 418.59f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Checkpoint, "Checkpoint", new Vector3(0f, 0f, -42.2f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Checkpoint, "Checkpoint (15)", new Vector3(-57.66f, 95.5f, 1141.65f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Checkpoint, "Checkpoint (21)", new Vector3(417f, 6.2f, 2527.6f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Checkpoint, "Checkpoint (4)", new Vector3(-118.71f, 0f, 479.97f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Checkpoint, "Checkpoint (3)", new Vector3(-118.71f, 0f, 118.02f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Checkpoint, "Checkpoint (8)", new Vector3(-354.94f, 0f, 300.5f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Checkpoint, "Checkpoint (16)", new Vector3(-296.3f, 95.5f, 1137.5f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Arena, "Arena Trigger", new Vector3(-716.9f, 48.5f, 791.7f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Checkpoint, "Checkpoint (18)", new Vector3(417f, 49.9f, 1615.8f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Checkpoint, "Checkpoint (5)", new Vector3(-237.7f, 0f, 541.3f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Checkpoint, "Checkpoint (13)", new Vector3(-296.3f, 95.5f, 959.2f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Checkpoint, "Checkpoint (10)", new Vector3(-713.81f, 54.2f, 541.3f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Arena, "Arena Trigger (1)", new Vector3(250.3f, 48.5f, 1683.1f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Checkpoint, "Checkpoint (14)", new Vector3(-57.66f, 95.5f, 959.2f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Checkpoint, "Checkpoint (23)", new Vector3(421.4f, -2.4f, 3734.3f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Checkpoint, "Checkpoint (7)", new Vector3(-240.52f, 0f, 300.5f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("Level2", TeleportDestinationKind.Checkpoint, "Checkpoint (12)", new Vector3(-468.4f, 97.64f, 959.2f), new Quaternion(0f, 0f, 0f, 1f)),
        new Destination("TutorialScene", TeleportDestinationKind.Arena, "Arena Trigger", new Vector3(-301.2f, 1.8999863f, 65.3f), new Quaternion(0f, 0f, 0f, 1f)),
    };

    static Runner runner;
    static Destination pendingDestination;

    public static List<Destination> GetDestinations()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        string activeSceneName = activeScene.IsValid() ? activeScene.name : string.Empty;
        List<Destination> destinations = new List<Destination>(DefaultCatalog.Length + 16);

        for (int i = 0; i < DefaultCatalog.Length; i++)
        {
            Destination destination = DefaultCatalog[i];
            if (!SceneMatches(destination.SceneName, activeSceneName) && Application.CanStreamedLevelBeLoaded(destination.SceneName))
                destinations.Add(destination);
        }

        AddRuntimeDestinations(activeScene, destinations);
        destinations.Sort(CompareDestinations);
        return destinations;
    }

    public static void TeleportTo(Destination destination)
    {
        if (destination == null)
            return;

        Runner serviceRunner = EnsureRunner();
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && SceneMatches(activeScene.name, destination.SceneName))
        {
            serviceRunner.StartCoroutine(TeleportWhenReady(destination, waitForSceneIntro: false));
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(destination.SceneName))
        {
            Debug.LogWarning($"[CheatTeleportService] Scene '{destination.SceneName}' is not in the build settings and cannot be loaded.");
            return;
        }

        pendingDestination = destination;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        PrepareGameplayStateForTeleport();
        SceneTransitionFader.LoadScene(destination.SceneName, LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (pendingDestination == null || !SceneMatches(scene.name, pendingDestination.SceneName))
            return;

        Destination destination = pendingDestination;
        pendingDestination = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        EnsureRunner().StartCoroutine(TeleportWhenReady(destination, waitForSceneIntro: true));
    }

    static IEnumerator TeleportWhenReady(Destination destination, bool waitForSceneIntro)
    {
        PrepareGameplayStateForTeleport();

        CarController player = null;
        float elapsed = 0f;
        while (elapsed < PlayerLookupTimeoutSeconds)
        {
            player = Object.FindFirstObjectByType<CarController>();
            if (player != null)
                break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (player == null)
        {
            Debug.LogWarning("[CheatTeleportService] Could not find a CarController to teleport.");
            yield break;
        }

        if (waitForSceneIntro)
        {
            elapsed = 0f;
            while (player != null && player.IntroInProgress && elapsed < IntroWaitTimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        ApplyTeleport(destination, player);
    }

    static void ApplyTeleport(Destination destination, CarController player)
    {
        if (destination == null || player == null)
            return;

        ArenaTrigger.ResetActiveArenasForPlayerRespawn();

        if (destination.Kind == TeleportDestinationKind.Checkpoint)
        {
            CheckpointTrigger checkpoint = FindBestCheckpoint(destination);
            if (checkpoint != null)
            {
                checkpoint.ApplyForPlayer(player);
                player.RespawnAtCheckpoint(0f, restoreFullHealth: true, clampDamageToOneHealth: false);
                SnapCameraToPlayer();
                return;
            }
        }
        else
        {
            ArenaTrigger arena = FindBestArena(destination);
            if (arena != null)
            {
                player.SetCheckpointRespawnPose(arena.transform.position, arena.transform.rotation);
                player.RespawnAtCheckpoint(0f, restoreFullHealth: true, clampDamageToOneHealth: false);
                SnapCameraToPlayer();
                arena.BeginArenaForPlayer(player);
                return;
            }
        }

        player.SetCheckpointRespawnPose(destination.FallbackPosition, destination.FallbackRotation);
        player.RespawnAtCheckpoint(0f, restoreFullHealth: true, clampDamageToOneHealth: false);
        SnapCameraToPlayer();
    }

    static CheckpointTrigger FindBestCheckpoint(Destination destination)
    {
        CheckpointTrigger[] checkpoints = Object.FindObjectsByType<CheckpointTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        CheckpointTrigger best = null;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < checkpoints.Length; i++)
        {
            CheckpointTrigger checkpoint = checkpoints[i];
            if (checkpoint == null || !SceneMatches(checkpoint.gameObject.scene.name, destination.SceneName))
                continue;
            if (!string.Equals(checkpoint.name, destination.ObjectName, System.StringComparison.Ordinal))
                continue;

            float score = (checkpoint.transform.position - destination.FallbackPosition).sqrMagnitude;
            if (score < bestScore)
            {
                best = checkpoint;
                bestScore = score;
            }
        }

        return best != null && bestScore <= PositionMatchMaxSqrDistance ? best : best ?? FindFirstCheckpointByName(destination);
    }

    static CheckpointTrigger FindFirstCheckpointByName(Destination destination)
    {
        CheckpointTrigger[] checkpoints = Object.FindObjectsByType<CheckpointTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < checkpoints.Length; i++)
        {
            CheckpointTrigger checkpoint = checkpoints[i];
            if (checkpoint != null &&
                SceneMatches(checkpoint.gameObject.scene.name, destination.SceneName) &&
                string.Equals(checkpoint.name, destination.ObjectName, System.StringComparison.Ordinal))
            {
                return checkpoint;
            }
        }

        return null;
    }

    static ArenaTrigger FindBestArena(Destination destination)
    {
        ArenaTrigger[] arenas = Object.FindObjectsByType<ArenaTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        ArenaTrigger best = null;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < arenas.Length; i++)
        {
            ArenaTrigger arena = arenas[i];
            if (arena == null || !SceneMatches(arena.gameObject.scene.name, destination.SceneName))
                continue;
            if (!string.Equals(arena.name, destination.ObjectName, System.StringComparison.Ordinal))
                continue;

            float score = (arena.transform.position - destination.FallbackPosition).sqrMagnitude;
            if (score < bestScore)
            {
                best = arena;
                bestScore = score;
            }
        }

        return best != null && bestScore <= PositionMatchMaxSqrDistance ? best : best ?? FindFirstArenaByName(destination);
    }

    static ArenaTrigger FindFirstArenaByName(Destination destination)
    {
        ArenaTrigger[] arenas = Object.FindObjectsByType<ArenaTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < arenas.Length; i++)
        {
            ArenaTrigger arena = arenas[i];
            if (arena != null &&
                SceneMatches(arena.gameObject.scene.name, destination.SceneName) &&
                string.Equals(arena.name, destination.ObjectName, System.StringComparison.Ordinal))
            {
                return arena;
            }
        }

        return null;
    }

    static void AddRuntimeDestinations(Scene activeScene, List<Destination> destinations)
    {
        if (!activeScene.IsValid() || string.IsNullOrWhiteSpace(activeScene.name))
            return;

        CheckpointTrigger[] checkpoints = Object.FindObjectsByType<CheckpointTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < checkpoints.Length; i++)
        {
            CheckpointTrigger checkpoint = checkpoints[i];
            if (checkpoint == null || checkpoint.gameObject.scene != activeScene)
                continue;

            destinations.Add(new Destination(
                activeScene.name,
                TeleportDestinationKind.Checkpoint,
                checkpoint.name,
                checkpoint.transform.position,
                checkpoint.transform.rotation));
        }

        ArenaTrigger[] arenas = Object.FindObjectsByType<ArenaTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < arenas.Length; i++)
        {
            ArenaTrigger arena = arenas[i];
            if (arena == null || arena.gameObject.scene != activeScene)
                continue;

            destinations.Add(new Destination(
                activeScene.name,
                TeleportDestinationKind.Arena,
                arena.name,
                arena.transform.position,
                arena.transform.rotation));
        }
    }

    static int CompareDestinations(Destination a, Destination b)
    {
        int sceneCompare = string.Compare(a.SceneName, b.SceneName, System.StringComparison.OrdinalIgnoreCase);
        if (sceneCompare != 0)
            return sceneCompare;

        int kindCompare = a.Kind.CompareTo(b.Kind);
        if (kindCompare != 0)
            return kindCompare;

        int nameCompare = string.Compare(a.ObjectName, b.ObjectName, System.StringComparison.OrdinalIgnoreCase);
        if (nameCompare != 0)
            return nameCompare;

        int zCompare = a.FallbackPosition.z.CompareTo(b.FallbackPosition.z);
        return zCompare != 0 ? zCompare : a.FallbackPosition.x.CompareTo(b.FallbackPosition.x);
    }

    static void PrepareGameplayStateForTeleport()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        GameState.SetPlaying();
    }

    static void SnapCameraToPlayer()
    {
        ThirdPersonCamera camera = Object.FindFirstObjectByType<ThirdPersonCamera>();
        if (camera == null)
            return;

        camera.SnapToPlayer();
        camera.ResumeFollow();
    }

    static bool SceneMatches(string a, string b)
    {
        return string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase);
    }

    static Runner EnsureRunner()
    {
        if (runner != null)
            return runner;

        GameObject runnerObject = new GameObject("Cheat Teleport Service");
        Object.DontDestroyOnLoad(runnerObject);
        runner = runnerObject.AddComponent<Runner>();
        return runner;
    }
}
