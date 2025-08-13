using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
    /// 게임의 핵심 루프를 관리하는 매니저
    ///  준비 시간 → 웨이브 전투 → 결과 처리 → 다음 웨이브 준비의 사이클을 관리합니다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        #region 열거형 및 상수

        public enum GameState
        {
            Preparing,      // 웨이브 준비 중 (15초)
            Fighting,       // 웨이브 전투 중
            WaveComplete,   // 웨이브 완료 처리
            GameOver,       // 게임 오버
            Victory,        // 게임 승리
            Paused          // 게임 일시정지
        }

        private const float PREPARATION_TIME = 15f;
        private const int INITIAL_TP_PER_WAVE = 3;
        private const int BASE_COIN_REWARD = 50;
        private const int TP_INCREMENT_PER_WAVE = 1;

        #endregion

        #region 필드 및 속성

        [Header("caffe wall")] 
        [SerializeField] private GameObject caffewall;
        private CaffeWallHealth caffeWallHealth;
        
        [Header("스테이지 데이터")]
        [SerializeField] private StageDataSO stageData; // ScriptableObject로 웨이브 데이터 관리

        [Header("게임 설정")]
        [SerializeField] private float preparationTime = PREPARATION_TIME;
        [SerializeField] private int initialTPPerWave = INITIAL_TP_PER_WAVE;
        [SerializeField] private int baseCoinReward = BASE_COIN_REWARD;

        [Header("참조")]
        [SerializeField] private InGameUIManager uiManager;
        [SerializeField] private TerretControl turretControl;
        [SerializeField] private SpawnManager spawnManager;

        [Header("수압프레스 설정")]
        [SerializeField] private HydroWaterPress hydroWaterPrefab;
        [SerializeField] private Transform hydroSpawnPoint;
        [SerializeField] private int hydroPressCost = 100;
        [SerializeField] private float hydroPressCooldown = 10f;

        private float lastHydroPressTime = 0f;
        private List<HydroWaterPress> activeHydroPresses = new List<HydroWaterPress>();

        public GameState currentState { get; private set; }
        private int currentWaveIndex = 0;
        private float preparationTimer;
        private bool isGameActive;

        private int enemiesKilled;
        private int totalEnemiesInWave;

        private CancellationTokenSource cancellationTokenSource;

        public static event Action<GameState> OnGameStateChanged;
        public static event Action<int> OnWaveStarted;
        public static event Action<int, int> OnWaveCompleted;
        public static event Action<float> OnPreparationTimeUpdated;
        public static event Action<int, int> OnEnemyKilled;
        public static event Action OnGameOver;
        public static event Action OnGameVictory;
        public static event Action<HydroWaterPress> OnHydroWaterPressSpawned;

        public int CurrentWave => currentWaveIndex + 1;
        public int TotalWaves => stageData != null ? stageData.waveDataList.Count : 0;
        public float PreparationTimeRemaining => preparationTimer;
        public bool IsPreparationPhase => currentState == GameState.Preparing;
        public bool IsFightingPhase => currentState == GameState.Fighting;
        public int EnemiesKilled => enemiesKilled;
        public int TotalEnemies => totalEnemiesInWave;

        public int HydroPressCost => hydroPressCost;
        public float HydroPressCooldown => hydroPressCooldown;
        public int ActiveHydroPressCount { get { CleanupHydroPresses(); return activeHydroPresses.Count; } }
        public bool IsHydroPressReady => CanUseHydroWaterPress();
        public float HydroPressRemainingCooldown => GetHydroPressRemainingCooldown();
        
        /// <summary>
        /// 현재 스테이지에서 클리어한 웨이브의 수를 반환합니다.
        /// </summary>
        public int ClearedWavesCount
        {
            get
            {
                if (stageData == null) return 0;
                // LINQ를 사용하여 isClear가 true인 웨이브의 개수를 셉니다.
                return stageData.waveDataList.Count(wave => wave.isClear);
            }
        }

        #endregion

        #region 유니티 생명주기

        void Awake()
        {
            FindRequiredComponents();
            OnGameOver += HandleGameOver;
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
            OnGameOver -= HandleGameOver;
        }

        #endregion

        #region 초기화

        private void FindRequiredComponents()
        {
            if (uiManager == null) uiManager = FindFirstObjectByType<InGameUIManager>();
            if (turretControl == null) turretControl = FindFirstObjectByType<TerretControl>();
            if (spawnManager == null) spawnManager = FindFirstObjectByType<SpawnManager>();
            
            if (caffewall != null)
            {
                caffeWallHealth = caffewall.GetComponent<CaffeWallHealth>();
                if (caffeWallHealth == null)
                {
                    Debug.LogError("[GameManager] caffewall에 CaffeWallHealth 컴포넌트가 없습니다!");
                }
            }
            else
            {
                Debug.LogWarning("[GameManager] caffewall GameObject가 할당되지 않았습니다.");
            }
        }

        private async UniTask InitializeGameAsync(CancellationToken cancellationToken)
        {
            try
            {
                await UniTask.Yield(cancellationToken);
                
                if (caffeWallHealth != null)
                {
                    caffeWallHealth.OnDied += HandleCaffeWallDeath;
                }
                
                if (!ValidateWaveData())
                {
                    isGameActive = false;
                    return; // 웨이브 데이터 없으면 게임 시작 중단
                }

                int lastClearedWaveIndex = FindLastClearedWaveIndex();
                currentWaveIndex = (lastClearedWaveIndex == -1) ? 0 : lastClearedWaveIndex + 1;

                if (currentWaveIndex >= TotalWaves && TotalWaves > 0)
                {
                    Debug.Log("[GameManager] 모든 웨이브를 클리어했습니다. 게임을 승리로 종료합니다.");
                    await EndGameAsync(true, cancellationToken);
                    return;
                }


                enemiesKilled = 0;
                totalEnemiesInWave = 0;

                if (spawnManager != null)
                {
                    SpawnManager.OnEnemyDestroyed += OnEnemyKilledHandler;
                }

                await SetupInitialResourcesAsync(cancellationToken);
                await StartPreparationPhaseAsync(cancellationToken);

                isGameActive = true;
                Debug.Log($"[GameManager] 게임 초기화 완료. 시작 웨이브: {CurrentWave}");
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
                DataManger.Instance.AddTP(initialTPPerWave);
                Debug.Log($"[GameManager] 초기 TP {initialTPPerWave} 지급");
            }
            else
            {
                Debug.LogWarning("[GameManager] DataManger를 사용할 수 없습니다!");
            }
        }

        private bool ValidateWaveData()
        {
            if (stageData == null)
            {
                Debug.LogError("[GameManager] StageData ScriptableObject가 할당되지 않았습니다!");
                return false;
            }
            if (stageData.waveDataList.Count == 0)
            {
                Debug.LogWarning("[GameManager] 웨이브 데이터가 비어있습니다.");
                return false;
            }
            return true;
        }

        #endregion

        #region 게임 루프

        private void UpdateGameLoop()
        {
            if (currentState == GameState.Preparing)
            {
                UpdatePreparationPhase();
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

        private async UniTask StartPreparationPhaseAsync(CancellationToken cancellationToken)
        {
            if (currentWaveIndex > 0)
            {
                if (DataManger.IsAvailable())
                {
                    int tpToGive = initialTPPerWave + (currentWaveIndex * TP_INCREMENT_PER_WAVE);
                    DataManger.Instance.AddTP(tpToGive);
                    Debug.Log($"[GameManager] 웨이브 {CurrentWave} 준비: TP {tpToGive} 지급");
                }
                await StartWaveAsync(cancellationToken);
            }
            else
            {
                ChangeGameState(GameState.Preparing);
                preparationTimer = preparationTime;
                await UniTask.Yield(cancellationToken);
                Debug.Log($"[GameManager] 웨이브 {CurrentWave} 준비 시작 ({preparationTime}초)");
            }
        }

        private async UniTask StartWaveAsync(CancellationToken cancellationToken)
        {
            ChangeGameState(GameState.Fighting);
            await UniTask.Yield(cancellationToken);

            if (uiManager != null)
            {
                uiManager.ShowWaveTextAsync($"Wave {CurrentWave} 시작!").Forget();
                uiManager.UpdateWaveDisplay(CurrentWave, TotalWaves);
            }

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

        private async UniTask CompleteWaveAsync(CancellationToken cancellationToken)
        {
            if (currentState == GameState.WaveComplete) return;

            ChangeGameState(GameState.WaveComplete);
            await UniTask.Yield(cancellationToken);

            if (uiManager != null)
            {
                await uiManager.ShowWaveEndTextAsync();
            }

            int coinReward = CalculateCoinReward();
            if (DataManger.IsAvailable())
            {
                DataManger.Instance.AddCoin(coinReward);
            }

            OnWaveCompleted?.Invoke(CurrentWave, coinReward);
            Debug.Log($"[GameManager] 웨이브 {CurrentWave} 완료! 코인 {coinReward} 획득");

            // 현재 완료된 웨이브의 isClear를 true로 설정
            WaveData completedWave = GetCurrentWaveData();
            if (completedWave != null)
            {
                completedWave.isClear = true;
                Debug.Log($"[GameManager] Wave {CurrentWave} is marked as clear.");
            }

            currentWaveIndex++;

            if (currentWaveIndex >= TotalWaves)
            {
                await EndGameAsync(true, cancellationToken);
            }
            else
            {
                if (uiManager != null)
                {
                    await uiManager.ShowWaveCountdownAsync(uiManager.CountdownDuration, cancellationToken);
                }
                await StartPreparationPhaseAsync(cancellationToken);
            }
        }

        #endregion

        #region 적 관리

        private async UniTask SpawnWaveEnemiesAsync(WaveData waveData, CancellationToken cancellationToken)
        {
            enemiesKilled = 0;
            totalEnemiesInWave = CalculateTotalEnemiesInWave(waveData);
            Debug.Log($"[GameManager] 웨이브 시작 - 총 {totalEnemiesInWave}마리의 적 스폰 예정");

            if (uiManager != null)
            {
                uiManager.UpdateEnemyCount(enemiesKilled, totalEnemiesInWave);
            }

            if (spawnManager != null)
            {
                await spawnManager.SpawnWaveAsync(waveData, currentWaveIndex, cancellationToken);
            }
            else
            {
                Debug.LogError("[GameManager] SpawnManager를 찾을 수 없습니다!");
            }
        }

        private void OnEnemyKilledHandler(GameObject enemy)
        {
            if (currentState != GameState.Fighting) return;

            enemiesKilled++;
            OnEnemyKilled?.Invoke(enemiesKilled, totalEnemiesInWave);

            if (uiManager != null)
            {
                uiManager.UpdateEnemyCount(enemiesKilled, totalEnemiesInWave);
            }

            if (enemiesKilled >= totalEnemiesInWave && totalEnemiesInWave > 0)
            {
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
        
        #region Wall Management

        private void HandleCaffeWallDeath()
        {
            Debug.Log("[GameManager] 벽이 파괴되었습니다. 게임 오버를 시작합니다.");
            TriggerGameOver();
        }

        #endregion

        #region 보상 및 계산

        private int CalculateCoinReward()
        {
            int baseReward = baseCoinReward;
            int waveBonus = CurrentWave * 10;
            int killBonus = enemiesKilled * 5;
            return baseReward + waveBonus + killBonus;
        }

        #endregion

        #region 게임 종료

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

            if (DataManger.IsAvailable())
            {
                DataManger.Instance.SaveUserData();
            }
        }

        public void TriggerGameOver()
        {
            if (isGameActive)
            {
                EndGameAsync(false, cancellationTokenSource.Token).Forget();
            }
        }

        private void HandleGameOver()
        {
            if (uiManager != null && DataManger.IsAvailable())
            {
                uiManager.ShowGameOverPanel(DataManger.Instance.GetAllCurrencyInfo().coin);
            }
        }

        #endregion

        #region 게임 제어

        public void PauseGame()
        {
            if (currentState != GameState.Paused && isGameActive)
            {
                ChangeGameState(GameState.Paused);
                Time.timeScale = 0f;
            }
        }

        public void ResumeGame()
        {
            if (currentState == GameState.Paused)
            {
                Time.timeScale = 1f;
                ChangeGameState(GameState.Preparing);
            }
        }

        public async UniTask RestartGameAsync()
        {
            CleanupCurrentGame();
            await InitializeGameAsync(cancellationTokenSource.Token);
        }

        #endregion

        #region 수압프레스 시스템

        public async UniTask<bool> SpawnHydroWaterPressAsync(CancellationToken cancellationToken = default)
        {
            if (!isGameActive || Time.time - lastHydroPressTime < hydroPressCooldown)
            {
                return false;
            }
            if (!DataManger.IsAvailable() || DataManger.Instance.GetCoin() < hydroPressCost)
            {
                return false;
            }
            if (hydroWaterPrefab == null)
            {
                Debug.LogError("[GameManager] 수압프레스 프리팹이 설정되지 않았습니다!");
                return false;
            }

            await UniTask.Yield(cancellationToken);

            CleanupHydroPresses();

            Vector3 spawnPosition = GetHydroPressSpawnPosition();
            Vector3 spawnDirection = GetHydroPressDirection();
            Quaternion spawnRotation = Quaternion.LookRotation(spawnDirection);

            HydroWaterPress hydroPress = Instantiate(hydroWaterPrefab, spawnPosition, spawnRotation);
            if (hydroPress == null)
            {
                return false;
            }

            activeHydroPresses.Add(hydroPress);

            if (!DataManger.Instance.SpendCoin(hydroPressCost))
            {
                Destroy(hydroPress.gameObject);
                activeHydroPresses.Remove(hydroPress);
                return false;
            }

            lastHydroPressTime = Time.time;
            OnHydroWaterPressSpawned?.Invoke(hydroPress);
            return true;
        }

        private Vector3 GetHydroPressSpawnPosition()
        {
            return hydroSpawnPoint != null ? hydroSpawnPoint.position : new Vector3(0, 1, -10);
        }

        private Vector3 GetHydroPressDirection()
        {
            return Vector3.forward;
        }

        public bool CanUseHydroWaterPress()
        {
            if (Time.time - lastHydroPressTime < hydroPressCooldown) return false;
            return DataManger.IsAvailable() && DataManger.Instance.GetCoin() >= hydroPressCost;
        }

        public float GetHydroPressRemainingCooldown()
        {
            return Mathf.Max(0f, hydroPressCooldown - (Time.time - lastHydroPressTime));
        }

        private void CleanupHydroPresses()
        {
            activeHydroPresses.RemoveAll(hydroPress => hydroPress == null);
        }

        #endregion

        #region 유틸리티 메서드

        private int FindLastClearedWaveIndex()
        {
            if (stageData == null || stageData.waveDataList == null) return -1;
            return stageData.waveDataList.FindLastIndex(wave => wave.isClear);
        }

        private void ChangeGameState(GameState newState)
        {
            if (currentState == newState) return;
            GameState previousState = currentState;
            currentState = newState;
            OnGameStateChanged?.Invoke(newState);
            Debug.Log($"[GameManager] 게임 상태 변경: {previousState} → {newState}");
        }

        private WaveData GetCurrentWaveData()
        {
            if (stageData != null && currentWaveIndex >= 0 && currentWaveIndex < stageData.waveDataList.Count)
            {
                return stageData.waveDataList[currentWaveIndex];
            }
            return null;
        }

        #endregion

        #region 리소스 정리

        private void CleanupCurrentGame()
        {
            Time.timeScale = 1f;
        }

        private void CleanupResources()
        {
            CleanupCurrentGame();
            if (spawnManager != null)
            {
                SpawnManager.OnEnemyDestroyed -= OnEnemyKilledHandler;
            }
            if (caffeWallHealth != null)
            {
                caffeWallHealth.OnDied -= HandleCaffeWallDeath;
            }
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }

        #endregion
    }
