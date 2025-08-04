using UnityEngine;
using UnityEngine.Pool;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System;

/// <summary>
/// 적의 스폰 및 드롭 아이템을 관리하는 매니저 클래스
/// Unity 오브젝트 풀을 적극 활용하여 성능 최적화
/// GameManager와 연동하여 웨이브 시스템 지원
/// </summary>
public class SpawnManager : MonoBehaviour
{
    #region 열거형 및 상수

    /// <summary>
    /// 스폰 상태를 정의하는 열거형
    /// </summary>
    public enum SpawnState
    {
        Waiting,    // 대기 중
        Spawning,   // 스폰 중
        Complete    // 완료
    }

    // 성능 최적화를 위한 상수
    private const int DEFAULT_POOL_SIZE = 20;
    private const int MAX_POOL_SIZE = 50;
    private const float SPAWN_CHECK_INTERVAL = 0.1f;

    #endregion

    #region 필드 및 속성

    [Header("스폰 설정")]
    [SerializeField] private Vector3 spawnAreaMin = new Vector3(-5, 0, -5);
    [SerializeField] private Vector3 spawnAreaMax = new Vector3(5, 0, 5);
    [SerializeField] private Transform enemyParent; // 적들의 부모 오브젝트
    
    [Header("오브젝트 풀 설정")]
    [SerializeField] private List<EnemyPoolData> enemyPoolData = new List<EnemyPoolData>();
    [SerializeField] private int defaultPoolSize = DEFAULT_POOL_SIZE;
    [SerializeField] private int maxPoolSize = MAX_POOL_SIZE;
    
    [Header("드롭 아이템 설정")]
    [SerializeField] private List<GameObject> dropItemPrefabs = new List<GameObject>();
    [SerializeField] private float dropChance = 0.3f; // 30% 확률
    [SerializeField] private Vector2 dropForceRange = new Vector2(2f, 5f);
    
    [Header("성능 설정")]
    [SerializeField] private float spawnCheckInterval = SPAWN_CHECK_INTERVAL;
    [SerializeField] private int maxEnemiesPerFrame = 3; // 한 프레임당 최대 스폰 수
    
    // 스폰 상태
    public SpawnState currentState { get; private set; }
    private bool isSpawning;
    private int _activeEnqueueTasks = 0;
    
    // 웨이브 진행 상태 추적
    private bool _allEnemiesInQueue;
    private bool _allEnemiesSpawned;
    
    // 오브젝트 풀
    private Dictionary<string, ObjectPool<GameObject>> enemyPools = new Dictionary<string, ObjectPool<GameObject>>();
    private ObjectPool<GameObject> dropItemPool;
    
    // 현재 스폰된 적들 추적
    private List<GameObject> activeEnemies = new List<GameObject>();
    private Queue<SpawnRequest> spawnQueue = new Queue<SpawnRequest>();
    
    // UniTask 관련
    private CancellationTokenSource cancellationTokenSource;
    
    // 이벤트
    public static event Action<GameObject> OnEnemySpawned;
    public static event Action<GameObject> OnEnemyDestroyed;
    public static event Action<GameObject, Vector3> OnItemDropped;
    public static event Action OnAllEnemiesSpawned; // 모든 적이 '필드에 생성'되었을 때 호출
    public static event Action OnAllEnemiesCleared;  // 모든 적이 '처치'되었을 때 호출
    
    // GameManager와의 연동을 위한 이벤트
    public static event Action<int, int> OnEnemyKilledUpdate; // (처치된 적 수, 전체 적 수)

    // 공개 속성
    public int ActiveEnemyCount => activeEnemies.Count;
    public bool IsSpawning => isSpawning;
    public int QueuedSpawnCount => spawnQueue.Count;

    #endregion

    #region 유니티 생명주기

    void Awake()
    {
        ValidateComponents();
    }

    async void Start()
    {
        cancellationTokenSource = new CancellationTokenSource();
        await InitializeAsync(cancellationTokenSource.Token);
    }

    void OnDestroy()
    {
        CleanupResources();
    }

    #endregion

    #region 초기화

    private void ValidateComponents()
    {
        if (enemyParent == null)
        {
            GameObject parent = new GameObject("Enemies");
            enemyParent = parent.transform;
            enemyParent.SetParent(transform);
        }
    }

    private async UniTask InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            currentState = SpawnState.Waiting;
            
            // 오브젝트 풀 초기화
            await InitializeEnemyPoolsAsync(cancellationToken);
            await InitializeDropItemPoolAsync(cancellationToken);
            
            // 스폰 처리 루프 시작
            StartSpawnProcessingAsync(cancellationTokenSource.Token).Forget();
            
            Debug.Log("[SpawnManager] 초기화 완료");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SpawnManager] 초기화 실패: {ex.Message}");
        }
    }

    private async UniTask InitializeEnemyPoolsAsync(CancellationToken cancellationToken)
    {
        foreach (var poolData in enemyPoolData)
        {
            if (poolData.enemyPrefab == null) continue;
            
            await UniTask.Yield(cancellationToken);
            
            string poolKey = poolData.enemyPrefab.name;
            var pool = new ObjectPool<GameObject>(
                () => CreateEnemy(poolData.enemyPrefab),
                OnGetEnemyFromPool,
                OnReleaseEnemyToPool,
                OnDestroyEnemy,
                maxSize: maxPoolSize
            );
            
            enemyPools[poolKey] = pool;
            
            // 프리로드
            await PreloadEnemyPoolAsync(pool, poolData.preloadCount, cancellationToken);
        }
        
        Debug.Log($"[SpawnManager] {enemyPools.Count}개의 적 풀 초기화 완료");
    }

    private async UniTask PreloadEnemyPoolAsync(ObjectPool<GameObject> pool, int count, CancellationToken cancellationToken)
    {
        List<GameObject> preloadedObjects = new List<GameObject>();
        
        for (int i = 0; i < count; i++)
        {
            if (i % 5 == 0) // 5개마다 프레임 분산
            {
                await UniTask.Yield(cancellationToken);
            }
            
            GameObject obj = pool.Get();
            preloadedObjects.Add(obj);
        }
        
        // 모든 오브젝트를 다시 풀에 반환
        foreach (var obj in preloadedObjects)
        {
            pool.Release(obj);
        }
    }

    private async UniTask InitializeDropItemPoolAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);
        
        if (dropItemPrefabs.Count > 0)
        {
            dropItemPool = new ObjectPool<GameObject>(
                CreateDropItem,
                OnGetDropItemFromPool,
                OnReleaseDropItemToPool,
                OnDestroyDropItem,
                maxSize: 30
            );
        }
    }

    #endregion

    #region 스폰 시스템

    /// <summary>
    /// 웨이브 데이터를 기반으로 적 스폰 절차를 시작합니다.
    /// </summary>
    public UniTask SpawnWaveAsync(WaveData waveData, CancellationToken cancellationToken = default)
    {
        if (waveData == null || isSpawning)
        {
            Debug.LogWarning("[SpawnManager] 이미 스폰 중이거나 웨이브 데이터가 없습니다.");
            return UniTask.CompletedTask;
        }

        // 새 웨이브를 위해 상태 초기화
        _allEnemiesInQueue = false;
        _allEnemiesSpawned = false;

        ManageWaveSpawningAsync(waveData, cancellationToken).Forget();
        return UniTask.CompletedTask;
    }

    /// <summary>
    /// 웨이브 스폰의 '계획' 단계. 모든 적을 스폰 큐에 추가합니다.
    /// </summary>
    private async UniTaskVoid ManageWaveSpawningAsync(WaveData waveData, CancellationToken cancellationToken)
    {
        currentState = SpawnState.Spawning;
        isSpawning = true;

        try
        {
            if (waveData.enemyGroups.Count == 0)
            {
                Debug.LogWarning("[SpawnManager] 웨이브에 적 그룹이 없습니다. 즉시 완료 처리합니다.");
                _allEnemiesInQueue = true;
                return;
            }

            _activeEnqueueTasks = waveData.enemyGroups.Count;

            foreach (var enemyGroup in waveData.enemyGroups)
            {
                SpawnEnemyGroupLoopAsync(enemyGroup, waveData.spawnPoint, cancellationToken).Forget();
            }

            await UniTask.WaitUntil(() => _activeEnqueueTasks == 0, cancellationToken: cancellationToken);

            _allEnemiesInQueue = true;
            Debug.Log("[SpawnManager] 모든 적 스폰 요청이 큐에 추가되었습니다.");
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[SpawnManager] 웨이브 스폰이 취소되었습니다.");
            isSpawning = false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SpawnManager] 웨이브 스폰 관리 중 오류 발생: {ex.Message}");
            isSpawning = false;
        }
    }

    /// <summary>
    /// 단일 적 그룹의 모든 적을 스폰 큐에 추가하는 백그라운드 루프입니다.
    /// </summary>
    private async UniTaskVoid SpawnEnemyGroupLoopAsync(EnemyGroup enemyGroup, Vector3 baseSpawnPoint, CancellationToken cancellationToken)
    {
        try
        {
            for (int i = 0; i < enemyGroup.count; i++)
            {
                if (cancellationToken.IsCancellationRequested) return;

                var spawnRequest = new SpawnRequest
                {
                    enemyPrefab = enemyGroup.enemyPrefab,
                    spawnPosition = GetRandomSpawnPosition(baseSpawnPoint),
                    spawnDelay = enemyGroup.spawnInterval
                };

                spawnQueue.Enqueue(spawnRequest);

                if (enemyGroup.spawnInterval > 0)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(enemyGroup.spawnInterval), cancellationToken: cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Debug.LogError($"[SpawnManager] 적 그룹 스폰 루프 오류: {ex.Message}");
        }
        finally
        {
            System.Threading.Interlocked.Decrement(ref _activeEnqueueTasks);
        }
    }

    /// <summary>
    /// 스폰 큐를 처리하여 실제로 적을 필드에 생성하는 '실행' 루프
    /// </summary>
    private async UniTask StartSpawnProcessingAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && this != null)
        {
            try
            {
                await ProcessSpawnQueueAsync(cancellationToken);

                // '계획'이 끝나고 '실행'도 끝났는지 확인
                if (isSpawning && _allEnemiesInQueue && spawnQueue.Count == 0)
                {
                    // 한 웨이브 당 한 번만 실행되도록 보장
                    if (!_allEnemiesSpawned)
                    {
                        _allEnemiesSpawned = true;
                        isSpawning = false; // 이제 진짜 스포닝 끝
                        currentState = SpawnState.Complete;
                        OnAllEnemiesSpawned?.Invoke();
                        Debug.Log("[SpawnManager] 현재 웨이브의 모든 적이 필드에 스폰 완료되었습니다.");
                    }
                }
                
                await UniTask.Delay((int)(spawnCheckInterval * 1000), cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SpawnManager] 스폰 처리 오류: {ex.Message}");
                await UniTask.Delay(1000, cancellationToken: cancellationToken);
            }
        }
    }

    private async UniTask ProcessSpawnQueueAsync(CancellationToken cancellationToken)
    {
        int spawnedThisFrame = 0;
        while (spawnQueue.Count > 0 && spawnedThisFrame < maxEnemiesPerFrame)
        {
            var request = spawnQueue.Dequeue();
            if (request.enemyPrefab == null || !request.enemyPrefab)
            {
                Debug.LogWarning("[SpawnManager] 유효하지 않은 enemyPrefab이어서 스폰을 건너뜁니다.");
                continue;
            }
            
            await SpawnEnemyAsync(request, cancellationToken);
            spawnedThisFrame++;
            
            if (spawnedThisFrame >= maxEnemiesPerFrame)
            {
                await UniTask.Yield(cancellationToken);
            }
        }
    }

    private async UniTask SpawnEnemyAsync(SpawnRequest request, CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);
        
        string poolKey = request.enemyPrefab.name;
        if (!enemyPools.TryGetValue(poolKey, out var pool))
        {
            Debug.LogError($"[SpawnManager] '{poolKey}' 적의 풀을 찾을 수 없습니다!");
            return;
        }
        
        GameObject enemy = pool.Get();
        if (enemy != null)
        {
            enemy.transform.SetPositionAndRotation(request.spawnPosition, Quaternion.identity);
            if (enemyParent != null)
            {
                enemy.transform.SetParent(enemyParent);
            }
            
            if (enemy.TryGetComponent<EnemyAdvanced>(out var enemyComponent))
            {
                enemyComponent.OnEnemyKilled += HandleEnemyDestroyed;
            }
            
            activeEnemies.Add(enemy);
            OnEnemySpawned?.Invoke(enemy);
        }
    }

    #endregion

    #region 적 관리

    private void HandleEnemyDestroyed(GameObject enemy)
    {
        if (activeEnemies.Contains(enemy))
        {
            activeEnemies.Remove(enemy);
            TryDropItem(enemy.transform.position);
            OnEnemyDestroyed?.Invoke(enemy);
            
            // '모든 적이 스폰되었고', '모든 활성 적이 제거되었는지' 확인
            if (_allEnemiesSpawned && activeEnemies.Count == 0)
            {
                Debug.Log("[SpawnManager] 웨이브의 모든 적이 처치되었습니다.");
                OnAllEnemiesCleared?.Invoke();
            }
        }
        
        ReturnEnemyToPool(enemy);
    }

    private void ReturnEnemyToPool(GameObject enemy)
    {
        string poolKey = enemy.name.Replace("(Clone)", "");
        if (enemyPools.TryGetValue(poolKey, out var pool))
        {
            pool.Release(enemy);
        }
        else
        {
            Debug.LogWarning($"[SpawnManager] '{poolKey}' 풀을 찾을 수 없어 오브젝트를 파괴합니다.");
            Destroy(enemy);
        }
    }

    public void ClearAllEnemies()
    {
        foreach (var enemy in activeEnemies.ToArray())
        {
            if (enemy != null)
            {
                ReturnEnemyToPool(enemy);
            }
        }
        
        activeEnemies.Clear();
        spawnQueue.Clear();
        
        currentState = SpawnState.Waiting;
        isSpawning = false;
        _allEnemiesInQueue = false;
        _allEnemiesSpawned = false;
        
        Debug.Log("[SpawnManager] 모든 적 제거 완료");
    }

    #endregion

    #region 드롭 아이템 시스템

    private void TryDropItem(Vector3 dropPosition)
    {
        if (dropItemPrefabs.Count == 0 || UnityEngine.Random.value > dropChance) return;
        
        GameObject dropItem = dropItemPool?.Get();
        if (dropItem != null)
        {
            SetupDropItem(dropItem, dropPosition);
        }
    }

    private void SetupDropItem(GameObject dropItem, Vector3 position)
    {
        dropItem.transform.position = position + Vector3.up * 0.5f;
        
        if (dropItem.TryGetComponent<Rigidbody>(out var rb))
        {
            Vector3 randomForce = new Vector3(
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(0.5f, 1f),
                UnityEngine.Random.Range(-1f, 1f)
            ).normalized * UnityEngine.Random.Range(dropForceRange.x, dropForceRange.y);
            
            rb.linearVelocity = Vector3.zero;
            rb.AddForce(randomForce, ForceMode.Impulse);
        }
        
        OnItemDropped?.Invoke(dropItem, position);
        AutoCollectDropItemAsync(dropItem, 30f, cancellationTokenSource.Token).Forget();
    }

    private async UniTask AutoCollectDropItemAsync(GameObject dropItem, float delay, CancellationToken cancellationToken)
    {
        await UniTask.Delay((int)(delay * 1000), cancellationToken: cancellationToken);
        
        if (dropItem != null && dropItem.activeInHierarchy)
        {
            dropItemPool?.Release(dropItem);
        }
    }

    #endregion

    #region 오브젝트 풀 콜백

    private GameObject CreateEnemy(GameObject prefab)
    {
        GameObject enemy = Instantiate(prefab, enemyParent);
        enemy.name = prefab.name;
        enemy.SetActive(false);
        return enemy;
    }

    private void OnGetEnemyFromPool(GameObject enemy)
    {
        enemy.SetActive(true);
        if (enemy.transform.parent != enemyParent)
        {
            enemy.transform.SetParent(enemyParent);
        }
        
        if (enemy.TryGetComponent<EnemyAdvanced>(out var enemyComponent))
        {
            enemyComponent.ResetEnemy();
        }
    }

    private void OnReleaseEnemyToPool(GameObject enemy)
    {
        enemy.SetActive(false);
        if (enemy.TryGetComponent<EnemyAdvanced>(out var enemyComponent))
        {
            enemyComponent.OnEnemyKilled -= HandleEnemyDestroyed;
        }
    }

    private void OnDestroyEnemy(GameObject enemy)
    {
        if (enemy != null)
        {
            Destroy(enemy);
        }
    }

    private GameObject CreateDropItem()
    {
        if (dropItemPrefabs.Count == 0) return null;
        GameObject randomPrefab = dropItemPrefabs[UnityEngine.Random.Range(0, dropItemPrefabs.Count)];
        GameObject dropItem = Instantiate(randomPrefab, enemyParent);
        dropItem.name = randomPrefab.name;
        dropItem.SetActive(false);
        return dropItem;
    }

    private void OnGetDropItemFromPool(GameObject dropItem)
    {
        dropItem.SetActive(true);
        if (dropItem.transform.parent != enemyParent)
        {
            dropItem.transform.SetParent(enemyParent);
        }
    }

    private void OnReleaseDropItemToPool(GameObject dropItem)
    {
        dropItem.SetActive(false);
    }

    private void OnDestroyDropItem(GameObject dropItem)
    {
        if (dropItem != null)
        {
            Destroy(dropItem);
        }
    }

    #endregion

    #region 유틸리티

    private Vector3 GetRandomSpawnPosition(Vector3 basePosition)
    {
        float x = UnityEngine.Random.Range(spawnAreaMin.x, spawnAreaMax.x);
        float y = UnityEngine.Random.Range(spawnAreaMin.y, spawnAreaMax.y);
        float z = UnityEngine.Random.Range(spawnAreaMin.z, spawnAreaMax.z);
        return new Vector3(x, y, z);
    }

    public string GetStatusInfo()
    {
        return $"상태: {currentState}, 활성 적: {activeEnemies.Count}, 대기 중: {spawnQueue.Count}, " +
               $"스폰 중: {isSpawning}, 큐 완료: {_allEnemiesInQueue}, 스폰 완료: {_allEnemiesSpawned}";
    }

    #endregion

    #region 리소스 정리

    private void CleanupResources()
    {
        ClearAllEnemies();
        
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        
        foreach (var pool in enemyPools.Values)
        {
            pool?.Dispose();
        }
        enemyPools.Clear();
        
        dropItemPool?.Dispose();
        
        Debug.Log("[SpawnManager] 리소스 정리 완료");
    }

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Vector3 center = (spawnAreaMin + spawnAreaMax) * 0.5f;
        Vector3 size = spawnAreaMax - spawnAreaMin;
        Gizmos.DrawCube(center, size);
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(center, size);
    }
#endif
}

#region 데이터 구조

/// <summary>
/// 적 풀 데이터를 저장하는 클래스
/// </summary>
[System.Serializable]
public class EnemyPoolData
{
    public GameObject enemyPrefab;
    public int preloadCount = 5;
}

/// <summary>
/// 스폰 요청을 저장하는 구조체
/// </summary>
public struct SpawnRequest
{
    public GameObject enemyPrefab;
    public Vector3 spawnPosition;
    public float spawnDelay;
}

#endregion
