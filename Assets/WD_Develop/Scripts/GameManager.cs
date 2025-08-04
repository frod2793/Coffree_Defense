using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using WD_Develop.Scripts;

/// <summary>
/// 게임의 핵심 루프를 관리하는 매니저
///  준비 시간 → 웨이브 전투 → 결과 처리 → 다음 웨이브 준비의 사이클을 관리합니다.
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
    
    [Header("수압프레스 설정")]
    [SerializeField] private HydroWaterPress hydroWaterPrefab; // 수압프레스 프리팹
    [SerializeField] private Transform hydroSpawnPoint; // 수압프레스 스폰 위치
    // 수압프레스 관련 변수
    private float lastHydroPressTime = 0f;
    private List<HydroWaterPress> activeHydroPresses = new List<HydroWaterPress>();
    
    [SerializeField] private int hydroPressCost = 100; // 수압프레스 비용
    [SerializeField] private float hydroPressCooldown = 10f; // 수압프레스 쿨다운
    
    // 게임 상태
    public GameState currentState { get; private set; }
    private int currentWaveIndex = 0;
    private float preparationTimer;
    private bool isGameActive;
    
    // 웨이브 관련
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
    public static event Action<HydroWaterPress> OnHydroWaterPressSpawned; // 수압프레스 생성 이벤트

    // 공개 속성
    public int CurrentWave => currentWaveIndex + 1;
    public int TotalWaves => totalWaves;
    public float PreparationTimeRemaining => preparationTimer;
    public bool IsPreparationPhase => currentState == GameState.Preparing;
    public bool IsFightingPhase => currentState == GameState.Fighting;
    public int EnemiesKilled => enemiesKilled;
    public int TotalEnemies => totalEnemiesInWave;
    
    // 수압프레스 공개 속성
    public int HydroPressCost => hydroPressCost;
    public float HydroPressCooldown => hydroPressCooldown;
    public int ActiveHydroPressCount { get { CleanupHydroPresses(); return activeHydroPresses.Count; } }
    public bool IsHydroPressReady => CanUseHydroWaterPress();
    public float HydroPressRemainingCooldown => GetHydroPressRemainingCooldown();

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

            // 이벤트 구독
            if (spawnManager != null)
            {
                SpawnManager.OnEnemyDestroyed += OnEnemyKilledHandler;
            }

            // DataManger 확인 및 초기 TP 지급
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
            Debug.LogWarning("[GameManager] DataManger를 사용할 수 없습니다!");
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
            // Fighting 및 WaveComplete 상태는 이벤트 기반으로 처리되므로 Update에서 제외
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
    
    #endregion

    #region 웨이브 관리

    /// <summary>
    /// 웨이브 준비 단계를 시작합니다.
    /// </summary>
    private async UniTask StartPreparationPhaseAsync(CancellationToken cancellationToken)
    {
        // 첫 웨이브가 아닐 경우, 준비 시간 없이 바로 다음 웨이브 시작
        if (currentWaveIndex > 0)
        {
            // TP 지급
            if (DataManger.IsAvailable())
            {
                int tpToGive = initialTPPerWave + (currentWaveIndex * TP_INCREMENT_PER_WAVE);
                DataManger.Instance.AddTP(tpToGive);
                Debug.Log($"[GameManager] 웨이브 {CurrentWave} 준비: TP {tpToGive} 지급");
            }

            Debug.Log($"[GameManager] 웨이브 {CurrentWave} 즉시 시작");
            await StartWaveAsync(cancellationToken); // 준비 시간 없이 바로 웨이브 시작
        }
        else
        {
            // 첫 웨이브일 경우에만 준비 시간 적용
            ChangeGameState(GameState.Preparing);
            preparationTimer = preparationTime;
            await UniTask.Yield(cancellationToken);
            Debug.Log($"[GameManager] 웨이브 {CurrentWave} 준비 시작 ({preparationTime}초)");
        }
    }

    /// <summary>
    /// 웨이브를 시작합니다.
    /// </summary>
    private async UniTask StartWaveAsync(CancellationToken cancellationToken)
    {
        ChangeGameState(GameState.Fighting);
        await UniTask.Yield(cancellationToken);

        // 웨이브 안내 텍스트 표시
        if (uiManager != null)
        {
            uiManager.ShowWaveTextAsync($"Wave {CurrentWave} 시작!").Forget();
            uiManager.UpdateWaveDisplay(CurrentWave, totalWaves);
        }

        // 현재 웨이브 데이터 가져오기
        WaveData currentWaveData = GetCurrentWaveData();
        if (currentWaveData != null)
        {
            await SpawnWaveEnemiesAsync(currentWaveData, cancellationToken);
        }
        else
        {
            Debug.LogError($"[GameManager] 웨이브 {CurrentWave} 데이터를 찾을 수 없습니다! 다음 웨이브를 진행합니다.");
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
        // 상태가 이미 WaveComplete이면 중복 실행 방지
        if (currentState == GameState.WaveComplete) return;
        
        ChangeGameState(GameState.WaveComplete);
        await UniTask.Yield(cancellationToken);

        // 웨이브 종료 안내 텍스트 표시
        if (uiManager != null)
        {
            await uiManager.ShowWaveEndTextAsync();
        }

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
            // 웨이브 종료 후 카운트다운 표시
            if (uiManager != null)
            {
                await uiManager.ShowWaveCountdownAsync(uiManager.CountdownDuration, cancellationToken);
            }
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
        enemiesKilled = 0;
        // 웨이브 시작 전에 총 적 수를 미리 계산합니다.
        totalEnemiesInWave = CalculateTotalEnemiesInWave(waveData);
        
        Debug.Log($"[GameManager] 웨이브 시작 - 총 {totalEnemiesInWave}마리의 적 스폰 예정");

        if (uiManager != null)
        {
            uiManager.UpdateEnemyCount(enemiesKilled, totalEnemiesInWave);
        }
        
        if (spawnManager != null)
        {
            // SpawnManager에게 웨이브 스폰 요청
            await spawnManager.SpawnWaveAsync(waveData, cancellationToken);
            Debug.Log($"[GameManager] SpawnManager의 스폰 요청 완료.");
        }
        else
        {
            Debug.LogError("[GameManager] SpawnManager를 찾을 수 없습니다!");
        }
    }

    private void OnEnemyKilledHandler(GameObject enemy)
    {
        // 전투 상태가 아니면 아무것도 하지 않음 (중복 처리 방지)
        if (currentState != GameState.Fighting) return;

        enemiesKilled++;
        OnEnemyKilled?.Invoke(enemiesKilled, totalEnemiesInWave);
        
        if (uiManager != null)
        {
            uiManager.UpdateEnemyCount(enemiesKilled, totalEnemiesInWave);
        }
        
        Debug.Log($"[GameManager] 적 처치: {enemiesKilled}/{totalEnemiesInWave}");
        
        // 모든 적이 처치되었는지 확인
        if (enemiesKilled >= totalEnemiesInWave && totalEnemiesInWave > 0)
        {
            // 웨이브 완료 처리
            CompleteWaveAsync(cancellationTokenSource.Token).Forget();
        }
    }

    private int CalculateTotalEnemiesInWave(WaveData waveData)
    {
        if (waveData == null) return 0;

        int totalEnemies = 0;
        foreach (var group in waveData.enemyGroups)
        {
            totalEnemies += group.count;
        }
        return totalEnemies;
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

    #region 수압프레스 시스템

    /// <summary>
    /// 수압프레스를 생성합니다
    /// </summary>
    public bool SpawnHydroWaterPress()
    {
        return SpawnHydroWaterPressAsync(cancellationTokenSource.Token).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 수압프레스를 비동기로 생성합니다
    /// </summary>
    public async UniTask<bool> SpawnHydroWaterPressAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 게임 활성 상태 체크
            if (!isGameActive)
            {
                Debug.LogWarning("[GameManager] 게임이 비활성 상태에서는 수압프레스를 사용할 수 없습니다.");
                return false;
            }

            // 쿨다운 체크
            if (Time.time - lastHydroPressTime < hydroPressCooldown)
            {
                float remainingCooldown = hydroPressCooldown - (Time.time - lastHydroPressTime);
                Debug.Log($"[GameManager] 수압프레스 쿨다운 중: {remainingCooldown:F1}초 남음");
                return false;
            }

            // DataManger 유효성 체크
            if (!DataManger.IsAvailable())
            {
                Debug.LogWarning("[GameManager] DataManger를 사용할 수 없습니다!");
                return false;
            }

            // 코인 체크
            if (DataManger.Instance.GetCoin() < hydroPressCost)
            {
                int currentCoin = DataManger.Instance.GetCoin();
                Debug.Log($"[GameManager] 수압프레스 비용 부족: {hydroPressCost} 코인 필요, 현재 {currentCoin} 코인 보유");
                return false;
            }

            // 수압프레스 프리팹 체크
            if (hydroWaterPrefab == null)
            {
                Debug.LogError("[GameManager] 수압프레스 프리팹이 설정되지 않았습니다!");
                return false;
            }

            await UniTask.Yield(cancellationToken);

            // 기존 파괴된 수압프레스 정리
            CleanupHydroPresses();

            // 스폰 위치 및 방향 계산
            Vector3 spawnPosition = GetHydroPressSpawnPosition();
            Vector3 spawnDirection = GetHydroPressDirection();
            Quaternion spawnRotation = Quaternion.LookRotation(spawnDirection);

            // 수압프레스 생성
            HydroWaterPress hydroPress = Instantiate(hydroWaterPrefab, spawnPosition, spawnRotation);
            
            if (hydroPress == null)
            {
                Debug.LogError("[GameManager] 수압프레스 생성에 실패했습니다!");
                return false;
            }

            // 수압프레스 초기화
            hydroPress.Initialize(spawnDirection);
            
            // 활성 수압프레스 목록에 추가
            activeHydroPresses.Add(hydroPress);
            
            // 코인 차감
            bool spendSuccess = DataManger.Instance.SpendCoin(hydroPressCost);
            if (!spendSuccess)
            {
                Debug.LogError("[GameManager] 코인 차감에 실패했습니다!");
                if (hydroPress != null)
                {
                    Destroy(hydroPress.gameObject);
                    activeHydroPresses.Remove(hydroPress);
                }
                return false;
            }

            // 쿨다운 갱신
            lastHydroPressTime = Time.time;

            // 성공 로그 및 이벤트 발생
            Debug.Log($"[GameManager] 수압프레스 생성 완료 - 위치: {spawnPosition}, 방향: {spawnDirection}, 비용: {hydroPressCost} 코인");
            
            // 수압프레스 생성 이벤트 (필요시 추가 가능)
            OnHydroWaterPressSpawned?.Invoke(hydroPress);

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameManager] 수압프레스 생성 중 오류 발생: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 수압프레스 스폰 위치를 계산합니다
    /// </summary>
    private Vector3 GetHydroPressSpawnPosition()
    {
        if (hydroSpawnPoint != null)
        {
            return hydroSpawnPoint.position;
        }

        // 기본 스폰 위치: 카메라 앞쪽 또는 맵 중앙
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            Vector3 cameraForward = mainCamera.transform.forward;
            cameraForward.y = 0; // Y축 제거
            return mainCamera.transform.position + cameraForward.normalized * 5f;
        }

        // 카메라가 없으면 맵 중앙에서 앞쪽으로
        return new Vector3(0, 1, -10);
    }

    /// <summary>
    /// 수압프레스 이동 방향을 계산합니다
    /// </summary>
    private Vector3 GetHydroPressDirection()
    {
        // 기본적으로 앞쪽 (Z+) 방향으로 이동
        return Vector3.forward;
    }

    /// <summary>
    /// 수압프레스를 사용 가능 여부를 확인합니다
    /// </summary>
    public bool CanUseHydroWaterPress()
    {
        // 쿨다운 체크
        if (Time.time - lastHydroPressTime < hydroPressCooldown)
            return false;

        // 코인 체크
        if (DataManger.IsAvailable())
        {
            return DataManger.Instance.GetCoin() >= hydroPressCost;
        }

        return false;
    }

    /// <summary>
    /// 수압프레스 쿨다운 남은 시간을 반환합니다
    /// </summary>
    public float GetHydroPressRemainingCooldown()
    {
        return Mathf.Max(0f, hydroPressCooldown - (Time.time - lastHydroPressTime));
    }

    /// <summary>
    /// 활성 수압프레스 목록을 정리합니다 (파괴된 오브젝트 제거)
    /// </summary>
    private void CleanupHydroPresses()
    {
        activeHydroPresses.RemoveAll(hydroPress => hydroPress == null);
    }

    /// <summary>
    /// 수압프레스 정보를 반환합니다
    /// </summary>
    public string GetHydroPressInfo()
    {
        CleanupHydroPresses();
        float cooldown = GetHydroPressRemainingCooldown();
        int currentCoin = DataManger.IsAvailable() ? DataManger.Instance.GetCoin() : 0;
        
        return $"수압프레스 - 비용: {hydroPressCost}, 보유코인: {currentCoin}, " +
               $"쿨다운: {cooldown:F1}s, 활성: {activeHydroPresses.Count}개";
    }

    #endregion

    #region 유틸리티 메서드

    /// <summary>
    /// 게임 상태를 변경합니다.
    /// </summary>
    private void ChangeGameState(GameState newState)
    {
        if (currentState == newState) return;
        
        GameState previousState = currentState;
        currentState = newState;
        
        OnGameStateChanged?.Invoke(newState);
        
        Debug.Log($"[GameManager] 게임 상태 변경: {previousState} → {newState}");
    }

    /// <summary>
    /// 현재 웨이브 데이터를 가져옵니다.
    /// </summary>
    private WaveData GetCurrentWaveData()
    {
        if (currentWaveIndex >= 0 && currentWaveIndex < waveDataList.Count)
        {
            return waveDataList[currentWaveIndex];
        }
        
        Debug.LogWarning($"[GameManager] 웨이브 인덱스 {currentWaveIndex}에 해당하는 데이터가 없습니다.");
        return null;
    }

    #endregion

    #region 리소스 정리

    private void CleanupCurrentGame()
    {
        // 현재 적들 정리
        // SpawnManager에서 관리하므로 여기서는 별도 처리 없음
        
        Time.timeScale = 1f;
    }

    private void CleanupResources()
    {
        CleanupCurrentGame();
        
        if (spawnManager != null)
        {
            SpawnManager.OnEnemyDestroyed -= OnEnemyKilledHandler;
        }
        
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        
        Debug.Log("[GameManager] 리소스 정리 완료");
    }

    #endregion
}
