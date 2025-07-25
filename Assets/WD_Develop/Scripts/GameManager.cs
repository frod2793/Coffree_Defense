using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;

/// <summary>
/// 게임의 핵심 루프를 관리하는 매니저
/// 15초 준비 시간 → 웨이브 전투 → 결과 처리 → 다음 웨이브 준비의 사이클을 관리합니다.
/// </summary>
public class GameManager : MonoBehaviour
{
    #region 열거형 및 상수

    /// <summary>
    /// 게임 상태를 정의하는 열거형
    /// </summary>
    public enum GameState
    {
        Preparing,      // 웨이브 준비 중 (15초)
        Fighting,       // 웨이브 전투 중
        WaveComplete,   // 웨이브 완료 처리
        GameOver,       // 게임 오버
        Victory,        // 게임 승리
        Paused          // 게임 일시정지
    }

    // 게임 설정 상수
    private const float PREPARATION_TIME = 15f;
    private const int INITIAL_TP_PER_WAVE = 3;
    private const int BASE_COIN_REWARD = 50;
    private const int TP_INCREMENT_PER_WAVE = 1;

    #endregion

    #region 필드 및 속성

    [Header("게임 설정")]
    [SerializeField] private int totalWaves = 10;
    [SerializeField] private float preparationTime = PREPARATION_TIME;
    [SerializeField] private int initialTPPerWave = INITIAL_TP_PER_WAVE;
    [SerializeField] private int baseCoinReward = BASE_COIN_REWARD;
    
    [Header("웨이브 설정")]
    [SerializeField] private List<WaveData> waveDataList = new List<WaveData>();
    
    [Header("참조")]
    [SerializeField] private InGameUIManager uiManager;
    [SerializeField] private TerretControl turretControl;
    [SerializeField] private SpawnManager spawnManager;
    
    // 게임 상태
    public GameState currentState { get; private set; }
    private int currentWaveIndex = 0;
    private float preparationTimer;
    private bool isGameActive;
    
    // 웨이브 관련
    private List<GameObject> currentEnemies = new List<GameObject>();
    private int enemiesKilled;
    private int totalEnemiesInWave;
    
    // UniTask 관련
    private CancellationTokenSource cancellationTokenSource;
    
    // 이벤트
    public static event Action<GameState> OnGameStateChanged;
    public static event Action<int> OnWaveStarted;
    public static event Action<int, int> OnWaveCompleted; // (웨이브 번호, 획득 코인)
    public static event Action<float> OnPreparationTimeUpdated;
    public static event Action<int, int> OnEnemyKilled; // (현재 처치 수, 전체 수)
    public static event Action OnGameOver;
    public static event Action OnGameVictory;

    // 공개 속성
    public int CurrentWave => currentWaveIndex + 1;
    public int TotalWaves => totalWaves;
    public float PreparationTimeRemaining => preparationTimer;
    public bool IsPreparationPhase => currentState == GameState.Preparing;
    public bool IsFightingPhase => currentState == GameState.Fighting;
    public int EnemiesKilled => enemiesKilled;
    public int TotalEnemies => totalEnemiesInWave;

    #endregion

    #region 유니티 생명주기

    void Awake()
    {
        // 필수 컴포넌트 찾기
        FindRequiredComponents();
    }

    async void Start()
    {
        cancellationTokenSource = new CancellationTokenSource();
        await InitializeGameAsync(cancellationTokenSource.Token);
    }

    void Update()
    {
        if (!isGameActive) return;

        UpdateGameLoop();
    }

    void OnDestroy()
    {
        CleanupResources();
    }

    #endregion

    #region 초기화

    private void FindRequiredComponents()
    {
        if (uiManager == null)
            uiManager = FindFirstObjectByType<InGameUIManager>();
        
        if (turretControl == null)
            turretControl = FindFirstObjectByType<TerretControl>();
            
        if (spawnManager == null)
            spawnManager = FindFirstObjectByType<SpawnManager>();
    }

    private async UniTask InitializeGameAsync(CancellationToken cancellationToken)
    {
        try
        {
            await UniTask.Yield(cancellationToken);
            
            // 게임 초기 설정
            currentWaveIndex = 0;
            enemiesKilled = 0;
            totalEnemiesInWave = 0;
            
            // DataManager 확인 및 초기 TP 지급
            await SetupInitialResourcesAsync(cancellationToken);
            
            // 웨이브 데이터 검증
            ValidateWaveData();
            
            // 첫 번째 웨이브 준비 시작
            await StartPreparationPhaseAsync(cancellationToken);
            
            isGameActive = true;
            
            Debug.Log("[GameManager] 게임 초기화 완료");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameManager] 게임 초기화 실패: {ex.Message}");
        }
    }

    private async UniTask SetupInitialResourcesAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);
        
        if (DataManger.IsAvailable())
        {
            // 게임 시작 시 초기 TP 지급
            DataManger.Instance.AddTP(initialTPPerWave);
            Debug.Log($"[GameManager] 초기 TP {initialTPPerWave} 지급");
        }
        else
        {
            Debug.LogWarning("[GameManager] DataManager를 사용할 수 없습니다!");
        }
    }

    private void ValidateWaveData()
    {
        if (waveDataList.Count < totalWaves)
        {
            Debug.LogWarning($"[GameManager] 웨이브 데이터가 부족합니다. {waveDataList.Count}/{totalWaves}");
        }
    }

    #endregion

    #region 게임 루프

    private void UpdateGameLoop()
    {
        switch (currentState)
        {
            case GameState.Preparing:
                UpdatePreparationPhase();
                break;
            case GameState.Fighting:
                UpdateFightingPhase();
                break;
            case GameState.WaveComplete:
                // 비동기로 처리되므로 여기서는 별도 처리 없음
                break;
        }
    }

    private void UpdatePreparationPhase()
    {
        preparationTimer -= Time.deltaTime;
        OnPreparationTimeUpdated?.Invoke(preparationTimer);
        
        if (preparationTimer <= 0f)
        {
            StartWaveAsync(cancellationTokenSource.Token).Forget();
        }
    }

    private void UpdateFightingPhase()
    {
        // 현재 적의 수 확인
        CleanupDestroyedEnemies();
        
        // 모든 적이 처치되었는지 확인
        if (currentEnemies.Count == 0 && totalEnemiesInWave > 0)
        {
            CompleteWaveAsync(cancellationTokenSource.Token).Forget();
        }
    }

    #endregion

    #region 웨이브 관리

    /// <summary>
    /// 웨이브 준비 단계를 시작합니다.
    /// </summary>
    private async UniTask StartPreparationPhaseAsync(CancellationToken cancellationToken)
    {
        ChangeGameState(GameState.Preparing);
        preparationTimer = preparationTime;
        
        await UniTask.Yield(cancellationToken);
        
        // 웨이브 시작 시 TP 지급 (첫 웨이브 제외)
        if (currentWaveIndex > 0 && DataManger.IsAvailable())
        {
            int tpToGive = initialTPPerWave + (currentWaveIndex * TP_INCREMENT_PER_WAVE);
            DataManger.Instance.AddTP(tpToGive);
            Debug.Log($"[GameManager] 웨이브 {CurrentWave} 준비: TP {tpToGive} 지급");
        }
        
        Debug.Log($"[GameManager] 웨이브 {CurrentWave} 준비 시작 ({preparationTime}초)");
    }

    /// <summary>
    /// 웨이브를 시작합니다.
    /// </summary>
    private async UniTask StartWaveAsync(CancellationToken cancellationToken)
    {
        ChangeGameState(GameState.Fighting);
        
        await UniTask.Yield(cancellationToken);
        
        // 현재 웨이브 데이터 가져오기
        WaveData currentWaveData = GetCurrentWaveData();
        if (currentWaveData != null)
        {
            await SpawnWaveEnemiesAsync(currentWaveData, cancellationToken);
        }
        else
        {
            Debug.LogError($"[GameManager] 웨이브 {CurrentWave} 데이터를 찾을 수 없습니다!");
            await CompleteWaveAsync(cancellationToken);
        }
        
        OnWaveStarted?.Invoke(CurrentWave);
        Debug.Log($"[GameManager] 웨이브 {CurrentWave} 시작!");
    }

    /// <summary>
    /// 웨이브를 완료합니다.
    /// </summary>
    private async UniTask CompleteWaveAsync(CancellationToken cancellationToken)
    {
        ChangeGameState(GameState.WaveComplete);
        
        await UniTask.Yield(cancellationToken);
        
        // 코인 보상 계산 및 지급
        int coinReward = CalculateCoinReward();
        if (DataManger.IsAvailable())
        {
            DataManger.Instance.AddCoin(coinReward);
        }
        
        OnWaveCompleted?.Invoke(CurrentWave, coinReward);
        Debug.Log($"[GameManager] 웨이브 {CurrentWave} 완료! 코인 {coinReward} 획득");
        
        // 다음 웨이브 준비 또는 게임 종료
        currentWaveIndex++;
        
        if (currentWaveIndex >= totalWaves)
        {
            await EndGameAsync(true, cancellationToken);
        }
        else
        {
            await StartPreparationPhaseAsync(cancellationToken);
        }
    }

    #endregion

    #region 적 관리

    /// <summary>
    /// 웨이브의 적들을 생성합니다.
    /// </summary>
    private async UniTask SpawnWaveEnemiesAsync(WaveData waveData, CancellationToken cancellationToken)
    {
        currentEnemies.Clear();
        enemiesKilled = 0;
        totalEnemiesInWave = 0;
        
        // 웨이브 데이터에 따라 적 생성
        foreach (var enemyGroup in waveData.enemyGroups)
        {
            for (int i = 0; i < enemyGroup.count; i++)
            {
                await UniTask.Delay((int)(enemyGroup.spawnInterval * 1000), DelayType.DeltaTime, 
                    PlayerLoopTiming.Update, cancellationToken);
                
                if (enemyGroup.enemyPrefab != null)
                {
                    GameObject enemy = SpawnEnemy(enemyGroup.enemyPrefab, waveData.spawnPoint);
                    if (enemy != null)
                    {
                        currentEnemies.Add(enemy);
                        totalEnemiesInWave++;
                        
                        // 적에게 이벤트 연결 - EnemyAdvanced 클래스 사용
                        var enemyComponent = enemy.GetComponent<EnemyAdvanced>();
                        if (enemyComponent != null)
                        {
                            enemyComponent.OnEnemyKilled += OnEnemyKilledHandler;
                        }
                    }
                }
            }
        }
        
        Debug.Log($"[GameManager] 총 {totalEnemiesInWave}마리의 적 생성 완료");
    }

    private GameObject SpawnEnemy(GameObject enemyPrefab, Vector3 spawnPoint)
    {
        try
        {
            GameObject enemy = Instantiate(enemyPrefab, spawnPoint, Quaternion.identity);
            return enemy;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameManager] 적 생성 실패: {ex.Message}");
            return null;
        }
    }

    private void OnEnemyKilledHandler(GameObject enemy)
    {
        enemiesKilled++;
        OnEnemyKilled?.Invoke(enemiesKilled, totalEnemiesInWave);
        
        // 리스트에서 제거
        if (currentEnemies.Contains(enemy))
        {
            currentEnemies.Remove(enemy);
        }
        
        Debug.Log($"[GameManager] 적 처치: {enemiesKilled}/{totalEnemiesInWave}");
    }

    private void CleanupDestroyedEnemies()
    {
        for (int i = currentEnemies.Count - 1; i >= 0; i--)
        {
            if (currentEnemies[i] == null)
            {
                currentEnemies.RemoveAt(i);
            }
        }
    }

    #endregion

    #region 보상 및 계산

    private int CalculateCoinReward()
    {
        // 기본 보상 + 웨이브 보너스 + 처치 보너스
        int baseReward = baseCoinReward;
        int waveBonus = CurrentWave * 10;
        int killBonus = enemiesKilled * 5;
        
        return baseReward + waveBonus + killBonus;
    }

    #endregion

    #region 게임 종료

    /// <summary>
    /// 게임을 종료합니다.
    /// </summary>
    private async UniTask EndGameAsync(bool victory, CancellationToken cancellationToken)
    {
        isGameActive = false;
        
        await UniTask.Yield(cancellationToken);
        
        if (victory)
        {
            ChangeGameState(GameState.Victory);
            OnGameVictory?.Invoke();
            Debug.Log("[GameManager] 게임 승리!");
        }
        else
        {
            ChangeGameState(GameState.GameOver);
            OnGameOver?.Invoke();
            Debug.Log("[GameManager] 게임 오버!");
        }
        
        // 데이터 저장
        if (DataManger.IsAvailable())
        {
            DataManger.Instance.SaveUserData();
        }
    }

    /// <summary>
    /// 게임 오버 처리 (외부에서 호출 가능)
    /// </summary>
    public void TriggerGameOver()
    {
        if (isGameActive)
        {
            EndGameAsync(false, cancellationTokenSource.Token).Forget();
        }
    }

    #endregion

    #region 게임 제어

    /// <summary>
    /// 게임을 일시정지합니다.
    /// </summary>
    public void PauseGame()
    {
        if (currentState != GameState.Paused && isGameActive)
        {
            ChangeGameState(GameState.Paused);
            Time.timeScale = 0f;
            Debug.Log("[GameManager] 게임 일시정지");
        }
    }

    /// <summary>
    /// 게임을 재개합니다.
    /// </summary>
    public void ResumeGame()
    {
        if (currentState == GameState.Paused)
        {
            Time.timeScale = 1f;
            ChangeGameState(GameState.Preparing); // 이전 상태로 복원하는 로직 필요시 추가
            Debug.Log("[GameManager] 게임 재개");
        }
    }

    /// <summary>
    /// 게임을 재시작합니다.
    /// </summary>
    public async UniTask RestartGameAsync()
    {
        CleanupCurrentGame();
        await InitializeGameAsync(cancellationTokenSource.Token);
    }

    #endregion

    #region 유틸리티

    private void ChangeGameState(GameState newState)
    {
        if (currentState != newState)
        {
            var previousState = currentState;
            currentState = newState;
            OnGameStateChanged?.Invoke(newState);
            Debug.Log($"[GameManager] 상태 변경: {previousState} → {newState}");
        }
    }

    private WaveData GetCurrentWaveData()
    {
        if (currentWaveIndex < waveDataList.Count)
        {
            return waveDataList[currentWaveIndex];
        }
        
        // 웨이브 데이터가 부족한 경우 기본 웨이브 생성
        return CreateDefaultWaveData();
    }

    private WaveData CreateDefaultWaveData()
    {
        // 기본 웨이브 데이터 생성 로직
        // 실제 게임에서는 적절한 기본값 설정 필요
        return new WaveData
        {
            waveNumber = CurrentWave,
            spawnPoint = Vector3.zero,
            enemyGroups = new List<EnemyGroup>()
        };
    }

    /// <summary>
    /// 게임 상태 정보를 반환합니다.
    /// </summary>
    public string GetGameStatusInfo()
    {
        return $"웨이브: {CurrentWave}/{totalWaves}, 상태: {currentState}, " +
               $"적 처치: {enemiesKilled}/{totalEnemiesInWave}";
    }

    #endregion

    #region 리소스 정리

    private void CleanupCurrentGame()
    {
        // 현재 적들 정리
        foreach (var enemy in currentEnemies)
        {
            if (enemy != null)
            {
                Destroy(enemy);
            }
        }
        currentEnemies.Clear();
        
        Time.timeScale = 1f;
    }

    private void CleanupResources()
    {
        CleanupCurrentGame();
        
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        
        Debug.Log("[GameManager] 리소스 정리 완료");
    }

    #endregion
}

#region 웨이브 데이터 구조

/// <summary>
/// 웨이브 정보를 저장하는 데이터 클래스
/// </summary>
[System.Serializable]
public class WaveData
{
    public int waveNumber;
    public Vector3 spawnPoint;
    public List<EnemyGroup> enemyGroups = new List<EnemyGroup>();
}

/// <summary>
/// 적 그룹 정보를 저장하는 데이터 클래스
/// </summary>
[System.Serializable]
public class EnemyGroup
{
    public GameObject enemyPrefab;
    public int count;
    public float spawnInterval;
}

#endregion
