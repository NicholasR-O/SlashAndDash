using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Arena/Arena Trigger")]
[RequireComponent(typeof(Collider))]
public class ArenaTrigger : MonoBehaviour
{
    sealed class SpawnedWall
    {
        public Transform transform;
        public Vector3 topPosition;
        public Vector3 bottomPosition;
        public Quaternion topRotation;
        public Quaternion fallenRotation;
        public Quaternion impactRotation;
        public Vector3 fallenPosition;
        public Vector3 impactPosition;
    }

    sealed class CompletionSinkObject
    {
        public Transform transform;
        public Vector3 startPosition;
        public Vector3 endPosition;
        public Quaternion startRotation;
    }

    [Header("Detection")]
    [SerializeField] string playerTag = "Player";

    [Header("Arena Size")]
    [SerializeField, Min(2f)] float arenaRadius = 18f;

    [Header("Walls")]
    [SerializeField] GameObject wallPrefab;
    [SerializeField] Transform spawnedWallParent;
    [SerializeField, Min(4)] int minWallCount = 8;
    [SerializeField, Min(4)] int maxWallCount = 64;
    [SerializeField, Min(0.5f)] float fallbackWallWidth = 3f;
    [SerializeField, Range(0f, 0.35f)] float allowedWidthOverlap = 0.08f;

    [Header("Wall Animation")]
    [SerializeField, Min(0f)] float wallSpawnDepth = 18f;
    [SerializeField, Min(0.05f)] float wallRiseDuration = 0.35f;
    [SerializeField, Min(0f)] float wallRiseStagger = 0.01f;
    [SerializeField, Min(0.05f)] float wallDropDuration = 0.85f;
    [SerializeField, Min(0f)] float wallDropStagger = 0.018f;
    [SerializeField, Min(0f)] float wallFallOutwardDistance = 5.25f;
    [SerializeField, Min(0f)] float wallFallDownDistance = 2.35f;
    [SerializeField, Min(0f)] float wallFallAngle = 96f;
    [SerializeField, Min(0f)] float wallFallImpactOutwardOvershoot = 0.65f;
    [SerializeField, Min(0f)] float wallFallImpactDownOvershoot = 0.35f;
    [SerializeField, Min(0f)] float wallFallImpactAngleOvershoot = 6f;
    [SerializeField, Min(0f)] float wallFallSettleDuration = 0.22f;
    [SerializeField, Min(0f)] float wallFallLingerDuration = 1.1f;
    [SerializeField] AnimationCurve wallRiseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] AnimationCurve wallDropCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Enemy Spawner")]
    [SerializeField] ArenaSpawner arenaSpawnerPrefab;
    [SerializeField] Transform spawnedSpawnerParent;

    [Header("Completion Props")]
    [SerializeField] bool sinkSpawnerHousesOnComplete = true;
    [SerializeField, Min(0.05f)] float houseSinkDuration = 1.15f;
    [SerializeField, Min(0f)] float houseSinkDepth = 28f;
    [SerializeField, Min(0f)] float houseSinkStagger = 0.04f;
    [SerializeField] AnimationCurve houseSinkCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    readonly List<SpawnedWall> spawnedWalls = new List<SpawnedWall>();
    readonly List<CompletionSinkObject> completionSinkObjects = new List<CompletionSinkObject>();

    bool hasTriggered;
    bool isShuttingDown;
    bool countedAsActiveArena;
    bool arenaProgressVisible;
    Transform cachedPlayer;
    ArenaSpawner activeSpawner;
    Collider triggerCollider;

    static readonly List<ArenaTrigger> activePlayerArenas = new List<ArenaTrigger>();

    public static bool PlayerIsInArena => activePlayerArenas.Count > 0;
    public static event System.Action<int> ArenaStarted;
    public static event System.Action<int> ArenaEnemyCountChanged;
    public static event System.Action ArenaEnded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetActiveArenaState()
    {
        activePlayerArenas.Clear();
        ArenaStarted = null;
        ArenaEnemyCountChanged = null;
        ArenaEnded = null;
    }

    public static void ResetActiveArenaForPlayerDeath()
    {
        ResetActiveArenasForPlayerRespawn();
    }

    public static void ResetActiveArenasForPlayerRespawn()
    {
        if (activePlayerArenas.Count == 0)
        {
            ProjectileCleanup.ClearAllProjectiles();
            return;
        }

        ArenaTrigger[] arenasToReset = activePlayerArenas.ToArray();
        for (int i = 0; i < arenasToReset.Length; i++)
        {
            ArenaTrigger arena = arenasToReset[i];
            if (arena != null)
                arena.ResetArenaForPlayerDeath();
        }

        activePlayerArenas.Clear();
        ProjectileCleanup.ClearAllProjectiles();
    }

    void Awake()
    {
        triggerCollider = GetComponent<Collider>();
    }

    void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasTriggered || isShuttingDown)
            return;

        if (!other.CompareTag(playerTag))
            return;

        Transform playerTransform = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform;
        BeginArenaForPlayer(playerTransform);
    }

    public bool BeginArenaForPlayer(CarController player)
    {
        return BeginArenaForPlayer(player != null ? player.transform : null);
    }

    public bool BeginArenaForPlayer(Transform playerTransform)
    {
        if (hasTriggered || isShuttingDown || playerTransform == null)
            return false;

        cachedPlayer = playerTransform;
        hasTriggered = true;
        MarkArenaActive();

        if (triggerCollider != null)
            triggerCollider.enabled = false;

        StartCoroutine(RunArenaRoutine());
        return true;
    }

    IEnumerator RunArenaRoutine()
    {
        if (!TrySpawnWalls())
        {
            Debug.LogWarning("ArenaTrigger failed to spawn walls due to missing wall prefab.", this);
            NotifyArenaEnded();
            MarkArenaInactive();
            yield break;
        }

        yield return AnimateWalls(isRising: true);

        if (arenaSpawnerPrefab == null)
        {
            Debug.LogWarning("ArenaTrigger has no ArenaSpawner prefab assigned.", this);
            NotifyArenaEnded();
            MarkArenaInactive();
            yield break;
        }

        activeSpawner = Instantiate(arenaSpawnerPrefab, transform.position, Quaternion.identity, spawnedSpawnerParent);
        CaptureCompletionSinkObjects(transform);
        activeSpawner.BeginSpawning(this, transform.position, arenaRadius, cachedPlayer);
        NotifyArenaStarted(activeSpawner.RemainingEnemyCount);
    }

    bool TrySpawnWalls()
    {
        if (wallPrefab == null)
            return false;

        int wallCount = CalculateWallCount(arenaRadius);
        spawnedWalls.Clear();

        for (int i = 0; i < wallCount; i++)
        {
            float t = (float)i / wallCount;
            float angle = t * Mathf.PI * 2f;
            Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            Vector3 topPosition = transform.position + outward * arenaRadius;
            Vector3 startPosition = topPosition + Vector3.down * wallSpawnDepth;
            Quaternion wallRotation = Quaternion.LookRotation(outward, Vector3.up);
            Vector3 fallAxis = Vector3.Cross(Vector3.up, outward).normalized;
            if (fallAxis.sqrMagnitude < 0.0001f)
                fallAxis = Vector3.right;

            Transform wall = Instantiate(wallPrefab, startPosition, wallRotation, spawnedWallParent).transform;
            spawnedWalls.Add(new SpawnedWall
            {
                transform = wall,
                topPosition = topPosition,
                bottomPosition = startPosition,
                topRotation = wallRotation,
                fallenRotation = Quaternion.AngleAxis(Mathf.Abs(wallFallAngle), fallAxis) * wallRotation,
                impactRotation = Quaternion.AngleAxis(Mathf.Abs(wallFallAngle) + Mathf.Abs(wallFallImpactAngleOvershoot), fallAxis) * wallRotation,
                fallenPosition = topPosition + outward * wallFallOutwardDistance + Vector3.down * wallFallDownDistance,
                impactPosition = topPosition
                    + outward * (wallFallOutwardDistance + wallFallImpactOutwardOvershoot)
                    + Vector3.down * (wallFallDownDistance + wallFallImpactDownOvershoot)
            });
        }

        return true;
    }

    IEnumerator AnimateWalls(bool isRising)
    {
        if (spawnedWalls.Count == 0)
            yield break;

        float duration = Mathf.Max(0.01f, isRising ? wallRiseDuration : wallDropDuration);
        float stagger = isRising ? wallRiseStagger : wallDropStagger;
        AnimationCurve curve = isRising ? wallRiseCurve : wallDropCurve;

        Vector3[] from = new Vector3[spawnedWalls.Count];
        Vector3[] to = new Vector3[spawnedWalls.Count];

        for (int i = 0; i < spawnedWalls.Count; i++)
        {
            SpawnedWall wall = spawnedWalls[i];
            if (wall == null || wall.transform == null)
                continue;

            from[i] = isRising ? wall.bottomPosition : wall.topPosition;
            to[i] = isRising ? wall.topPosition : wall.impactPosition;
            wall.transform.position = from[i];
            wall.transform.rotation = wall.topRotation;
        }

        float elapsed = 0f;
        float totalDuration = duration + stagger * Mathf.Max(0, spawnedWalls.Count - 1);
        while (elapsed < totalDuration)
        {
            for (int i = 0; i < spawnedWalls.Count; i++)
            {
                SpawnedWall wall = spawnedWalls[i];
                if (wall == null || wall.transform == null)
                    continue;

                float wallTime = Mathf.Clamp01((elapsed - (i * stagger)) / duration);
                float eased = curve != null ? curve.Evaluate(wallTime) : wallTime;
                wall.transform.position = Vector3.LerpUnclamped(from[i], to[i], eased);
                if (!isRising)
                    wall.transform.rotation = Quaternion.SlerpUnclamped(wall.topRotation, wall.impactRotation, eased);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < spawnedWalls.Count; i++)
        {
            SpawnedWall wall = spawnedWalls[i];
            if (wall != null && wall.transform != null)
            {
                wall.transform.position = to[i];
                if (!isRising)
                    wall.transform.rotation = wall.impactRotation;
            }
        }

        if (!isRising)
        {
            yield return AnimateWallFallSettle();
            if (wallFallLingerDuration > 0f)
                yield return new WaitForSeconds(wallFallLingerDuration);
        }
    }

    IEnumerator AnimateWallFallSettle()
    {
        float duration = Mathf.Max(0f, wallFallSettleDuration);
        if (duration <= 0.001f)
        {
            SetWallsToSettledFallPose();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            for (int i = 0; i < spawnedWalls.Count; i++)
            {
                SpawnedWall wall = spawnedWalls[i];
                if (wall == null || wall.transform == null)
                    continue;

                wall.transform.position = Vector3.LerpUnclamped(wall.impactPosition, wall.fallenPosition, eased);
                wall.transform.rotation = Quaternion.SlerpUnclamped(wall.impactRotation, wall.fallenRotation, eased);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        SetWallsToSettledFallPose();
    }

    void SetWallsToSettledFallPose()
    {
        for (int i = 0; i < spawnedWalls.Count; i++)
        {
            SpawnedWall wall = spawnedWalls[i];
            if (wall == null || wall.transform == null)
                continue;

            wall.transform.position = wall.fallenPosition;
            wall.transform.rotation = wall.fallenRotation;
        }
    }

    int CalculateWallCount(float radius)
    {
        float circumference = 2f * Mathf.PI * radius;
        float wallWidth = EstimateWallWidth();
        float targetSpacing = Mathf.Max(0.1f, wallWidth * (1f - allowedWidthOverlap));
        int countFromSpacing = Mathf.Max(1, Mathf.RoundToInt(circumference / targetSpacing));
        return Mathf.Clamp(countFromSpacing, minWallCount, maxWallCount);
    }

    float EstimateWallWidth()
    {
        if (wallPrefab == null)
            return fallbackWallWidth;

        float widest = 0f;

        Collider[] colliders = wallPrefab.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Vector3 size = colliders[i].bounds.size;
            float horizontalSize = Mathf.Max(size.x, size.z);
            if (horizontalSize > widest)
                widest = horizontalSize;
        }

        if (widest > 0f)
            return widest;

        Renderer[] renderers = wallPrefab.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Vector3 size = renderers[i].bounds.size;
            float horizontalSize = Mathf.Max(size.x, size.z);
            if (horizontalSize > widest)
                widest = horizontalSize;
        }

        return widest > 0f ? widest : fallbackWallWidth;
    }

    public void OnArenaCleared()
    {
        if (isShuttingDown)
            return;

        NotifyArenaEnded();
        ProjectileCleanup.ClearAllProjectiles();
        StartCoroutine(ShutdownRoutine());
    }

    public void OnArenaEnemyCountChanged(int remainingEnemies)
    {
        ArenaEnemyCountChanged?.Invoke(Mathf.Max(0, remainingEnemies));
    }

    IEnumerator ShutdownRoutine()
    {
        isShuttingDown = true;

        if (activeSpawner != null)
        {
            Destroy(activeSpawner.gameObject);
            activeSpawner = null;
        }

        yield return AnimateWalls(isRising: false);
        AddFallenWallsToCompletionSinkObjects();
        yield return AnimateCompletionSinkObjects();

        for (int i = 0; i < spawnedWalls.Count; i++)
        {
            SpawnedWall wall = spawnedWalls[i];
            if (wall != null && wall.transform != null)
                Destroy(wall.transform.gameObject);
        }

        spawnedWalls.Clear();
        MarkArenaInactive();
        Destroy(gameObject);
    }

    public void ResetArenaForPlayerDeath()
    {
        if (!hasTriggered && !isShuttingDown)
            return;

        StopAllCoroutines();
        isShuttingDown = false;
        NotifyArenaEnded();

        if (activeSpawner != null)
        {
            activeSpawner.StopSpawningForArenaReset();
            Destroy(activeSpawner.gameObject);
            activeSpawner = null;
        }

        for (int i = 0; i < spawnedWalls.Count; i++)
        {
            SpawnedWall wall = spawnedWalls[i];
            if (wall != null && wall.transform != null)
                Destroy(wall.transform.gameObject);
        }

        spawnedWalls.Clear();
        RestoreCompletionSinkObjects();
        completionSinkObjects.Clear();
        hasTriggered = false;
        cachedPlayer = null;
        MarkArenaInactive();

        if (triggerCollider != null)
            triggerCollider.enabled = true;
    }

    void CaptureCompletionSinkObjects(Transform root)
    {
        completionSinkObjects.Clear();
        if (!sinkSpawnerHousesOnComplete || root == null)
            return;

        completionSinkObjects.Add(new CompletionSinkObject
        {
            transform = root,
            startPosition = root.position,
            endPosition = root.position + Vector3.down * houseSinkDepth,
            startRotation = root.rotation
        });
    }

    void AddFallenWallsToCompletionSinkObjects()
    {
        if (!sinkSpawnerHousesOnComplete)
            return;

        for (int i = 0; i < spawnedWalls.Count; i++)
        {
            SpawnedWall wall = spawnedWalls[i];
            if (wall == null || wall.transform == null || HasCompletionSinkObject(wall.transform))
                continue;

            completionSinkObjects.Add(new CompletionSinkObject
            {
                transform = wall.transform,
                startPosition = wall.transform.position,
                endPosition = wall.transform.position + Vector3.down * houseSinkDepth,
                startRotation = wall.transform.rotation
            });
        }
    }

    bool HasCompletionSinkObject(Transform target)
    {
        for (int i = 0; i < completionSinkObjects.Count; i++)
        {
            CompletionSinkObject sinkObject = completionSinkObjects[i];
            if (sinkObject != null && sinkObject.transform == target)
                return true;
        }

        return false;
    }

    IEnumerator AnimateCompletionSinkObjects()
    {
        if (completionSinkObjects.Count == 0)
            yield break;

        float duration = Mathf.Max(0.01f, houseSinkDuration);
        float totalDuration = duration + houseSinkStagger * Mathf.Max(0, completionSinkObjects.Count - 1);
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            for (int i = 0; i < completionSinkObjects.Count; i++)
            {
                CompletionSinkObject sinkObject = completionSinkObjects[i];
                if (sinkObject == null || sinkObject.transform == null)
                    continue;

                float objectTime = Mathf.Clamp01((elapsed - (i * houseSinkStagger)) / duration);
                float eased = houseSinkCurve != null ? houseSinkCurve.Evaluate(objectTime) : objectTime;
                sinkObject.transform.position = Vector3.LerpUnclamped(sinkObject.startPosition, sinkObject.endPosition, eased);
                sinkObject.transform.rotation = sinkObject.startRotation;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < completionSinkObjects.Count; i++)
        {
            CompletionSinkObject sinkObject = completionSinkObjects[i];
            if (sinkObject != null && sinkObject.transform != null)
                sinkObject.transform.position = sinkObject.endPosition;
        }
    }

    void RestoreCompletionSinkObjects()
    {
        for (int i = 0; i < completionSinkObjects.Count; i++)
        {
            CompletionSinkObject sinkObject = completionSinkObjects[i];
            if (sinkObject == null || sinkObject.transform == null)
                continue;

            sinkObject.transform.position = sinkObject.startPosition;
            sinkObject.transform.rotation = sinkObject.startRotation;
        }
    }

    void OnDestroy()
    {
        if (countedAsActiveArena || isShuttingDown)
            NotifyArenaEnded();
        MarkArenaInactive();
    }

    void NotifyArenaStarted(int remainingEnemies)
    {
        int count = Mathf.Max(0, remainingEnemies);
        arenaProgressVisible = true;
        ArenaStarted?.Invoke(count);
        ArenaEnemyCountChanged?.Invoke(count);
    }

    void NotifyArenaEnded()
    {
        if (!arenaProgressVisible)
            return;

        arenaProgressVisible = false;
        ArenaEnded?.Invoke();
    }

    void MarkArenaActive()
    {
        if (countedAsActiveArena)
            return;

        countedAsActiveArena = true;
        if (!activePlayerArenas.Contains(this))
            activePlayerArenas.Add(this);
    }

    void MarkArenaInactive()
    {
        if (!countedAsActiveArena)
            return;

        countedAsActiveArena = false;
        activePlayerArenas.Remove(this);
    }

    void OnValidate()
    {
        maxWallCount = Mathf.Max(maxWallCount, minWallCount);
        fallbackWallWidth = Mathf.Max(0.5f, fallbackWallWidth);
        wallRiseDuration = Mathf.Max(0.05f, wallRiseDuration);
        wallDropDuration = Mathf.Max(0.05f, wallDropDuration);
        wallFallOutwardDistance = Mathf.Max(0f, wallFallOutwardDistance);
        wallFallDownDistance = Mathf.Max(0f, wallFallDownDistance);
        wallFallAngle = Mathf.Max(0f, wallFallAngle);
        wallFallImpactOutwardOvershoot = Mathf.Max(0f, wallFallImpactOutwardOvershoot);
        wallFallImpactDownOvershoot = Mathf.Max(0f, wallFallImpactDownOvershoot);
        wallFallImpactAngleOvershoot = Mathf.Max(0f, wallFallImpactAngleOvershoot);
        wallFallSettleDuration = Mathf.Max(0f, wallFallSettleDuration);
        wallFallLingerDuration = Mathf.Max(0f, wallFallLingerDuration);
        houseSinkDuration = Mathf.Max(0.05f, houseSinkDuration);
        houseSinkDepth = Mathf.Max(0f, houseSinkDepth);
        houseSinkStagger = Mathf.Max(0f, houseSinkStagger);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, arenaRadius);
    }
}
