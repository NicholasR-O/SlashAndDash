using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[AddComponentMenu("Tutorial/Tutorial Trigger")]
[RequireComponent(typeof(Collider))]
public class TutorialTrigger : MonoBehaviour
{
    [System.Serializable]
    public struct WallPlacement
    {
        public Vector3 localPosition;
        public Vector3 localEulerAngles;

        public WallPlacement(Vector3 localPosition, Vector3 localEulerAngles)
        {
            this.localPosition = localPosition;
            this.localEulerAngles = localEulerAngles;
        }
    }

    [System.Serializable]
    public struct EnemySpawnPlacement
    {
        public Vector3 localPosition;
        public Vector3 localEulerAngles;

        public EnemySpawnPlacement(Vector3 localPosition, Vector3 localEulerAngles)
        {
            this.localPosition = localPosition;
            this.localEulerAngles = localEulerAngles;
        }
    }

    [System.Serializable]
    public class TutorialStep
    {
        [TextArea(2, 5)]
        public string tutorialText = "Do the tutorial action.";
        public CompletionCondition completionCondition = CompletionCondition.Move;
        public MoveDirection moveDirection = MoveDirection.Any;
        [Min(1)]
        public int requiredCount = 1;
        [Min(0f)]
        public float requiredHeldSeconds = 0.15f;
        [Min(0f)]
        public float movementInputThreshold = 0.2f;
        [Min(0f)]
        public float movementSpeedThreshold = 2f;
        [Min(0)]
        public int targetEnemyAliveCount;
        public bool showProgressCounter;
        [Tooltip("Optional tokens: {text}, {current}, {target}")]
        public string counterFormat = "{text} ({current}/{target})";
        public bool raiseWallsOnStepStart = true;
        public bool dropWallsOnStepComplete;
        public bool clearTextOnStepComplete;
    }

    sealed class SpawnedWall
    {
        public Transform transform;
        public Vector3 topPosition;
        public Vector3 bottomPosition;
    }

    struct StepCounterSnapshot
    {
        public int jumpCount;
        public int dashCount;
        public int driftCount;
        public int fireCount;
        public int throwCount;
        public int boostCount;
        public int enemyGrappledCount;
    }

    public enum CompletionCondition
    {
        Move,
        Jump,
        Dash,
        Drift,
        Aim,
        Fire,
        ThrowEnemy,
        Boost,
        Airborne,
        BoostStacks,
        EnemyGrappled,
        EnemiesRemainingAtMost
    }

    public enum MoveDirection
    {
        Any,
        Forward,
        Backward,
        Left,
        Right
    }

    [Header("Detection")]
    [SerializeField] string playerTag = "Player";
    [SerializeField] string enemyTag = "Enemy";

    [Header("Tutorial Steps")]
    [SerializeField] List<TutorialStep> tutorialSteps = new List<TutorialStep>();
    [SerializeField] bool hideTutorialMessageWhenComplete = true;
    [SerializeField] bool dropWallsWhenTutorialCompletes = true;
    [SerializeField] bool destroyTriggerWhenComplete = true;

    [Header("Tutorial Chaining")]
    [SerializeField] TutorialTrigger nextTutorialTrigger;

    [Header("Wall")]
    [SerializeField] GameObject wallPrefab;
    [SerializeField, HideInInspector] List<WallPlacement> wallPlacements = new List<WallPlacement>();

    [Header("Enemy Spawning")]
    [SerializeField] GameObject enemyPrefab;
    [SerializeField, HideInInspector] List<EnemySpawnPlacement> enemySpawnPlacements = new List<EnemySpawnPlacement>();
    [SerializeField] bool spawnEnemiesWhenTutorialStarts = true;

    [Header("Wall Animation")]
    [SerializeField, Min(0f)] float wallSpawnDepth = 12f;
    [SerializeField, Min(0.05f)] float wallRiseDuration = 0.35f;
    [SerializeField, Min(0.05f)] float wallDropDuration = 0.25f;
    [SerializeField] AnimationCurve wallRiseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] AnimationCurve wallDropCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Legacy Data Migration")]
    [SerializeField, HideInInspector, FormerlySerializedAs("manualWallPosition")] Vector3 legacySingleWallPosition;
    [SerializeField, HideInInspector, FormerlySerializedAs("manualWallEulerAngles")] Vector3 legacySingleWallEulerAngles;
    [SerializeField, HideInInspector, FormerlySerializedAs("manualWallPlacementIsLocal")] bool legacySingleWallWasLocal = true;
    [SerializeField, HideInInspector, FormerlySerializedAs("useWallAnchor")] bool legacyUseWallAnchor;
    [SerializeField, HideInInspector, FormerlySerializedAs("wallAnchor")] Transform legacyWallAnchor;
    [SerializeField, HideInInspector] bool wallPlacementsMigrated;

    [SerializeField, HideInInspector, TextArea(2, 4)] string tutorialText = "Do the tutorial action.";
    [SerializeField, HideInInspector] CompletionCondition completionCondition = CompletionCondition.Move;
    [SerializeField, HideInInspector, Min(0f)] float requiredHeldSeconds = 0.15f;
    [SerializeField, HideInInspector, Min(0f)] float movementInputThreshold = 0.2f;
    [SerializeField, HideInInspector, Min(0f)] float movementSpeedThreshold = 2f;
    [SerializeField, HideInInspector] bool tutorialStepsMigrated;

    const float DefaultWallForwardDistance = 10f;
    static readonly Vector3 FallbackWallPreviewSize = new Vector3(8f, 6f, 1.2f);

    readonly List<SpawnedWall> spawnedWalls = new List<SpawnedWall>();
    Collider triggerCollider;

    CarController playerCar;
    GrappleController playerGrapple;
    HUDScript hud;
    Transform cachedPlayerTransform;

    bool hasTriggered;
    bool subscribedToPlayerEvents;
    bool tutorialMessageVisible;

    int jumpCount;
    int dashCount;
    int driftCount;
    int fireCount;
    int throwCount;
    int boostCount;
    int enemyGrappledCount;
    float heldConditionTimer;
    string lastHudMessage = string.Empty;

    public int WallCount
    {
        get
        {
            EnsureWallPlacementsInitialized();
            return wallPlacements.Count;
        }
    }

    public int EnemySpawnCount
    {
        get
        {
            EnsureEnemySpawnPlacementsInitialized();
            return enemySpawnPlacements.Count;
        }
    }

    public float WallSpawnDepth => Mathf.Max(0f, wallSpawnDepth);
    public Vector3 WallPreviewSize => EstimateWallPreviewSize();

    public bool EnsureWallDataForEditing()
    {
        int previousCount = wallPlacements != null ? wallPlacements.Count : -1;
        bool previousMigrated = wallPlacementsMigrated;
        EnsureWallPlacementsInitialized();
        return previousCount != wallPlacements.Count || previousMigrated != wallPlacementsMigrated;
    }

    public bool EnsureEnemySpawnDataForEditing()
    {
        int previousCount = enemySpawnPlacements != null ? enemySpawnPlacements.Count : -1;
        EnsureEnemySpawnPlacementsInitialized();
        return previousCount != enemySpawnPlacements.Count;
    }

    public bool EnsureStepDataForEditing()
    {
        int previousCount = tutorialSteps != null ? tutorialSteps.Count : -1;
        bool previousMigrated = tutorialStepsMigrated;
        EnsureTutorialStepsInitialized();
        return previousCount != tutorialSteps.Count || previousMigrated != tutorialStepsMigrated;
    }

    void Awake()
    {
        EnsureWallPlacementsInitialized();
        EnsureEnemySpawnPlacementsInitialized();
        EnsureTutorialStepsInitialized();

        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        wallPlacements = new List<WallPlacement>
        {
            new WallPlacement(GetDefaultWallLocalPosition(), Vector3.zero)
        };
        wallPlacementsMigrated = true;

        enemySpawnPlacements = new List<EnemySpawnPlacement>();

        tutorialSteps = new List<TutorialStep>
        {
            CreateDefaultStep()
        };
        tutorialStepsMigrated = true;
    }

    void OnDestroy()
    {
        ClearHudMessage();
        UnsubscribeFromPlayerEvents();
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasTriggered)
            return;

        if (!other.CompareTag(playerTag))
            return;

        Transform playerTransform = other.attachedRigidbody != null
            ? other.attachedRigidbody.transform
            : other.transform;

        ActivateTutorial(playerTransform);
    }

    public void ActivateTutorial(Transform playerTransform = null)
    {
        if (hasTriggered)
            return;

        hasTriggered = true;

        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
            triggerCollider.enabled = false;

        ResolvePlayerReferences(playerTransform);
        StartCoroutine(RunTutorialRoutine());
    }

    IEnumerator RunTutorialRoutine()
    {
        hud = FindFirstObjectByType<HUDScript>();
        EnsureTutorialStepsInitialized();

        if (spawnEnemiesWhenTutorialStarts)
            SpawnEnemies();

        for (int i = 0; i < tutorialSteps.Count; i++)
        {
            TutorialStep step = tutorialSteps[i];
            if (step == null)
                continue;

            heldConditionTimer = 0f;
            StepCounterSnapshot stepStartCounters = CaptureCounters();

            if (step.raiseWallsOnStepStart)
                yield return EnsureWallsRaised();

            bool stepCompleted = false;
            while (!stepCompleted)
            {
                stepCompleted = IsStepConditionMet(step, stepStartCounters, out int current, out int target);
                UpdateHudStepMessage(step, current, target);
                yield return null;
            }

            if (step.clearTextOnStepComplete)
                ClearHudMessage();

            if (step.dropWallsOnStepComplete)
                yield return DropAndDestroyWalls();
        }

        if (hideTutorialMessageWhenComplete)
            ClearHudMessage();

        if (dropWallsWhenTutorialCompletes)
            yield return DropAndDestroyWalls();

        UnsubscribeFromPlayerEvents();

        TriggerNextTutorial();

        if (destroyTriggerWhenComplete)
            Destroy(gameObject);
    }

    void ResolvePlayerReferences(Transform playerTransform)
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            playerTransform = player != null ? player.transform : null;
        }

        playerCar = playerTransform != null ? playerTransform.GetComponentInParent<CarController>() : null;
        if (playerCar == null)
            playerCar = FindFirstObjectByType<CarController>();

        playerGrapple = playerTransform != null ? playerTransform.GetComponentInParent<GrappleController>() : null;
        if (playerGrapple == null && playerCar != null)
            playerGrapple = playerCar.GetComponent<GrappleController>();
        if (playerGrapple == null)
            playerGrapple = FindFirstObjectByType<GrappleController>();

        cachedPlayerTransform = playerTransform != null
            ? playerTransform
            : playerCar != null ? playerCar.transform : null;

        SubscribeToPlayerEvents();
    }

    void SubscribeToPlayerEvents()
    {
        if (subscribedToPlayerEvents)
            return;

        if (playerCar != null)
        {
            playerCar.JumpPerformed += OnPlayerJumpPerformed;
            playerCar.DashPerformed += OnPlayerDashPerformed;
            playerCar.DriftStarted += OnPlayerDriftStarted;
            playerCar.BoostStackGained += OnPlayerBoostStackGained;
        }

        if (playerGrapple != null)
        {
            playerGrapple.FirePerformed += OnPlayerFirePerformed;
            playerGrapple.EnemyThrown += OnPlayerEnemyThrown;
            playerGrapple.EnemyGrappled += OnPlayerEnemyGrappled;
        }

        subscribedToPlayerEvents = true;
    }

    void UnsubscribeFromPlayerEvents()
    {
        if (!subscribedToPlayerEvents)
            return;

        if (playerCar != null)
        {
            playerCar.JumpPerformed -= OnPlayerJumpPerformed;
            playerCar.DashPerformed -= OnPlayerDashPerformed;
            playerCar.DriftStarted -= OnPlayerDriftStarted;
            playerCar.BoostStackGained -= OnPlayerBoostStackGained;
        }

        if (playerGrapple != null)
        {
            playerGrapple.FirePerformed -= OnPlayerFirePerformed;
            playerGrapple.EnemyThrown -= OnPlayerEnemyThrown;
            playerGrapple.EnemyGrappled -= OnPlayerEnemyGrappled;
        }

        subscribedToPlayerEvents = false;
    }

    StepCounterSnapshot CaptureCounters()
    {
        return new StepCounterSnapshot
        {
            jumpCount = jumpCount,
            dashCount = dashCount,
            driftCount = driftCount,
            fireCount = fireCount,
            throwCount = throwCount,
            boostCount = boostCount,
            enemyGrappledCount = enemyGrappledCount
        };
    }

    bool IsStepConditionMet(TutorialStep step, StepCounterSnapshot snapshot, out int current, out int target)
    {
        current = 0;
        target = Mathf.Max(1, step.requiredCount);

        switch (step.completionCondition)
        {
            case CompletionCondition.Move:
                target = 1;
                current = IsHeldConditionActive(
                    IsMoveConditionActive(step.moveDirection, step.movementInputThreshold, step.movementSpeedThreshold),
                    step.requiredHeldSeconds)
                    ? 1
                    : 0;
                return current >= target;

            case CompletionCondition.Jump:
                current = jumpCount - snapshot.jumpCount;
                return current >= target;

            case CompletionCondition.Dash:
                current = dashCount - snapshot.dashCount;
                return current >= target;

            case CompletionCondition.Drift:
                current = driftCount - snapshot.driftCount;
                return current >= target;

            case CompletionCondition.Aim:
                target = 1;
                current = IsHeldConditionActive(IsAimConditionActive(), step.requiredHeldSeconds) ? 1 : 0;
                return current >= target;

            case CompletionCondition.Fire:
                current = fireCount - snapshot.fireCount;
                return current >= target;

            case CompletionCondition.ThrowEnemy:
                current = throwCount - snapshot.throwCount;
                return current >= target;

            case CompletionCondition.Boost:
                current = boostCount - snapshot.boostCount;
                return current >= target;

            case CompletionCondition.Airborne:
                target = 1;
                bool isAirborne = playerCar != null && !playerCar.IsGrounded;
                current = IsHeldConditionActive(isAirborne, step.requiredHeldSeconds) ? 1 : 0;
                return current >= target;

            case CompletionCondition.BoostStacks:
                current = playerCar != null ? playerCar.CurrentBoostStacks : 0;
                return current >= target;

            case CompletionCondition.EnemyGrappled:
                current = enemyGrappledCount - snapshot.enemyGrappledCount;
                return current >= target;

            case CompletionCondition.EnemiesRemainingAtMost:
                target = Mathf.Max(0, step.targetEnemyAliveCount);
                current = CountAliveEnemies();
                return current <= target;

            default:
                return false;
        }
    }

    bool IsMoveConditionActive(MoveDirection direction, float inputThreshold, float speedThreshold)
    {
        if (playerCar == null)
            return false;

        float threshold = Mathf.Max(0f, inputThreshold);
        Vector2 moveInput = playerCar.MoveInput;

        switch (direction)
        {
            case MoveDirection.Forward:
                return moveInput.y >= threshold;
            case MoveDirection.Backward:
                return moveInput.y <= -threshold;
            case MoveDirection.Left:
                return moveInput.x <= -threshold;
            case MoveDirection.Right:
                return moveInput.x >= threshold;
            case MoveDirection.Any:
            default:
                bool hasInput = moveInput.sqrMagnitude >= threshold * threshold;
                bool hasSpeed = playerCar.CurrentSpeed >= Mathf.Max(0f, speedThreshold);
                return hasInput || hasSpeed;
        }
    }

    bool IsAimConditionActive()
    {
        if (playerGrapple != null)
            return playerGrapple.IsAiming;

        return GrappleController.IsAimingStatic;
    }

    int CountAliveEnemies()
    {
        if (string.IsNullOrWhiteSpace(enemyTag))
            return 0;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        int aliveCount = 0;
        for (int i = 0; i < enemies.Length; i++)
        {
            Enemy enemy = enemies[i] != null ? enemies[i].GetComponent<Enemy>() : null;
            if (enemy == null || enemy.IsAlive)
                aliveCount++;
        }

        return aliveCount;
    }

    bool IsHeldConditionActive(bool conditionIsTrue, float requiredSeconds)
    {
        if (!conditionIsTrue)
        {
            heldConditionTimer = 0f;
            return false;
        }

        heldConditionTimer += Time.deltaTime;
        return heldConditionTimer >= Mathf.Max(0f, requiredSeconds);
    }

    void UpdateHudStepMessage(TutorialStep step, int current, int target)
    {
        if (hud == null)
            return;

        string message = BuildStepDisplayText(step, current, target);
        if (message == lastHudMessage)
            return;

        hud.ShowTutorialMessage(message);
        tutorialMessageVisible = !string.IsNullOrWhiteSpace(message);
        lastHudMessage = message;
    }

    string BuildStepDisplayText(TutorialStep step, int current, int target)
    {
        string baseText = string.IsNullOrWhiteSpace(step.tutorialText) ? string.Empty : step.tutorialText.Trim();
        if (!step.showProgressCounter)
            return baseText;

        string format = string.IsNullOrWhiteSpace(step.counterFormat)
            ? "{text} ({current}/{target})"
            : step.counterFormat;

        return format
            .Replace("{text}", baseText)
            .Replace("{current}", current.ToString())
            .Replace("{target}", target.ToString());
    }

    void ClearHudMessage()
    {
        if (hud != null && tutorialMessageVisible)
            hud.HideTutorialMessage();

        tutorialMessageVisible = false;
        lastHudMessage = string.Empty;
    }

    void TriggerNextTutorial()
    {
        if (nextTutorialTrigger == null || nextTutorialTrigger == this)
            return;

        nextTutorialTrigger.ActivateTutorial(cachedPlayerTransform);
    }

    IEnumerator EnsureWallsRaised()
    {
        if (spawnedWalls.Count > 0)
            yield break;

        if (!TrySpawnWalls())
            yield break;

        yield return AnimateWalls(isRising: true);
    }

    IEnumerator DropAndDestroyWalls()
    {
        if (spawnedWalls.Count == 0)
            yield break;

        yield return AnimateWalls(isRising: false);

        for (int i = 0; i < spawnedWalls.Count; i++)
        {
            SpawnedWall wall = spawnedWalls[i];
            if (wall != null && wall.transform != null)
                Destroy(wall.transform.gameObject);
        }

        spawnedWalls.Clear();
    }

    bool TrySpawnWalls()
    {
        if (wallPrefab == null)
            return false;

        EnsureWallPlacementsInitialized();
        spawnedWalls.Clear();

        for (int i = 0; i < wallPlacements.Count; i++)
        {
            if (!TryGetWallPlacementWorld(i, out Vector3 wallTopPosition, out Quaternion wallRotation))
                continue;

            Vector3 wallBottomPosition = wallTopPosition + Vector3.down * Mathf.Max(0f, wallSpawnDepth);
            Transform wall = Instantiate(wallPrefab, wallBottomPosition, wallRotation, transform).transform;

            spawnedWalls.Add(new SpawnedWall
            {
                transform = wall,
                topPosition = wallTopPosition,
                bottomPosition = wallBottomPosition
            });
        }

        return spawnedWalls.Count > 0;
    }

    IEnumerator AnimateWalls(bool isRising)
    {
        if (spawnedWalls.Count == 0)
            yield break;

        float duration = Mathf.Max(0.01f, isRising ? wallRiseDuration : wallDropDuration);
        AnimationCurve curve = isRising ? wallRiseCurve : wallDropCurve;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = curve != null ? curve.Evaluate(t) : t;

            for (int i = 0; i < spawnedWalls.Count; i++)
            {
                SpawnedWall wall = spawnedWalls[i];
                if (wall == null || wall.transform == null)
                    continue;

                Vector3 from = isRising ? wall.bottomPosition : wall.topPosition;
                Vector3 to = isRising ? wall.topPosition : wall.bottomPosition;
                wall.transform.position = Vector3.LerpUnclamped(from, to, eased);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < spawnedWalls.Count; i++)
        {
            SpawnedWall wall = spawnedWalls[i];
            if (wall == null || wall.transform == null)
                continue;

            wall.transform.position = isRising ? wall.topPosition : wall.bottomPosition;
        }
    }

    void SpawnEnemies()
    {
        TrySpawnEnemies();
    }

    bool TrySpawnEnemies()
    {
        if (enemyPrefab == null)
            return false;

        EnsureEnemySpawnPlacementsInitialized();

        if (enemySpawnPlacements.Count == 0)
            return false;

        Transform parent = null;

        for (int i = 0; i < enemySpawnPlacements.Count; i++)
        {
            if (!TryGetEnemySpawnPlacementWorld(i, out Vector3 position, out Quaternion rotation))
                continue;

            Instantiate(enemyPrefab, position, rotation, parent);
        }

        return true;
    }

    public void AddWallPlacement()
    {
        EnsureWallPlacementsInitialized();

        if (wallPlacements.Count == 0)
        {
            wallPlacements.Add(new WallPlacement(GetDefaultWallLocalPosition(), Vector3.zero));
            return;
        }

        WallPlacement last = wallPlacements[wallPlacements.Count - 1];
        float spacing = Mathf.Max(2f, Mathf.Max(WallPreviewSize.x, WallPreviewSize.z));
        Vector3 localOffset = Quaternion.Euler(last.localEulerAngles) * Vector3.right * spacing;
        wallPlacements.Add(new WallPlacement(last.localPosition + localOffset, last.localEulerAngles));
    }

    public void DuplicateWallPlacement(int index)
    {
        if (!IsWallIndexValid(index))
            return;

        WallPlacement source = wallPlacements[index];
        float spacing = Mathf.Max(2f, Mathf.Max(WallPreviewSize.x, WallPreviewSize.z));
        Vector3 localOffset = Quaternion.Euler(source.localEulerAngles) * Vector3.right * spacing;
        wallPlacements.Insert(index + 1, new WallPlacement(source.localPosition + localOffset, source.localEulerAngles));
    }

    public void RemoveWallPlacement(int index)
    {
        if (!IsWallIndexValid(index))
            return;

        if (wallPlacements.Count <= 1)
        {
            wallPlacements[0] = new WallPlacement(GetDefaultWallLocalPosition(), Vector3.zero);
            return;
        }

        wallPlacements.RemoveAt(index);
    }

    public bool IsWallIndexValid(int index)
    {
        return wallPlacements != null && index >= 0 && index < wallPlacements.Count;
    }

    public bool TryGetWallPlacementWorld(int index, out Vector3 position, out Quaternion rotation)
    {
        EnsureWallPlacementsInitialized();

        if (!IsWallIndexValid(index))
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return false;
        }

        WallPlacement placement = wallPlacements[index];
        position = transform.TransformPoint(placement.localPosition);
        rotation = transform.rotation * Quaternion.Euler(placement.localEulerAngles);
        return true;
    }

    public void SetWallPlacementWorld(int index, Vector3 worldPosition, Quaternion worldRotation)
    {
        EnsureWallPlacementsInitialized();
        if (!IsWallIndexValid(index))
            return;

        WallPlacement placement = wallPlacements[index];
        placement.localPosition = transform.InverseTransformPoint(worldPosition);
        placement.localEulerAngles = (Quaternion.Inverse(transform.rotation) * worldRotation).eulerAngles;
        wallPlacements[index] = placement;
    }

    public void AddEnemySpawnPlacement()
    {
        EnsureEnemySpawnPlacementsInitialized();

        if (enemySpawnPlacements.Count == 0)
        {
            enemySpawnPlacements.Add(new EnemySpawnPlacement(GetDefaultEnemySpawnLocalPosition(), Vector3.zero));
            return;
        }

        EnemySpawnPlacement last = enemySpawnPlacements[enemySpawnPlacements.Count - 1];
        enemySpawnPlacements.Add(new EnemySpawnPlacement(last.localPosition + Vector3.right * 2f, last.localEulerAngles));
    }

    public void DuplicateEnemySpawnPlacement(int index)
    {
        if (!IsEnemySpawnIndexValid(index))
            return;

        EnemySpawnPlacement source = enemySpawnPlacements[index];
        enemySpawnPlacements.Insert(index + 1, new EnemySpawnPlacement(source.localPosition + Vector3.right * 2f, source.localEulerAngles));
    }

    public void RemoveEnemySpawnPlacement(int index)
    {
        if (!IsEnemySpawnIndexValid(index))
            return;

        enemySpawnPlacements.RemoveAt(index);
    }

    public bool IsEnemySpawnIndexValid(int index)
    {
        return enemySpawnPlacements != null && index >= 0 && index < enemySpawnPlacements.Count;
    }

    public bool TryGetEnemySpawnPlacementWorld(int index, out Vector3 position, out Quaternion rotation)
    {
        EnsureEnemySpawnPlacementsInitialized();

        if (!IsEnemySpawnIndexValid(index))
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return false;
        }

        EnemySpawnPlacement placement = enemySpawnPlacements[index];
        position = transform.TransformPoint(placement.localPosition);
        rotation = transform.rotation * Quaternion.Euler(placement.localEulerAngles);
        return true;
    }

    public void SetEnemySpawnPlacementWorld(int index, Vector3 worldPosition, Quaternion worldRotation)
    {
        EnsureEnemySpawnPlacementsInitialized();
        if (!IsEnemySpawnIndexValid(index))
            return;

        EnemySpawnPlacement placement = enemySpawnPlacements[index];
        placement.localPosition = transform.InverseTransformPoint(worldPosition);
        placement.localEulerAngles = (Quaternion.Inverse(transform.rotation) * worldRotation).eulerAngles;
        enemySpawnPlacements[index] = placement;
    }

    void OnPlayerJumpPerformed() => jumpCount++;
    void OnPlayerDashPerformed() => dashCount++;
    void OnPlayerDriftStarted() => driftCount++;
    void OnPlayerFirePerformed() => fireCount++;
    void OnPlayerEnemyThrown() => throwCount++;
    void OnPlayerBoostStackGained() => boostCount++;
    void OnPlayerEnemyGrappled() => enemyGrappledCount++;

    void OnValidate()
    {
        EnsureWallPlacementsInitialized();
        EnsureEnemySpawnPlacementsInitialized();
        EnsureTutorialStepsInitialized();

        wallRiseDuration = Mathf.Max(0.05f, wallRiseDuration);
        wallDropDuration = Mathf.Max(0.05f, wallDropDuration);

        if (tutorialSteps == null)
            return;

        for (int i = 0; i < tutorialSteps.Count; i++)
        {
            TutorialStep step = tutorialSteps[i];
            if (step == null)
                continue;

            step.requiredCount = Mathf.Max(1, step.requiredCount);
            step.requiredHeldSeconds = Mathf.Max(0f, step.requiredHeldSeconds);
            step.movementInputThreshold = Mathf.Max(0f, step.movementInputThreshold);
            step.movementSpeedThreshold = Mathf.Max(0f, step.movementSpeedThreshold);
            step.targetEnemyAliveCount = Mathf.Max(0, step.targetEnemyAliveCount);
        }
    }

    void EnsureTutorialStepsInitialized()
    {
        if (tutorialSteps == null)
            tutorialSteps = new List<TutorialStep>();

        if (!tutorialStepsMigrated)
        {
            if (tutorialSteps.Count == 0)
            {
                TutorialStep migratedStep = new TutorialStep
                {
                    tutorialText = tutorialText,
                    completionCondition = completionCondition,
                    requiredHeldSeconds = Mathf.Max(0f, requiredHeldSeconds),
                    movementInputThreshold = Mathf.Max(0f, movementInputThreshold),
                    movementSpeedThreshold = Mathf.Max(0f, movementSpeedThreshold),
                    raiseWallsOnStepStart = true,
                    dropWallsOnStepComplete = true
                };

                tutorialSteps.Add(migratedStep);
            }

            tutorialStepsMigrated = true;
        }

        if (tutorialSteps.Count == 0)
            tutorialSteps.Add(CreateDefaultStep());
    }

    TutorialStep CreateDefaultStep()
    {
        return new TutorialStep
        {
            tutorialText = "Do the tutorial action.",
            completionCondition = CompletionCondition.Move,
            moveDirection = MoveDirection.Any,
            requiredCount = 1,
            requiredHeldSeconds = 0.15f,
            movementInputThreshold = 0.2f,
            movementSpeedThreshold = 2f,
            raiseWallsOnStepStart = true,
            dropWallsOnStepComplete = true
        };
    }

    void EnsureWallPlacementsInitialized()
    {
        if (wallPlacements == null)
            wallPlacements = new List<WallPlacement>();

        if (!wallPlacementsMigrated)
        {
            if (wallPlacements.Count == 0)
            {
                if (legacyUseWallAnchor && legacyWallAnchor != null)
                {
                    Vector3 migratedLocalPosition = transform.InverseTransformPoint(legacyWallAnchor.position);
                    Vector3 migratedLocalEuler = (Quaternion.Inverse(transform.rotation) * legacyWallAnchor.rotation).eulerAngles;
                    wallPlacements.Add(new WallPlacement(migratedLocalPosition, migratedLocalEuler));
                }
                else
                {
                    Vector3 migratedLocalPosition = legacySingleWallPosition;
                    Quaternion migratedLocalRotation = Quaternion.Euler(legacySingleWallEulerAngles);

                    if (!legacySingleWallWasLocal)
                    {
                        migratedLocalPosition = transform.InverseTransformPoint(legacySingleWallPosition);
                        migratedLocalRotation = Quaternion.Inverse(transform.rotation) * Quaternion.Euler(legacySingleWallEulerAngles);
                    }

                    if (migratedLocalPosition == Vector3.zero && legacySingleWallEulerAngles == Vector3.zero)
                        migratedLocalPosition = GetDefaultWallLocalPosition();

                    wallPlacements.Add(new WallPlacement(migratedLocalPosition, migratedLocalRotation.eulerAngles));
                }
            }

            wallPlacementsMigrated = true;
        }

        if (wallPlacements.Count == 0)
            wallPlacements.Add(new WallPlacement(GetDefaultWallLocalPosition(), Vector3.zero));
    }

    void EnsureEnemySpawnPlacementsInitialized()
    {
        if (enemySpawnPlacements == null)
            enemySpawnPlacements = new List<EnemySpawnPlacement>();
    }

    Vector3 GetDefaultWallLocalPosition()
    {
        return Vector3.forward * DefaultWallForwardDistance;
    }

    Vector3 GetDefaultEnemySpawnLocalPosition()
    {
        return Vector3.forward * DefaultWallForwardDistance;
    }

    void OnDrawGizmosSelected()
    {
        EnsureWallPlacementsInitialized();

        Gizmos.color = new Color(0.15f, 0.9f, 1f, 0.75f);
        Gizmos.DrawWireSphere(transform.position, 0.4f);

        Vector3 wallSize = EstimateWallPreviewSize();
        float depth = Mathf.Max(0f, wallSpawnDepth);

        for (int i = 0; i < wallPlacements.Count; i++)
        {
            WallPlacement placement = wallPlacements[i];
            Vector3 wallPosition = transform.TransformPoint(placement.localPosition);
            Quaternion wallRotation = transform.rotation * Quaternion.Euler(placement.localEulerAngles);
            DrawWallPlacementGizmo(wallPosition, wallRotation, wallSize, depth);
        }

        EnsureEnemySpawnPlacementsInitialized();
        Gizmos.color = new Color(0.35f, 1f, 0.45f, 0.85f);

        for (int i = 0; i < enemySpawnPlacements.Count; i++)
        {
            EnemySpawnPlacement placement = enemySpawnPlacements[i];
            Vector3 spawnPosition = transform.TransformPoint(placement.localPosition);
            Quaternion spawnRotation = transform.rotation * Quaternion.Euler(placement.localEulerAngles);

            Gizmos.DrawSphere(spawnPosition, 0.35f);
            Gizmos.DrawLine(spawnPosition, spawnPosition + (spawnRotation * Vector3.forward * 1.2f));
        }
    }

    void DrawWallPlacementGizmo(Vector3 topPosition, Quaternion rotation, Vector3 wallSize, float dropDepth)
    {
        Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.9f);
        Gizmos.DrawSphere(topPosition, 0.2f);
        Gizmos.DrawLine(topPosition, topPosition + Vector3.down * dropDepth);

        DrawWallPreviewGizmo(topPosition, rotation, wallSize, new Color(1f, 0.6f, 0.15f, 0.8f));
        DrawWallPreviewGizmo(topPosition + Vector3.down * dropDepth, rotation, wallSize, new Color(0.55f, 0.85f, 1f, 0.6f));
    }

    Vector3 EstimateWallPreviewSize()
    {
        if (wallPrefab == null)
            return FallbackWallPreviewSize;

        Bounds bounds = default;
        bool hasBounds = false;

        Collider[] colliders = wallPrefab.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (!hasBounds)
            {
                bounds = colliders[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(colliders[i].bounds);
            }
        }

        if (!hasBounds)
        {
            Renderer[] renderers = wallPrefab.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!hasBounds)
                {
                    bounds = renderers[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }
        }

        if (!hasBounds)
            return FallbackWallPreviewSize;

        Vector3 size = bounds.size;
        if (size.x <= 0.01f || size.y <= 0.01f || size.z <= 0.01f)
            return FallbackWallPreviewSize;

        return size;
    }

    static void DrawWallPreviewGizmo(Vector3 position, Quaternion rotation, Vector3 size, Color color)
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
        Gizmos.color = color;
        Gizmos.DrawWireCube(Vector3.zero, size);

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
