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
    // [SerializeField] private List<Transform> spawnPoints = new List<Transform>(); // 기존 스폰포인트 비활성화
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
    public static event Action OnAllEnemiesSpawned;
    public static event Action OnAllEnemiesCleared;
    
    // GameManager와의 연동을 위한 이벤트
    public static event Action<int, int> OnEnemyKilledUpdate; // (처치된 적 수, 전체 적 수)

    // 공개 속성
    public int ActiveEnemyCount => activeEnemies.Count;
    public bool IsSpawning => isSpawning;
    public int QueuedSpawnCount => spawnQueue.Count;
    public int TotalEnemiesSpawned { get; private set; } // 총 스폰된 적 수 추적

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
    /// 웨이브 데이터를 기반으로 적들을 스폰합니다.
    /// </summary>
    public async UniTask SpawnWaveAsync(WaveData waveData, CancellationToken cancellationToken = default)
    {
        if (waveData == null || isSpawning)
        {
            Debug.LogWarning("[SpawnManager] 이미 스폰 중이거나 웨이브 데이터가 없습니다.");
            return;
        }

        currentState = SpawnState.Spawning;
        isSpawning = true;
        
        // 웨이브 시작 시 총 스폰된 적 수 초기화
        TotalEnemiesSpawned = 0;
        
        try
        {
            await ProcessWaveSpawnAsync(waveData, cancellationToken);
            
            currentState = SpawnState.Complete;
            OnAllEnemiesSpawned?.Invoke();
            
            Debug.Log($"[SpawnManager] SpawnManager를 통해 총 {TotalEnemiesSpawned}마리의 적 스폰 완료");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SpawnManager] 웨이브 스폰 오류: {ex.Message}");
        }
        finally
        {
            isSpawning = false;
        }
    }

    private async UniTask ProcessWaveSpawnAsync(WaveData waveData, CancellationToken cancellationToken)
    {
        foreach (var enemyGroup in waveData.enemyGroups)
        {
            await SpawnEnemyGroupAsync(enemyGroup, waveData.spawnPoint, cancellationToken);
        }
    }

    private async UniTask SpawnEnemyGroupAsync(EnemyGroup enemyGroup, Vector3 baseSpawnPoint, CancellationToken cancellationToken)
    {
        for (int i = 0; i < enemyGroup.count; i++)
        {
            // 스폰 요청을 큐에 추가
            var spawnRequest = new SpawnRequest
            {
                enemyPrefab = enemyGroup.enemyPrefab,
                spawnPosition = GetRandomSpawnPosition(baseSpawnPoint),
                spawnDelay = enemyGroup.spawnInterval
            };
            
            spawnQueue.Enqueue(spawnRequest);
            
            if (enemyGroup.spawnInterval > 0)
            {
                await UniTask.Delay((int)(enemyGroup.spawnInterval * 1000), 
                    DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
            }
        }
    }

    /// <summary>
    /// 스폰 큐를 처리하는 비동기 루프
    /// </summary>
    private async UniTask StartSpawnProcessingAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && this != null)
        {
            try
            {
                await ProcessSpawnQueueAsync(cancellationToken);
                
                await UniTask.Delay((int)(spawnCheckInterval * 1000), 
                    DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SpawnManager] 스폰 처리 오류: {ex.Message}");
                await UniTask.Delay(1000, DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
            }
        }
    }

    private async UniTask ProcessSpawnQueueAsync(CancellationToken cancellationToken)
    {
        int spawnedThisFrame = 0;
        while (spawnQueue.Count > 0 && spawnedThisFrame < maxEnemiesPerFrame)
        {
            var request = spawnQueue.Dequeue();
            // null 체크 및 파괴된 오브젝트 방지
            if (request.enemyPrefab == null)
            {
                Debug.LogWarning("[SpawnManager] enemyPrefab이 null이어서 스폰을 건너뜁니다.");
                continue;
            }
            // 이미 Destroy된 프리팹이거나 유효하지 않은 경우 건너뜀
            if (request.enemyPrefab == null || !request.enemyPrefab)
            {
                Debug.LogWarning("[SpawnManager] enemyPrefab이 Destroy되어 스폰을 건너뜁니다.");
                continue;
            }
            try
            {
                await SpawnEnemyAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SpawnManager] SpawnEnemyAsync 예외: {ex.Message}");
            }
            spawnedThisFrame++;
            if (spawnedThisFrame >= maxEnemiesPerFrame)
            {
                await UniTask.Yield(cancellationToken);
            }
        }
    }

    /// <summary>
    /// 개별 적을 스폰합니다.
    /// </summary>
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
            // 적 오브젝트 활성화 (가장 중요!)
            enemy.SetActive(true);
            
            // 위치 및 회전 설정
            enemy.transform.position = request.spawnPosition;
            enemy.transform.rotation = Quaternion.identity;
            
            // 부모 설정 (있는 경우)
            if (enemyParent != null)
            {
                enemy.transform.SetParent(enemyParent);
            }
            
            // Enemy 컴포넌트 초기화 및 이벤트 연결
            if (enemy.TryGetComponent<EnemyAdvanced>(out var enemyComponent))
            {
                // 적 컴포넌트 초기화 (필요한 경우)
                enemyComponent.ResetEnemy();
                enemyComponent.OnEnemyKilled += HandleEnemyDestroyed;
            }
            else
            {
                Debug.LogWarning($"[SpawnManager] 스폰된 적 {enemy.name}에 EnemyAdvanced 컴포넌트가 없습니다!");
            }
            
            activeEnemies.Add(enemy);
            TotalEnemiesSpawned++; // 총 스폰된 적 수 증가
            OnEnemySpawned?.Invoke(enemy);
            
            Debug.Log($"[SpawnManager] 적 스폰 성공: {enemy.name} at {request.spawnPosition}, 활성화 상태: {enemy.activeInHierarchy}, 총 스폰: {TotalEnemiesSpawned}");
        }
        else
        {
            Debug.LogError("[SpawnManager] 오브젝트 풀에서 적을 가져올 수 없습니다!");
        }
    }

    #endregion

    #region 적 관리

    /// <summary>
    /// 적이 파괴되었을 때 호출되는 핸들러
    /// </summary>
    private void HandleEnemyDestroyed(GameObject enemy)
    {
        if (activeEnemies.Contains(enemy))
        {
            activeEnemies.Remove(enemy);
            
            // 드롭 아이템 생성
            TryDropItem(enemy.transform.position);
            
            OnEnemyDestroyed?.Invoke(enemy);
            
            // 모든 적이 제거되었는지 확인
            if (activeEnemies.Count == 0 && currentState == SpawnState.Complete)
            {
                OnAllEnemiesCleared?.Invoke();
            }
        }
        
        // 적을 풀로 반환
        ReturnEnemyToPool(enemy);
    }

    /// <summary>
    /// 적을 풀로 반환합니다.
    /// </summary>
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

    /// <summary>
    /// 모든 활성 적들을 제거합니다.
    /// </summary>
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
        
        Debug.Log("[SpawnManager] 모든 적 제거 완료");
    }

    #endregion

    #region 드롭 아이템 시스템

    /// <summary>
    /// 아이템 드롭을 시도합니다.
    /// </summary>
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
        
        // 물리 효과 적용
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
        
        // 일정 시간 후 자동 회수
        AutoCollectDropItemAsync(dropItem, 30f, cancellationTokenSource.Token).Forget();
    }

    private async UniTask AutoCollectDropItemAsync(GameObject dropItem, float delay, CancellationToken cancellationToken)
    {
        await UniTask.Delay((int)(delay * 1000), DelayType.DeltaTime, 
            PlayerLoopTiming.Update, cancellationToken);
        
        if (dropItem != null && dropItem.activeInHierarchy)
        {
            dropItemPool?.Release(dropItem);
        }
    }

    #endregion

    #region 오브젝트 풀 콜백

    private GameObject CreateEnemy(GameObject prefab)
    {
        // 적을 enemyParent 하위에 생성
        GameObject enemy = Instantiate(prefab, enemyParent);
        
        // (Clone) 제거하여 깔끔한 이름으로 설정
        enemy.name = prefab.name;
        
        // 초기에는 비활성화 상태로 생성 (풀에서 관리)
        enemy.SetActive(false);
        
        Debug.Log($"[SpawnManager] 적 생성: {enemy.name}, 부모: {enemy.transform.parent?.name ?? "없음"}");
        
        return enemy;
    }

    private void OnGetEnemyFromPool(GameObject enemy)
    {
        // 풀에서 가져올 때 활성화
        enemy.SetActive(true);
        
        // 부모가 올바르게 설정되어 있는지 확인
        if (enemy.transform.parent != enemyParent)
        {
            enemy.transform.SetParent(enemyParent);
            Debug.Log($"[SpawnManager] 적 {enemy.name}의 부모를 {enemyParent.name}으로 재설정");
        }
        
        // 적 상태 초기화 (ResetEnemy를 사용하여 완전한 초기화)
        if (enemy.TryGetComponent<EnemyAdvanced>(out var enemyComponent))
        {
            enemyComponent.ResetEnemy();
            Debug.Log($"[SpawnManager] 적 {enemy.name} 완전 초기화 완료 - 이동속도: {enemyComponent.MoveSpeed}");
        }
        else
        {
            Debug.LogWarning($"[SpawnManager] 적 {enemy.name}에 EnemyAdvanced 컴포넌트가 없습니다!");
        }
    }

    private void OnReleaseEnemyToPool(GameObject enemy)
    {
        // 풀로 반환할 때 비활성화
        enemy.SetActive(false);
        
        // 이벤트 연결 해제
        if (enemy.TryGetComponent<EnemyAdvanced>(out var enemyComponent))
        {
            enemyComponent.OnEnemyKilled -= HandleEnemyDestroyed;
        }
    }

    private void OnDestroyEnemy(GameObject enemy)
    {
        if (enemy != null)
        {
            Debug.Log($"[SpawnManager] 적 오브젝트 파괴: {enemy.name}");
            Destroy(enemy);
        }
    }

    private GameObject CreateDropItem()
    {
        if (dropItemPrefabs.Count == 0) return null;
        
        GameObject randomPrefab = dropItemPrefabs[UnityEngine.Random.Range(0, dropItemPrefabs.Count)];
        
        // 드롭 아이템도 enemyParent 하위에 생성하여 정리 용이
        GameObject dropItem = Instantiate(randomPrefab, enemyParent);
        
        // (Clone) 제거
        dropItem.name = randomPrefab.name;
        
        // 초기에는 비활성화
        dropItem.SetActive(false);
        
        return dropItem;
    }

    private void OnGetDropItemFromPool(GameObject dropItem)
    {
        dropItem.SetActive(true);
        
        // 부모 확인 및 재설정
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
        // spawnPoints 대신 bound 내 랜덤 위치 반환
        float x = UnityEngine.Random.Range(spawnAreaMin.x, spawnAreaMax.x);
        float y = UnityEngine.Random.Range(spawnAreaMin.y, spawnAreaMax.y);
        float z = UnityEngine.Random.Range(spawnAreaMin.z, spawnAreaMax.z);
        return new Vector3(x, y, z);
    }

    /// <summary>
    /// 스폰 매니저 상태 정보를 반환합니다.
    /// </summary>
    public string GetStatusInfo()
    {
        return $"상태: {currentState}, 활성 적: {activeEnemies.Count}, 대기 중: {spawnQueue.Count}, " +
               $"풀 개수: {enemyPools.Count}";
    }

    #endregion

    #region 리소스 정리

    private void CleanupResources()
    {
        ClearAllEnemies();
        
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        
        // 오브젝트 풀 정리
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
