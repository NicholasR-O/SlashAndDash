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
    [SerializeField, Min(0.05f)] float wallDropDuration = 0.3f;
    [SerializeField, Min(0f)] float wallDropStagger = 0.005f;
    [SerializeField] AnimationCurve wallRiseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] AnimationCurve wallDropCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Enemy Spawner")]
    [SerializeField] ArenaSpawner arenaSpawnerPrefab;
    [SerializeField] Transform spawnedSpawnerParent;

    readonly List<SpawnedWall> spawnedWalls = new List<SpawnedWall>();

    bool hasTriggered;
    bool isShuttingDown;
    Transform cachedPlayer;
    ArenaSpawner activeSpawner;
    Collider triggerCollider;

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

        cachedPlayer = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform;
        hasTriggered = true;

        if (triggerCollider != null)
            triggerCollider.enabled = false;

        StartCoroutine(RunArenaRoutine());
    }

    IEnumerator RunArenaRoutine()
    {
        if (!TrySpawnWalls())
        {
            Debug.LogWarning("ArenaTrigger failed to spawn walls due to missing wall prefab.", this);
            yield break;
        }

        yield return AnimateWalls(isRising: true);

        if (arenaSpawnerPrefab == null)
        {
            Debug.LogWarning("ArenaTrigger has no ArenaSpawner prefab assigned.", this);
            yield break;
        }

        activeSpawner = Instantiate(arenaSpawnerPrefab, transform.position, Quaternion.identity, spawnedSpawnerParent);
        activeSpawner.BeginSpawning(this, transform.position, arenaRadius, cachedPlayer);
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

            Transform wall = Instantiate(wallPrefab, startPosition, wallRotation, spawnedWallParent).transform;
            spawnedWalls.Add(new SpawnedWall
            {
                transform = wall,
                topPosition = topPosition,
                bottomPosition = startPosition
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
            to[i] = isRising ? wall.topPosition : wall.bottomPosition;
            wall.transform.position = from[i];
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
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < spawnedWalls.Count; i++)
        {
            SpawnedWall wall = spawnedWalls[i];
            if (wall != null && wall.transform != null)
                wall.transform.position = to[i];
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

        StartCoroutine(ShutdownRoutine());
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

        for (int i = 0; i < spawnedWalls.Count; i++)
        {
            SpawnedWall wall = spawnedWalls[i];
            if (wall != null && wall.transform != null)
                Destroy(wall.transform.gameObject);
        }

        spawnedWalls.Clear();
        Destroy(gameObject);
    }

    void OnValidate()
    {
        maxWallCount = Mathf.Max(maxWallCount, minWallCount);
        fallbackWallWidth = Mathf.Max(0.5f, fallbackWallWidth);
        wallRiseDuration = Mathf.Max(0.05f, wallRiseDuration);
        wallDropDuration = Mathf.Max(0.05f, wallDropDuration);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, arenaRadius);
    }
}
