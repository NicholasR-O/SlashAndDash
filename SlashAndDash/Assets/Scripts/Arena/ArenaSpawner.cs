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
    [SerializeField] string playerTag = "Player";
    [SerializeField] Transform spawnedEnemyParent;

    readonly List<RuntimeSpawnType> runtimeTypes = new List<RuntimeSpawnType>();
    Coroutine spawnRoutine;
    ArenaTrigger owningTrigger;
    Transform playerTransform;
    Vector3 arenaCenter;
    float arenaRadius;

    public void BeginSpawning(ArenaTrigger trigger, Vector3 center, float radius, Transform player)
    {
        if (spawnRoutine != null)
            return;

        owningTrigger = trigger;
        arenaCenter = center;
        arenaRadius = Mathf.Max(1f, radius);
        playerTransform = player != null ? player : FindPlayerTransform();

        BuildRuntimeTypes();
        spawnRoutine = StartCoroutine(SpawnLoop());
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

        NotifyArenaClearedAndCleanup();
    }

    void TrySpawnEnemy(RuntimeSpawnType spawnType)
    {
        if (!TryGetSpawnPosition(out Vector3 spawnPosition))
            return;

        Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        GameObject enemy = Instantiate(spawnType.config.EnemyPrefab, spawnPosition, rotation, spawnedEnemyParent);

        ArenaSpawnedEnemyMarker marker = enemy.GetComponent<ArenaSpawnedEnemyMarker>();
        if (marker == null)
            marker = enemy.AddComponent<ArenaSpawnedEnemyMarker>();

        marker.Initialize(this, spawnType.id);

        spawnType.remainingToSpawn--;
        spawnType.aliveCount++;
    }

    bool TryGetSpawnPosition(out Vector3 spawnPosition)
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
                spawnPosition = navMeshHit.position;
                return true;
            }
        }

        spawnPosition = Vector3.zero;
        return false;
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
            owningTrigger.OnArenaCleared();

        spawnRoutine = null;
        Destroy(gameObject);
    }

    public void NotifySpawnedEnemyDestroyed(int typeId)
    {
        for (int i = 0; i < runtimeTypes.Count; i++)
        {
            RuntimeSpawnType spawnType = runtimeTypes[i];
            if (spawnType.id != typeId)
                continue;

            spawnType.aliveCount = Mathf.Max(0, spawnType.aliveCount - 1);
            return;
        }
    }

    void OnValidate()
    {
        spawnInterval = Mathf.Max(0.05f, spawnInterval);
        maxSpawnAttempts = Mathf.Max(1, maxSpawnAttempts);
        navMeshSampleDistance = Mathf.Max(0.5f, navMeshSampleDistance);
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

    void OnDestroy()
    {
        if (hasNotified || owner == null)
            return;

        hasNotified = true;
        owner.NotifySpawnedEnemyDestroyed(enemyTypeId);
    }
}
