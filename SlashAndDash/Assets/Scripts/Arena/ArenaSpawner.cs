using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[AddComponentMenu("Arena/Arena Spawner")]
public class ArenaSpawner : MonoBehaviour
{
    [System.Serializable]
    public class EnemySpawnType
    {
        [SerializeField] GameObject enemyPrefab;
        [SerializeField, Min(0)] int totalToSpawn = 6;
        [SerializeField, Min(1)] int maxAliveAtOnce = 3;

        public GameObject EnemyPrefab => enemyPrefab;
        public int TotalToSpawn => totalToSpawn;
        public int MaxAliveAtOnce => maxAliveAtOnce;
    }

    sealed class RuntimeSpawnType
    {
        public EnemySpawnType config;
        public int remainingToSpawn;
        public int aliveCount;
        public int id;
    }

    [Header("Spawn Types")]
    [SerializeField] List<EnemySpawnType> enemyTypes = new List<EnemySpawnType>();

    [Header("Spawn Timing")]
    [SerializeField, Min(0.05f)] float spawnInterval = 0.5f;

    [Header("Spawn Placement")]
    [SerializeField, Min(0f)] float minDistanceFromPlayer = 8f;
    [SerializeField, Min(1)] int maxSpawnAttempts = 20;
    [SerializeField, Min(0.5f)] float navMeshSampleDistance = 4f;
    [SerializeField, Min(0f)] float spawnClearancePadding = 0.2f;
    [SerializeField, Min(0.1f)] float fallbackSpawnClearanceRadius = 0.75f;
    [SerializeField, Min(0.2f)] float fallbackSpawnClearanceHeight = 2f;
    [SerializeField] LayerMask spawnBlockerMask = ~0;
    [SerializeField, Min(0f)] float occupiedFootprintPadding = 0.75f;
    [SerializeField, Min(0f)] float occupiedFootprintVerticalPadding = 0.5f;
    [SerializeField, Min(0f)] float blockerCacheExtraRadius = 16f;
    [SerializeField] string playerTag = "Player";
    [SerializeField] Transform spawnedEnemyParent;

    [Header("Throwable Vases")]
    [SerializeField] GameObject throwableVasePrefab;
    [SerializeField, Min(0)] int minThrowableVasesAtOnce = 4;
    [SerializeField, Min(0)] int maxThrowableVasesAtOnce = 5;

    readonly List<RuntimeSpawnType> runtimeTypes = new List<RuntimeSpawnType>();
    readonly List<ArenaSpawnedEnemyMarker> spawnedEnemies = new List<ArenaSpawnedEnemyMarker>();
    readonly List<ThrowableVase> spawnedThrowableVases = new List<ThrowableVase>();
    readonly List<Collider> cachedSpawnBlockers = new List<Collider>();
    Coroutine spawnRoutine;
    ArenaTrigger owningTrigger;
    Transform playerTransform;
    Vector3 arenaCenter;
    float arenaRadius;
    int targetThrowableVaseCount;
    bool suppressThrowableVaseMaintenance;
    int groundLayer = -1;

    public int RemainingEnemyCount
    {
        get
        {
            int remaining = 0;
            for (int i = 0; i < runtimeTypes.Count; i++)
            {
                RuntimeSpawnType spawnType = runtimeTypes[i];
                if (spawnType == null)
                    continue;

                remaining += Mathf.Max(0, spawnType.remainingToSpawn);
                remaining += Mathf.Max(0, spawnType.aliveCount);
            }

            return remaining;
        }
    }

    public void BeginSpawning(ArenaTrigger trigger, Vector3 center, float radius, Transform player)
    {
        if (spawnRoutine != null)
            return;

        owningTrigger = trigger;
        arenaCenter = center;
        arenaRadius = Mathf.Max(1f, radius);
        playerTransform = player != null ? player : FindPlayerTransform();
        suppressThrowableVaseMaintenance = false;

        BuildRuntimeTypes();
        groundLayer = LayerMask.NameToLayer("Ground");
        CacheSpawnBlockers();
        RefreshArenaProgress();
        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    public void StopSpawningForArenaReset()
    {
        suppressThrowableVaseMaintenance = true;
        owningTrigger = null;
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            ArenaSpawnedEnemyMarker marker = spawnedEnemies[i];
            if (marker == null)
                continue;

            marker.DetachFromSpawner();
            Destroy(marker.gameObject);
        }

        spawnedEnemies.Clear();
        cachedSpawnBlockers.Clear();
        DestroySpawnedThrowableVases();
        runtimeTypes.Clear();
        RefreshArenaProgress();
    }

    void BuildRuntimeTypes()
    {
        runtimeTypes.Clear();

        for (int i = 0; i < enemyTypes.Count; i++)
        {
            EnemySpawnType entry = enemyTypes[i];
            if (entry == null || entry.EnemyPrefab == null || entry.TotalToSpawn <= 0)
                continue;

            runtimeTypes.Add(new RuntimeSpawnType
            {
                config = entry,
                remainingToSpawn = entry.TotalToSpawn,
                aliveCount = 0,
                id = i
            });
        }
    }

    IEnumerator SpawnLoop()
    {
        if (runtimeTypes.Count == 0)
        {
            NotifyArenaClearedAndCleanup();
            yield break;
        }

        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.05f, spawnInterval));

        while (!AllSpawnsComplete())
        {
            MaintainThrowableVases();

            for (int i = 0; i < runtimeTypes.Count; i++)
            {
                RuntimeSpawnType spawnType = runtimeTypes[i];
                if (spawnType.remainingToSpawn <= 0)
                    continue;

                if (spawnType.aliveCount >= spawnType.config.MaxAliveAtOnce)
                    continue;

                TrySpawnEnemy(spawnType);
            }

            yield return wait;
        }

        DestroySpawnedThrowableVases();
        NotifyArenaClearedAndCleanup();
    }

    void TrySpawnEnemy(RuntimeSpawnType spawnType)
    {
        if (!TryGetSpawnPosition(spawnType.config.EnemyPrefab, out Vector3 spawnPosition))
            return;

        Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        GameObject enemy = Instantiate(spawnType.config.EnemyPrefab, spawnPosition, rotation, spawnedEnemyParent);

        ArenaSpawnedEnemyMarker marker = enemy.GetComponent<ArenaSpawnedEnemyMarker>();
        if (marker == null)
            marker = enemy.AddComponent<ArenaSpawnedEnemyMarker>();

        marker.Initialize(this, spawnType.id);
        spawnedEnemies.Add(marker);

        spawnType.remainingToSpawn--;
        spawnType.aliveCount++;
        RefreshArenaProgress();
        MaintainThrowableVases();
    }

    bool TryGetSpawnPosition(GameObject enemyPrefab, out Vector3 spawnPosition)
    {
        if (playerTransform == null)
            playerTransform = FindPlayerTransform();

        Vector3 playerFlat = playerTransform != null
            ? new Vector3(playerTransform.position.x, 0f, playerTransform.position.z)
            : Vector3.zero;

        for (int attempt = 0; attempt < Mathf.Max(1, maxSpawnAttempts); attempt++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * arenaRadius;
            Vector3 candidate = arenaCenter + new Vector3(randomOffset.x, 0f, randomOffset.y);

            if (playerTransform != null)
            {
                Vector3 candidateFlat = new Vector3(candidate.x, 0f, candidate.z);
                if (Vector3.Distance(candidateFlat, playerFlat) < minDistanceFromPlayer)
                    continue;
            }

            if (NavMesh.SamplePosition(candidate, out NavMeshHit navMeshHit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                if (IsSpawnClear(navMeshHit.position, enemyPrefab))
                {
                    spawnPosition = navMeshHit.position;
                    return true;
                }
            }
        }

        spawnPosition = Vector3.zero;
        return false;
    }

    bool IsSpawnClear(Vector3 position, GameObject enemyPrefab)
    {
        GetSpawnClearance(enemyPrefab, out float radius, out float height);
        if (IsBlockedByCachedFootprint(position, radius, height))
            return false;

        float bottomOffset = radius + 0.05f;
        Vector3 bottom = position + Vector3.up * bottomOffset;
        Vector3 top = position + Vector3.up * Mathf.Max(bottomOffset, height - radius + 0.05f);

        Collider[] hits = Physics.OverlapCapsule(bottom, top, radius, spawnBlockerMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (ShouldIgnoreSpawnBlocker(hit))
                continue;

            return false;
        }

        return true;
    }

    void CacheSpawnBlockers()
    {
        cachedSpawnBlockers.Clear();

        float searchRadius = Mathf.Max(0f, arenaRadius) + Mathf.Max(0f, blockerCacheExtraRadius);
        Collider[] hits = Physics.OverlapSphere(arenaCenter, searchRadius, spawnBlockerMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (ShouldIgnoreSpawnBlocker(hit))
                continue;

            cachedSpawnBlockers.Add(hit);
        }
    }

    bool IsBlockedByCachedFootprint(Vector3 position, float radius, float height)
    {
        for (int i = cachedSpawnBlockers.Count - 1; i >= 0; i--)
        {
            Collider blocker = cachedSpawnBlockers[i];
            if (ShouldIgnoreSpawnBlocker(blocker))
            {
                cachedSpawnBlockers.RemoveAt(i);
                continue;
            }

            if (BoundsBlocksSpawn(position, radius, height, blocker.bounds))
                return true;
        }

        return false;
    }

    bool BoundsBlocksSpawn(Vector3 position, float radius, float height, Bounds bounds)
    {
        float horizontalPadding = radius + Mathf.Max(0f, occupiedFootprintPadding);
        float verticalPadding = Mathf.Max(0f, occupiedFootprintVerticalPadding);
        float minY = position.y - verticalPadding;
        float maxY = position.y + Mathf.Max(height, radius * 2f) + verticalPadding;

        if (bounds.max.y < minY || bounds.min.y > maxY)
            return false;

        if (position.x < bounds.min.x - horizontalPadding || position.x > bounds.max.x + horizontalPadding)
            return false;

        if (position.z < bounds.min.z - horizontalPadding || position.z > bounds.max.z + horizontalPadding)
            return false;

        return true;
    }

    bool ShouldIgnoreSpawnBlocker(Collider hit)
    {
        if (hit == null || !hit.enabled || hit.isTrigger)
            return true;

        if (hit is TerrainCollider)
            return true;

        if (groundLayer >= 0 && hit.gameObject.layer == groundLayer)
            return true;

        if (playerTransform != null)
        {
            Transform hitTransform = hit.transform;
            if (hitTransform != null && hitTransform.IsChildOf(playerTransform))
                return true;

            if (hit.attachedRigidbody != null && hit.attachedRigidbody.transform.IsChildOf(playerTransform))
                return true;
        }

        return false;
    }

    void GetSpawnClearance(GameObject enemyPrefab, out float radius, out float height)
    {
        radius = fallbackSpawnClearanceRadius;
        height = fallbackSpawnClearanceHeight;

        if (enemyPrefab == null)
        {
            ApplySpawnClearancePadding(ref radius, ref height);
            return;
        }

        NavMeshAgent prefabAgent = enemyPrefab.GetComponentInChildren<NavMeshAgent>(true);
        if (prefabAgent != null)
        {
            radius = Mathf.Max(0.1f, prefabAgent.radius);
            height = Mathf.Max(radius * 2f, prefabAgent.height);
        }

        ApplySpawnClearancePadding(ref radius, ref height);
    }

    void ApplySpawnClearancePadding(ref float radius, ref float height)
    {
        float padding = Mathf.Max(0f, spawnClearancePadding);
        radius = Mathf.Max(0.1f, radius + padding);
        height = Mathf.Max(radius * 2f, height + padding);
    }

    Transform FindPlayerTransform()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        return player != null ? player.transform : null;
    }

    bool AllSpawnsComplete()
    {
        for (int i = 0; i < runtimeTypes.Count; i++)
        {
            RuntimeSpawnType spawnType = runtimeTypes[i];
            if (spawnType.remainingToSpawn > 0 || spawnType.aliveCount > 0)
                return false;
        }

        return true;
    }

    void NotifyArenaClearedAndCleanup()
    {
        if (owningTrigger != null)
        {
            owningTrigger.OnArenaCleared();
        }
        else
        {
            Destroy(gameObject);
        }

        spawnRoutine = null;
    }

    public void NotifySpawnedEnemyDestroyed(int typeId)
    {
        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            if (spawnedEnemies[i] == null)
                spawnedEnemies.RemoveAt(i);
        }

        for (int i = 0; i < runtimeTypes.Count; i++)
        {
            RuntimeSpawnType spawnType = runtimeTypes[i];
            if (spawnType.id != typeId)
                continue;

            spawnType.aliveCount = Mathf.Max(0, spawnType.aliveCount - 1);
            RefreshArenaProgress();
            return;
        }

        RefreshArenaProgress();
    }

    void MaintainThrowableVases()
    {
        if (!CanMaintainThrowableVases())
            return;

        CleanupDestroyedThrowableVases();

        if (!ShouldSpawnThrowableVases())
        {
            targetThrowableVaseCount = 0;
            DestroySpawnedThrowableVases();
            return;
        }

        int maxCount = Mathf.Max(0, maxThrowableVasesAtOnce);
        int minCount = Mathf.Clamp(minThrowableVasesAtOnce, 0, maxCount);
        if (maxCount <= 0)
            return;

        if (targetThrowableVaseCount < minCount || targetThrowableVaseCount > maxCount)
            targetThrowableVaseCount = Random.Range(minCount, maxCount + 1);

        while (spawnedThrowableVases.Count < targetThrowableVaseCount)
        {
            if (!TrySpawnThrowableVase())
                break;
        }
    }

    bool ShouldSpawnThrowableVases()
    {
        if (throwableVasePrefab == null)
            return false;

        bool hasRemainingEnemy = false;

        for (int i = 0; i < spawnedEnemies.Count; i++)
        {
            ArenaSpawnedEnemyMarker marker = spawnedEnemies[i];
            if (marker == null)
                continue;

            Enemy enemy = marker.GetComponent<Enemy>();
            if (enemy == null || !enemy.IsAlive)
                continue;

            hasRemainingEnemy = true;
            if (enemy.CanBeGrappled)
                return false;
        }

        for (int i = 0; i < runtimeTypes.Count; i++)
        {
            RuntimeSpawnType spawnType = runtimeTypes[i];
            if (spawnType == null || spawnType.remainingToSpawn <= 0)
                continue;

            hasRemainingEnemy = true;
            if (PrefabCanBeGrappled(spawnType.config.EnemyPrefab))
                return false;
        }

        return hasRemainingEnemy;
    }

    bool PrefabCanBeGrappled(GameObject enemyPrefab)
    {
        if (enemyPrefab == null)
            return false;

        Enemy enemy = enemyPrefab.GetComponent<Enemy>();
        return enemy != null && enemy.CanBeGrappled;
    }

    bool TrySpawnThrowableVase()
    {
        if (!CanMaintainThrowableVases() || throwableVasePrefab == null)
            return false;

        if (!TryGetSpawnPosition(throwableVasePrefab, out Vector3 spawnPosition))
            return false;

        Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        GameObject vaseObject = Instantiate(throwableVasePrefab, spawnPosition, rotation, spawnedEnemyParent);
        ThrowableVase vase = vaseObject.GetComponent<ThrowableVase>();
        if (vase == null)
        {
            Destroy(vaseObject);
            return false;
        }

        spawnedThrowableVases.Add(vase);
        return true;
    }

    bool CanMaintainThrowableVases()
    {
        return !suppressThrowableVaseMaintenance && isActiveAndEnabled;
    }

    void DestroySpawnedThrowableVases()
    {
        for (int i = spawnedThrowableVases.Count - 1; i >= 0; i--)
        {
            ThrowableVase vase = spawnedThrowableVases[i];
            if (vase != null)
                Destroy(vase.gameObject);
        }

        spawnedThrowableVases.Clear();
    }

    void CleanupDestroyedThrowableVases()
    {
        for (int i = spawnedThrowableVases.Count - 1; i >= 0; i--)
        {
            if (spawnedThrowableVases[i] == null)
                spawnedThrowableVases.RemoveAt(i);
        }
    }

    void RefreshArenaProgress()
    {
        CleanupDestroyedEnemyMarkers();
        int remaining = RemainingEnemyCount;
        if (owningTrigger != null)
            owningTrigger.OnArenaEnemyCountChanged(remaining);
    }

    void CleanupDestroyedEnemyMarkers()
    {
        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            if (spawnedEnemies[i] == null)
                spawnedEnemies.RemoveAt(i);
        }
    }

    void OnValidate()
    {
        spawnInterval = Mathf.Max(0.05f, spawnInterval);
        maxSpawnAttempts = Mathf.Max(1, maxSpawnAttempts);
        navMeshSampleDistance = Mathf.Max(0.5f, navMeshSampleDistance);
        spawnClearancePadding = Mathf.Max(0f, spawnClearancePadding);
        fallbackSpawnClearanceRadius = Mathf.Max(0.1f, fallbackSpawnClearanceRadius);
        fallbackSpawnClearanceHeight = Mathf.Max(0.2f, fallbackSpawnClearanceHeight);
        occupiedFootprintPadding = Mathf.Max(0f, occupiedFootprintPadding);
        occupiedFootprintVerticalPadding = Mathf.Max(0f, occupiedFootprintVerticalPadding);
        blockerCacheExtraRadius = Mathf.Max(0f, blockerCacheExtraRadius);
        maxThrowableVasesAtOnce = Mathf.Max(0, maxThrowableVasesAtOnce);
        minThrowableVasesAtOnce = Mathf.Clamp(minThrowableVasesAtOnce, 0, maxThrowableVasesAtOnce);
    }

    void OnDisable()
    {
        suppressThrowableVaseMaintenance = true;
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    void OnDestroy()
    {
        suppressThrowableVaseMaintenance = true;
    }
}

public class ArenaSpawnedEnemyMarker : MonoBehaviour
{
    ArenaSpawner owner;
    int enemyTypeId;
    bool hasNotified;

    public void Initialize(ArenaSpawner spawner, int typeId)
    {
        owner = spawner;
        enemyTypeId = typeId;
        hasNotified = false;
    }

    public void DetachFromSpawner()
    {
        owner = null;
        hasNotified = true;
    }

    void OnDestroy()
    {
        if (hasNotified || owner == null)
            return;

        hasNotified = true;
        owner.NotifySpawnedEnemyDestroyed(enemyTypeId);
    }
}
